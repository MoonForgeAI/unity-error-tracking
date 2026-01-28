using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace MoonForge.ErrorTracking
{
    /// <summary>
    /// Helper class for intercepting and reporting network errors from UnityWebRequest.
    /// Provides utilities for wrapping HTTP requests with automatic error tracking.
    /// </summary>
    public static class NetworkErrorInterceptor
    {
        private static ErrorTrackerConfig _config;
        private static Action<NetworkRequest, string, int> _onNetworkError;

        /// <summary>
        /// HTTP status codes that are considered errors
        /// </summary>
        public static int ErrorStatusCodeThreshold { get; set; } = 400;

        /// <summary>
        /// Whether to automatically add breadcrumbs for all requests
        /// </summary>
        public static bool AddBreadcrumbsForAllRequests { get; set; } = true;

        /// <summary>
        /// Whether to capture errors for failed requests (connection errors)
        /// </summary>
        public static bool CaptureConnectionErrors { get; set; } = true;

        /// <summary>
        /// Whether to capture errors for HTTP error status codes (4xx, 5xx)
        /// </summary>
        public static bool CaptureHttpErrors { get; set; } = true;

        /// <summary>
        /// Initialize the network error interceptor
        /// </summary>
        internal static void Initialize(ErrorTrackerConfig config, Action<NetworkRequest, string, int> onNetworkError)
        {
            _config = config;
            _onNetworkError = onNetworkError;
        }

        /// <summary>
        /// Send a request with automatic error tracking.
        /// Use this as a wrapper around UnityWebRequest.SendWebRequest().
        /// </summary>
        /// <param name="request">The UnityWebRequest to send</param>
        /// <param name="onComplete">Callback when request completes (success or failure)</param>
        /// <returns>Coroutine enumerator</returns>
        public static IEnumerator SendTrackedRequest(UnityWebRequest request, Action<UnityWebRequest> onComplete = null)
        {
            var startTime = Time.realtimeSinceStartup;
            var url = request.url;
            var method = request.method;

            yield return request.SendWebRequest();

            var durationMs = (Time.realtimeSinceStartup - startTime) * 1000f;
            var statusCode = (int)request.responseCode;

            // Add breadcrumb for all requests if enabled
            if (AddBreadcrumbsForAllRequests)
            {
                BreadcrumbTracker.Instance.AddNetworkRequest(method, url, statusCode, durationMs);
            }

            // Check for errors
            var isError = false;
            string errorMessage = null;

            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.DataProcessingError)
            {
                if (CaptureConnectionErrors)
                {
                    isError = true;
                    errorMessage = request.error ?? "Connection error";
                }
            }
            else if (request.result == UnityWebRequest.Result.ProtocolError)
            {
                if (CaptureHttpErrors && statusCode >= ErrorStatusCodeThreshold)
                {
                    isError = true;
                    errorMessage = $"HTTP {statusCode}: {request.error ?? GetStatusCodeDescription(statusCode)}";
                }
            }

            // Report error if detected
            if (isError && _onNetworkError != null)
            {
                var networkRequest = new NetworkRequest
                {
                    url = SanitizeUrl(url),
                    method = method,
                    statusCode = statusCode > 0 ? statusCode : null,
                    durationMs = durationMs
                };

                _onNetworkError(networkRequest, errorMessage, statusCode);
            }

            // Call the completion callback
            onComplete?.Invoke(request);
        }

        /// <summary>
        /// Manually report a network error
        /// </summary>
        public static void ReportError(string url, string method, int statusCode, string errorMessage, float? durationMs = null)
        {
            if (_onNetworkError == null) return;

            var networkRequest = new NetworkRequest
            {
                url = SanitizeUrl(url),
                method = method,
                statusCode = statusCode > 0 ? statusCode : null,
                durationMs = durationMs
            };

            _onNetworkError(networkRequest, errorMessage, statusCode);

            // Add breadcrumb
            BreadcrumbTracker.Instance.AddNetworkRequest(method, url, statusCode, durationMs);
        }

        /// <summary>
        /// Report an error from a UnityWebRequest
        /// </summary>
        public static void ReportError(UnityWebRequest request, float durationMs)
        {
            if (_onNetworkError == null) return;
            if (request.result == UnityWebRequest.Result.Success) return;

            var statusCode = (int)request.responseCode;
            var errorMessage = request.error ?? $"HTTP {statusCode}";

            ReportError(request.url, request.method, statusCode, errorMessage, durationMs);
        }

        /// <summary>
        /// Remove sensitive information from URLs (query parameters that might contain tokens)
        /// </summary>
        private static string SanitizeUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            if (_config == null || !_config.scrubSensitiveData) return url;

            try
            {
                var uri = new Uri(url);
                if (string.IsNullOrEmpty(uri.Query)) return url;

                // List of parameter names to scrub
                var sensitiveParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "token", "access_token", "refresh_token", "api_key", "apikey",
                    "key", "secret", "password", "pwd", "auth", "authorization",
                    "bearer", "session", "sessionid", "session_id"
                };

                var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
                var sanitizedParams = new List<string>();

                foreach (string key in queryParams.AllKeys)
                {
                    if (key == null) continue;

                    var value = sensitiveParams.Contains(key) ? "[REDACTED]" : queryParams[key];
                    sanitizedParams.Add($"{key}={value}");
                }

                var sanitizedQuery = sanitizedParams.Count > 0
                    ? "?" + string.Join("&", sanitizedParams)
                    : "";

                return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}{sanitizedQuery}";
            }
            catch
            {
                return url;
            }
        }

        /// <summary>
        /// Get a human-readable description for HTTP status codes
        /// </summary>
        private static string GetStatusCodeDescription(int statusCode)
        {
            return statusCode switch
            {
                400 => "Bad Request",
                401 => "Unauthorized",
                403 => "Forbidden",
                404 => "Not Found",
                405 => "Method Not Allowed",
                408 => "Request Timeout",
                409 => "Conflict",
                410 => "Gone",
                422 => "Unprocessable Entity",
                429 => "Too Many Requests",
                500 => "Internal Server Error",
                501 => "Not Implemented",
                502 => "Bad Gateway",
                503 => "Service Unavailable",
                504 => "Gateway Timeout",
                _ => "Error"
            };
        }
    }

    /// <summary>
    /// Extension methods for UnityWebRequest to add easy tracking
    /// </summary>
    public static class UnityWebRequestExtensions
    {
        /// <summary>
        /// Send the request with automatic error tracking
        /// </summary>
        public static IEnumerator SendWithTracking(this UnityWebRequest request, Action<UnityWebRequest> onComplete = null)
        {
            return NetworkErrorInterceptor.SendTrackedRequest(request, onComplete);
        }
    }
}

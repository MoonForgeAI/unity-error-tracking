using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace MoonForge.ErrorTracking
{
    /// <summary>
    /// HTTP transport for sending errors to the MoonForge API
    /// </summary>
    public class HttpTransport
    {
        private readonly ErrorTrackerConfig _config;
        private readonly MonoBehaviour _coroutineRunner;

        public HttpTransport(ErrorTrackerConfig config, MonoBehaviour coroutineRunner)
        {
            _config = config;
            _coroutineRunner = coroutineRunner;
        }

        /// <summary>
        /// Send a single error payload
        /// </summary>
        public void SendError(ErrorPayload payload, Action<ErrorSubmissionResponse> onComplete)
        {
            _coroutineRunner.StartCoroutine(SendErrorCoroutine(payload, onComplete));
        }

        /// <summary>
        /// Send a batch of errors
        /// </summary>
        public void SendBatch(ErrorBatchPayload payload, Action<BatchSubmissionResponse> onComplete)
        {
            _coroutineRunner.StartCoroutine(SendBatchCoroutine(payload, onComplete));
        }

        private IEnumerator SendErrorCoroutine(ErrorPayload payload, Action<ErrorSubmissionResponse> onComplete)
        {
            var json = JsonUtility.ToJson(payload);
            var url = _config.GetErrorsApiUrl();

            var attempt = 0;
            ErrorSubmissionResponse response = null;

            while (attempt <= _config.maxRetries)
            {
                using (var request = CreatePostRequest(url, json))
                {
                    request.timeout = (int)_config.requestTimeout;

                    if (_config.debugMode)
                    {
                        Debug.Log($"[MoonForge] Sending error to {url} (attempt {attempt + 1})");
                    }

                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        try
                        {
                            response = JsonUtility.FromJson<ErrorSubmissionResponse>(request.downloadHandler.text);
                            if (_config.debugMode)
                            {
                                Debug.Log($"[MoonForge] Error sent successfully: {response?.errorId}");
                            }
                        }
                        catch (Exception ex)
                        {
                            response = new ErrorSubmissionResponse
                            {
                                status = "error",
                                error = $"Failed to parse response: {ex.Message}"
                            };
                        }
                        break;
                    }

                    // Check if we should retry
                    if (ShouldRetry(request))
                    {
                        attempt++;
                        if (attempt <= _config.maxRetries)
                        {
                            var delay = GetRetryDelay(attempt);
                            if (_config.debugMode)
                            {
                                Debug.Log($"[MoonForge] Request failed, retrying in {delay}s: {request.error}");
                            }
                            yield return new WaitForSeconds(delay);
                            continue;
                        }
                    }

                    // Final failure
                    response = new ErrorSubmissionResponse
                    {
                        status = "error",
                        error = request.error ?? $"HTTP {request.responseCode}"
                    };

                    // Check for rate limiting
                    if (request.responseCode == 429)
                    {
                        response.reason = "rate_limit";
                        var retryAfter = request.GetResponseHeader("Retry-After");
                        if (int.TryParse(retryAfter, out var retrySeconds))
                        {
                            response.retryAfterMs = retrySeconds * 1000;
                        }
                    }

                    break;
                }
            }

            onComplete?.Invoke(response);
        }

        private IEnumerator SendBatchCoroutine(ErrorBatchPayload payload, Action<BatchSubmissionResponse> onComplete)
        {
            var json = JsonUtility.ToJson(payload);
            var url = _config.GetBatchErrorsApiUrl();

            var attempt = 0;
            BatchSubmissionResponse response = null;

            while (attempt <= _config.maxRetries)
            {
                using (var request = CreatePostRequest(url, json))
                {
                    request.timeout = (int)_config.requestTimeout;

                    if (_config.debugMode)
                    {
                        Debug.Log($"[MoonForge] Sending batch ({payload.errors?.Count ?? 0} errors) to {url} (attempt {attempt + 1})");
                    }

                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        try
                        {
                            response = JsonUtility.FromJson<BatchSubmissionResponse>(request.downloadHandler.text);
                            if (_config.debugMode)
                            {
                                Debug.Log($"[MoonForge] Batch sent successfully: {response?.accepted}/{response?.total} accepted");
                            }
                        }
                        catch (Exception ex)
                        {
                            response = new BatchSubmissionResponse
                            {
                                status = "error",
                                error = $"Failed to parse response: {ex.Message}"
                            };
                        }
                        break;
                    }

                    // Check if we should retry
                    if (ShouldRetry(request))
                    {
                        attempt++;
                        if (attempt <= _config.maxRetries)
                        {
                            var delay = GetRetryDelay(attempt);
                            if (_config.debugMode)
                            {
                                Debug.Log($"[MoonForge] Batch request failed, retrying in {delay}s: {request.error}");
                            }
                            yield return new WaitForSeconds(delay);
                            continue;
                        }
                    }

                    // Final failure
                    response = new BatchSubmissionResponse
                    {
                        status = "error",
                        error = request.error ?? $"HTTP {request.responseCode}"
                    };
                    break;
                }
            }

            onComplete?.Invoke(response);
        }

        private UnityWebRequest CreatePostRequest(string url, string json)
        {
            var request = new UnityWebRequest(url, "POST");
            var bodyRaw = Encoding.UTF8.GetBytes(json);

            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            return request;
        }

        private bool ShouldRetry(UnityWebRequest request)
        {
            // Retry on network errors
            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                return true;
            }

            // Retry on server errors (5xx) but not client errors (4xx)
            if (request.responseCode >= 500 && request.responseCode < 600)
            {
                return true;
            }

            // Retry on rate limit (429) with exponential backoff
            if (request.responseCode == 429)
            {
                return true;
            }

            return false;
        }

        private float GetRetryDelay(int attempt)
        {
            // Exponential backoff with jitter
            var baseDelay = _config.retryBaseDelay * Mathf.Pow(2, attempt - 1);
            var jitter = UnityEngine.Random.Range(0f, 0.3f * baseDelay);
            return baseDelay + jitter;
        }

        /// <summary>
        /// Check if the device has network connectivity
        /// </summary>
        public bool HasConnectivity()
        {
            return Application.internetReachability != NetworkReachability.NotReachable;
        }
    }
}

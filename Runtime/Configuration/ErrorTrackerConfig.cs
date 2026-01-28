using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoonForge.ErrorTracking
{
    /// <summary>
    /// Configuration for MoonForge Error Tracking SDK
    /// Create via Assets > Create > MoonForge > Error Tracker Config
    /// </summary>
    [CreateAssetMenu(fileName = "MoonForgeErrorTrackerConfig", menuName = "MoonForge/Error Tracker Config")]
    public class ErrorTrackerConfig : ScriptableObject
    {
        [Header("Required Settings")]
        [Tooltip("Your MoonForge Game ID (UUID format)")]
        public string gameId;

        [Tooltip("MoonForge API endpoint base URL (without /api/errors)")]
        public string apiEndpoint = "https://moonforge-collector-wso5qviqda-uc.a.run.app";

        [Header("Error Capture Settings")]
        [Tooltip("Capture unhandled exceptions automatically")]
        public bool captureUnhandledExceptions = true;

        [Tooltip("Capture Unity log errors (Debug.LogError, Debug.LogException)")]
        public bool captureLogErrors = true;

        [Tooltip("Capture native crashes (iOS/Android)")]
        public bool captureNativeCrashes = true;

        [Tooltip("Minimum error level to capture")]
        public ErrorLevel minimumLevel = ErrorLevel.Warning;

        [Header("Breadcrumb Settings")]
        [Tooltip("Maximum number of breadcrumbs to keep")]
        [Range(10, 200)]
        public int maxBreadcrumbs = 100;

        [Tooltip("Automatically track scene changes")]
        public bool trackSceneChanges = true;

        [Header("Batching Settings")]
        [Tooltip("Enable batched error submission for better performance")]
        public bool enableBatching = true;

        [Tooltip("Maximum errors in a single batch")]
        [Range(1, 50)]
        public int maxBatchSize = 20;

        [Tooltip("Maximum time (seconds) to wait before sending a batch")]
        [Range(1f, 60f)]
        public float maxBatchWaitTime = 10f;

        [Header("Offline Storage")]
        [Tooltip("Store errors when offline and send when connection is restored")]
        public bool enableOfflineStorage = true;

        [Tooltip("Maximum number of errors to store offline")]
        [Range(10, 1000)]
        public int maxOfflineErrors = 100;

        [Header("Sampling Settings")]
        [Tooltip("Enable client-side adaptive sampling to reduce volume")]
        public bool enableSampling = true;

        [Tooltip("Base sample rate for exceptions (0.0 - 1.0)")]
        [Range(0f, 1f)]
        public float exceptionSampleRate = 0.8f;

        [Tooltip("Base sample rate for network errors (0.0 - 1.0)")]
        [Range(0f, 1f)]
        public float networkErrorSampleRate = 0.5f;

        [Tooltip("Base sample rate for custom errors (0.0 - 1.0)")]
        [Range(0f, 1f)]
        public float customErrorSampleRate = 0.3f;

        [Header("Network Settings")]
        [Tooltip("Request timeout in seconds")]
        [Range(5f, 120f)]
        public float requestTimeout = 30f;

        [Tooltip("Maximum retry attempts for failed submissions")]
        [Range(0, 5)]
        public int maxRetries = 3;

        [Tooltip("Base delay between retries (exponential backoff)")]
        [Range(1f, 10f)]
        public float retryBaseDelay = 2f;

        [Header("Privacy Settings")]
        [Tooltip("Scrub potentially sensitive data from error messages")]
        public bool scrubSensitiveData = true;

        [Tooltip("Patterns to scrub (regex)")]
        public List<string> scrubPatterns = new List<string>
        {
            @"password[\""\']?\s*[:=]\s*[\""\']?[^\s\""\']+",
            @"token[\""\']?\s*[:=]\s*[\""\']?[^\s\""\']+",
            @"api[_-]?key[\""\']?\s*[:=]\s*[\""\']?[^\s\""\']+",
            @"secret[\""\']?\s*[:=]\s*[\""\']?[^\s\""\']+",
            @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b"
        };

        [Header("Symbol Upload Settings")]
        [Tooltip("Automatically upload symbol files after builds for symbolication")]
        public bool autoUploadSymbols = true;

        [Header("Debug Settings")]
        [Tooltip("Enable debug logging for the SDK")]
        public bool debugMode = false;

        [Tooltip("Send errors in Editor (useful for testing)")]
        public bool enableInEditor = false;

        /// <summary>
        /// Validate the configuration
        /// </summary>
        public bool Validate(out string error)
        {
            if (string.IsNullOrEmpty(gameId))
            {
                error = "Game ID is required";
                return false;
            }

            if (!Guid.TryParse(gameId, out _))
            {
                error = "Game ID must be a valid UUID";
                return false;
            }

            if (string.IsNullOrEmpty(apiEndpoint))
            {
                error = "API endpoint is required";
                return false;
            }

            if (!Uri.TryCreate(apiEndpoint, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                error = "API endpoint must be a valid HTTP/HTTPS URL";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Get the full API URL for error submission
        /// </summary>
        public string GetErrorsApiUrl()
        {
            var baseUrl = apiEndpoint.TrimEnd('/');
            return $"{baseUrl}/api/errors";
        }

        /// <summary>
        /// Get the full API URL for batch error submission
        /// </summary>
        public string GetBatchErrorsApiUrl()
        {
            var baseUrl = apiEndpoint.TrimEnd('/');
            return $"{baseUrl}/api/errors/batch";
        }

        /// <summary>
        /// Get the collector URL (alias for apiEndpoint for backwards compatibility)
        /// </summary>
        public string collectorUrl => apiEndpoint;
    }
}

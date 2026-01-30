using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace MoonForge.ErrorTracking.Analytics
{
    /// <summary>
    /// HTTP transport for sending analytics events to the MoonForge API
    /// </summary>
    internal class AnalyticsTransport
    {
        private readonly ErrorTrackerConfig _config;
        private readonly MonoBehaviour _coroutineRunner;
        private readonly Queue<QueuedAnalyticsEvent> _offlineQueue;
        private readonly int _maxOfflineQueueSize;
        private bool _isSendingOfflineQueue;

        private const string OFFLINE_STORAGE_KEY = "MoonForge_Analytics_OfflineQueue";

        public AnalyticsTransport(ErrorTrackerConfig config, MonoBehaviour coroutineRunner)
        {
            _config = config;
            _coroutineRunner = coroutineRunner;
            _offlineQueue = new Queue<QueuedAnalyticsEvent>();
            _maxOfflineQueueSize = 100;

            LoadOfflineQueue();
        }

        /// <summary>
        /// Send an analytics event
        /// </summary>
        public void SendEvent(AnalyticsEvent analyticsEvent, Action<AnalyticsSubmissionResponse> onComplete = null)
        {
            var json = SerializeEvent(analyticsEvent);
            SendJsonPayload(json, onComplete);
        }

        /// <summary>
        /// Send an identify event
        /// </summary>
        public void SendIdentify(IdentifyEvent identifyEvent, Action<AnalyticsSubmissionResponse> onComplete = null)
        {
            var json = SerializeIdentify(identifyEvent);
            SendJsonPayload(json, onComplete);
        }

        private void SendJsonPayload(string json, Action<AnalyticsSubmissionResponse> onComplete)
        {
            if (!HasConnectivity())
            {
                if (_config.debugMode)
                {
                    Debug.Log("[MoonForge Analytics] No connectivity, queuing event");
                }
                QueueOfflineEvent(json);
                onComplete?.Invoke(new AnalyticsSubmissionResponse { status = "queued" });
                return;
            }

            _coroutineRunner.StartCoroutine(SendEventCoroutine(json, onComplete));
        }

        private IEnumerator SendEventCoroutine(string json, Action<AnalyticsSubmissionResponse> onComplete)
        {
            var url = GetAnalyticsApiUrl();

            using (var request = CreatePostRequest(url, json))
            {
                request.timeout = (int)_config.requestTimeout;

                if (_config.debugMode)
                {
                    Debug.Log($"[MoonForge Analytics] Sending event to {url}");
                }

                yield return request.SendWebRequest();

                AnalyticsSubmissionResponse response;

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        response = JsonUtility.FromJson<AnalyticsSubmissionResponse>(request.downloadHandler.text);
                        if (_config.debugMode)
                        {
                            Debug.Log($"[MoonForge Analytics] Event sent successfully");
                        }
                    }
                    catch (Exception ex)
                    {
                        response = new AnalyticsSubmissionResponse
                        {
                            status = "error",
                            error = $"Failed to parse response: {ex.Message}"
                        };
                    }
                }
                else
                {
                    response = new AnalyticsSubmissionResponse
                    {
                        status = "error",
                        error = request.error ?? $"HTTP {request.responseCode}"
                    };

                    // Queue for retry on network errors
                    if (ShouldRetry(request))
                    {
                        QueueOfflineEvent(json);
                    }
                }

                onComplete?.Invoke(response);
            }
        }

        private string GetAnalyticsApiUrl()
        {
            var baseUrl = _config.apiEndpoint.TrimEnd('/');
            return $"{baseUrl}/api/send";
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
            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                return true;
            }

            if (request.responseCode >= 500 && request.responseCode < 600)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Check if the device has network connectivity
        /// </summary>
        public bool HasConnectivity()
        {
            return Application.internetReachability != NetworkReachability.NotReachable;
        }

        /// <summary>
        /// Attempt to send queued offline events
        /// </summary>
        public void FlushOfflineQueue()
        {
            if (_isSendingOfflineQueue || _offlineQueue.Count == 0 || !HasConnectivity())
            {
                return;
            }

            _coroutineRunner.StartCoroutine(FlushOfflineQueueCoroutine());
        }

        private IEnumerator FlushOfflineQueueCoroutine()
        {
            _isSendingOfflineQueue = true;

            if (_config.debugMode)
            {
                Debug.Log($"[MoonForge Analytics] Flushing {_offlineQueue.Count} queued events");
            }

            while (_offlineQueue.Count > 0 && HasConnectivity())
            {
                var queuedEvent = _offlineQueue.Peek();
                var url = GetAnalyticsApiUrl();
                var success = false;

                using (var request = CreatePostRequest(url, queuedEvent.jsonPayload))
                {
                    request.timeout = (int)_config.requestTimeout;
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        success = true;
                        _offlineQueue.Dequeue();
                    }
                    else if (!ShouldRetry(request))
                    {
                        // Non-retryable error, discard the event
                        _offlineQueue.Dequeue();
                    }
                    else
                    {
                        // Retryable error, increment retry count
                        queuedEvent.retryCount++;
                        if (queuedEvent.retryCount >= 3)
                        {
                            _offlineQueue.Dequeue();
                        }
                        break;
                    }
                }

                if (success)
                {
                    yield return null; // Small delay between sends
                }
            }

            SaveOfflineQueue();
            _isSendingOfflineQueue = false;
        }

        private void QueueOfflineEvent(string json)
        {
            if (_offlineQueue.Count >= _maxOfflineQueueSize)
            {
                // Remove oldest event to make room
                _offlineQueue.Dequeue();
            }

            _offlineQueue.Enqueue(new QueuedAnalyticsEvent
            {
                jsonPayload = json,
                queuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                retryCount = 0
            });

            SaveOfflineQueue();
        }

        private void SaveOfflineQueue()
        {
            try
            {
                var items = _offlineQueue.ToArray();
                var wrapper = new OfflineQueueWrapper { events = new List<QueuedAnalyticsEvent>(items) };
                var json = JsonUtility.ToJson(wrapper);
                PlayerPrefs.SetString(OFFLINE_STORAGE_KEY, json);
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                if (_config.debugMode)
                {
                    Debug.LogWarning($"[MoonForge Analytics] Failed to save offline queue: {ex.Message}");
                }
            }
        }

        private void LoadOfflineQueue()
        {
            try
            {
                var json = PlayerPrefs.GetString(OFFLINE_STORAGE_KEY, "");
                if (!string.IsNullOrEmpty(json))
                {
                    var wrapper = JsonUtility.FromJson<OfflineQueueWrapper>(json);
                    if (wrapper?.events != null)
                    {
                        foreach (var item in wrapper.events)
                        {
                            _offlineQueue.Enqueue(item);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (_config.debugMode)
                {
                    Debug.LogWarning($"[MoonForge Analytics] Failed to load offline queue: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Custom JSON serialization for analytics events to handle Dictionary properly
        /// </summary>
        private string SerializeEvent(AnalyticsEvent analyticsEvent)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"type\":\"{analyticsEvent.type}\",");
            sb.Append("\"payload\":{");

            var p = analyticsEvent.payload;
            var fields = new List<string>();

            fields.Add($"\"game\":\"{EscapeJsonString(p.game)}\"");

            if (!string.IsNullOrEmpty(p.hostname))
                fields.Add($"\"hostname\":\"{EscapeJsonString(p.hostname)}\"");

            if (!string.IsNullOrEmpty(p.screen))
                fields.Add($"\"screen\":\"{EscapeJsonString(p.screen)}\"");

            if (!string.IsNullOrEmpty(p.language))
                fields.Add($"\"language\":\"{EscapeJsonString(p.language)}\"");

            if (!string.IsNullOrEmpty(p.url))
                fields.Add($"\"url\":\"{EscapeJsonString(p.url)}\"");

            if (!string.IsNullOrEmpty(p.title))
                fields.Add($"\"title\":\"{EscapeJsonString(p.title)}\"");

            if (!string.IsNullOrEmpty(p.referrer))
                fields.Add($"\"referrer\":\"{EscapeJsonString(p.referrer)}\"");

            if (!string.IsNullOrEmpty(p.name))
                fields.Add($"\"name\":\"{EscapeJsonString(p.name)}\"");

            if (p.data != null && p.data.Count > 0)
                fields.Add($"\"data\":{SerializeDictionary(p.data)}");

            fields.Add($"\"timestamp\":{p.timestamp}");

            sb.Append(string.Join(",", fields));
            sb.Append("}}");

            return sb.ToString();
        }

        private string SerializeIdentify(IdentifyEvent identifyEvent)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"type\":\"{identifyEvent.type}\",");
            sb.Append("\"payload\":{");

            var p = identifyEvent.payload;
            var fields = new List<string>();

            fields.Add($"\"game\":\"{EscapeJsonString(p.game)}\"");
            fields.Add($"\"id\":\"{EscapeJsonString(p.id)}\"");

            if (p.data != null && p.data.Count > 0)
                fields.Add($"\"data\":{SerializeDictionary(p.data)}");

            fields.Add($"\"timestamp\":{p.timestamp}");

            sb.Append(string.Join(",", fields));
            sb.Append("}}");

            return sb.ToString();
        }

        private string SerializeDictionary(Dictionary<string, object> dict)
        {
            var sb = new StringBuilder();
            sb.Append("{");

            var fields = new List<string>();
            foreach (var kvp in dict)
            {
                var value = SerializeValue(kvp.Value);
                fields.Add($"\"{EscapeJsonString(kvp.Key)}\":{value}");
            }

            sb.Append(string.Join(",", fields));
            sb.Append("}");
            return sb.ToString();
        }

        private string SerializeValue(object value)
        {
            if (value == null)
                return "null";

            if (value is string s)
                return $"\"{EscapeJsonString(s)}\"";

            if (value is bool b)
                return b ? "true" : "false";

            if (value is int || value is long || value is float || value is double || value is decimal)
                return value.ToString();

            if (value is Dictionary<string, object> dict)
                return SerializeDictionary(dict);

            // Fallback for other types
            return $"\"{EscapeJsonString(value.ToString())}\"";
        }

        private string EscapeJsonString(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";

            var sb = new StringBuilder();
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            sb.AppendFormat("\\u{0:X4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        [Serializable]
        private class OfflineQueueWrapper
        {
            public List<QueuedAnalyticsEvent> events;
        }
    }
}

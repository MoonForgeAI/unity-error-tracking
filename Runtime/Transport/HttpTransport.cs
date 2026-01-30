using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
            var json = SerializeErrorPayload(payload);
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
            var json = SerializeBatchPayload(payload);
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
                    var errorMessage = request.error ?? $"HTTP {request.responseCode}";

                    // Log response body for debugging client errors (4xx)
                    if (_config.debugMode && request.responseCode >= 400 && request.responseCode < 500)
                    {
                        var responseBody = request.downloadHandler?.text ?? "(no response body)";
                        Debug.LogWarning($"[MoonForge] Batch send failed: {errorMessage}\nResponse: {responseBody}");
                    }

                    response = new BatchSubmissionResponse
                    {
                        status = "error",
                        error = errorMessage
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

        #region JSON Serialization

        /// <summary>
        /// Custom JSON serialization for ErrorPayload (Unity's JsonUtility doesn't handle Lists/Dictionaries properly)
        /// </summary>
        private string SerializeErrorPayload(ErrorPayload payload)
        {
            var sb = new StringBuilder();
            sb.Append("{\"type\":\"error\",\"payload\":");
            sb.Append(SerializeErrorPayloadInner(payload.payload, includeGame: true));
            sb.Append("}");
            return sb.ToString();
        }

        /// <summary>
        /// Custom JSON serialization for ErrorBatchPayload
        /// </summary>
        private string SerializeBatchPayload(ErrorBatchPayload payload)
        {
            var sb = new StringBuilder();
            sb.Append("{\"type\":\"error_batch\"");
            sb.Append($",\"game\":\"{EscapeJsonString(payload.game)}\"");
            sb.Append(",\"errors\":[");

            if (payload.errors != null)
            {
                for (int i = 0; i < payload.errors.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(SerializeBatchErrorItem(payload.errors[i]));
                }
            }

            sb.Append("]}");
            return sb.ToString();
        }

        private string SerializeErrorPayloadInner(ErrorPayloadInner p, bool includeGame)
        {
            var fields = new List<string>();

            if (includeGame && !string.IsNullOrEmpty(p.game))
                fields.Add($"\"game\":\"{EscapeJsonString(p.game)}\"");

            fields.Add($"\"errorType\":\"{EscapeJsonString(p.errorType)}\"");
            fields.Add($"\"errorCategory\":\"{EscapeJsonString(p.errorCategory)}\"");
            fields.Add($"\"errorLevel\":\"{EscapeJsonString(p.errorLevel)}\"");
            fields.Add($"\"message\":\"{EscapeJsonString(p.message)}\"");

            if (p.frames != null && p.frames.Count > 0)
                fields.Add($"\"frames\":{SerializeStackFrames(p.frames)}");

            if (!string.IsNullOrEmpty(p.rawStackTrace))
                fields.Add($"\"rawStackTrace\":\"{EscapeJsonString(p.rawStackTrace)}\"");

            if (!string.IsNullOrEmpty(p.exceptionClass))
                fields.Add($"\"exceptionClass\":\"{EscapeJsonString(p.exceptionClass)}\"");

            if (!string.IsNullOrEmpty(p.fingerprint))
                fields.Add($"\"fingerprint\":\"{EscapeJsonString(p.fingerprint)}\"");

            if (p.device != null)
                fields.Add($"\"device\":{SerializeDeviceContext(p.device)}");

            if (p.network != null)
                fields.Add($"\"network\":{SerializeNetworkContext(p.network)}");

            if (p.gameState != null)
                fields.Add($"\"gameState\":{SerializeGameState(p.gameState)}");

            fields.Add($"\"appVersion\":\"{EscapeJsonString(p.appVersion)}\"");
            fields.Add($"\"buildNumber\":\"{EscapeJsonString(p.buildNumber)}\"");

            if (!string.IsNullOrEmpty(p.unityVersion))
                fields.Add($"\"unityVersion\":\"{EscapeJsonString(p.unityVersion)}\"");

            if (!string.IsNullOrEmpty(p.userId))
                fields.Add($"\"userId\":\"{EscapeJsonString(p.userId)}\"");

            if (!string.IsNullOrEmpty(p.sessionId))
                fields.Add($"\"sessionId\":\"{EscapeJsonString(p.sessionId)}\"");

            if (p.breadcrumbs != null && p.breadcrumbs.Count > 0)
                fields.Add($"\"breadcrumbs\":{SerializeBreadcrumbs(p.breadcrumbs)}");

            if (p.timestamp.HasValue)
                fields.Add($"\"timestamp\":{p.timestamp.Value}");

            if (p.networkRequest != null)
                fields.Add($"\"networkRequest\":{SerializeNetworkRequest(p.networkRequest)}");

            if (p.tags != null && p.tags.Count > 0)
                fields.Add($"\"tags\":{SerializeStringDictionary(p.tags)}");

            return "{" + string.Join(",", fields) + "}";
        }

        private string SerializeBatchErrorItem(BatchErrorItem item)
        {
            var fields = new List<string>();

            fields.Add($"\"clientErrorId\":\"{EscapeJsonString(item.clientErrorId)}\"");
            fields.Add($"\"errorType\":\"{EscapeJsonString(item.errorType)}\"");
            fields.Add($"\"errorCategory\":\"{EscapeJsonString(item.errorCategory)}\"");
            fields.Add($"\"errorLevel\":\"{EscapeJsonString(item.errorLevel)}\"");
            fields.Add($"\"message\":\"{EscapeJsonString(item.message)}\"");

            if (item.frames != null && item.frames.Count > 0)
                fields.Add($"\"frames\":{SerializeStackFrames(item.frames)}");

            if (!string.IsNullOrEmpty(item.rawStackTrace))
                fields.Add($"\"rawStackTrace\":\"{EscapeJsonString(item.rawStackTrace)}\"");

            if (!string.IsNullOrEmpty(item.exceptionClass))
                fields.Add($"\"exceptionClass\":\"{EscapeJsonString(item.exceptionClass)}\"");

            if (!string.IsNullOrEmpty(item.fingerprint))
                fields.Add($"\"fingerprint\":\"{EscapeJsonString(item.fingerprint)}\"");

            if (item.device != null)
                fields.Add($"\"device\":{SerializeDeviceContext(item.device)}");

            if (item.network != null)
                fields.Add($"\"network\":{SerializeNetworkContext(item.network)}");

            if (item.gameState != null)
                fields.Add($"\"gameState\":{SerializeGameState(item.gameState)}");

            fields.Add($"\"appVersion\":\"{EscapeJsonString(item.appVersion)}\"");
            fields.Add($"\"buildNumber\":\"{EscapeJsonString(item.buildNumber)}\"");

            if (!string.IsNullOrEmpty(item.unityVersion))
                fields.Add($"\"unityVersion\":\"{EscapeJsonString(item.unityVersion)}\"");

            if (!string.IsNullOrEmpty(item.userId))
                fields.Add($"\"userId\":\"{EscapeJsonString(item.userId)}\"");

            if (!string.IsNullOrEmpty(item.sessionId))
                fields.Add($"\"sessionId\":\"{EscapeJsonString(item.sessionId)}\"");

            if (item.breadcrumbs != null && item.breadcrumbs.Count > 0)
                fields.Add($"\"breadcrumbs\":{SerializeBreadcrumbs(item.breadcrumbs)}");

            if (item.timestamp.HasValue)
                fields.Add($"\"timestamp\":{item.timestamp.Value}");

            if (item.networkRequest != null)
                fields.Add($"\"networkRequest\":{SerializeNetworkRequest(item.networkRequest)}");

            if (item.tags != null && item.tags.Count > 0)
                fields.Add($"\"tags\":{SerializeStringDictionary(item.tags)}");

            return "{" + string.Join(",", fields) + "}";
        }

        private string SerializeStackFrames(List<StackFrame> frames)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < frames.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(SerializeStackFrame(frames[i]));
            }
            sb.Append("]");
            return sb.ToString();
        }

        private string SerializeStackFrame(StackFrame frame)
        {
            var fields = new List<string>();

            if (!string.IsNullOrEmpty(frame.module))
                fields.Add($"\"module\":\"{EscapeJsonString(frame.module)}\"");
            if (!string.IsNullOrEmpty(frame.function))
                fields.Add($"\"function\":\"{EscapeJsonString(frame.function)}\"");
            if (!string.IsNullOrEmpty(frame.filename))
                fields.Add($"\"filename\":\"{EscapeJsonString(frame.filename)}\"");
            if (frame.lineno > 0)
                fields.Add($"\"lineno\":{frame.lineno}");
            if (frame.colno > 0)
                fields.Add($"\"colno\":{frame.colno}");
            if (!string.IsNullOrEmpty(frame.instructionAddress))
                fields.Add($"\"instructionAddress\":\"{EscapeJsonString(frame.instructionAddress)}\"");
            if (!string.IsNullOrEmpty(frame.symbolAddress))
                fields.Add($"\"symbolAddress\":\"{EscapeJsonString(frame.symbolAddress)}\"");
            if (frame.inApp)
                fields.Add("\"inApp\":true");
            if (!string.IsNullOrEmpty(frame.package))
                fields.Add($"\"package\":\"{EscapeJsonString(frame.package)}\"");

            return "{" + string.Join(",", fields) + "}";
        }

        private string SerializeDeviceContext(DeviceContext device)
        {
            var fields = new List<string>();

            fields.Add($"\"platform\":\"{EscapeJsonString(device.platform)}\"");
            fields.Add($"\"osVersion\":\"{EscapeJsonString(device.osVersion)}\"");
            fields.Add($"\"deviceModel\":\"{EscapeJsonString(device.deviceModel)}\"");

            if (!string.IsNullOrEmpty(device.manufacturer))
                fields.Add($"\"manufacturer\":\"{EscapeJsonString(device.manufacturer)}\"");
            if (!string.IsNullOrEmpty(device.cpuArchitecture))
                fields.Add($"\"cpuArchitecture\":\"{EscapeJsonString(device.cpuArchitecture)}\"");
            if (device.memoryUsedMb.HasValue)
                fields.Add($"\"memoryUsedMb\":{device.memoryUsedMb.Value.ToString(CultureInfo.InvariantCulture)}");
            if (device.memoryAvailableMb.HasValue)
                fields.Add($"\"memoryAvailableMb\":{device.memoryAvailableMb.Value.ToString(CultureInfo.InvariantCulture)}");
            if (device.cpuUsagePercent.HasValue)
                fields.Add($"\"cpuUsagePercent\":{device.cpuUsagePercent.Value.ToString(CultureInfo.InvariantCulture)}");
            if (device.fps.HasValue)
                fields.Add($"\"fps\":{device.fps.Value.ToString(CultureInfo.InvariantCulture)}");
            if (device.batteryLevel.HasValue)
                fields.Add($"\"batteryLevel\":{device.batteryLevel.Value.ToString(CultureInfo.InvariantCulture)}");
            if (device.batteryCharging.HasValue)
                fields.Add($"\"batteryCharging\":{(device.batteryCharging.Value ? "true" : "false")}");
            if (!string.IsNullOrEmpty(device.thermalState))
                fields.Add($"\"thermalState\":\"{EscapeJsonString(device.thermalState)}\"");

            return "{" + string.Join(",", fields) + "}";
        }

        private string SerializeNetworkContext(NetworkContext network)
        {
            var fields = new List<string>();

            if (!string.IsNullOrEmpty(network.type))
                fields.Add($"\"type\":\"{EscapeJsonString(network.type)}\"");
            if (!string.IsNullOrEmpty(network.carrier))
                fields.Add($"\"carrier\":\"{EscapeJsonString(network.carrier)}\"");
            if (!string.IsNullOrEmpty(network.effectiveType))
                fields.Add($"\"effectiveType\":\"{EscapeJsonString(network.effectiveType)}\"");

            return "{" + string.Join(",", fields) + "}";
        }

        private string SerializeGameState(GameState state)
        {
            var fields = new List<string>();

            if (!string.IsNullOrEmpty(state.sceneName))
                fields.Add($"\"sceneName\":\"{EscapeJsonString(state.sceneName)}\"");
            if (!string.IsNullOrEmpty(state.gameMode))
                fields.Add($"\"gameMode\":\"{EscapeJsonString(state.gameMode)}\"");
            if (!string.IsNullOrEmpty(state.levelId))
                fields.Add($"\"levelId\":\"{EscapeJsonString(state.levelId)}\"");
            if (state.customData != null && state.customData.Count > 0)
                fields.Add($"\"customData\":{SerializeObjectDictionary(state.customData)}");

            return "{" + string.Join(",", fields) + "}";
        }

        private string SerializeBreadcrumbs(List<Breadcrumb> breadcrumbs)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < breadcrumbs.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(SerializeBreadcrumb(breadcrumbs[i]));
            }
            sb.Append("]");
            return sb.ToString();
        }

        private string SerializeBreadcrumb(Breadcrumb bc)
        {
            var fields = new List<string>();

            fields.Add($"\"type\":\"{EscapeJsonString(bc.type)}\"");
            if (!string.IsNullOrEmpty(bc.category))
                fields.Add($"\"category\":\"{EscapeJsonString(bc.category)}\"");
            if (!string.IsNullOrEmpty(bc.message))
                fields.Add($"\"message\":\"{EscapeJsonString(bc.message)}\"");
            fields.Add($"\"level\":\"{EscapeJsonString(bc.level)}\"");
            if (bc.data != null && bc.data.Count > 0)
                fields.Add($"\"data\":{SerializeObjectDictionary(bc.data)}");
            fields.Add($"\"timestamp\":{bc.timestamp}");

            return "{" + string.Join(",", fields) + "}";
        }

        private string SerializeNetworkRequest(NetworkRequest req)
        {
            var fields = new List<string>();

            fields.Add($"\"url\":\"{EscapeJsonString(req.url)}\"");
            fields.Add($"\"method\":\"{EscapeJsonString(req.method)}\"");
            if (req.statusCode.HasValue)
                fields.Add($"\"statusCode\":{req.statusCode.Value}");
            if (req.durationMs.HasValue)
                fields.Add($"\"durationMs\":{req.durationMs.Value.ToString(CultureInfo.InvariantCulture)}");
            if (req.requestHeaders != null && req.requestHeaders.Count > 0)
                fields.Add($"\"requestHeaders\":{SerializeStringDictionary(req.requestHeaders)}");
            if (req.responseHeaders != null && req.responseHeaders.Count > 0)
                fields.Add($"\"responseHeaders\":{SerializeStringDictionary(req.responseHeaders)}");

            return "{" + string.Join(",", fields) + "}";
        }

        private string SerializeStringDictionary(Dictionary<string, string> dict)
        {
            var sb = new StringBuilder("{");
            bool first = true;
            foreach (var kvp in dict)
            {
                if (!first) sb.Append(",");
                sb.Append($"\"{EscapeJsonString(kvp.Key)}\":\"{EscapeJsonString(kvp.Value)}\"");
                first = false;
            }
            sb.Append("}");
            return sb.ToString();
        }

        private string SerializeObjectDictionary(Dictionary<string, object> dict)
        {
            var sb = new StringBuilder("{");
            bool first = true;
            foreach (var kvp in dict)
            {
                if (!first) sb.Append(",");
                sb.Append($"\"{EscapeJsonString(kvp.Key)}\":{SerializeValue(kvp.Value)}");
                first = false;
            }
            sb.Append("}");
            return sb.ToString();
        }

        private string SerializeValue(object value)
        {
            if (value == null)
                return "null";
            if (value is bool b)
                return b ? "true" : "false";
            if (value is int || value is long)
                return value.ToString();
            if (value is float f)
                return f.ToString(CultureInfo.InvariantCulture);
            if (value is double d)
                return d.ToString(CultureInfo.InvariantCulture);
            if (value is string s)
                return $"\"{EscapeJsonString(s)}\"";
            if (value is Dictionary<string, object> dict)
                return SerializeObjectDictionary(dict);
            // Default to string representation
            return $"\"{EscapeJsonString(value.ToString())}\"";
        }

        private string EscapeJsonString(string str)
        {
            if (string.IsNullOrEmpty(str))
                return "";

            var sb = new StringBuilder(str.Length);
            foreach (char c in str)
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
                            sb.Append($"\\u{(int)c:X4}");
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        #endregion
    }
}

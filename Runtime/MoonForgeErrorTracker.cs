using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoonForge.ErrorTracking
{
    /// <summary>
    /// Main entry point for MoonForge Error Tracking SDK.
    /// Provides crash and exception monitoring for Unity games.
    /// </summary>
    public class MoonForgeErrorTracker : MonoBehaviour
    {
        private static MoonForgeErrorTracker _instance;
        private static bool _isInitialized;
        private static bool _isShuttingDown;

        /// <summary>
        /// Singleton instance of the error tracker
        /// </summary>
        public static MoonForgeErrorTracker Instance
        {
            get
            {
                if (_isShuttingDown) return null;

                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<MoonForgeErrorTracker>();
                }

                return _instance;
            }
        }

        /// <summary>
        /// Check if the tracker is initialized and ready
        /// </summary>
        public static bool IsInitialized => _isInitialized && _instance != null;

        [Header("Configuration")]
        [SerializeField]
        private ErrorTrackerConfig _config;

        // Components
        private UnityExceptionHandler _exceptionHandler;
        private HttpTransport _transport;
        private BatchQueue _batchQueue;
        private OfflineStorage _offlineStorage;
        private AdaptiveSampler _sampler;

        // State
        private string _userId;
        private string _sessionId;
        private Dictionary<string, string> _userTags;
        private float _lastCleanupTime;
        private const float CleanupInterval = 300f; // 5 minutes

        #region Initialization

        /// <summary>
        /// Initialize the error tracker with configuration.
        /// Call this early in your game's startup (e.g., in a boot scene).
        /// </summary>
        public static MoonForgeErrorTracker Initialize(ErrorTrackerConfig config)
        {
            if (_isInitialized)
            {
                Debug.LogWarning("[MoonForge] Error tracker is already initialized");
                return _instance;
            }

            if (config == null)
            {
                Debug.LogError("[MoonForge] Configuration is required");
                return null;
            }

            if (!config.Validate(out var error))
            {
                Debug.LogError($"[MoonForge] Invalid configuration: {error}");
                return null;
            }

            // Check if we should run in editor
            if (Application.isEditor && !config.enableInEditor)
            {
                Debug.Log("[MoonForge] Error tracking disabled in Editor (set enableInEditor to true to enable)");
                return null;
            }

            // Create GameObject
            var go = new GameObject("MoonForgeErrorTracker");
            DontDestroyOnLoad(go);

            var tracker = go.AddComponent<MoonForgeErrorTracker>();
            tracker._config = config;
            tracker.InitializeComponents();

            _instance = tracker;
            _isInitialized = true;

            if (config.debugMode)
            {
                Debug.Log("[MoonForge] Error tracker initialized");
            }

            return tracker;
        }

        private void InitializeComponents()
        {
            // Generate session ID
            _sessionId = Guid.NewGuid().ToString();
            _userTags = new Dictionary<string, string>();

            // Initialize components
            _transport = new HttpTransport(_config, this);
            _offlineStorage = new OfflineStorage(_config);
            _sampler = new AdaptiveSampler(_config);
            _batchQueue = new BatchQueue(_config, _transport, OnSendFailed);

            // Initialize context collectors
            BreadcrumbTracker.Instance.Configure(_config.maxBreadcrumbs);

            // Initialize exception handler
            _exceptionHandler = new UnityExceptionHandler(_config, OnErrorCaptured);
            _exceptionHandler.Register();

            // Initialize native crash handler (iOS/Android)
            if (_config.captureNativeCrashes)
            {
                NativeCrashHandler.Initialize(_config, OnNativeCrashCaptured);
            }

            // Initialize network error interceptor
            NetworkErrorInterceptor.Initialize(_config, OnNetworkErrorCaptured);

            // Subscribe to scene changes
            if (_config.trackSceneChanges)
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
            }

            // Send any stored offline errors
            SendStoredErrors();
        }

        private void OnDestroy()
        {
            _isShuttingDown = true;

            if (_exceptionHandler != null)
            {
                _exceptionHandler.Unregister();
            }

            // Shutdown native crash handler
            if (NativeCrashHandler.IsInitialized)
            {
                NativeCrashHandler.Shutdown();
            }

            if (_config.trackSceneChanges)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }

            // Flush remaining errors
            _batchQueue?.Flush();

            _isInitialized = false;
            _instance = null;
        }

        #endregion

        #region Unity Lifecycle

        private void Update()
        {
            // Update FPS tracking
            DeviceContextCollector.Instance.UpdateFps();

            // Update batch queue
            if (_config.enableBatching)
            {
                _batchQueue?.Update();
            }

            // Periodic cleanup
            if (Time.unscaledTime - _lastCleanupTime > CleanupInterval)
            {
                _lastCleanupTime = Time.unscaledTime;
                _sampler?.Cleanup();
                _offlineStorage?.Cleanup();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                // Flush errors when app is paused
                _batchQueue?.Flush();
            }
            else
            {
                // Try to send stored errors when app resumes
                SendStoredErrors();
            }
        }

        private void OnApplicationQuit()
        {
            // Store any queued errors before quit
            if (_config.enableOfflineStorage && _batchQueue != null)
            {
                var queuedItems = _batchQueue.GetQueuedItems();
                foreach (var item in queuedItems)
                {
                    // Convert back to payload for storage
                    var payload = new ErrorPayloadInner
                    {
                        game = _config.gameId,
                        errorType = item.errorType,
                        errorCategory = item.errorCategory,
                        errorLevel = item.errorLevel,
                        message = item.message,
                        exceptionClass = item.exceptionClass,
                        device = item.device,
                        gameState = item.gameState,
                        appVersion = item.appVersion,
                        buildNumber = item.buildNumber,
                        timestamp = item.timestamp
                    };
                    _offlineStorage.Store(payload);
                }
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Set the current user for error attribution
        /// </summary>
        /// <param name="userId">User identifier</param>
        /// <param name="tags">Optional user tags (e.g., plan, role)</param>
        public void SetUser(string userId, Dictionary<string, string> tags = null)
        {
            _userId = userId;
            _userTags = tags ?? new Dictionary<string, string>();

            if (_config.debugMode)
            {
                Debug.Log($"[MoonForge] User set: {userId}");
            }
        }

        /// <summary>
        /// Clear the current user (on logout)
        /// </summary>
        public void ClearUser()
        {
            _userId = null;
            _userTags?.Clear();
        }

        /// <summary>
        /// Set the current game state
        /// </summary>
        public void SetGameState(string sceneName = null, string gameMode = null, string levelId = null)
        {
            if (!string.IsNullOrEmpty(gameMode))
            {
                GameStateCollector.Instance.SetGameMode(gameMode);
            }
            if (!string.IsNullOrEmpty(levelId))
            {
                GameStateCollector.Instance.SetLevelId(levelId);
            }
        }

        /// <summary>
        /// Add custom game state data
        /// </summary>
        public void SetGameStateData(string key, object value)
        {
            GameStateCollector.Instance.SetCustomData(key, value);
        }

        /// <summary>
        /// Add a breadcrumb to track user actions
        /// </summary>
        public void AddBreadcrumb(string message, BreadcrumbType type = BreadcrumbType.User, BreadcrumbLevel level = BreadcrumbLevel.Info, string category = null)
        {
            BreadcrumbTracker.Instance.Add(type, message, level, category);
        }

        /// <summary>
        /// Add a breadcrumb with custom data
        /// </summary>
        public void AddBreadcrumb(Breadcrumb breadcrumb)
        {
            BreadcrumbTracker.Instance.Add(breadcrumb);
        }

        /// <summary>
        /// Manually capture an exception
        /// </summary>
        public void CaptureException(Exception exception, ErrorLevel level = ErrorLevel.Error, Dictionary<string, string> tags = null)
        {
            if (exception == null) return;

            var payload = new ErrorPayloadInner
            {
                game = _config.gameId,
                errorType = "exception",
                errorCategory = "handled",
                errorLevel = level.ToString().ToLowerInvariant(),
                message = exception.Message,
                exceptionClass = exception.GetType().FullName,
                rawStackTrace = exception.StackTrace,
                device = DeviceContextCollector.Instance.Collect(),
                network = DeviceContextCollector.Instance.CollectNetworkContext(),
                gameState = GameStateCollector.Instance.Collect(),
                appVersion = Application.version,
                buildNumber = GetBuildNumber(),
                unityVersion = Application.unityVersion,
                userId = _userId,
                sessionId = _sessionId,
                breadcrumbs = BreadcrumbTracker.Instance.GetBreadcrumbs(),
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                tags = MergeTags(tags)
            };

            OnErrorCaptured(payload);
        }

        /// <summary>
        /// Capture a network error
        /// </summary>
        public void CaptureNetworkError(string url, string method, int statusCode, string errorMessage, float? durationMs = null, Dictionary<string, string> tags = null)
        {
            var payload = new ErrorPayloadInner
            {
                game = _config.gameId,
                errorType = "network",
                errorCategory = "handled",
                errorLevel = statusCode >= 500 ? "error" : "warning",
                message = errorMessage,
                device = DeviceContextCollector.Instance.Collect(),
                network = DeviceContextCollector.Instance.CollectNetworkContext(),
                gameState = GameStateCollector.Instance.Collect(),
                appVersion = Application.version,
                buildNumber = GetBuildNumber(),
                unityVersion = Application.unityVersion,
                userId = _userId,
                sessionId = _sessionId,
                breadcrumbs = BreadcrumbTracker.Instance.GetBreadcrumbs(),
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                networkRequest = new NetworkRequest
                {
                    url = url,
                    method = method,
                    statusCode = statusCode,
                    durationMs = durationMs
                },
                tags = MergeTags(tags)
            };

            // Add network breadcrumb
            BreadcrumbTracker.Instance.AddNetworkRequest(method, url, statusCode, durationMs);

            OnErrorCaptured(payload);
        }

        /// <summary>
        /// Capture a custom error
        /// </summary>
        public void CaptureMessage(string message, ErrorLevel level = ErrorLevel.Info, Dictionary<string, string> tags = null)
        {
            var payload = new ErrorPayloadInner
            {
                game = _config.gameId,
                errorType = "custom",
                errorCategory = "handled",
                errorLevel = level.ToString().ToLowerInvariant(),
                message = message,
                device = DeviceContextCollector.Instance.Collect(),
                network = DeviceContextCollector.Instance.CollectNetworkContext(),
                gameState = GameStateCollector.Instance.Collect(),
                appVersion = Application.version,
                buildNumber = GetBuildNumber(),
                unityVersion = Application.unityVersion,
                userId = _userId,
                sessionId = _sessionId,
                breadcrumbs = BreadcrumbTracker.Instance.GetBreadcrumbs(),
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                tags = MergeTags(tags)
            };

            OnErrorCaptured(payload);
        }

        /// <summary>
        /// Flush all queued errors immediately
        /// </summary>
        public void Flush()
        {
            _batchQueue?.Flush();
        }

        #endregion

        #region Internal Methods

        private void OnErrorCaptured(ErrorPayloadInner payload)
        {
            if (_config.debugMode)
            {
                Debug.Log($"[MoonForge] OnErrorCaptured: {payload.errorLevel} - {payload.message?.Substring(0, Math.Min(50, payload.message?.Length ?? 0))}");
            }

            // Add user context
            payload.userId = _userId;
            payload.sessionId = _sessionId;

            // Apply sampling
            var decision = _sampler.ShouldSample(payload);
            if (!decision.ShouldSend)
            {
                if (_config.debugMode)
                {
                    Debug.Log($"[MoonForge] Error sampled out (rate={decision.SampleRate})");
                }
                return;
            }

            payload.fingerprint = decision.Fingerprint;

            // Check connectivity
            if (!_transport.HasConnectivity())
            {
                if (_config.debugMode)
                {
                    Debug.Log("[MoonForge] No connectivity, storing offline");
                }
                if (_config.enableOfflineStorage)
                {
                    _offlineStorage.Store(payload);
                }
                return;
            }

            // Send or queue
            if (_config.enableBatching)
            {
                if (_config.debugMode)
                {
                    Debug.Log("[MoonForge] Queuing error for batch send");
                }
                _batchQueue.Enqueue(payload);
            }
            else
            {
                if (_config.debugMode)
                {
                    Debug.Log("[MoonForge] Sending error directly");
                }
                SendErrorDirectly(payload);
            }
        }

        private void SendErrorDirectly(ErrorPayloadInner payload)
        {
            var errorPayload = new ErrorPayload { payload = payload };

            _transport.SendError(errorPayload, response =>
            {
                if (response?.status == "error" && _config.enableOfflineStorage)
                {
                    _offlineStorage.Store(payload);
                }
            });
        }

        private void OnSendFailed(ErrorPayloadInner payload)
        {
            if (_config.enableOfflineStorage)
            {
                _offlineStorage.Store(payload);
            }
        }

        private void SendStoredErrors()
        {
            if (!_config.enableOfflineStorage) return;
            if (!_transport.HasConnectivity()) return;

            var storedErrors = _offlineStorage.GetStoredErrors();
            if (storedErrors.Count == 0) return;

            if (_config.debugMode)
            {
                Debug.Log($"[MoonForge] Sending {storedErrors.Count} stored errors");
            }

            // Clear storage and enqueue
            _offlineStorage.Clear();

            foreach (var error in storedErrors)
            {
                if (_config.enableBatching)
                {
                    _batchQueue.Enqueue(error);
                }
                else
                {
                    SendErrorDirectly(error);
                }
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            BreadcrumbTracker.Instance.AddNavigation($"Loaded scene: {scene.name}", "scene");
        }

        private void OnNativeCrashCaptured(ErrorPayloadInner payload)
        {
            // Native crashes are always critical - skip sampling
            payload.userId = _userId;
            payload.sessionId = _sessionId;
            payload.tags = MergeTags(payload.tags);

            if (_config.debugMode)
            {
                Debug.Log($"[MoonForge] Native crash captured: {payload.message}");
            }

            // Try to send immediately (crash is imminent)
            if (_transport.HasConnectivity())
            {
                SendErrorDirectly(payload);
            }
            else if (_config.enableOfflineStorage)
            {
                _offlineStorage.Store(payload);
            }
        }

        private void OnNetworkErrorCaptured(NetworkRequest networkRequest, string errorMessage, int statusCode)
        {
            var payload = new ErrorPayloadInner
            {
                game = _config.gameId,
                errorType = "network",
                errorCategory = "handled",
                errorLevel = statusCode >= 500 ? "error" : "warning",
                message = errorMessage,
                device = DeviceContextCollector.Instance.Collect(),
                network = DeviceContextCollector.Instance.CollectNetworkContext(),
                gameState = GameStateCollector.Instance.Collect(),
                appVersion = Application.version,
                buildNumber = GetBuildNumber(),
                unityVersion = Application.unityVersion,
                userId = _userId,
                sessionId = _sessionId,
                breadcrumbs = BreadcrumbTracker.Instance.GetBreadcrumbs(),
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                networkRequest = networkRequest
            };

            OnErrorCaptured(payload);
        }

        private Dictionary<string, string> MergeTags(Dictionary<string, string> additionalTags)
        {
            var tags = new Dictionary<string, string>();

            // Add user tags
            if (_userTags != null)
            {
                foreach (var kvp in _userTags)
                {
                    tags[kvp.Key] = kvp.Value;
                }
            }

            // Add additional tags
            if (additionalTags != null)
            {
                foreach (var kvp in additionalTags)
                {
                    tags[kvp.Key] = kvp.Value;
                }
            }

            return tags.Count > 0 ? tags : null;
        }

        private string GetBuildNumber()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                    .GetStatic<AndroidJavaObject>("currentActivity"))
                using (var packageManager = activity.Call<AndroidJavaObject>("getPackageManager"))
                {
                    var packageName = activity.Call<string>("getPackageName");
                    var packageInfo = packageManager.Call<AndroidJavaObject>("getPackageInfo", packageName, 0);
                    var versionCode = packageInfo.Get<long>("longVersionCode");
                    return versionCode.ToString();
                }
            }
            catch
            {
                return Application.buildGUID?.Substring(0, 8) ?? "unknown";
            }
#else
            return Application.buildGUID?.Substring(0, 8) ?? "unknown";
#endif
        }

        #endregion
    }
}

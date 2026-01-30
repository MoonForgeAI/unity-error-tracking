using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoonForge.ErrorTracking.Analytics
{
    /// <summary>
    /// Main analytics class for MoonForge SDK.
    /// Provides screen view tracking, custom event tracking, and user identification.
    /// </summary>
    public static class MoonForgeAnalytics
    {
        private static AnalyticsTransport _transport;
        private static ErrorTrackerConfig _config;
        private static MonoBehaviour _coroutineRunner;
        private static bool _isInitialized;

        private static string _sessionId;
        private static string _distinctId;
        private static string _currentScene;
        private static string _previousScene;
        private static Dictionary<string, object> _userProperties;
        private static long _sessionStartTime;
        private static long _lastActivityTime;

        /// <summary>
        /// Check if analytics is initialized and ready
        /// </summary>
        public static bool IsInitialized => _isInitialized;

        /// <summary>
        /// Initialize analytics with the given configuration.
        /// Called automatically by MoonForgeErrorTracker if analytics is enabled.
        /// </summary>
        internal static void Initialize(ErrorTrackerConfig config, MonoBehaviour coroutineRunner)
        {
            if (_isInitialized)
            {
                if (config.debugMode)
                {
                    Debug.LogWarning("[MoonForge Analytics] Already initialized");
                }
                return;
            }

            if (!config.enableAnalytics)
            {
                if (config.debugMode)
                {
                    Debug.Log("[MoonForge Analytics] Analytics disabled in configuration");
                }
                return;
            }

            _config = config;
            _coroutineRunner = coroutineRunner;
            _transport = new AnalyticsTransport(config, coroutineRunner);
            _userProperties = new Dictionary<string, object>();

            // Generate or restore session/distinct IDs
            _sessionId = Guid.NewGuid().ToString();
            _distinctId = GetOrCreateDistinctId();
            _sessionStartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            _lastActivityTime = _sessionStartTime;

            // Get current scene
            _currentScene = SceneManager.GetActiveScene().name;

            // Subscribe to scene changes if auto-tracking is enabled
            if (config.trackSceneViewsAutomatically)
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
            }

            _isInitialized = true;

            if (config.debugMode)
            {
                Debug.Log($"[MoonForge Analytics] Initialized with distinctId: {_distinctId}, sessionId: {_sessionId}");
            }

            // Track session start
            TrackEvent("session_start", new Dictionary<string, object>
            {
                { "session_id", _sessionId }
            });

            // Track initial screen view
            if (config.trackSceneViewsAutomatically && !string.IsNullOrEmpty(_currentScene))
            {
                TrackScreenView(_currentScene);
            }

            // Flush any offline events
            _transport.FlushOfflineQueue();
        }

        /// <summary>
        /// Shutdown analytics and clean up resources
        /// </summary>
        internal static void Shutdown()
        {
            if (!_isInitialized)
                return;

            // Unsubscribe from scene changes
            SceneManager.sceneLoaded -= OnSceneLoaded;

            // Track session end
            var sessionDuration = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - _sessionStartTime;
            TrackEvent("session_end", new Dictionary<string, object>
            {
                { "session_id", _sessionId },
                { "duration_seconds", sessionDuration }
            });

            _isInitialized = false;
            _transport = null;
            _config = null;
            _coroutineRunner = null;
        }

        /// <summary>
        /// Track a screen/scene view
        /// </summary>
        /// <param name="screenName">The name of the screen or scene</param>
        public static void TrackScreenView(string screenName)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[MoonForge Analytics] Not initialized. Call MoonForgeErrorTracker.Initialize() first.");
                return;
            }

            if (string.IsNullOrEmpty(screenName))
                return;

            UpdateLastActivity();

            var analyticsEvent = new AnalyticsEvent
            {
                type = "event",
                payload = new AnalyticsEventPayload
                {
                    game = _config.gameId,
                    screen = $"{Screen.width}x{Screen.height}",
                    language = GetLanguageCode(),
                    url = $"scene://{screenName}",
                    title = screenName,
                    referrer = !string.IsNullOrEmpty(_previousScene) ? $"scene://{_previousScene}" : null,
                    name = null, // null name indicates a pageview/screen view
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                }
            };

            _transport.SendEvent(analyticsEvent);

            if (_config.debugMode)
            {
                Debug.Log($"[MoonForge Analytics] Tracked screen view: {screenName}");
            }
        }

        /// <summary>
        /// Track a custom event
        /// </summary>
        /// <param name="eventName">The name of the event</param>
        /// <param name="data">Optional event data</param>
        public static void TrackEvent(string eventName, Dictionary<string, object> data = null)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[MoonForge Analytics] Not initialized. Call MoonForgeErrorTracker.Initialize() first.");
                return;
            }

            if (string.IsNullOrEmpty(eventName))
                return;

            UpdateLastActivity();

            // Merge user properties with event data
            var mergedData = new Dictionary<string, object>();
            if (_userProperties != null)
            {
                foreach (var kvp in _userProperties)
                {
                    mergedData[kvp.Key] = kvp.Value;
                }
            }
            if (data != null)
            {
                foreach (var kvp in data)
                {
                    mergedData[kvp.Key] = kvp.Value;
                }
            }

            var analyticsEvent = new AnalyticsEvent
            {
                type = "event",
                payload = new AnalyticsEventPayload
                {
                    game = _config.gameId,
                    screen = $"{Screen.width}x{Screen.height}",
                    language = GetLanguageCode(),
                    url = !string.IsNullOrEmpty(_currentScene) ? $"scene://{_currentScene}" : null,
                    title = _currentScene,
                    name = eventName,
                    data = mergedData.Count > 0 ? mergedData : null,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                }
            };

            _transport.SendEvent(analyticsEvent);

            if (_config.debugMode)
            {
                Debug.Log($"[MoonForge Analytics] Tracked event: {eventName}");
            }
        }

        /// <summary>
        /// Identify the current user
        /// </summary>
        /// <param name="userId">The user's unique identifier</param>
        /// <param name="traits">Optional user traits/properties</param>
        public static void Identify(string userId, Dictionary<string, object> traits = null)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[MoonForge Analytics] Not initialized. Call MoonForgeErrorTracker.Initialize() first.");
                return;
            }

            if (string.IsNullOrEmpty(userId))
                return;

            // Update distinct ID to user ID
            _distinctId = userId;
            SaveDistinctId(userId);

            UpdateLastActivity();

            var identifyEvent = new IdentifyEvent
            {
                type = "identify",
                payload = new IdentifyEventPayload
                {
                    game = _config.gameId,
                    id = userId,
                    data = traits,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                }
            };

            _transport.SendIdentify(identifyEvent);

            if (_config.debugMode)
            {
                Debug.Log($"[MoonForge Analytics] Identified user: {userId}");
            }
        }

        /// <summary>
        /// Set a user property that will be included with all subsequent events
        /// </summary>
        /// <param name="key">Property key</param>
        /// <param name="value">Property value</param>
        public static void SetUserProperty(string key, object value)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[MoonForge Analytics] Not initialized. Call MoonForgeErrorTracker.Initialize() first.");
                return;
            }

            if (string.IsNullOrEmpty(key))
                return;

            _userProperties[key] = value;

            if (_config.debugMode)
            {
                Debug.Log($"[MoonForge Analytics] Set user property: {key} = {value}");
            }
        }

        /// <summary>
        /// Remove a user property
        /// </summary>
        /// <param name="key">Property key to remove</param>
        public static void RemoveUserProperty(string key)
        {
            if (!_isInitialized || string.IsNullOrEmpty(key))
                return;

            _userProperties.Remove(key);
        }

        /// <summary>
        /// Clear all user properties
        /// </summary>
        public static void ClearUserProperties()
        {
            if (!_isInitialized)
                return;

            _userProperties.Clear();
        }

        /// <summary>
        /// Flush any queued offline events
        /// </summary>
        public static void Flush()
        {
            if (!_isInitialized)
                return;

            _transport?.FlushOfflineQueue();
        }

        /// <summary>
        /// Reset the analytics state (clears user ID and properties)
        /// </summary>
        public static void Reset()
        {
            if (!_isInitialized)
                return;

            _distinctId = Guid.NewGuid().ToString();
            SaveDistinctId(_distinctId);
            _userProperties.Clear();
            _sessionId = Guid.NewGuid().ToString();
            _sessionStartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (_config.debugMode)
            {
                Debug.Log("[MoonForge Analytics] Reset analytics state");
            }
        }

        /// <summary>
        /// Get the current distinct ID
        /// </summary>
        public static string GetDistinctId()
        {
            return _distinctId;
        }

        /// <summary>
        /// Get the current session ID
        /// </summary>
        public static string GetSessionId()
        {
            return _sessionId;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_isInitialized)
                return;

            _previousScene = _currentScene;
            _currentScene = scene.name;

            TrackScreenView(scene.name);
        }

        private static void UpdateLastActivity()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Check for session timeout
            if (_config != null && _config.sessionTimeoutSeconds > 0)
            {
                var timeSinceLastActivity = now - _lastActivityTime;
                if (timeSinceLastActivity > _config.sessionTimeoutSeconds)
                {
                    // Session timed out, start a new one
                    var oldSessionId = _sessionId;
                    _sessionId = Guid.NewGuid().ToString();
                    _sessionStartTime = now;

                    if (_config.debugMode)
                    {
                        Debug.Log($"[MoonForge Analytics] Session timed out, starting new session: {_sessionId}");
                    }

                    // Track new session start
                    TrackEvent("session_start", new Dictionary<string, object>
                    {
                        { "session_id", _sessionId },
                        { "previous_session_id", oldSessionId }
                    });
                }
            }

            _lastActivityTime = now;
        }

        private static string GetOrCreateDistinctId()
        {
            const string key = "MoonForge_Analytics_DistinctId";
            var storedId = PlayerPrefs.GetString(key, "");

            if (string.IsNullOrEmpty(storedId))
            {
                storedId = Guid.NewGuid().ToString();
                PlayerPrefs.SetString(key, storedId);
                PlayerPrefs.Save();
            }

            return storedId;
        }

        private static void SaveDistinctId(string id)
        {
            const string key = "MoonForge_Analytics_DistinctId";
            PlayerPrefs.SetString(key, id);
            PlayerPrefs.Save();
        }

        private static string GetLanguageCode()
        {
            // Map Unity's SystemLanguage to language codes
            var language = Application.systemLanguage;

            return language switch
            {
                SystemLanguage.English => "en",
                SystemLanguage.French => "fr",
                SystemLanguage.German => "de",
                SystemLanguage.Spanish => "es",
                SystemLanguage.Italian => "it",
                SystemLanguage.Portuguese => "pt",
                SystemLanguage.Russian => "ru",
                SystemLanguage.Japanese => "ja",
                SystemLanguage.Chinese => "zh",
                SystemLanguage.ChineseSimplified => "zh-CN",
                SystemLanguage.ChineseTraditional => "zh-TW",
                SystemLanguage.Korean => "ko",
                SystemLanguage.Arabic => "ar",
                SystemLanguage.Dutch => "nl",
                SystemLanguage.Polish => "pl",
                SystemLanguage.Turkish => "tr",
                SystemLanguage.Thai => "th",
                SystemLanguage.Vietnamese => "vi",
                SystemLanguage.Indonesian => "id",
                SystemLanguage.Swedish => "sv",
                SystemLanguage.Norwegian => "no",
                SystemLanguage.Danish => "da",
                SystemLanguage.Finnish => "fi",
                SystemLanguage.Greek => "el",
                SystemLanguage.Hebrew => "he",
                SystemLanguage.Hungarian => "hu",
                SystemLanguage.Czech => "cs",
                SystemLanguage.Romanian => "ro",
                SystemLanguage.Ukrainian => "uk",
                _ => "en"
            };
        }
    }
}

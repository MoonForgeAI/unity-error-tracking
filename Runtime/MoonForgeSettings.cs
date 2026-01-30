using UnityEngine;
using System;
using System.IO;

namespace MoonForge.ErrorTracking
{
    /// <summary>
    /// Centralized settings for MoonForge Error Tracking.
    /// Auto-loads from Resources and provides simple configuration.
    ///
    /// SETUP: Just set your Game ID via the menu: MoonForge > Setup Error Tracking
    /// That's it! The SDK will auto-initialize on game start.
    /// </summary>
    public class MoonForgeSettings : ScriptableObject
    {
        private const string SETTINGS_PATH = "MoonForgeSettings";
        private const string RESOURCES_FOLDER = "Assets/Resources";
        private const string ASSET_PATH = "Assets/Resources/MoonForgeSettings.asset";

        private static MoonForgeSettings _instance;

        // ═══════════════════════════════════════════════════════════════════
        // REQUIRED SETTINGS
        // ═══════════════════════════════════════════════════════════════════

        [Header("═══ REQUIRED ═══")]
        [Tooltip("Your Game ID from MoonForge dashboard (Settings > Game ID)")]
        public string gameId = "";

        // ═══════════════════════════════════════════════════════════════════
        // BASIC SETTINGS (Most users only need these)
        // ═══════════════════════════════════════════════════════════════════

        [Header("═══ BASIC SETTINGS ═══")]
        [Tooltip("Enable error tracking (turn off to disable all tracking)")]
        public bool enabled = true;

        [Tooltip("Enable tracking in Unity Editor (useful for testing)")]
        public bool enableInEditor = false;

        [Tooltip("Show debug logs in console")]
        public bool debugMode = false;

        // ═══════════════════════════════════════════════════════════════════
        // ADVANCED SETTINGS (Hidden by default in inspector)
        // ═══════════════════════════════════════════════════════════════════

        [Header("═══ ANALYTICS ═══")]
        [Tooltip("Enable analytics tracking (screen views, custom events, user identification)")]
        public bool enableAnalytics = true;

        [Tooltip("Automatically track scene/screen views when scenes are loaded")]
        public bool trackSceneViewsAutomatically = true;

        [Tooltip("Session timeout in seconds. A new session starts after this period of inactivity.")]
        [Range(60, 7200)]
        public int sessionTimeoutSeconds = 1800;

        [Header("═══ ADVANCED (Optional) ═══")]
        [Tooltip("API endpoint base URL - the URL of your MoonForge collector service (without /api/errors)")]
        public string apiEndpoint = "https://collector.moonforge.co";

        [Tooltip("Capture unhandled C# exceptions")]
        public bool captureUnhandledExceptions = true;

        [Tooltip("Capture Debug.LogError and Debug.LogException calls")]
        public bool captureLogErrors = true;

        [Tooltip("Capture native iOS/Android crashes")]
        public bool captureNativeCrashes = true;

        [Tooltip("Track scene changes as breadcrumbs")]
        public bool trackSceneChanges = true;

        [Tooltip("Maximum breadcrumbs to keep (10-200)")]
        [Range(10, 200)]
        public int maxBreadcrumbs = 100;

        [Tooltip("Enable batching for better performance")]
        public bool enableBatching = true;

        [Tooltip("Batch size before sending")]
        [Range(1, 50)]
        public int batchSize = 10;

        [Tooltip("Max seconds to wait before sending batch")]
        [Range(1, 60)]
        public float batchWaitSeconds = 5f;

        [Tooltip("Store errors offline when no connection")]
        public bool enableOfflineStorage = true;

        [Tooltip("Automatically scrub sensitive data (passwords, tokens, etc.)")]
        public bool scrubSensitiveData = true;

        [Tooltip("Request timeout in seconds")]
        [Range(5, 60)]
        public int requestTimeoutSeconds = 30;

        [Tooltip("Auto-upload debug symbols on build")]
        public bool autoUploadSymbols = true;

        // ═══════════════════════════════════════════════════════════════════
        // INSTANCE ACCESS
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gets the MoonForge settings instance. Auto-loads from Resources.
        /// </summary>
        public static MoonForgeSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<MoonForgeSettings>(SETTINGS_PATH);

                    #if UNITY_EDITOR
                    // In editor, create if not exists (helpful for first-time setup)
                    if (_instance == null)
                    {
                        _instance = CreateSettingsAsset();
                    }
                    #endif
                }
                return _instance;
            }
        }

        /// <summary>
        /// Check if settings are valid for initialization.
        /// </summary>
        public bool IsValid => !string.IsNullOrEmpty(gameId) && IsValidGameId(gameId);

        /// <summary>
        /// Check if the SDK should be active in current environment.
        /// </summary>
        public bool ShouldBeActive
        {
            get
            {
                if (!enabled) return false;

                #if UNITY_EDITOR
                return enableInEditor;
                #else
                return true;
                #endif
            }
        }

        /// <summary>
        /// Validates Game ID format (UUID).
        /// </summary>
        public static bool IsValidGameId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            return Guid.TryParse(id, out _);
        }

        #if UNITY_EDITOR
        /// <summary>
        /// Creates the settings asset in Resources folder.
        /// </summary>
        internal static MoonForgeSettings CreateSettingsAsset()
        {
            // Ensure Resources folder exists
            if (!UnityEditor.AssetDatabase.IsValidFolder(RESOURCES_FOLDER))
            {
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");
            }

            // Create the asset
            var settings = CreateInstance<MoonForgeSettings>();
            UnityEditor.AssetDatabase.CreateAsset(settings, ASSET_PATH);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();

            Debug.Log("[MoonForge] Created settings asset at: " + ASSET_PATH);
            return settings;
        }

        /// <summary>
        /// Gets or creates the settings asset (editor only).
        /// </summary>
        public static MoonForgeSettings GetOrCreateSettings()
        {
            var settings = UnityEditor.AssetDatabase.LoadAssetAtPath<MoonForgeSettings>(ASSET_PATH);
            if (settings == null)
            {
                settings = CreateSettingsAsset();
            }
            return settings;
        }

        /// <summary>
        /// Opens the settings in inspector.
        /// </summary>
        public static void SelectSettings()
        {
            var settings = GetOrCreateSettings();
            UnityEditor.Selection.activeObject = settings;
            UnityEditor.EditorGUIUtility.PingObject(settings);
        }
        #endif

        /// <summary>
        /// Converts simplified settings to full ErrorTrackerConfig for the tracker.
        /// </summary>
        internal ErrorTrackerConfig ToFullConfig()
        {
            var config = CreateInstance<ErrorTrackerConfig>();

            // Required
            config.gameId = gameId;
            config.apiEndpoint = apiEndpoint;

            // Error capture
            config.captureUnhandledExceptions = captureUnhandledExceptions;
            config.captureLogErrors = captureLogErrors;
            config.captureNativeCrashes = captureNativeCrashes;

            // Breadcrumbs
            config.maxBreadcrumbs = maxBreadcrumbs;
            config.trackSceneChanges = trackSceneChanges;

            // Batching
            config.enableBatching = enableBatching;
            config.maxBatchSize = batchSize;
            config.maxBatchWaitTime = batchWaitSeconds;

            // Offline
            config.enableOfflineStorage = enableOfflineStorage;

            // Privacy
            config.scrubSensitiveData = scrubSensitiveData;

            // Network
            config.requestTimeout = requestTimeoutSeconds;

            // Symbols
            config.autoUploadSymbols = autoUploadSymbols;

            // Debug
            config.debugMode = debugMode;
            config.enableInEditor = enableInEditor;

            // Analytics
            config.enableAnalytics = enableAnalytics;
            config.trackSceneViewsAutomatically = trackSceneViewsAutomatically;
            config.sessionTimeoutSeconds = sessionTimeoutSeconds;

            return config;
        }
    }

    /// <summary>
    /// Alias for ErrorLevel for more intuitive API usage.
    /// </summary>
    public enum ErrorSeverity
    {
        Info = ErrorLevel.Info,
        Warning = ErrorLevel.Warning,
        Error = ErrorLevel.Error,
        Fatal = ErrorLevel.Fatal
    }
}

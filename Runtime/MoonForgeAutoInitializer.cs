using UnityEngine;

namespace MoonForge.ErrorTracking
{
    /// <summary>
    /// Automatically initializes MoonForge Error Tracking on game start.
    /// No manual setup required - just configure your Game ID in MoonForgeSettings.
    ///
    /// This script uses [RuntimeInitializeOnLoadMethod] to run before any scene loads,
    /// ensuring errors are captured from the very first frame.
    /// </summary>
    public static class MoonForgeAutoInitializer
    {
        private static bool _initializationAttempted = false;

        /// <summary>
        /// Called automatically before the first scene loads.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            Debug.Log("[MoonForge] AutoInitialize called");

            if (_initializationAttempted)
            {
                Debug.Log("[MoonForge] Already attempted initialization, skipping");
                return;
            }
            _initializationAttempted = true;

            var settings = MoonForgeSettings.Instance;

            // Check if settings exist
            if (settings == null)
            {
                Debug.LogWarning("[MoonForge] Settings not found. Run 'MoonForge > Setup Error Tracking' to configure.");
                return;
            }

            Debug.Log($"[MoonForge] Settings loaded - enabled={settings.enabled}, enableInEditor={settings.enableInEditor}, gameId={settings.gameId}");

            // Check if SDK should be active
            if (!settings.ShouldBeActive)
            {
                Debug.Log($"[MoonForge] Error tracking disabled - ShouldBeActive=false (enabled={settings.enabled}, enableInEditor={settings.enableInEditor})");
                return;
            }

            // Validate settings
            if (!settings.IsValid)
            {
                Debug.LogWarning($"[MoonForge] Invalid Game ID: '{settings.gameId}'. Please enter your Game ID via 'MoonForge > Setup Error Tracking'.");
                return;
            }

            Debug.Log("[MoonForge] Settings valid, initializing tracker...");

            // Initialize the tracker
            try
            {
                var config = settings.ToFullConfig();
                var tracker = MoonForgeErrorTracker.Initialize(config);

                if (tracker != null)
                {
                    Debug.Log($"[MoonForge] Error tracking initialized successfully! Endpoint: {config.apiEndpoint}");
                }
                else
                {
                    Debug.LogWarning("[MoonForge] Tracker.Initialize returned null");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MoonForge] Failed to initialize: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Manually trigger initialization (useful if you disabled auto-init).
        /// </summary>
        public static void Initialize()
        {
            _initializationAttempted = false;
            AutoInitialize();
        }

        /// <summary>
        /// Check if MoonForge is currently initialized.
        /// </summary>
        public static bool IsInitialized => MoonForgeErrorTracker.Instance != null;
    }
}

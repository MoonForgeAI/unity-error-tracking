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
            if (_initializationAttempted) return;
            _initializationAttempted = true;

            var settings = MoonForgeSettings.Instance;

            // Check if settings exist
            if (settings == null)
            {
                #if UNITY_EDITOR
                Debug.LogWarning("[MoonForge] Settings not found. Run 'MoonForge > Setup Error Tracking' to configure.");
                #endif
                return;
            }

            // Check if SDK should be active
            if (!settings.ShouldBeActive)
            {
                if (settings.debugMode)
                {
                    Debug.Log("[MoonForge] Error tracking disabled in current environment.");
                }
                return;
            }

            // Validate settings
            if (!settings.IsValid)
            {
                #if UNITY_EDITOR
                Debug.LogWarning("[MoonForge] Invalid Game ID. Please enter your Game ID via 'MoonForge > Setup Error Tracking'.");
                #endif
                return;
            }

            // Initialize the tracker
            try
            {
                var config = settings.ToFullConfig();
                var tracker = MoonForgeErrorTracker.Initialize(config);

                if (tracker != null && settings.debugMode)
                {
                    Debug.Log("[MoonForge] Error tracking initialized successfully!");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MoonForge] Failed to initialize: {ex.Message}");
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

using UnityEngine;
using MoonForge.ErrorTracking;

namespace MoonForge.Samples
{
    /// <summary>
    /// Example script showing how to initialize MoonForge Error Tracking.
    /// Attach this to a GameObject in your boot/splash scene.
    /// </summary>
    public class ErrorTrackerInitializer : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        [Tooltip("Drag your ErrorTrackerConfig asset here")]
        private ErrorTrackerConfig config;

        [Header("User Settings (Optional)")]
        [SerializeField]
        [Tooltip("Set to true to automatically set user from PlayerPrefs")]
        private bool autoSetUser = false;

        [SerializeField]
        [Tooltip("PlayerPrefs key for user ID")]
        private string userIdPlayerPrefsKey = "UserId";

        private void Awake()
        {
            if (config == null)
            {
                Debug.LogError("[MoonForge] ErrorTrackerConfig is not assigned!");
                return;
            }

            // Initialize the error tracker
            var tracker = MoonForgeErrorTracker.Initialize(config);

            if (tracker == null)
            {
                Debug.LogWarning("[MoonForge] Error tracker initialization failed or disabled");
                return;
            }

            // Set user if available
            if (autoSetUser)
            {
                var userId = PlayerPrefs.GetString(userIdPlayerPrefsKey, null);
                if (!string.IsNullOrEmpty(userId))
                {
                    tracker.SetUser(userId);
                }
            }

            Debug.Log("[MoonForge] Error tracking initialized successfully");
        }
    }
}

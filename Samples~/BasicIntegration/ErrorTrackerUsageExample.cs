using System;
using System.Collections.Generic;
using UnityEngine;
using MoonForge.ErrorTracking;

namespace MoonForge.Samples
{
    /// <summary>
    /// Example script showing common usage patterns for MoonForge Error Tracking
    /// </summary>
    public class ErrorTrackerUsageExample : MonoBehaviour
    {
        private void Start()
        {
            // Ensure tracker is initialized
            if (!MoonForgeErrorTracker.IsInitialized)
            {
                Debug.LogWarning("Error tracker not initialized. Make sure ErrorTrackerInitializer runs first.");
                return;
            }

            // Example: Set user after login
            SetUserExample();

            // Example: Add breadcrumbs
            AddBreadcrumbsExample();

            // Example: Set game state
            SetGameStateExample();
        }

        /// <summary>
        /// Example: Setting user identity after login
        /// </summary>
        public void SetUserExample()
        {
            // Call this after user logs in
            MoonForgeErrorTracker.Instance?.SetUser(
                userId: "user-12345",
                tags: new Dictionary<string, string>
                {
                    { "subscription", "premium" },
                    { "level", "42" }
                }
            );
        }

        /// <summary>
        /// Example: Adding breadcrumbs to track user actions
        /// </summary>
        public void AddBreadcrumbsExample()
        {
            var tracker = MoonForgeErrorTracker.Instance;
            if (tracker == null) return;

            // Simple breadcrumb
            tracker.AddBreadcrumb("Player opened inventory", BreadcrumbType.User);

            // Breadcrumb with category
            tracker.AddBreadcrumb("Equipped sword", BreadcrumbType.User, BreadcrumbLevel.Info, "inventory");

            // Breadcrumb with custom data
            tracker.AddBreadcrumb(new Breadcrumb(BreadcrumbType.User, "Purchased item", BreadcrumbLevel.Info, "shop")
                .WithData(new Dictionary<string, object>
                {
                    { "item_id", "sword_01" },
                    { "price", 100 },
                    { "currency", "gold" }
                }));
        }

        /// <summary>
        /// Example: Setting game state for context
        /// </summary>
        public void SetGameStateExample()
        {
            var tracker = MoonForgeErrorTracker.Instance;
            if (tracker == null) return;

            // Set current game mode and level
            tracker.SetGameState(
                gameMode: "pvp",
                levelId: "arena_01"
            );

            // Add custom state data
            tracker.SetGameStateData("match_id", "match-12345");
            tracker.SetGameStateData("team_size", 4);
            tracker.SetGameStateData("is_ranked", true);
        }

        /// <summary>
        /// Example: Capturing a handled exception
        /// </summary>
        public void CaptureExceptionExample()
        {
            try
            {
                // Some risky operation
                throw new InvalidOperationException("Something went wrong");
            }
            catch (Exception ex)
            {
                // Capture the exception
                MoonForgeErrorTracker.Instance?.CaptureException(
                    exception: ex,
                    level: ErrorLevel.Error,
                    tags: new Dictionary<string, string>
                    {
                        { "feature", "combat" },
                        { "action", "attack" }
                    }
                );

                // Handle the error gracefully in your game
                Debug.LogWarning($"Handled error: {ex.Message}");
            }
        }

        /// <summary>
        /// Example: Capturing a network error
        /// </summary>
        public void CaptureNetworkErrorExample()
        {
            // After a failed API call
            MoonForgeErrorTracker.Instance?.CaptureNetworkError(
                url: "https://api.game.com/matchmaking",
                method: "POST",
                statusCode: 503,
                errorMessage: "Service Unavailable",
                durationMs: 5000f,
                tags: new Dictionary<string, string>
                {
                    { "endpoint", "matchmaking" }
                }
            );
        }

        /// <summary>
        /// Example: Capturing a custom message
        /// </summary>
        public void CaptureCustomMessageExample()
        {
            MoonForgeErrorTracker.Instance?.CaptureMessage(
                message: "Player attempted exploit: infinite gold",
                level: ErrorLevel.Warning,
                tags: new Dictionary<string, string>
                {
                    { "category", "security" },
                    { "severity", "medium" }
                }
            );
        }

        /// <summary>
        /// Example: Clear user on logout
        /// </summary>
        public void OnLogout()
        {
            MoonForgeErrorTracker.Instance?.ClearUser();
        }

        /// <summary>
        /// Example: Force flush errors (e.g., before scene transition)
        /// </summary>
        public void OnSceneTransition()
        {
            MoonForgeErrorTracker.Instance?.Flush();
        }
    }
}

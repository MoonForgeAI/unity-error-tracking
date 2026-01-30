using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoonForge.ErrorTracking.Analytics
{
    /// <summary>
    /// Inner payload for analytics events sent to /api/send endpoint
    /// </summary>
    [Serializable]
    public class AnalyticsEventPayload
    {
        /// <summary>
        /// Game ID (UUID format)
        /// </summary>
        public string game;

        /// <summary>
        /// Optional hostname
        /// </summary>
        public string hostname;

        /// <summary>
        /// Screen resolution (e.g., "1920x1080")
        /// </summary>
        public string screen;

        /// <summary>
        /// Device language (e.g., "en-US")
        /// </summary>
        public string language;

        /// <summary>
        /// Current scene as URL (e.g., "scene://MainMenu")
        /// </summary>
        public string url;

        /// <summary>
        /// Scene/screen name
        /// </summary>
        public string title;

        /// <summary>
        /// Previous scene as URL (e.g., "scene://Loading")
        /// </summary>
        public string referrer;

        /// <summary>
        /// Event name. If null, treated as a pageview/screen view
        /// </summary>
        public string name;

        /// <summary>
        /// Custom event data
        /// </summary>
        public Dictionary<string, object> data;

        /// <summary>
        /// Unix timestamp in seconds
        /// </summary>
        public long timestamp;
    }

    /// <summary>
    /// Full analytics event for submission
    /// </summary>
    [Serializable]
    public class AnalyticsEvent
    {
        public string type = "event";
        public AnalyticsEventPayload payload;
    }

    /// <summary>
    /// Inner payload for identify events
    /// </summary>
    [Serializable]
    public class IdentifyEventPayload
    {
        /// <summary>
        /// Game ID (UUID format)
        /// </summary>
        public string game;

        /// <summary>
        /// User ID to identify
        /// </summary>
        public string id;

        /// <summary>
        /// User traits/properties
        /// </summary>
        public Dictionary<string, object> data;

        /// <summary>
        /// Unix timestamp in seconds
        /// </summary>
        public long timestamp;
    }

    /// <summary>
    /// Full identify event for submission
    /// </summary>
    [Serializable]
    public class IdentifyEvent
    {
        public string type = "identify";
        public IdentifyEventPayload payload;
    }

    /// <summary>
    /// Response from analytics event submission
    /// </summary>
    [Serializable]
    public class AnalyticsSubmissionResponse
    {
        public string status;
        public string error;
        public string cache;
    }

    /// <summary>
    /// Queued analytics event for offline storage
    /// </summary>
    [Serializable]
    internal class QueuedAnalyticsEvent
    {
        public string jsonPayload;
        public long queuedAt;
        public int retryCount;
    }
}

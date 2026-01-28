using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoonForge.ErrorTracking
{
    /// <summary>
    /// Stack frame information for error reporting
    /// </summary>
    [Serializable]
    public class StackFrame
    {
        public string module;
        public string function;
        public string filename;
        public int lineno;
        public int colno;
        public string instructionAddress;
        public string symbolAddress;
        public bool inApp;
        public string package;
    }

    /// <summary>
    /// Breadcrumb representing a user action or event before an error
    /// </summary>
    [Serializable]
    public class Breadcrumb
    {
        public string type;
        public string category;
        public string message;
        public string level;
        public Dictionary<string, object> data;
        public long timestamp;

        public Breadcrumb(BreadcrumbType type, string message, BreadcrumbLevel level = BreadcrumbLevel.Info, string category = null)
        {
            this.type = type.ToString().ToLowerInvariant();
            this.message = message;
            this.level = level.ToString().ToLowerInvariant();
            this.category = category;
            this.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public Breadcrumb WithData(Dictionary<string, object> data)
        {
            this.data = data;
            return this;
        }
    }

    /// <summary>
    /// Device context information
    /// </summary>
    [Serializable]
    public class DeviceContext
    {
        public string platform;
        public string osVersion;
        public string deviceModel;
        public string manufacturer;
        public string cpuArchitecture;
        public float? memoryUsedMb;
        public float? memoryAvailableMb;
        public float? cpuUsagePercent;
        public float? fps;
        public float? batteryLevel;
        public bool? batteryCharging;
        public string thermalState;
    }

    /// <summary>
    /// Network context information
    /// </summary>
    [Serializable]
    public class NetworkContext
    {
        public string type;
        public string carrier;
        public string effectiveType;
    }

    /// <summary>
    /// Game state at time of error
    /// </summary>
    [Serializable]
    public class GameState
    {
        public string sceneName;
        public string gameMode;
        public string levelId;
        public Dictionary<string, object> customData;
    }

    /// <summary>
    /// Network request information for network errors
    /// </summary>
    [Serializable]
    public class NetworkRequest
    {
        public string url;
        public string method;
        public int? statusCode;
        public float? durationMs;
        public Dictionary<string, string> requestHeaders;
        public Dictionary<string, string> responseHeaders;
    }

    /// <summary>
    /// Inner payload for error submission
    /// </summary>
    [Serializable]
    public class ErrorPayloadInner
    {
        public string game;
        public string errorType;
        public string errorCategory;
        public string errorLevel;
        public string message;

        public List<StackFrame> frames;
        public string rawStackTrace;

        public string exceptionClass;
        public string fingerprint;

        public DeviceContext device;
        public NetworkContext network;
        public GameState gameState;

        public string appVersion;
        public string buildNumber;
        public string unityVersion;

        public string userId;
        public string sessionId;

        public List<Breadcrumb> breadcrumbs;

        public long? timestamp;

        public NetworkRequest networkRequest;

        public Dictionary<string, string> tags;
    }

    /// <summary>
    /// Full error payload for single error submission
    /// </summary>
    [Serializable]
    public class ErrorPayload
    {
        public string type = "error";
        public ErrorPayloadInner payload;
    }

    /// <summary>
    /// Error item in batch submission (without game field)
    /// </summary>
    [Serializable]
    public class BatchErrorItem
    {
        public string clientErrorId;
        public string errorType;
        public string errorCategory;
        public string errorLevel;
        public string message;

        public List<StackFrame> frames;
        public string rawStackTrace;

        public string exceptionClass;
        public string fingerprint;

        public DeviceContext device;
        public NetworkContext network;
        public GameState gameState;

        public string appVersion;
        public string buildNumber;
        public string unityVersion;

        public string userId;
        public string sessionId;

        public List<Breadcrumb> breadcrumbs;

        public long? timestamp;

        public NetworkRequest networkRequest;

        public Dictionary<string, string> tags;
    }

    /// <summary>
    /// Batch error payload for multiple error submission
    /// </summary>
    [Serializable]
    public class ErrorBatchPayload
    {
        public string type = "error_batch";
        public string game;
        public List<BatchErrorItem> errors;
    }

    /// <summary>
    /// Response from error submission
    /// </summary>
    [Serializable]
    public class ErrorSubmissionResponse
    {
        public string status;
        public string errorId;
        public string fingerprint;
        public float sampleRate;
        public string error;
        public string reason;
        public int? retryAfterMs;
    }

    /// <summary>
    /// Response from batch error submission
    /// </summary>
    [Serializable]
    public class BatchSubmissionResponse
    {
        public string status;
        public string batchId;
        public int total;
        public int accepted;
        public int sampledOut;
        public List<BatchResultItem> results;
        public string error;
    }

    [Serializable]
    public class BatchResultItem
    {
        public string clientErrorId;
        public string status;
        public string errorId;
        public string fingerprint;
        public float sampleRate;
        public int occurrenceCount;
    }
}

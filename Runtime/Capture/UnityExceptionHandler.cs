using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MoonForge.ErrorTracking
{
    /// <summary>
    /// Handles Unity exceptions and log messages for error capture
    /// </summary>
    public class UnityExceptionHandler
    {
        private readonly ErrorTrackerConfig _config;
        private readonly Action<ErrorPayloadInner> _onErrorCaptured;

        private bool _isRegistered;
        private HashSet<string> _recentErrors;
        private float _dedupeWindowSeconds = 1f;
        private float _lastCleanupTime;

        public UnityExceptionHandler(ErrorTrackerConfig config, Action<ErrorPayloadInner> onErrorCaptured)
        {
            _config = config;
            _onErrorCaptured = onErrorCaptured;
            _recentErrors = new HashSet<string>();
        }

        /// <summary>
        /// Start capturing exceptions
        /// </summary>
        public void Register()
        {
            if (_isRegistered) return;

            Application.logMessageReceived += OnLogMessageReceived;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            _isRegistered = true;

            if (_config.debugMode)
            {
                Debug.Log("[MoonForge] Exception handler registered");
            }
        }

        /// <summary>
        /// Stop capturing exceptions
        /// </summary>
        public void Unregister()
        {
            if (!_isRegistered) return;

            Application.logMessageReceived -= OnLogMessageReceived;
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;

            _isRegistered = false;

            if (_config.debugMode)
            {
                Debug.Log("[MoonForge] Exception handler unregistered");
            }
        }

        private void OnLogMessageReceived(string condition, string stackTrace, LogType logType)
        {
            // Skip our own debug logs to avoid recursion
            if (condition.StartsWith("[MoonForge]")) return;

            if (_config.debugMode)
            {
                Debug.Log($"[MoonForge] Log received: type={logType}, message={condition.Substring(0, Math.Min(50, condition.Length))}...");
            }

            // Check if we should capture this log type
            if (!ShouldCapture(logType))
            {
                if (_config.debugMode)
                {
                    Debug.Log($"[MoonForge] Skipping log type {logType} (not configured to capture)");
                }
                return;
            }

            // Deduplicate rapid-fire errors
            var errorKey = $"{logType}:{condition}";
            if (IsDuplicate(errorKey))
            {
                if (_config.debugMode)
                {
                    Debug.Log($"[MoonForge] Skipping duplicate error");
                }
                return;
            }

            if (_config.debugMode)
            {
                Debug.Log($"[MoonForge] Capturing error: {logType} - {condition.Substring(0, Math.Min(100, condition.Length))}");
            }

            var payload = CreatePayload(condition, stackTrace, logType);
            _onErrorCaptured?.Invoke(payload);
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            if (!_config.captureUnhandledExceptions) return;

            var exception = args.ExceptionObject as Exception;
            if (exception == null) return;

            var errorKey = $"unhandled:{exception.GetType().FullName}:{exception.Message}";
            if (IsDuplicate(errorKey)) return;

            var payload = CreatePayloadFromException(exception, args.IsTerminating);
            _onErrorCaptured?.Invoke(payload);
        }

        private bool ShouldCapture(LogType logType)
        {
            switch (logType)
            {
                case LogType.Exception:
                    return _config.captureUnhandledExceptions;

                case LogType.Error:
                    return _config.captureLogErrors && _config.minimumLevel <= ErrorLevel.Error;

                case LogType.Assert:
                    return _config.captureLogErrors && _config.minimumLevel <= ErrorLevel.Error;

                case LogType.Warning:
                    return _config.captureLogErrors && _config.minimumLevel <= ErrorLevel.Warning;

                case LogType.Log:
                    return false; // Never capture Debug.Log

                default:
                    return false;
            }
        }

        private bool IsDuplicate(string errorKey)
        {
            var now = Time.unscaledTime;

            // Periodic cleanup
            if (now - _lastCleanupTime > _dedupeWindowSeconds * 2)
            {
                _recentErrors.Clear();
                _lastCleanupTime = now;
            }

            if (_recentErrors.Contains(errorKey))
            {
                return true;
            }

            _recentErrors.Add(errorKey);
            return false;
        }

        private ErrorPayloadInner CreatePayload(string condition, string stackTrace, LogType logType)
        {
            var (errorType, errorCategory, errorLevel) = ClassifyLogType(logType);
            var exceptionClass = ExtractExceptionClass(condition);
            var message = ScrubMessage(condition);

            var payload = new ErrorPayloadInner
            {
                game = _config.gameId,
                errorType = errorType.ToString().ToLowerInvariant(),
                errorCategory = errorCategory.ToString().ToLowerInvariant(),
                errorLevel = errorLevel.ToString().ToLowerInvariant(),
                message = message,
                exceptionClass = exceptionClass,
                rawStackTrace = stackTrace,
                frames = ParseStackFrames(stackTrace),
                device = DeviceContextCollector.Instance.Collect(),
                network = DeviceContextCollector.Instance.CollectNetworkContext(),
                gameState = GameStateCollector.Instance.Collect(),
                appVersion = Application.version,
                buildNumber = GetBuildNumber(),
                unityVersion = Application.unityVersion,
                breadcrumbs = BreadcrumbTracker.Instance.GetBreadcrumbs(),
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            return payload;
        }

        private ErrorPayloadInner CreatePayloadFromException(Exception exception, bool isTerminating)
        {
            var message = ScrubMessage(exception.Message);
            var stackTrace = exception.StackTrace ?? "";

            var payload = new ErrorPayloadInner
            {
                game = _config.gameId,
                errorType = isTerminating ? "crash" : "exception",
                errorCategory = "unhandled",
                errorLevel = isTerminating ? "fatal" : "error",
                message = message,
                exceptionClass = exception.GetType().FullName,
                rawStackTrace = stackTrace,
                frames = ParseStackFrames(stackTrace),
                device = DeviceContextCollector.Instance.Collect(),
                network = DeviceContextCollector.Instance.CollectNetworkContext(),
                gameState = GameStateCollector.Instance.Collect(),
                appVersion = Application.version,
                buildNumber = GetBuildNumber(),
                unityVersion = Application.unityVersion,
                breadcrumbs = BreadcrumbTracker.Instance.GetBreadcrumbs(),
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            return payload;
        }

        private (ErrorType type, ErrorCategory category, ErrorLevel level) ClassifyLogType(LogType logType)
        {
            return logType switch
            {
                LogType.Exception => (ErrorType.Exception, ErrorCategory.Unhandled, ErrorLevel.Error),
                LogType.Error => (ErrorType.Exception, ErrorCategory.Managed, ErrorLevel.Error),
                LogType.Assert => (ErrorType.Exception, ErrorCategory.Managed, ErrorLevel.Error),
                LogType.Warning => (ErrorType.Custom, ErrorCategory.Managed, ErrorLevel.Warning),
                _ => (ErrorType.Custom, ErrorCategory.Managed, ErrorLevel.Info)
            };
        }

        private string ExtractExceptionClass(string condition)
        {
            // Try to extract exception class from Unity's format
            // e.g., "NullReferenceException: Object reference not set..."
            var colonIndex = condition.IndexOf(':');
            if (colonIndex > 0)
            {
                var potentialClass = condition.Substring(0, colonIndex).Trim();
                if (potentialClass.EndsWith("Exception") || potentialClass.EndsWith("Error"))
                {
                    return potentialClass;
                }
            }

            return null;
        }

        private string ScrubMessage(string message)
        {
            if (!_config.scrubSensitiveData || string.IsNullOrEmpty(message))
            {
                return message;
            }

            var scrubbed = message;
            foreach (var pattern in _config.scrubPatterns)
            {
                try
                {
                    scrubbed = Regex.Replace(scrubbed, pattern, "[REDACTED]", RegexOptions.IgnoreCase);
                }
                catch
                {
                    // Invalid regex pattern, skip
                }
            }

            return scrubbed;
        }

        private List<StackFrame> ParseStackFrames(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace))
            {
                return null;
            }

            var frames = new List<StackFrame>();
            var lines = stackTrace.Split('\n');

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine)) continue;

                var frame = ParseStackFrame(trimmedLine);
                if (frame != null)
                {
                    frames.Add(frame);
                }
            }

            return frames.Count > 0 ? frames : null;
        }

        private StackFrame ParseStackFrame(string line)
        {
            var frame = new StackFrame();

            // Unity stack trace format examples:
            // "PlayerController.Update () (at Assets/Scripts/PlayerController.cs:42)"
            // "UnityEngine.MonoBehaviour:Update()"
            // "Assembly-CSharp.dll!PlayerController.Update()"

            // Try to parse Unity format: "ClassName.MethodName (params) (at file:line)"
            var atMatch = Regex.Match(line, @"^(.+?)\s*\(.*?\)\s*\(at\s+(.+?):(\d+)\)");
            if (atMatch.Success)
            {
                frame.function = atMatch.Groups[1].Value.Trim();
                frame.filename = atMatch.Groups[2].Value.Trim();
                if (int.TryParse(atMatch.Groups[3].Value, out var lineNo))
                {
                    frame.lineno = lineNo;
                }
                frame.inApp = IsInAppFrame(frame.filename, frame.function);
                frame.module = ExtractModule(frame.function);
                return frame;
            }

            // Try simpler format: "Namespace.Class.Method()"
            var simpleMatch = Regex.Match(line, @"^(.+?)\s*\(");
            if (simpleMatch.Success)
            {
                frame.function = simpleMatch.Groups[1].Value.Trim();
                frame.inApp = IsInAppFrame(null, frame.function);
                frame.module = ExtractModule(frame.function);
                return frame;
            }

            // Fallback: just use the whole line as function
            frame.function = line;
            frame.inApp = false;
            return frame;
        }

        private bool IsInAppFrame(string filename, string function)
        {
            // Consider it "in app" if it's in Assets folder
            if (!string.IsNullOrEmpty(filename) && filename.StartsWith("Assets/"))
            {
                return true;
            }

            // Exclude Unity engine and system namespaces
            if (!string.IsNullOrEmpty(function))
            {
                if (function.StartsWith("UnityEngine.") ||
                    function.StartsWith("UnityEditor.") ||
                    function.StartsWith("System.") ||
                    function.StartsWith("Mono."))
                {
                    return false;
                }
            }

            // Filename-based check
            if (!string.IsNullOrEmpty(filename))
            {
                return !filename.Contains("Library/") &&
                       !filename.Contains("Packages/");
            }

            return true; // Assume in-app if we can't tell
        }

        private string ExtractModule(string function)
        {
            if (string.IsNullOrEmpty(function)) return null;

            var lastDot = function.LastIndexOf('.');
            if (lastDot > 0)
            {
                var secondLastDot = function.LastIndexOf('.', lastDot - 1);
                if (secondLastDot >= 0)
                {
                    return function.Substring(0, secondLastDot);
                }
                return function.Substring(0, lastDot);
            }

            return null;
        }

        private string GetBuildNumber()
        {
#if UNITY_ANDROID
            return GetAndroidVersionCode();
#elif UNITY_IOS
            return GetIOSBuildNumber();
#else
            return Application.buildGUID?.Substring(0, 8) ?? "unknown";
#endif
        }

        private string GetAndroidVersionCode()
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
                return "unknown";
            }
#else
            return "unknown";
#endif
        }

        private string GetIOSBuildNumber()
        {
#if UNITY_IOS && !UNITY_EDITOR
            // iOS build number requires native plugin or Info.plist access
            return PlayerSettings.iOS.buildNumber;
#else
            return "unknown";
#endif
        }
    }
}

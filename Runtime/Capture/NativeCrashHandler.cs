using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using AOT;

namespace MoonForge.ErrorTracking
{
    /// <summary>
    /// Bridge to native crash handlers for iOS and Android.
    /// Captures native crashes (SIGSEGV, SIGABRT, etc.) that can't be caught by managed code.
    /// </summary>
    public class NativeCrashHandler
    {
        private static NativeCrashHandler _instance;
        private static bool _isInitialized;

        private readonly ErrorTrackerConfig _config;
        private readonly Action<ErrorPayloadInner> _onCrashCaptured;

        // Prevent GC from collecting the delegate
        private static CrashCallbackDelegate _callbackDelegate;

        public static NativeCrashHandler Instance => _instance;
        public static bool IsInitialized => _isInitialized;

        #region Native Plugin Imports

#if UNITY_IOS && !UNITY_EDITOR
        private delegate void CrashCallbackDelegate(string crashJson);

        [DllImport("__Internal")]
        private static extern void MoonForge_InitializeCrashHandler(CrashCallbackDelegate callback);

        [DllImport("__Internal")]
        private static extern void MoonForge_ShutdownCrashHandler();

        [DllImport("__Internal")]
        private static extern int MoonForge_IsCrashHandlerInitialized();

        [DllImport("__Internal")]
        private static extern IntPtr MoonForge_GetThermalState();

        [DllImport("__Internal")]
        private static extern void MoonForge_GetMemoryInfo(out float usedMB, out float availableMB);

        [DllImport("__Internal")]
        private static extern IntPtr MoonForge_GetCarrierName();

        [DllImport("__Internal")]
        private static extern void MoonForge_SimulateCrash(int crashType);

#elif UNITY_ANDROID && !UNITY_EDITOR
        private delegate void CrashCallbackDelegate(string crashJson);

        // Android uses JNI bridge through Java class
        private static AndroidJavaObject _crashHandlerJava;

#else
        // Editor/Standalone - no-op
        private delegate void CrashCallbackDelegate(string crashJson);
#endif

        #endregion

        private NativeCrashHandler(ErrorTrackerConfig config, Action<ErrorPayloadInner> onCrashCaptured)
        {
            _config = config;
            _onCrashCaptured = onCrashCaptured;
        }

        /// <summary>
        /// Initialize native crash handling
        /// </summary>
        public static void Initialize(ErrorTrackerConfig config, Action<ErrorPayloadInner> onCrashCaptured)
        {
            if (_isInitialized)
            {
                Debug.LogWarning("[MoonForge] Native crash handler already initialized");
                return;
            }

            _instance = new NativeCrashHandler(config, onCrashCaptured);

#if UNITY_IOS && !UNITY_EDITOR
            InitializeIOS();
#elif UNITY_ANDROID && !UNITY_EDITOR
            InitializeAndroid();
#else
            if (config.debugMode)
            {
                Debug.Log("[MoonForge] Native crash handler not available on this platform");
            }
#endif

            _isInitialized = true;

            if (config.debugMode)
            {
                Debug.Log("[MoonForge] Native crash handler initialized");
            }
        }

        /// <summary>
        /// Shutdown native crash handling
        /// </summary>
        public static void Shutdown()
        {
            if (!_isInitialized) return;

#if UNITY_IOS && !UNITY_EDITOR
            MoonForge_ShutdownCrashHandler();
#elif UNITY_ANDROID && !UNITY_EDITOR
            ShutdownAndroid();
#endif

            _instance = null;
            _isInitialized = false;
        }

        #region iOS Implementation

#if UNITY_IOS && !UNITY_EDITOR
        private static void InitializeIOS()
        {
            _callbackDelegate = OnNativeCrash;
            MoonForge_InitializeCrashHandler(_callbackDelegate);
        }

        [MonoPInvokeCallback(typeof(CrashCallbackDelegate))]
        private static void OnNativeCrash(string crashJson)
        {
            try
            {
                _instance?.ProcessNativeCrash(crashJson);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MoonForge] Error processing native crash: {ex.Message}");
            }
        }

        /// <summary>
        /// Get iOS thermal state
        /// </summary>
        public static string GetIOSThermalState()
        {
            try
            {
                IntPtr ptr = MoonForge_GetThermalState();
                if (ptr != IntPtr.Zero)
                {
                    return Marshal.PtrToStringAnsi(ptr);
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Get iOS memory info
        /// </summary>
        public static void GetIOSMemoryInfo(out float usedMB, out float availableMB)
        {
            try
            {
                MoonForge_GetMemoryInfo(out usedMB, out availableMB);
            }
            catch
            {
                usedMB = 0;
                availableMB = 0;
            }
        }

        /// <summary>
        /// Get iOS carrier name
        /// </summary>
        public static string GetIOSCarrierName()
        {
            try
            {
                IntPtr ptr = MoonForge_GetCarrierName();
                if (ptr != IntPtr.Zero)
                {
                    string carrier = Marshal.PtrToStringAnsi(ptr);
                    // Note: Native code uses strdup, we should free this
                    // but since it's called infrequently, we'll accept the small leak
                    return carrier;
                }
            }
            catch { }
            return null;
        }
#endif

        #endregion

        #region Android Implementation

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void InitializeAndroid()
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var context = activity.Call<AndroidJavaObject>("getApplicationContext"))
                {
                    using (var crashHandlerClass = new AndroidJavaClass("com.moonforge.errortracking.MoonForgeCrashHandler"))
                    {
                        _crashHandlerJava = crashHandlerClass.CallStatic<AndroidJavaObject>("initialize", context);
                        _crashHandlerJava?.Call("start");
                    }
                }

                // Set up Java crash callback using Unity's Application.logMessageReceived
                // Native crashes are handled by the native library directly
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MoonForge] Failed to initialize Android crash handler: {ex.Message}");
            }
        }

        private static void ShutdownAndroid()
        {
            try
            {
                _crashHandlerJava?.Call("stop");
                _crashHandlerJava?.Dispose();
                _crashHandlerJava = null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MoonForge] Failed to shutdown Android crash handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Get Android thermal state
        /// </summary>
        public static string GetAndroidThermalState()
        {
            try
            {
                return _crashHandlerJava?.Call<string>("getThermalState");
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Get Android memory info
        /// </summary>
        public static void GetAndroidMemoryInfo(out float usedMB, out float availableMB)
        {
            usedMB = 0;
            availableMB = 0;

            try
            {
                float[] info = _crashHandlerJava?.Call<float[]>("getMemoryInfo");
                if (info != null && info.Length >= 2)
                {
                    usedMB = info[0];
                    availableMB = info[1];
                }
            }
            catch { }
        }

        /// <summary>
        /// Get Android carrier name
        /// </summary>
        public static string GetAndroidCarrierName()
        {
            try
            {
                return _crashHandlerJava?.Call<string>("getCarrierName");
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Get Android CPU architecture
        /// </summary>
        public static string GetAndroidCpuArchitecture()
        {
            try
            {
                return _crashHandlerJava?.Call<string>("getCpuArchitecture");
            }
            catch { }
            return null;
        }
#endif

        #endregion

        #region Crash Processing

        private void ProcessNativeCrash(string crashJson)
        {
            if (string.IsNullOrEmpty(crashJson))
            {
                Debug.LogError("[MoonForge] Received empty crash JSON");
                return;
            }

            if (_config.debugMode)
            {
                Debug.Log($"[MoonForge] Processing native crash: {crashJson.Substring(0, Math.Min(200, crashJson.Length))}...");
            }

            try
            {
                // Parse the crash JSON
                var crashData = JsonUtility.FromJson<NativeCrashData>(crashJson);

                // Build error payload
                var payload = new ErrorPayloadInner
                {
                    game = _config.gameId,
                    errorType = "crash",
                    errorCategory = "native",
                    errorLevel = "fatal",
                    message = BuildCrashMessage(crashData),
                    exceptionClass = crashData.signalName ?? crashData.exceptionName,
                    rawStackTrace = BuildRawStackTrace(crashData),
                    frames = ParseNativeFrames(crashData),
                    device = DeviceContextCollector.Instance.Collect(),
                    network = DeviceContextCollector.Instance.CollectNetworkContext(),
                    gameState = GameStateCollector.Instance.Collect(),
                    appVersion = Application.version,
                    buildNumber = GetBuildNumber(),
                    unityVersion = Application.unityVersion,
                    breadcrumbs = BreadcrumbTracker.Instance.GetBreadcrumbs(),
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                // Notify the error tracker
                _onCrashCaptured?.Invoke(payload);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MoonForge] Failed to process native crash: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private string BuildCrashMessage(NativeCrashData crashData)
        {
            if (!string.IsNullOrEmpty(crashData.signalName))
            {
                return $"{crashData.signalName}: {crashData.signalDescription ?? "Native crash"}";
            }
            if (!string.IsNullOrEmpty(crashData.exceptionName))
            {
                return $"{crashData.exceptionName}: {crashData.exceptionReason ?? "Native exception"}";
            }
            return "Native crash";
        }

        private string BuildRawStackTrace(NativeCrashData crashData)
        {
            if (crashData.frames == null || crashData.frames.Length == 0)
            {
                return null;
            }

            var lines = new List<string>();
            foreach (var frame in crashData.frames)
            {
                var line = $"#{frame.frame} {frame.address}";
                if (!string.IsNullOrEmpty(frame.module))
                {
                    line += $" [{frame.module}]";
                }
                if (!string.IsNullOrEmpty(frame.symbol))
                {
                    line += $" {frame.symbol}";
                    if (!string.IsNullOrEmpty(frame.offset))
                    {
                        line += $"+{frame.offset}";
                    }
                }
                lines.Add(line);
            }

            return string.Join("\n", lines);
        }

        private List<StackFrame> ParseNativeFrames(NativeCrashData crashData)
        {
            if (crashData.frames == null || crashData.frames.Length == 0)
            {
                return null;
            }

            var frames = new List<StackFrame>();
            foreach (var nativeFrame in crashData.frames)
            {
                var frame = new StackFrame
                {
                    module = nativeFrame.module,
                    function = nativeFrame.symbol,
                    instructionAddress = nativeFrame.address,
                    inApp = IsInAppFrame(nativeFrame)
                };
                frames.Add(frame);
            }

            return frames;
        }

        private bool IsInAppFrame(NativeStackFrame frame)
        {
            if (string.IsNullOrEmpty(frame.module))
            {
                return false;
            }

            // Common system libraries to exclude
            var systemLibs = new[]
            {
                "libc.so", "libm.so", "libdl.so", "liblog.so",
                "libSystem", "libdyld", "libsystem",
                "CoreFoundation", "Foundation", "UIKit",
                "libunity.so", "libil2cpp.so", "libmono.so"
            };

            foreach (var lib in systemLibs)
            {
                if (frame.module.Contains(lib))
                {
                    return false;
                }
            }

            return true;
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

        #region Testing

        /// <summary>
        /// Simulate a native crash for testing (DEBUG builds only)
        /// </summary>
        /// <param name="crashType">0=SIGSEGV, 1=SIGABRT, 2=SIGBUS</param>
        [System.Diagnostics.Conditional("DEBUG")]
        public static void SimulateCrash(int crashType = 0)
        {
            Debug.LogWarning($"[MoonForge] Simulating native crash (type {crashType})");

#if UNITY_IOS && !UNITY_EDITOR
            MoonForge_SimulateCrash(crashType);
#elif UNITY_ANDROID && !UNITY_EDITOR
            _crashHandlerJava?.Call("simulateCrash", crashType);
#else
            Debug.Log("[MoonForge] Crash simulation not available on this platform");
#endif
        }

        #endregion

        #region Native Data Structures

        [Serializable]
        private class NativeCrashData
        {
            // Signal crash fields
            public int signal;
            public string signalName;
            public string signalDescription;
            public string faultAddress;
            public long threadId;
            public int siCode;

            // NSException fields (iOS)
            public string exceptionType;
            public string exceptionName;
            public string exceptionReason;

            // Stack frames
            public NativeStackFrame[] frames;
        }

        [Serializable]
        private class NativeStackFrame
        {
            public int frame;
            public string address;
            public string module;
            public string symbol;
            public string offset;
        }

        #endregion
    }
}

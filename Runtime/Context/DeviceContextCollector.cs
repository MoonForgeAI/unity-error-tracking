using System;
using UnityEngine;
using UnityEngine.Profiling;

namespace MoonForge.ErrorTracking
{
    /// <summary>
    /// Collects device and system context information
    /// </summary>
    public class DeviceContextCollector
    {
        private float _lastFps;
        private float _fpsUpdateInterval = 0.5f;
        private float _fpsAccumulator;
        private int _fpsFrameCount;
        private float _fpsTimeLeft;

        private static DeviceContextCollector _instance;
        public static DeviceContextCollector Instance => _instance ??= new DeviceContextCollector();

        /// <summary>
        /// Update FPS tracking (call from Update loop)
        /// </summary>
        public void UpdateFps()
        {
            _fpsTimeLeft -= Time.unscaledDeltaTime;
            _fpsAccumulator += Time.unscaledDeltaTime;
            _fpsFrameCount++;

            if (_fpsTimeLeft <= 0f)
            {
                _lastFps = _fpsFrameCount / _fpsAccumulator;
                _fpsTimeLeft = _fpsUpdateInterval;
                _fpsAccumulator = 0f;
                _fpsFrameCount = 0;
            }
        }

        /// <summary>
        /// Collect current device context
        /// </summary>
        public DeviceContext Collect()
        {
            var context = new DeviceContext
            {
                platform = GetPlatform(),
                osVersion = SystemInfo.operatingSystem,
                deviceModel = SystemInfo.deviceModel,
                manufacturer = GetManufacturer(),
                cpuArchitecture = GetCpuArchitecture(),
                memoryUsedMb = GetUsedMemoryMb(),
                memoryAvailableMb = GetAvailableMemoryMb(),
                fps = _lastFps > 0 ? _lastFps : null,
                batteryLevel = GetBatteryLevel(),
                batteryCharging = GetBatteryCharging(),
                thermalState = GetThermalState()
            };

            return context;
        }

        /// <summary>
        /// Collect current network context
        /// </summary>
        public NetworkContext CollectNetworkContext()
        {
            return new NetworkContext
            {
                type = GetNetworkType(),
                carrier = GetCarrier(),
                effectiveType = null
            };
        }

        private string GetPlatform()
        {
#if UNITY_IOS
            return "ios";
#elif UNITY_ANDROID
            return "android";
#elif UNITY_STANDALONE_WIN
            return "windows";
#elif UNITY_STANDALONE_OSX
            return "macos";
#elif UNITY_STANDALONE_LINUX
            return "linux";
#elif UNITY_WEBGL
            return "webgl";
#elif UNITY_SWITCH
            return "switch";
#elif UNITY_PS4 || UNITY_PS5
            return "playstation";
#elif UNITY_XBOXONE || UNITY_GAMECORE
            return "xbox";
#else
            return Application.platform.ToString().ToLowerInvariant();
#endif
        }

        private string GetManufacturer()
        {
#if UNITY_IOS
            return "Apple";
#elif UNITY_ANDROID
            return GetAndroidManufacturer();
#else
            return null;
#endif
        }

        private string GetAndroidManufacturer()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var build = new AndroidJavaClass("android.os.Build"))
                {
                    return build.GetStatic<string>("MANUFACTURER");
                }
            }
            catch
            {
                return null;
            }
#else
            return null;
#endif
        }

        private string GetCpuArchitecture()
        {
            var processorType = SystemInfo.processorType;

            if (processorType.Contains("ARM64") || processorType.Contains("arm64") || processorType.Contains("aarch64"))
                return "arm64";
            if (processorType.Contains("ARM") || processorType.Contains("arm"))
                return "arm";
            if (processorType.Contains("x86_64") || processorType.Contains("AMD64") || processorType.Contains("x64"))
                return "x86_64";
            if (processorType.Contains("x86") || processorType.Contains("i386") || processorType.Contains("i686"))
                return "x86";

            return processorType;
        }

        private float? GetUsedMemoryMb()
        {
            try
            {
                // Get allocated memory from the profiler
                var allocatedMemory = Profiler.GetTotalAllocatedMemoryLong();
                return allocatedMemory / (1024f * 1024f);
            }
            catch
            {
                return null;
            }
        }

        private float? GetAvailableMemoryMb()
        {
            try
            {
                var systemMemory = SystemInfo.systemMemorySize;
                if (systemMemory > 0)
                    return systemMemory;
                return null;
            }
            catch
            {
                return null;
            }
        }

        private float? GetBatteryLevel()
        {
            var level = SystemInfo.batteryLevel;
            if (level < 0)
                return null;
            return level * 100f;
        }

        private bool? GetBatteryCharging()
        {
            var status = SystemInfo.batteryStatus;
            if (status == BatteryStatus.Unknown)
                return null;
            return status == BatteryStatus.Charging || status == BatteryStatus.Full;
        }

        private string GetThermalState()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return GetIOSThermalState();
#elif UNITY_ANDROID && !UNITY_EDITOR
            return GetAndroidThermalState();
#else
            return null;
#endif
        }

        private string GetIOSThermalState()
        {
#if UNITY_IOS && !UNITY_EDITOR
            // iOS thermal state would need native plugin
            // For now return null - will be implemented in native plugin
            return null;
#else
            return null;
#endif
        }

        private string GetAndroidThermalState()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                    .GetStatic<AndroidJavaObject>("currentActivity"))
                using (var powerManager = activity.Call<AndroidJavaObject>("getSystemService", "power"))
                {
                    if (powerManager != null)
                    {
                        var thermalStatus = powerManager.Call<int>("getCurrentThermalStatus");
                        return thermalStatus switch
                        {
                            0 => "nominal",
                            1 => "fair",
                            2 => "fair",
                            3 => "serious",
                            4 => "critical",
                            5 => "critical",
                            6 => "critical",
                            _ => null
                        };
                    }
                }
            }
            catch
            {
                // Thermal API not available on this device/version
            }
            return null;
#else
            return null;
#endif
        }

        private string GetNetworkType()
        {
            return Application.internetReachability switch
            {
                NetworkReachability.NotReachable => "none",
                NetworkReachability.ReachableViaLocalAreaNetwork => "wifi",
                NetworkReachability.ReachableViaCarrierDataNetwork => "cellular",
                _ => null
            };
        }

        private string GetCarrier()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return GetAndroidCarrier();
#elif UNITY_IOS && !UNITY_EDITOR
            // iOS carrier requires native plugin
            return null;
#else
            return null;
#endif
        }

        private string GetAndroidCarrier()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                    .GetStatic<AndroidJavaObject>("currentActivity"))
                using (var telephonyManager = activity.Call<AndroidJavaObject>("getSystemService", "phone"))
                {
                    if (telephonyManager != null)
                    {
                        return telephonyManager.Call<string>("getNetworkOperatorName");
                    }
                }
            }
            catch
            {
                // Telephony service not available
            }
            return null;
#else
            return null;
#endif
        }
    }
}

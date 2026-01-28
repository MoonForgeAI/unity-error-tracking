package com.moonforge.errortracking;

import android.app.ActivityManager;
import android.content.Context;
import android.os.Build;
import android.os.Debug;
import android.os.PowerManager;
import android.telephony.TelephonyManager;
import android.util.Log;

import java.io.BufferedReader;
import java.io.FileReader;

/**
 * MoonForge Crash Handler - Java helper class for Android
 *
 * Provides Android-specific functionality that requires Java APIs:
 * - Memory information
 * - Thermal state
 * - Carrier information
 * - Device information
 */
public class MoonForgeCrashHandler {
    private static final String TAG = "MoonForgeCrash";

    private static MoonForgeCrashHandler instance;
    private Context context;
    private boolean isInitialized = false;

    // Native methods
    private native void nativeInit();
    private native void nativeShutdown();
    private native void nativeSimulateCrash(int crashType);

    static {
        try {
            System.loadLibrary("moonforge_crash_handler");
            Log.d(TAG, "Native library loaded");
        } catch (UnsatisfiedLinkError e) {
            Log.e(TAG, "Failed to load native library: " + e.getMessage());
        }
    }

    private MoonForgeCrashHandler(Context context) {
        this.context = context.getApplicationContext();
    }

    /**
     * Initialize the crash handler
     */
    public static synchronized MoonForgeCrashHandler initialize(Context context) {
        if (instance == null) {
            instance = new MoonForgeCrashHandler(context);
        }
        return instance;
    }

    /**
     * Get the singleton instance
     */
    public static MoonForgeCrashHandler getInstance() {
        return instance;
    }

    /**
     * Start native crash monitoring
     */
    public void start() {
        if (isInitialized) {
            Log.d(TAG, "Already initialized");
            return;
        }

        try {
            nativeInit();
            isInitialized = true;
            Log.d(TAG, "Crash handler started");
        } catch (Exception e) {
            Log.e(TAG, "Failed to start crash handler: " + e.getMessage());
        }
    }

    /**
     * Stop native crash monitoring
     */
    public void stop() {
        if (!isInitialized) {
            return;
        }

        try {
            nativeShutdown();
            isInitialized = false;
            Log.d(TAG, "Crash handler stopped");
        } catch (Exception e) {
            Log.e(TAG, "Failed to stop crash handler: " + e.getMessage());
        }
    }

    /**
     * Get memory info
     * @return Array of [usedMB, availableMB]
     */
    public float[] getMemoryInfo() {
        float[] result = new float[2];

        try {
            ActivityManager activityManager = (ActivityManager) context.getSystemService(Context.ACTIVITY_SERVICE);
            if (activityManager != null) {
                ActivityManager.MemoryInfo memInfo = new ActivityManager.MemoryInfo();
                activityManager.getMemoryInfo(memInfo);

                // Available memory
                result[1] = memInfo.availMem / (1024f * 1024f);

                // Used memory (total - available)
                result[0] = (memInfo.totalMem - memInfo.availMem) / (1024f * 1024f);
            }
        } catch (Exception e) {
            Log.e(TAG, "Failed to get memory info: " + e.getMessage());
        }

        return result;
    }

    /**
     * Get native heap memory usage
     * @return Array of [allocatedMB, freeMB]
     */
    public float[] getNativeHeapInfo() {
        float[] result = new float[2];

        try {
            result[0] = Debug.getNativeHeapAllocatedSize() / (1024f * 1024f);
            result[1] = Debug.getNativeHeapFreeSize() / (1024f * 1024f);
        } catch (Exception e) {
            Log.e(TAG, "Failed to get native heap info: " + e.getMessage());
        }

        return result;
    }

    /**
     * Get thermal state
     * @return Thermal state string or null
     */
    public String getThermalState() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            try {
                PowerManager powerManager = (PowerManager) context.getSystemService(Context.POWER_SERVICE);
                if (powerManager != null) {
                    int thermalStatus = powerManager.getCurrentThermalStatus();
                    switch (thermalStatus) {
                        case PowerManager.THERMAL_STATUS_NONE:
                            return "nominal";
                        case PowerManager.THERMAL_STATUS_LIGHT:
                        case PowerManager.THERMAL_STATUS_MODERATE:
                            return "fair";
                        case PowerManager.THERMAL_STATUS_SEVERE:
                            return "serious";
                        case PowerManager.THERMAL_STATUS_CRITICAL:
                        case PowerManager.THERMAL_STATUS_EMERGENCY:
                        case PowerManager.THERMAL_STATUS_SHUTDOWN:
                            return "critical";
                        default:
                            return null;
                    }
                }
            } catch (Exception e) {
                Log.e(TAG, "Failed to get thermal state: " + e.getMessage());
            }
        }
        return null;
    }

    /**
     * Get carrier name
     * @return Carrier name or null
     */
    public String getCarrierName() {
        try {
            TelephonyManager telephonyManager = (TelephonyManager) context.getSystemService(Context.TELEPHONY_SERVICE);
            if (telephonyManager != null) {
                String carrier = telephonyManager.getNetworkOperatorName();
                if (carrier != null && !carrier.isEmpty()) {
                    return carrier;
                }
            }
        } catch (Exception e) {
            Log.e(TAG, "Failed to get carrier name: " + e.getMessage());
        }
        return null;
    }

    /**
     * Get device manufacturer
     */
    public String getManufacturer() {
        return Build.MANUFACTURER;
    }

    /**
     * Get device model
     */
    public String getModel() {
        return Build.MODEL;
    }

    /**
     * Get Android version
     */
    public String getAndroidVersion() {
        return Build.VERSION.RELEASE;
    }

    /**
     * Get API level
     */
    public int getApiLevel() {
        return Build.VERSION.SDK_INT;
    }

    /**
     * Get CPU architecture
     */
    public String getCpuArchitecture() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            String[] abis = Build.SUPPORTED_ABIS;
            if (abis != null && abis.length > 0) {
                return abis[0];
            }
        }
        return Build.CPU_ABI;
    }

    /**
     * Check if device is rooted
     */
    public boolean isRooted() {
        String[] paths = {
            "/system/app/Superuser.apk",
            "/sbin/su",
            "/system/bin/su",
            "/system/xbin/su",
            "/data/local/xbin/su",
            "/data/local/bin/su",
            "/system/sd/xbin/su",
            "/system/bin/failsafe/su",
            "/data/local/su"
        };

        for (String path : paths) {
            if (new java.io.File(path).exists()) {
                return true;
            }
        }

        return false;
    }

    /**
     * Get CPU usage (requires reading /proc/stat)
     * @return CPU usage percentage (0-100) or -1 if unavailable
     */
    public float getCpuUsage() {
        try {
            BufferedReader reader = new BufferedReader(new FileReader("/proc/stat"));
            String line = reader.readLine();
            reader.close();

            if (line != null && line.startsWith("cpu ")) {
                String[] parts = line.split("\\s+");
                if (parts.length >= 5) {
                    long user = Long.parseLong(parts[1]);
                    long nice = Long.parseLong(parts[2]);
                    long system = Long.parseLong(parts[3]);
                    long idle = Long.parseLong(parts[4]);

                    long total = user + nice + system + idle;
                    long used = user + nice + system;

                    if (total > 0) {
                        return (used * 100f) / total;
                    }
                }
            }
        } catch (Exception e) {
            // Ignore - may not have permission
        }
        return -1;
    }

    /**
     * Simulate a crash for testing (debug only)
     * @param crashType 0=SIGSEGV, 1=SIGABRT, 2=SIGBUS
     */
    public void simulateCrash(int crashType) {
        if (BuildConfig.DEBUG) {
            Log.w(TAG, "Simulating crash type " + crashType);
            nativeSimulateCrash(crashType);
        } else {
            Log.w(TAG, "Crash simulation is only available in debug builds");
        }
    }
}

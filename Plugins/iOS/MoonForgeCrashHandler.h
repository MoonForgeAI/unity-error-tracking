/**
 * MoonForge Crash Handler for iOS
 *
 * Captures native crashes (signals) and collects crash information
 * for reporting to the MoonForge error tracking service.
 */

#ifndef MoonForgeCrashHandler_h
#define MoonForgeCrashHandler_h

#import <Foundation/Foundation.h>

#ifdef __cplusplus
extern "C" {
#endif

/**
 * Callback function type for crash notifications
 * @param crashJson JSON string containing crash information
 */
typedef void (*MoonForgeCrashCallback)(const char* crashJson);

/**
 * Initialize the crash handler
 * @param callback Function to call when a crash is captured
 */
void MoonForge_InitializeCrashHandler(MoonForgeCrashCallback callback);

/**
 * Shutdown the crash handler and restore original signal handlers
 */
void MoonForge_ShutdownCrashHandler(void);

/**
 * Check if crash handler is initialized
 * @return 1 if initialized, 0 otherwise
 */
int MoonForge_IsCrashHandlerInitialized(void);

/**
 * Get the current thermal state
 * @return Thermal state string ("nominal", "fair", "serious", "critical") or NULL
 */
const char* MoonForge_GetThermalState(void);

/**
 * Get device memory info
 * @param usedMB Output parameter for used memory in MB
 * @param availableMB Output parameter for available memory in MB
 */
void MoonForge_GetMemoryInfo(float* usedMB, float* availableMB);

/**
 * Get the carrier name
 * @return Carrier name string or NULL (caller must free)
 */
const char* MoonForge_GetCarrierName(void);

/**
 * Simulate a crash for testing (debug only)
 * @param crashType Type of crash to simulate (0=SIGSEGV, 1=SIGABRT, 2=SIGBUS)
 */
void MoonForge_SimulateCrash(int crashType);

#ifdef __cplusplus
}
#endif

#endif /* MoonForgeCrashHandler_h */

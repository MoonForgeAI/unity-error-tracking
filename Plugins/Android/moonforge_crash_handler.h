/**
 * MoonForge Crash Handler for Android (NDK)
 *
 * Captures native crashes (signals) on Android devices.
 */

#ifndef MOONFORGE_CRASH_HANDLER_H
#define MOONFORGE_CRASH_HANDLER_H

#include <jni.h>

#ifdef __cplusplus
extern "C" {
#endif

/**
 * Callback function type for crash notifications
 */
typedef void (*MoonForgeCrashCallback)(const char* crashJson);

/**
 * Initialize the crash handler
 * @param env JNI environment
 * @param callback Function to call when a crash is captured
 */
void MoonForge_Android_InitializeCrashHandler(JNIEnv* env, MoonForgeCrashCallback callback);

/**
 * Shutdown the crash handler
 */
void MoonForge_Android_ShutdownCrashHandler(void);

/**
 * Check if crash handler is initialized
 */
int MoonForge_Android_IsInitialized(void);

/**
 * Set the Java VM reference (called from Unity)
 */
void MoonForge_Android_SetJavaVM(JavaVM* vm);

// JNI functions
JNIEXPORT void JNICALL Java_com_moonforge_errortracking_CrashHandler_nativeInit(JNIEnv* env, jobject obj);
JNIEXPORT void JNICALL Java_com_moonforge_errortracking_CrashHandler_nativeShutdown(JNIEnv* env, jobject obj);
JNIEXPORT void JNICALL Java_com_moonforge_errortracking_CrashHandler_nativeSimulateCrash(JNIEnv* env, jobject obj, jint crashType);

#ifdef __cplusplus
}
#endif

#endif // MOONFORGE_CRASH_HANDLER_H

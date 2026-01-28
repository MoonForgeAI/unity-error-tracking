/**
 * MoonForge Crash Handler for Android (NDK)
 *
 * Implements signal-based crash capturing for native Android crashes.
 * Captures SIGSEGV, SIGABRT, SIGBUS, SIGFPE, SIGILL, SIGTRAP
 */

#include "moonforge_crash_handler.h"

#include <android/log.h>
#include <dlfcn.h>
#include <signal.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <unwind.h>
#include <pthread.h>

#define LOG_TAG "MoonForgeCrash"
#define LOGD(...) __android_log_print(ANDROID_LOG_DEBUG, LOG_TAG, __VA_ARGS__)
#define LOGE(...) __android_log_print(ANDROID_LOG_ERROR, LOG_TAG, __VA_ARGS__)

// Signals to handle
static const int kSignalsToHandle[] = {
    SIGABRT,
    SIGBUS,
    SIGFPE,
    SIGILL,
    SIGSEGV,
    SIGTRAP,
    SIGPIPE
};
static const int kNumSignals = sizeof(kSignalsToHandle) / sizeof(kSignalsToHandle[0]);

// Previous signal handlers
static struct sigaction previousHandlers[NSIG];

// Callback for crash notification
static MoonForgeCrashCallback crashCallback = NULL;

// Flag to prevent re-entry
static volatile sig_atomic_t isHandlingCrash = 0;

// Initialization state
static int isInitialized = 0;

// Java VM reference
static JavaVM* javaVM = NULL;

// Buffer for crash JSON (pre-allocated)
static char crashJsonBuffer[32768];

// Stack trace structure for unwinding
struct BacktraceState {
    void** current;
    void** end;
};

#define MAX_FRAMES 128
static void* stackFrames[MAX_FRAMES];
static int stackFrameCount = 0;

// Signal names
static const char* signalName(int signal) {
    switch (signal) {
        case SIGABRT: return "SIGABRT";
        case SIGBUS: return "SIGBUS";
        case SIGFPE: return "SIGFPE";
        case SIGILL: return "SIGILL";
        case SIGSEGV: return "SIGSEGV";
        case SIGTRAP: return "SIGTRAP";
        case SIGPIPE: return "SIGPIPE";
        default: return "UNKNOWN";
    }
}

static const char* signalDescription(int signal) {
    switch (signal) {
        case SIGABRT: return "Abort signal";
        case SIGBUS: return "Bus error (bad memory access)";
        case SIGFPE: return "Floating-point exception";
        case SIGILL: return "Illegal instruction";
        case SIGSEGV: return "Segmentation fault (invalid memory reference)";
        case SIGTRAP: return "Trace/breakpoint trap";
        case SIGPIPE: return "Broken pipe";
        default: return "Unknown signal";
    }
}

// Stack unwinding callback
static _Unwind_Reason_Code unwindCallback(struct _Unwind_Context* context, void* arg) {
    struct BacktraceState* state = (struct BacktraceState*)arg;
    uintptr_t pc = _Unwind_GetIP(context);

    if (pc) {
        if (state->current == state->end) {
            return _URC_END_OF_STACK;
        }
        *state->current++ = (void*)pc;
    }

    return _URC_NO_REASON;
}

// Capture stack trace using libunwind
static int captureStackTrace(void** buffer, int maxFrames) {
    struct BacktraceState state = { buffer, buffer + maxFrames };
    _Unwind_Backtrace(unwindCallback, &state);
    return state.current - buffer;
}

// Format stack trace as JSON
static void formatStackTraceJson(char* buffer, size_t bufferSize, void** frames, int frameCount) {
    size_t offset = 0;
    offset += snprintf(buffer + offset, bufferSize - offset, "[");

    for (int i = 0; i < frameCount && offset < bufferSize - 256; i++) {
        if (i > 0) {
            offset += snprintf(buffer + offset, bufferSize - offset, ",");
        }

        void* addr = frames[i];

        // Try to get symbol info
        Dl_info info;
        const char* symbolName = "???";
        const char* moduleName = "???";
        ptrdiff_t symbolOffset = 0;

        if (dladdr(addr, &info)) {
            if (info.dli_sname) {
                symbolName = info.dli_sname;
                symbolOffset = (char*)addr - (char*)info.dli_saddr;
            }
            if (info.dli_fname) {
                // Get just the filename, not full path
                const char* lastSlash = strrchr(info.dli_fname, '/');
                moduleName = lastSlash ? lastSlash + 1 : info.dli_fname;
            }
        }

        offset += snprintf(buffer + offset, bufferSize - offset,
            "{\"frame\":%d,\"address\":\"%p\",\"module\":\"%s\",\"symbol\":\"%s\",\"offset\":\"%td\"}",
            i, addr, moduleName, symbolName, symbolOffset);
    }

    offset += snprintf(buffer + offset, bufferSize - offset, "]");
}

// Signal handler
static void signalHandler(int signal, siginfo_t* info, void* context) {
    // Prevent re-entry
    if (isHandlingCrash) {
        return;
    }
    isHandlingCrash = 1;

    LOGE("Caught signal %d (%s)", signal, signalName(signal));

    // Get fault address
    void* faultAddress = info ? info->si_addr : NULL;

    // Capture stack trace
    stackFrameCount = captureStackTrace(stackFrames, MAX_FRAMES);

    // Format stack trace as JSON
    char stackTraceJson[16384];
    formatStackTraceJson(stackTraceJson, sizeof(stackTraceJson), stackFrames, stackFrameCount);

    // Get thread info
    pthread_t thread = pthread_self();

    // Build crash JSON
    snprintf(crashJsonBuffer, sizeof(crashJsonBuffer),
        "{"
        "\"signal\":%d,"
        "\"signalName\":\"%s\","
        "\"signalDescription\":\"%s\","
        "\"faultAddress\":\"%p\","
        "\"threadId\":%lu,"
        "\"siCode\":%d,"
        "\"frames\":%s"
        "}",
        signal,
        signalName(signal),
        signalDescription(signal),
        faultAddress,
        (unsigned long)thread,
        info ? info->si_code : 0,
        stackTraceJson
    );

    // Call the callback
    if (crashCallback) {
        crashCallback(crashJsonBuffer);
    }

    // Re-raise the signal with the original handler
    struct sigaction* previousHandler = &previousHandlers[signal];

    // Uninstall our handler first
    sigaction(signal, previousHandler, NULL);

    // Re-raise the signal
    raise(signal);
}

// Public API implementation

void MoonForge_Android_SetJavaVM(JavaVM* vm) {
    javaVM = vm;
}

void MoonForge_Android_InitializeCrashHandler(JNIEnv* env, MoonForgeCrashCallback callback) {
    if (isInitialized) {
        LOGD("Crash handler already initialized");
        return;
    }

    crashCallback = callback;

    // Install signal handlers
    for (int i = 0; i < kNumSignals; i++) {
        int sig = kSignalsToHandle[i];

        struct sigaction action;
        memset(&action, 0, sizeof(action));
        action.sa_sigaction = signalHandler;
        action.sa_flags = SA_SIGINFO | SA_ONSTACK;
        sigemptyset(&action.sa_mask);

        // Save previous handler
        if (sigaction(sig, &action, &previousHandlers[sig]) != 0) {
            LOGE("Failed to install handler for signal %d", sig);
        }
    }

    // Allocate alternate signal stack
    stack_t ss;
    ss.ss_sp = malloc(SIGSTKSZ);
    if (ss.ss_sp != NULL) {
        ss.ss_size = SIGSTKSZ;
        ss.ss_flags = 0;
        sigaltstack(&ss, NULL);
    }

    isInitialized = 1;
    LOGD("Crash handler initialized");
}

void MoonForge_Android_ShutdownCrashHandler(void) {
    if (!isInitialized) {
        return;
    }

    // Restore signal handlers
    for (int i = 0; i < kNumSignals; i++) {
        int sig = kSignalsToHandle[i];
        sigaction(sig, &previousHandlers[sig], NULL);
    }

    crashCallback = NULL;
    isInitialized = 0;
    LOGD("Crash handler shutdown");
}

int MoonForge_Android_IsInitialized(void) {
    return isInitialized;
}

// JNI functions

JNIEXPORT void JNICALL Java_com_moonforge_errortracking_CrashHandler_nativeInit(JNIEnv* env, jobject obj) {
    // This is called from Java to initialize the native handler
    // The actual initialization is done via Unity's P/Invoke
    LOGD("Native init called from Java");
}

JNIEXPORT void JNICALL Java_com_moonforge_errortracking_CrashHandler_nativeShutdown(JNIEnv* env, jobject obj) {
    MoonForge_Android_ShutdownCrashHandler();
}

JNIEXPORT void JNICALL Java_com_moonforge_errortracking_CrashHandler_nativeSimulateCrash(JNIEnv* env, jobject obj, jint crashType) {
#ifndef NDEBUG
    LOGD("Simulating crash type %d", crashType);
    switch (crashType) {
        case 0:
            // SIGSEGV - null pointer dereference
            {
                int* ptr = NULL;
                *ptr = 42;
            }
            break;
        case 1:
            // SIGABRT - abort
            abort();
            break;
        case 2:
            // SIGBUS - bus error
            {
                char* ptr = (char*)1;
                *ptr = 42;
            }
            break;
        default:
            LOGE("Unknown crash type: %d", crashType);
            break;
    }
#else
    LOGD("SimulateCrash is only available in debug builds");
#endif
}

// Unity calls this on library load
JNIEXPORT jint JNI_OnLoad(JavaVM* vm, void* reserved) {
    javaVM = vm;
    LOGD("JNI_OnLoad: JavaVM set");
    return JNI_VERSION_1_6;
}

/**
 * MoonForge Crash Handler for iOS
 *
 * Implements signal-based crash capturing for native iOS crashes.
 * Captures SIGSEGV, SIGABRT, SIGBUS, SIGFPE, SIGILL, SIGTRAP
 */

#import "MoonForgeCrashHandler.h"
#import <UIKit/UIKit.h>
#import <mach/mach.h>
#import <execinfo.h>
#import <signal.h>
#import <sys/sysctl.h>
#import <CoreTelephony/CTCarrier.h>
#import <CoreTelephony/CTTelephonyNetworkInfo.h>

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

// Previous signal handlers (for chaining)
static struct sigaction previousHandlers[NSIG];

// Callback for crash notification
static MoonForgeCrashCallback crashCallback = NULL;

// Flag to prevent re-entry
static volatile sig_atomic_t isHandlingCrash = 0;

// Initialization state
static int isInitialized = 0;

// Buffer for crash JSON (pre-allocated to avoid malloc in signal handler)
static char crashJsonBuffer[32768];

#pragma mark - Signal Handler

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

static void captureStackTrace(char* buffer, size_t bufferSize) {
    void* callstack[128];
    int frames = backtrace(callstack, 128);
    char** symbols = backtrace_symbols(callstack, frames);

    size_t offset = 0;
    offset += snprintf(buffer + offset, bufferSize - offset, "[");

    for (int i = 0; i < frames && offset < bufferSize - 100; i++) {
        if (i > 0) {
            offset += snprintf(buffer + offset, bufferSize - offset, ",");
        }

        // Parse symbol: "index  module  address  function"
        const char* symbol = symbols ? symbols[i] : "???";

        // Escape quotes in symbol
        char escapedSymbol[512];
        size_t j = 0, k = 0;
        while (symbol[j] && k < sizeof(escapedSymbol) - 2) {
            if (symbol[j] == '"' || symbol[j] == '\\') {
                escapedSymbol[k++] = '\\';
            }
            escapedSymbol[k++] = symbol[j++];
        }
        escapedSymbol[k] = '\0';

        offset += snprintf(buffer + offset, bufferSize - offset,
            "{\"frame\":%d,\"symbol\":\"%s\",\"address\":\"%p\"}",
            i, escapedSymbol, callstack[i]);
    }

    offset += snprintf(buffer + offset, bufferSize - offset, "]");

    if (symbols) {
        free(symbols);
    }
}

static void signalHandler(int signal, siginfo_t* info, void* context) {
    // Prevent re-entry
    if (isHandlingCrash) {
        return;
    }
    isHandlingCrash = 1;

    // Get fault address
    void* faultAddress = info ? info->si_addr : NULL;

    // Capture stack trace
    char stackTraceJson[16384];
    captureStackTrace(stackTraceJson, sizeof(stackTraceJson));

    // Get thread info
    mach_port_t thread = mach_thread_self();

    // Build crash JSON
    snprintf(crashJsonBuffer, sizeof(crashJsonBuffer),
        "{"
        "\"signal\":%d,"
        "\"signalName\":\"%s\","
        "\"signalDescription\":\"%s\","
        "\"faultAddress\":\"%p\","
        "\"threadId\":%u,"
        "\"frames\":%s"
        "}",
        signal,
        signalName(signal),
        signalDescription(signal),
        faultAddress,
        thread,
        stackTraceJson
    );

    // Call the callback
    if (crashCallback) {
        crashCallback(crashJsonBuffer);
    }

    // Re-raise the signal with the original handler
    struct sigaction* previousHandler = &previousHandlers[signal];
    if (previousHandler->sa_flags & SA_SIGINFO) {
        if (previousHandler->sa_sigaction) {
            previousHandler->sa_sigaction(signal, info, context);
        }
    } else if (previousHandler->sa_handler != SIG_DFL && previousHandler->sa_handler != SIG_IGN) {
        previousHandler->sa_handler(signal);
    } else {
        // Reset to default and re-raise
        signal(signal, SIG_DFL);
        raise(signal);
    }
}

#pragma mark - Exception Handler

static NSUncaughtExceptionHandler* previousExceptionHandler = NULL;

static void exceptionHandler(NSException* exception) {
    if (isHandlingCrash) {
        return;
    }
    isHandlingCrash = 1;

    NSArray* callStackSymbols = [exception callStackSymbols];
    NSArray* callStackReturnAddresses = [exception callStackReturnAddresses];

    // Build frames JSON
    NSMutableString* framesJson = [NSMutableString stringWithString:@"["];
    for (NSUInteger i = 0; i < callStackSymbols.count; i++) {
        if (i > 0) [framesJson appendString:@","];

        NSString* symbol = callStackSymbols[i];
        NSNumber* address = i < callStackReturnAddresses.count ? callStackReturnAddresses[i] : @0;

        // Escape the symbol
        NSString* escapedSymbol = [[symbol stringByReplacingOccurrencesOfString:@"\\" withString:@"\\\\"]
                                   stringByReplacingOccurrencesOfString:@"\"" withString:@"\\\""];

        [framesJson appendFormat:@"{\"frame\":%lu,\"symbol\":\"%@\",\"address\":\"0x%llx\"}",
         (unsigned long)i, escapedSymbol, [address unsignedLongLongValue]];
    }
    [framesJson appendString:@"]"];

    // Build crash JSON
    NSString* name = exception.name ?: @"NSException";
    NSString* reason = exception.reason ?: @"Unknown reason";

    // Escape strings
    name = [[name stringByReplacingOccurrencesOfString:@"\\" withString:@"\\\\"]
            stringByReplacingOccurrencesOfString:@"\"" withString:@"\\\""];
    reason = [[reason stringByReplacingOccurrencesOfString:@"\\" withString:@"\\\\"]
              stringByReplacingOccurrencesOfString:@"\"" withString:@"\\\""];

    NSString* crashJson = [NSString stringWithFormat:
        @"{"
        "\"exceptionType\":\"NSException\","
        "\"exceptionName\":\"%@\","
        "\"exceptionReason\":\"%@\","
        "\"frames\":%@"
        "}",
        name, reason, framesJson];

    // Call the callback
    if (crashCallback) {
        crashCallback([crashJson UTF8String]);
    }

    // Call previous handler
    if (previousExceptionHandler) {
        previousExceptionHandler(exception);
    }
}

#pragma mark - Public API

void MoonForge_InitializeCrashHandler(MoonForgeCrashCallback callback) {
    if (isInitialized) {
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
        sigaction(sig, &action, &previousHandlers[sig]);
    }

    // Install NSException handler
    previousExceptionHandler = NSGetUncaughtExceptionHandler();
    NSSetUncaughtExceptionHandler(exceptionHandler);

    isInitialized = 1;
}

void MoonForge_ShutdownCrashHandler(void) {
    if (!isInitialized) {
        return;
    }

    // Restore signal handlers
    for (int i = 0; i < kNumSignals; i++) {
        int sig = kSignalsToHandle[i];
        sigaction(sig, &previousHandlers[sig], NULL);
    }

    // Restore exception handler
    NSSetUncaughtExceptionHandler(previousExceptionHandler);
    previousExceptionHandler = NULL;

    crashCallback = NULL;
    isInitialized = 0;
}

int MoonForge_IsCrashHandlerInitialized(void) {
    return isInitialized;
}

const char* MoonForge_GetThermalState(void) {
    if (@available(iOS 11.0, *)) {
        NSProcessInfoThermalState state = [[NSProcessInfo processInfo] thermalState];
        switch (state) {
            case NSProcessInfoThermalStateNominal:
                return "nominal";
            case NSProcessInfoThermalStateFair:
                return "fair";
            case NSProcessInfoThermalStateSerious:
                return "serious";
            case NSProcessInfoThermalStateCritical:
                return "critical";
        }
    }
    return NULL;
}

void MoonForge_GetMemoryInfo(float* usedMB, float* availableMB) {
    // Get used memory
    struct task_basic_info info;
    mach_msg_type_number_t size = TASK_BASIC_INFO_COUNT;
    kern_return_t kerr = task_info(mach_task_self(),
                                   TASK_BASIC_INFO,
                                   (task_info_t)&info,
                                   &size);

    if (kerr == KERN_SUCCESS) {
        *usedMB = (float)info.resident_size / (1024.0f * 1024.0f);
    } else {
        *usedMB = 0;
    }

    // Get available memory (total physical memory)
    *availableMB = (float)[[NSProcessInfo processInfo] physicalMemory] / (1024.0f * 1024.0f);
}

const char* MoonForge_GetCarrierName(void) {
    CTTelephonyNetworkInfo* networkInfo = [[CTTelephonyNetworkInfo alloc] init];

    if (@available(iOS 12.0, *)) {
        NSDictionary<NSString*, CTCarrier*>* carriers = [networkInfo serviceSubscriberCellularProviders];
        for (CTCarrier* carrier in carriers.allValues) {
            NSString* carrierName = carrier.carrierName;
            if (carrierName && carrierName.length > 0) {
                return strdup([carrierName UTF8String]);
            }
        }
    } else {
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
        CTCarrier* carrier = [networkInfo subscriberCellularProvider];
        if (carrier && carrier.carrierName) {
            return strdup([carrier.carrierName UTF8String]);
        }
#pragma clang diagnostic pop
    }

    return NULL;
}

void MoonForge_SimulateCrash(int crashType) {
#if DEBUG
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
            NSLog(@"[MoonForge] Unknown crash type: %d", crashType);
            break;
    }
#else
    NSLog(@"[MoonForge] SimulateCrash is only available in DEBUG builds");
#endif
}

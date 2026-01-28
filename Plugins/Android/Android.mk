# Android.mk for MoonForge Crash Handler

LOCAL_PATH := $(call my-dir)

include $(CLEAR_VARS)

LOCAL_MODULE := moonforge_crash_handler
LOCAL_SRC_FILES := moonforge_crash_handler.c
LOCAL_LDLIBS := -llog -ldl
LOCAL_CFLAGS := -Wall -Wextra -fvisibility=hidden

# Enable unwind support
LOCAL_LDFLAGS := -Wl,--no-undefined

include $(BUILD_SHARED_LIBRARY)

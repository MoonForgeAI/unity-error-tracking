# MoonForge Unity SDK - User Identification Guide

## Table of Contents
1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Quick Start](#quick-start)
4. [Integration Guide](#integration-guide)
   - [Step 1: Initialize the SDK](#step-1-initialize-the-sdk)
   - [Step 2: Identify the User](#step-2-identify-the-user)
   - [Step 3: Handle Logout](#step-3-handle-logout)
5. [API Reference](#api-reference)
   - [SetUser](#setuser)
   - [ClearUser](#clearuser)
   - [MoonForgeAnalytics.Identify](#moonforgeanalyticsidentify)
6. [Integration Patterns](#integration-patterns)
   - [Firebase Authentication](#firebase-authentication)
   - [PlayFab](#playfab)
   - [Custom Backend](#custom-backend)
   - [Guest / Anonymous Users](#guest--anonymous-users)
   - [Apple Game Center / Google Play Games](#apple-game-center--google-play-games)
7. [User Tags](#user-tags)
8. [Verification](#verification)
9. [Common Mistakes](#common-mistakes)
10. [FAQ](#faq)

---

## Overview

MoonForge uses a `userId` to associate error reports and analytics events with individual players. When a userId is set, every crash, exception, network error, and analytics event captured by the SDK is tagged with that identifier, enabling you to:

- Search for all errors affecting a specific player
- Correlate crash patterns with user segments (e.g., free vs. premium)
- Reproduce issues with full context of who was affected
- Track error impact across your player base

**The SDK never collects userId automatically.** You must explicitly call `SetUser()` after your game's authentication flow completes. This gives you full control over what identifier is sent and when.

---

## Prerequisites

- MoonForge Unity SDK installed and configured (Game ID set via **MoonForge > Setup Error Tracking**)
- The SDK initializes automatically on game start via `MoonForgeAutoInitializer` - no manual initialization code required unless you've disabled auto-init
- Your game has a user authentication system (or a way to generate a unique player ID)

---

## Quick Start

If you just want to get userId flowing to MoonForge, add this single call after your login/auth completes:

```csharp
using MoonForge.ErrorTracking;

// After your login succeeds:
MoonForgeErrorTracker.Instance?.SetUser("your-player-id");
```

That's it. All subsequent error reports and analytics events will include this userId.

Read on for the full integration guide, best practices, and verification steps.

---

## Integration Guide

### Step 1: Initialize the SDK

The SDK auto-initializes before the first scene loads. Verify it's running by checking the console for:

```
[MoonForge] Error tracking initialized successfully!
```

If you need to confirm programmatically:

```csharp
if (MoonForgeErrorTracker.IsInitialized)
{
    Debug.Log("MoonForge is ready");
}
```

> **Note:** If you don't see the initialization log, ensure your Game ID is configured via **MoonForge > Setup Error Tracking** in the Unity menu.

### Step 2: Identify the User

Call `SetUser()` immediately after your authentication flow succeeds. This applies to both error tracking and analytics.

```csharp
using MoonForge.ErrorTracking;
using MoonForge.ErrorTracking.Analytics;

public class AuthManager : MonoBehaviour
{
    public void OnLoginSuccess(string playerId, string playerName)
    {
        // 1. Set user for error tracking
        //    All crashes and errors from this point forward include this userId
        MoonForgeErrorTracker.Instance?.SetUser(playerId);

        // 2. Identify user for analytics (optional but recommended)
        //    Links analytics events to this user and sends an identify event
        MoonForgeAnalytics.Identify(playerId, new Dictionary<string, object>
        {
            { "name", playerName },
            { "signup_date", "2025-01-15" }
        });
    }
}
```

**What happens when you call `SetUser()`:**
- The userId is stored in memory for the current session
- Every error report created after this call includes the userId
- If `debugMode` is enabled in settings, you'll see: `[MoonForge] User set: <userId>`

**What happens when you call `MoonForgeAnalytics.Identify()`:**
- The analytics distinct ID is updated from an anonymous UUID to the provided userId
- The userId is persisted to `PlayerPrefs` so returning users are recognized across sessions
- An `identify` event is sent to the MoonForge backend with any traits you provide

### Step 3: Handle Logout

When a user logs out, clear their identity so subsequent errors are not attributed to them:

```csharp
public void OnLogout()
{
    // Clear user from error tracking
    MoonForgeErrorTracker.Instance?.ClearUser();

    // Note: Analytics distinct ID is NOT reset on ClearUser().
    // To fully reset analytics identity, the user would need to
    // reinstall the app or you can call Identify() with a new anonymous ID.
}
```

---

## API Reference

### SetUser

```csharp
public void SetUser(string userId, Dictionary<string, string> tags = null)
```

Sets the current user for error attribution. All error reports captured after this call will include the provided userId.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `userId` | `string` | Yes | A unique identifier for the user. Can be any string: database ID, Firebase UID, PlayFab ID, email hash, etc. |
| `tags` | `Dictionary<string, string>` | No | Key-value pairs of metadata about the user (e.g., subscription tier, player level). Included with every error report. |

**Example:**

```csharp
MoonForgeErrorTracker.Instance?.SetUser(
    userId: "usr_a1b2c3d4",
    tags: new Dictionary<string, string>
    {
        { "plan", "premium" },
        { "level", "42" },
        { "region", "us-west" }
    }
);
```

### ClearUser

```csharp
public void ClearUser()
```

Clears the current user identity and all associated tags. Call this on logout. Errors captured after this call will have no userId attached.

**Example:**

```csharp
MoonForgeErrorTracker.Instance?.ClearUser();
```

### MoonForgeAnalytics.Identify

```csharp
public static void Identify(string userId, Dictionary<string, object> traits = null)
```

Identifies the user for analytics tracking. This updates the analytics distinct ID from an anonymous ID to the provided userId and sends an `identify` event to MoonForge.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `userId` | `string` | Yes | The same unique identifier you pass to `SetUser()`. Must not be null or empty. |
| `traits` | `Dictionary<string, object>` | No | User properties/traits to associate with the user profile (e.g., name, email, signup date, subscription). Values can be strings, numbers, or booleans. |

**Example:**

```csharp
MoonForgeAnalytics.Identify("usr_a1b2c3d4", new Dictionary<string, object>
{
    { "name", "Jane Doe" },
    { "email", "jane@example.com" },
    { "subscription", "premium" },
    { "total_purchases", 12 },
    { "first_seen", "2025-03-01T00:00:00Z" }
});
```

> **Important:** Always call both `SetUser()` and `MoonForgeAnalytics.Identify()` with the **same userId** so error reports and analytics events can be correlated.

---

## Integration Patterns

### Firebase Authentication

```csharp
using Firebase.Auth;
using MoonForge.ErrorTracking;
using MoonForge.ErrorTracking.Analytics;

public class FirebaseAuthManager : MonoBehaviour
{
    private FirebaseAuth auth;

    private void Start()
    {
        auth = FirebaseAuth.DefaultInstance;
        auth.StateChanged += OnAuthStateChanged;
    }

    private void OnAuthStateChanged(object sender, EventArgs e)
    {
        var user = auth.CurrentUser;

        if (user != null)
        {
            // User signed in - identify them to MoonForge
            MoonForgeErrorTracker.Instance?.SetUser(user.UserId, new Dictionary<string, string>
            {
                { "provider", user.ProviderId },
                { "email_verified", user.IsEmailVerified.ToString() }
            });

            MoonForgeAnalytics.Identify(user.UserId, new Dictionary<string, object>
            {
                { "display_name", user.DisplayName },
                { "provider", user.ProviderId }
            });
        }
        else
        {
            // User signed out
            MoonForgeErrorTracker.Instance?.ClearUser();
        }
    }

    private void OnDestroy()
    {
        auth.StateChanged -= OnAuthStateChanged;
    }
}
```

### PlayFab

```csharp
using PlayFab;
using PlayFab.ClientModels;
using MoonForge.ErrorTracking;
using MoonForge.ErrorTracking.Analytics;

public class PlayFabAuthManager : MonoBehaviour
{
    public void Login(string customId)
    {
        PlayFabClientAPI.LoginWithCustomID(
            new LoginWithCustomIDRequest
            {
                CustomId = customId,
                CreateAccount = true
            },
            OnLoginSuccess,
            OnLoginFailure
        );
    }

    private void OnLoginSuccess(LoginResult result)
    {
        // Use PlayFab's PlayFabId as the userId
        MoonForgeErrorTracker.Instance?.SetUser(result.PlayFabId, new Dictionary<string, string>
        {
            { "newly_created", result.NewlyCreated.ToString() }
        });

        MoonForgeAnalytics.Identify(result.PlayFabId, new Dictionary<string, object>
        {
            { "newly_created", result.NewlyCreated }
        });
    }

    private void OnLoginFailure(PlayFabError error)
    {
        // Capture the login failure as a network error
        MoonForgeErrorTracker.Instance?.CaptureNetworkError(
            url: "playfab/LoginWithCustomID",
            method: "POST",
            statusCode: error.HttpCode,
            errorMessage: error.ErrorMessage
        );
    }
}
```

### Custom Backend

```csharp
using UnityEngine;
using UnityEngine.Networking;
using MoonForge.ErrorTracking;
using MoonForge.ErrorTracking.Analytics;

public class CustomAuthManager : MonoBehaviour
{
    public async void Login(string email, string password)
    {
        using var request = UnityWebRequest.Post(
            "https://api.yourgame.com/auth/login",
            $"{{\"email\":\"{email}\",\"password\":\"{password}\"}}",
            "application/json"
        );

        await request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            var response = JsonUtility.FromJson<LoginResponse>(request.downloadHandler.text);

            // Set the user in MoonForge using your backend's user ID
            MoonForgeErrorTracker.Instance?.SetUser(response.userId, new Dictionary<string, string>
            {
                { "role", response.role }
            });

            MoonForgeAnalytics.Identify(response.userId, new Dictionary<string, object>
            {
                { "email", email },
                { "role", response.role }
            });
        }
    }

    public void Logout()
    {
        MoonForgeErrorTracker.Instance?.ClearUser();
    }

    [System.Serializable]
    private class LoginResponse
    {
        public string userId;
        public string role;
        public string token;
    }
}
```

### Guest / Anonymous Users

If your game supports guest play before login, you can still track users with a device-generated ID:

```csharp
using UnityEngine;
using MoonForge.ErrorTracking;

public class GuestUserManager : MonoBehaviour
{
    private const string GuestIdKey = "MoonForge_GuestId";

    private void Start()
    {
        // Generate or retrieve a persistent guest ID
        var guestId = PlayerPrefs.GetString(GuestIdKey, null);

        if (string.IsNullOrEmpty(guestId))
        {
            guestId = "guest_" + System.Guid.NewGuid().ToString("N").Substring(0, 12);
            PlayerPrefs.SetString(GuestIdKey, guestId);
            PlayerPrefs.Save();
        }

        // Set guest as the user until they log in
        MoonForgeErrorTracker.Instance?.SetUser(guestId, new Dictionary<string, string>
        {
            { "account_type", "guest" }
        });
    }

    public void OnLoginSuccess(string realUserId)
    {
        // Upgrade from guest to authenticated user
        MoonForgeErrorTracker.Instance?.SetUser(realUserId, new Dictionary<string, string>
        {
            { "account_type", "authenticated" }
        });

        MoonForgeAnalytics.Identify(realUserId);
    }
}
```

### Apple Game Center / Google Play Games

```csharp
using MoonForge.ErrorTracking;

public class PlatformAuthManager : MonoBehaviour
{
    // Apple Game Center
    public void OnGameCenterAuthenticated(string gamePlayerId)
    {
        MoonForgeErrorTracker.Instance?.SetUser(gamePlayerId, new Dictionary<string, string>
        {
            { "platform", "ios" },
            { "auth_provider", "game_center" }
        });

        MoonForgeAnalytics.Identify(gamePlayerId, new Dictionary<string, object>
        {
            { "auth_provider", "game_center" }
        });
    }

    // Google Play Games
    public void OnPlayGamesAuthenticated(string playerId)
    {
        MoonForgeErrorTracker.Instance?.SetUser(playerId, new Dictionary<string, string>
        {
            { "platform", "android" },
            { "auth_provider", "play_games" }
        });

        MoonForgeAnalytics.Identify(playerId, new Dictionary<string, object>
        {
            { "auth_provider", "play_games" }
        });
    }
}
```

---

## User Tags

User tags are key-value pairs attached to every error report for the identified user. They help you filter and segment errors in the MoonForge dashboard.

### Recommended Tags

| Tag Key | Example Value | Purpose |
|---------|---------------|---------|
| `plan` / `subscription` | `"free"`, `"premium"`, `"vip"` | Prioritize errors affecting paying users |
| `level` | `"42"` | Correlate crashes with player progression |
| `region` | `"us-west"`, `"eu-central"` | Identify region-specific issues |
| `ab_group` | `"control"`, `"variant_a"` | Detect errors caused by A/B test variants |
| `account_type` | `"guest"`, `"authenticated"` | Distinguish guest vs. logged-in user issues |
| `install_source` | `"organic"`, `"ad_campaign_x"` | Track quality by acquisition channel |

### Updating Tags

Tags are set when you call `SetUser()`. To update tags mid-session (e.g., when a player levels up or upgrades their subscription), call `SetUser()` again with the same userId and new tags:

```csharp
// Player upgraded to premium mid-session
MoonForgeErrorTracker.Instance?.SetUser("usr_a1b2c3d4", new Dictionary<string, string>
{
    { "plan", "premium" },      // updated
    { "level", "42" },
    { "region", "us-west" }
});
```

> **Note:** Calling `SetUser()` replaces all previous tags. Always pass the complete set of tags you want associated with the user.

---

## Verification

After integrating userId, verify that it's being sent correctly.

### 1. Enable Debug Mode

In your MoonForge Settings (Unity Inspector or **MoonForge > Setup Error Tracking**), enable **Debug Mode**. You'll see console logs confirming the user was set:

```
[MoonForge] User set: usr_a1b2c3d4
```

### 2. Trigger a Test Error

After setting the user, trigger a test error to confirm the userId is included in the payload:

```csharp
// After SetUser has been called:
MoonForgeErrorTracker.Instance?.CaptureMessage(
    "User identification test",
    ErrorLevel.Info,
    new Dictionary<string, string> { { "test", "user_id_verification" } }
);
```

### 3. Check the MoonForge Dashboard

1. Go to your game in the MoonForge dashboard
2. Navigate to **Errors** or **Error Tracking**
3. Find the test error you just sent
4. Verify the **User** field shows your userId
5. Verify any **Tags** you set are visible

### 4. Verify Analytics Identity

If you also called `MoonForgeAnalytics.Identify()`, check:

```
[MoonForge Analytics] Identified user: usr_a1b2c3d4
```

### Programmatic Verification

You can add a verification helper to confirm everything is wired correctly during development:

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
public static class MoonForgeVerification
{
    public static void VerifyUserSetup()
    {
        var tracker = MoonForgeErrorTracker.Instance;

        if (tracker == null)
        {
            Debug.LogError("[MoonForge Verify] SDK not initialized!");
            return;
        }

        if (!MoonForgeErrorTracker.IsInitialized)
        {
            Debug.LogError("[MoonForge Verify] SDK not ready!");
            return;
        }

        // Send a verification event
        tracker.CaptureMessage(
            "[Verification] User ID integration check",
            ErrorLevel.Info,
            new Dictionary<string, string>
            {
                { "verification", "user_id_check" },
                { "timestamp", System.DateTimeOffset.UtcNow.ToString("o") }
            }
        );

        Debug.Log("[MoonForge Verify] Verification event sent. Check dashboard for the event with userId attached.");
    }
}
#endif
```

---

## Common Mistakes

### 1. Calling SetUser before SDK initialization

```csharp
// WRONG - SDK may not be initialized yet
void Awake()
{
    MoonForgeErrorTracker.Instance?.SetUser("user-123");
}
```

```csharp
// CORRECT - Check initialization first
void Start()
{
    if (MoonForgeErrorTracker.IsInitialized)
    {
        MoonForgeErrorTracker.Instance?.SetUser("user-123");
    }
}
```

The SDK auto-initializes via `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`, so by the time `Start()` runs in your first scene, it should be ready. However, `Awake()` in the same frame may race with it.

### 2. Forgetting to call ClearUser on logout

If a user logs out and another logs in on the same device, errors from the second user would be attributed to the first user if you don't call `ClearUser()` between sessions.

```csharp
public void SwitchAccount(string newUserId)
{
    // Always clear before setting a new user
    MoonForgeErrorTracker.Instance?.ClearUser();
    MoonForgeErrorTracker.Instance?.SetUser(newUserId);
}
```

### 3. Using different IDs for SetUser and Analytics.Identify

```csharp
// WRONG - Different IDs prevent correlation
MoonForgeErrorTracker.Instance?.SetUser("firebase_uid_123");
MoonForgeAnalytics.Identify("playfab_id_456");
```

```csharp
// CORRECT - Same ID for both
string userId = "firebase_uid_123";
MoonForgeErrorTracker.Instance?.SetUser(userId);
MoonForgeAnalytics.Identify(userId);
```

### 4. Passing PII directly as userId

```csharp
// AVOID - Email as userId exposes PII in error reports
MoonForgeErrorTracker.Instance?.SetUser("jane@example.com");
```

```csharp
// BETTER - Use an opaque ID, pass PII as analytics traits only
MoonForgeErrorTracker.Instance?.SetUser("usr_a1b2c3d4");
MoonForgeAnalytics.Identify("usr_a1b2c3d4", new Dictionary<string, object>
{
    { "email", "jane@example.com" }  // stored server-side, not in crash reports
});
```

### 5. Not handling the null-conditional operator

```csharp
// This silently does nothing if Instance is null:
MoonForgeErrorTracker.Instance?.SetUser("user-123");

// If you need to know whether it succeeded:
var tracker = MoonForgeErrorTracker.Instance;
if (tracker != null)
{
    tracker.SetUser("user-123");
    Debug.Log("User set successfully");
}
else
{
    Debug.LogWarning("MoonForge not available - userId not set");
}
```

---

## FAQ

**Q: What should I use as the userId?**
Use your backend's unique user identifier - the same ID you use in your database. Firebase UID, PlayFab PlayFabId, a UUID from your custom auth, or any stable unique string. Avoid using sequential integers or easily guessable values.

**Q: Is userId required?**
No. The SDK works without a userId. Errors are still captured with a `sessionId` for grouping. However, without a userId, you cannot search for errors by player or correlate errors with specific user accounts in the MoonForge dashboard.

**Q: When should I call SetUser?**
Immediately after authentication succeeds. The sooner you call it, the more errors will have a userId attached. Any errors that occur before `SetUser()` is called will not have a userId.

**Q: Does the userId persist across sessions?**
`SetUser()` (error tracking) does **not** persist - you must call it each time the game starts after the user logs in. `MoonForgeAnalytics.Identify()` **does** persist the distinct ID to `PlayerPrefs`, so analytics events from returning users are automatically associated.

**Q: Can I change the userId mid-session?**
Yes. Call `SetUser()` again with the new ID. This is useful for account switching. Call `ClearUser()` first if you want a clean break between users.

**Q: What's the maximum length for userId?**
There's no enforced limit in the SDK, but we recommend keeping it under 128 characters for compatibility with the MoonForge backend.

**Q: Are user tags searchable in the dashboard?**
Yes. Tags are indexed and can be used to filter and search errors in the MoonForge dashboard.

**Q: What data is sent with each error?**
When a userId is set, each error report includes:
- `userId` - the string you provided
- `userTags` - the key-value pairs you provided
- `sessionId` - auto-generated unique ID for this game session
- Device context (OS, model, memory, etc.)
- Game state (current scene, game mode, custom data)
- Breadcrumbs (recent user actions leading up to the error)
- Stack trace and exception details

**Q: Does the SDK collect any user data automatically?**
The SDK collects device-level information (OS version, device model, screen resolution, available memory) automatically. It does **not** collect any personally identifiable information (PII) unless you explicitly pass it via `SetUser()`, tags, or `MoonForgeAnalytics.Identify()`.

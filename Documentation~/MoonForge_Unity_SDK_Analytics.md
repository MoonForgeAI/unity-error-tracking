# MoonForge Unity SDK - Analytics Documentation

## Table of Contents
1. [Overview](#overview)
2. [Installation](#installation)
3. [Configuration](#configuration)
4. [Analytics API](#analytics-api)
   - [Automatic Tracking](#automatic-tracking)
   - [Screen Views](#screen-views)
   - [Custom Events](#custom-events)
   - [User Identification](#user-identification)
   - [User Properties](#user-properties)
5. [Common Use Cases](#common-use-cases)
6. [API Reference](#api-reference)
7. [Data Reference](#data-reference)
8. [Best Practices](#best-practices)

---

## Overview

The MoonForge Unity SDK provides comprehensive analytics tracking for your Unity games. It automatically captures:

- **Session tracking** - Start/end events with duration
- **Screen/Scene views** - Automatic tracking when scenes load
- **Custom events** - Track any game event with custom data
- **User identification** - Associate events with user accounts

### Key Features

- **Zero-config setup** - Works out of the box with automatic scene tracking
- **Offline support** - Events are queued when offline and sent when connectivity returns
- **Session management** - Automatic session timeout detection (default: 30 minutes)
- **Persistent user ID** - Anonymous users get a persistent `distinctId` across sessions
- **Lightweight** - Minimal performance impact on your game

---

## Installation

### Via Unity Package Manager (Git URL)

1. Open Unity Package Manager (`Window > Package Manager`)
2. Click `+` > `Add package from git URL`
3. Enter: `https://github.com/MoonForgeAI/unity-error-tracking.git`

### Via Package Manifest

Add to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.moonforge.errortracking": "https://github.com/MoonForgeAI/unity-error-tracking.git"
  }
}
```

---

## Configuration

### Quick Setup

1. Go to `MoonForge > Setup Error Tracking` in the Unity menu
2. Enter your **Game ID** from the MoonForge dashboard
3. Enable **"Enable In Editor"** for testing
4. Done! The SDK auto-initializes on game start

### Configuration Options

Access settings via `Resources/MoonForgeSettings.asset`:

| Setting | Default | Description |
|---------|---------|-------------|
| `gameId` | (required) | Your Game ID from MoonForge dashboard |
| `enabled` | `true` | Master switch for all tracking |
| `enableInEditor` | `false` | Enable tracking in Unity Editor |
| `debugMode` | `false` | Show debug logs in console |
| **Analytics** | | |
| `enableAnalytics` | `true` | Enable analytics tracking |
| `trackSceneViewsAutomatically` | `true` | Auto-track scene loads |
| `sessionTimeoutSeconds` | `1800` | Session timeout (30 min default) |

---

## Analytics API

### Namespace

```csharp
using MoonForge.ErrorTracking.Analytics;
```

### Checking Initialization

```csharp
if (MoonForgeAnalytics.IsInitialized)
{
    // Safe to call analytics methods
}
```

---

### Automatic Tracking

When `trackSceneViewsAutomatically` is enabled (default), the SDK automatically tracks:

1. **Session Start** - When the game launches
2. **Screen Views** - Every time a scene loads
3. **Session End** - When the game closes (queued for next session if offline)

No code required for basic tracking!

---

### Screen Views

Track when players navigate to different screens or scenes.

#### Automatic (Default)
Scene views are tracked automatically when `trackSceneViewsAutomatically = true`.

#### Manual Tracking

```csharp
// Track a screen view manually
MoonForgeAnalytics.TrackScreenView("MainMenu");

// Track UI screens within a scene
MoonForgeAnalytics.TrackScreenView("InventoryPanel");
MoonForgeAnalytics.TrackScreenView("SettingsScreen");
MoonForgeAnalytics.TrackScreenView("ShopScreen");
```

---

### Custom Events

Track any game event with optional custom data.

#### Basic Event

```csharp
// Simple event
MoonForgeAnalytics.TrackEvent("button_click");

// Event with single property
MoonForgeAnalytics.TrackEvent("level_complete");
```

#### Event with Data

```csharp
// Track level completion with details
MoonForgeAnalytics.TrackEvent("level_complete", new Dictionary<string, object>
{
    { "level_id", "level_05" },
    { "score", 15000 },
    { "stars", 3 },
    { "time_seconds", 142.5f },
    { "deaths", 2 }
});
```

#### Common Game Events

```csharp
// Tutorial progress
MoonForgeAnalytics.TrackEvent("tutorial_step", new Dictionary<string, object>
{
    { "step", 3 },
    { "step_name", "first_battle" }
});

// In-app purchase
MoonForgeAnalytics.TrackEvent("purchase", new Dictionary<string, object>
{
    { "item_id", "gem_pack_100" },
    { "price", 4.99 },
    { "currency", "USD" },
    { "quantity", 1 }
});

// Battle/Match result
MoonForgeAnalytics.TrackEvent("match_end", new Dictionary<string, object>
{
    { "result", "victory" },
    { "match_type", "ranked" },
    { "duration_seconds", 320 },
    { "score", 2500 },
    { "opponent_level", 15 }
});

// Achievement unlocked
MoonForgeAnalytics.TrackEvent("achievement_unlock", new Dictionary<string, object>
{
    { "achievement_id", "first_win" },
    { "achievement_name", "First Victory" },
    { "category", "combat" }
});

// Item action
MoonForgeAnalytics.TrackEvent("item_equipped", new Dictionary<string, object>
{
    { "item_id", "sword_legendary_01" },
    { "item_type", "weapon" },
    { "rarity", "legendary" },
    { "level", 50 }
});

// Social action
MoonForgeAnalytics.TrackEvent("friend_added", new Dictionary<string, object>
{
    { "method", "search" },
    { "friend_count", 15 }
});

// Error/Issue tracking
MoonForgeAnalytics.TrackEvent("soft_error", new Dictionary<string, object>
{
    { "error_type", "inventory_full" },
    { "item_attempted", "health_potion" }
});
```

---

### User Identification

Associate analytics events with a user account after login.

#### Identify User

```csharp
// Basic identification
MoonForgeAnalytics.Identify("user_12345");

// With user traits
MoonForgeAnalytics.Identify("user_12345", new Dictionary<string, object>
{
    { "email", "player@example.com" },
    { "name", "PlayerOne" },
    { "created_at", "2024-01-15" },
    { "subscription", "premium" },
    { "level", 42 }
});
```

#### When to Call Identify

```csharp
// After successful login
public void OnLoginSuccess(string userId, UserData userData)
{
    MoonForgeAnalytics.Identify(userId, new Dictionary<string, object>
    {
        { "username", userData.Username },
        { "account_type", userData.IsPremium ? "premium" : "free" },
        { "registration_date", userData.CreatedAt.ToString("yyyy-MM-dd") }
    });
}
```

#### Reset on Logout

```csharp
public void OnLogout()
{
    // Reset generates a new anonymous distinctId
    MoonForgeAnalytics.Reset();
}
```

---

### User Properties

Set persistent properties that are included with ALL subsequent events.

#### Set Properties

```csharp
// Set individual properties
MoonForgeAnalytics.SetUserProperty("subscription_tier", "gold");
MoonForgeAnalytics.SetUserProperty("player_level", 25);
MoonForgeAnalytics.SetUserProperty("total_playtime_hours", 150);

// These will be included in every event automatically
MoonForgeAnalytics.TrackEvent("item_purchased"); // includes subscription_tier, player_level, etc.
```

#### Update Properties

```csharp
// Update when player levels up
public void OnLevelUp(int newLevel)
{
    MoonForgeAnalytics.SetUserProperty("player_level", newLevel);

    MoonForgeAnalytics.TrackEvent("level_up", new Dictionary<string, object>
    {
        { "new_level", newLevel }
    });
}
```

#### Remove Properties

```csharp
// Remove a single property
MoonForgeAnalytics.RemoveUserProperty("temporary_buff");

// Clear all properties
MoonForgeAnalytics.ClearUserProperties();
```

---

## Common Use Cases

### Game Session Tracking

```csharp
public class GameSessionManager : MonoBehaviour
{
    void Start()
    {
        // Session automatically starts, but you can add context
        MoonForgeAnalytics.SetUserProperty("game_version", Application.version);
        MoonForgeAnalytics.SetUserProperty("device_model", SystemInfo.deviceModel);
    }

    public void StartNewGame()
    {
        MoonForgeAnalytics.TrackEvent("new_game_started", new Dictionary<string, object>
        {
            { "difficulty", selectedDifficulty },
            { "character_class", selectedClass }
        });
    }

    public void OnGameOver(bool won, int score)
    {
        MoonForgeAnalytics.TrackEvent("game_over", new Dictionary<string, object>
        {
            { "result", won ? "win" : "loss" },
            { "score", score },
            { "play_time_seconds", Time.timeSinceLevelLoad }
        });
    }
}
```

### E-Commerce / IAP Tracking

```csharp
public class StoreManager : MonoBehaviour
{
    public void OnPurchaseComplete(Product product, bool success)
    {
        if (success)
        {
            MoonForgeAnalytics.TrackEvent("purchase_complete", new Dictionary<string, object>
            {
                { "product_id", product.Id },
                { "product_name", product.Name },
                { "price", product.Price },
                { "currency", product.Currency },
                { "store", Application.platform.ToString() }
            });
        }
        else
        {
            MoonForgeAnalytics.TrackEvent("purchase_failed", new Dictionary<string, object>
            {
                { "product_id", product.Id },
                { "error_reason", "payment_declined" }
            });
        }
    }

    public void OnStoreOpened()
    {
        MoonForgeAnalytics.TrackScreenView("Store");
    }

    public void OnItemViewed(string itemId)
    {
        MoonForgeAnalytics.TrackEvent("store_item_viewed", new Dictionary<string, object>
        {
            { "item_id", itemId }
        });
    }
}
```

### Funnel Tracking

```csharp
public class OnboardingManager : MonoBehaviour
{
    public void OnTutorialStep(int step, string stepName)
    {
        MoonForgeAnalytics.TrackEvent("tutorial_progress", new Dictionary<string, object>
        {
            { "step_number", step },
            { "step_name", stepName }
        });
    }

    public void OnTutorialComplete()
    {
        MoonForgeAnalytics.TrackEvent("tutorial_complete", new Dictionary<string, object>
        {
            { "total_time_seconds", tutorialTimer },
            { "skipped_steps", skippedSteps.Count }
        });
    }

    public void OnTutorialSkipped(int atStep)
    {
        MoonForgeAnalytics.TrackEvent("tutorial_skipped", new Dictionary<string, object>
        {
            { "skipped_at_step", atStep }
        });
    }
}
```

### Multiplayer/Social Tracking

```csharp
public class MultiplayerManager : MonoBehaviour
{
    public void OnMatchFound(MatchInfo match)
    {
        MoonForgeAnalytics.TrackEvent("match_found", new Dictionary<string, object>
        {
            { "match_type", match.Type },
            { "player_count", match.PlayerCount },
            { "queue_time_seconds", match.QueueTime }
        });
    }

    public void OnMatchEnd(MatchResult result)
    {
        MoonForgeAnalytics.TrackEvent("match_end", new Dictionary<string, object>
        {
            { "result", result.Won ? "victory" : "defeat" },
            { "score", result.Score },
            { "kills", result.Kills },
            { "deaths", result.Deaths },
            { "duration_seconds", result.Duration }
        });
    }
}
```

---

## API Reference

### MoonForgeAnalytics (Static Class)

| Method | Description |
|--------|-------------|
| `TrackScreenView(string screenName)` | Track a screen/scene view |
| `TrackEvent(string eventName, Dictionary<string, object> data = null)` | Track a custom event |
| `Identify(string userId, Dictionary<string, object> traits = null)` | Identify the current user |
| `SetUserProperty(string key, object value)` | Set a persistent user property |
| `RemoveUserProperty(string key)` | Remove a user property |
| `ClearUserProperties()` | Clear all user properties |
| `Flush()` | Force send queued events |
| `Reset()` | Reset user ID and properties |
| `GetDistinctId()` | Get current distinct/user ID |
| `GetSessionId()` | Get current session ID |

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsInitialized` | `bool` | Check if analytics is ready |

---

## Data Reference

### Automatically Captured Data

Every event includes:

| Field | Description | Example |
|-------|-------------|---------|
| `game_id` | Your game identifier | `26a83984-d621-...` |
| `session_id` | Current session | `a88160b6-d738-...` |
| `screen` | Screen resolution | `1920x1080` |
| `language` | Device language | `en` |
| `device` | Device type | `desktop`, `mobile` |
| `created_at` | Event timestamp | `2024-01-30 10:41:45` |

### Event Types

| Type | `event_name` | Description |
|------|--------------|-------------|
| Screen View | (empty) | Scene/screen navigation |
| Custom Event | Your event name | Any custom event |
| Session Start | `session_start` | Game launch |
| Session End | `session_end` | Game close |

### Data Types Supported

```csharp
new Dictionary<string, object>
{
    { "string_value", "hello" },
    { "int_value", 42 },
    { "float_value", 3.14f },
    { "double_value", 3.14159 },
    { "bool_value", true },
    { "long_value", 9999999999L }
}
```

---

## Best Practices

### Event Naming

```csharp
// Good - lowercase with underscores, verb_noun format
MoonForgeAnalytics.TrackEvent("button_clicked");
MoonForgeAnalytics.TrackEvent("level_completed");
MoonForgeAnalytics.TrackEvent("item_purchased");

// Avoid - inconsistent naming
MoonForgeAnalytics.TrackEvent("ButtonClicked");  // PascalCase
MoonForgeAnalytics.TrackEvent("LEVEL COMPLETE"); // Spaces and caps
```

### Property Naming

```csharp
// Good - lowercase with underscores
{ "player_level", 25 }
{ "item_id", "sword_01" }
{ "total_score", 15000 }

// Avoid
{ "PlayerLevel", 25 }    // PascalCase
{ "item-id", "sword_01" } // Hyphens
```

### Don't Over-Track

```csharp
// Good - meaningful events
MoonForgeAnalytics.TrackEvent("enemy_killed", new Dictionary<string, object>
{
    { "enemy_type", "boss" }
});

// Avoid - too granular
MoonForgeAnalytics.TrackEvent("player_moved");  // Every frame? No!
MoonForgeAnalytics.TrackEvent("mouse_clicked"); // Too generic
```

### Batch Related Data

```csharp
// Good - single event with all data
MoonForgeAnalytics.TrackEvent("level_complete", new Dictionary<string, object>
{
    { "level", 5 },
    { "score", 15000 },
    { "time", 120 },
    { "stars", 3 }
});

// Avoid - multiple events for same action
MoonForgeAnalytics.TrackEvent("level_complete");
MoonForgeAnalytics.TrackEvent("score_recorded");
MoonForgeAnalytics.TrackEvent("stars_earned");
```

---

## Troubleshooting

### Events Not Appearing

1. Check `debugMode = true` in settings for console logs
2. Verify `enableInEditor = true` for editor testing
3. Confirm `enableAnalytics = true`
4. Check Game ID is correct

### Checking Logs

Enable debug mode to see:
```
[MoonForge Analytics] Initialized with distinctId: xxx, sessionId: xxx
[MoonForge Analytics] Tracked event: level_complete
[MoonForge Analytics] Event sent successfully
```

### Offline Events

Events are automatically queued when offline and sent when connectivity returns. Force send with:

```csharp
MoonForgeAnalytics.Flush();
```

---

## Support

- **Documentation**: https://docs.moonforge.co
- **Dashboard**: https://app.moonforge.co
- **GitHub Issues**: https://github.com/MoonForgeAI/unity-error-tracking/issues

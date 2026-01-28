# MoonForge Error Tracking for Unity

Production-ready error tracking for Unity games. Captures crashes, exceptions, and network errors with full context for debugging.

## ✨ One-Click Setup

MoonForge now features **automatic initialization** - no code required for basic setup!

### Quick Start (2 Steps)

1. **Install the package** via Package Manager
2. **Run Setup Wizard**: `MoonForge > Setup Error Tracking` menu
3. **Paste your Game ID** from the MoonForge dashboard
4. **Done!** MoonForge auto-initializes when your game starts

That's it! No scripts to write, no GameObjects to create.

---

## Features

- **Zero-Code Setup**: Auto-initializes on game start - just configure and go
- **Automatic Exception Capture**: Captures unhandled exceptions and Unity log errors
- **Native Crash Support**: iOS and Android native crash handling
- **Network Error Tracking**: Capture HTTP errors with request/response context
- **Breadcrumbs**: Track user actions leading up to errors
- **Offline Support**: Store errors when offline, send when connection restored
- **Adaptive Sampling**: Reduce volume while preserving important errors
- **Batching**: Efficient batched error submission
- **Scene Persistence**: Survives scene transitions automatically

---

## Installation

### Using Unity Package Manager (Recommended)

1. Open **Window > Package Manager**
2. Click **"+" > "Add package from git URL..."**
3. Enter: `https://github.com/moonforge/unity-error-tracking.git`

### Manual Installation

1. Download the latest release
2. Copy `MoonForgeErrorTracking/` to your project's `Packages/` folder

---

## Setup

### Method 1: Setup Wizard (Recommended)

1. Go to **MoonForge > Setup Error Tracking** in the Unity menu
2. Paste your **Game ID** from the MoonForge dashboard
3. Enable **"Enable Error Tracking"**
4. (Optional) Enable **"Enable in Editor"** to test in Play mode
5. Click **Test Connection** to verify

**MoonForge will automatically start tracking when your game runs!**

### Method 2: Project Settings

1. Go to **Edit > Project Settings > MoonForge**
2. Configure your settings there

### Method 3: Manual (Advanced)

For games with complex bootstrapping, you can still initialize manually:

```csharp
using MoonForge.ErrorTracking;

public class GameBootstrap : MonoBehaviour
{
    void Awake()
    {
        // Only needed if you disabled auto-init or need custom timing
        MoonForgeAutoInitializer.Initialize();
    }
}
```

---

## Usage

### Set User Identity (Recommended)

After user login, associate errors with the user:

```csharp
MoonForgeErrorTracker.Instance.SetUser("user-123", new Dictionary<string, string>
{
    { "subscription", "premium" },
    { "level", "42" }
});

// On logout
MoonForgeErrorTracker.Instance.ClearUser();
```

### Add Breadcrumbs

Track user actions leading up to errors:

```csharp
// Simple breadcrumb
MoonForgeErrorTracker.Instance.AddBreadcrumb("Clicked attack button", BreadcrumbType.User);

// With custom data
MoonForgeErrorTracker.Instance.AddBreadcrumb(
    new Breadcrumb(BreadcrumbType.User, "Purchased item")
        .WithData(new Dictionary<string, object>
        {
            { "item_id", "sword_01" },
            { "price", 100 }
        })
);
```

### Set Game State

Provide context about what the player was doing:

```csharp
MoonForgeErrorTracker.Instance.SetGameState(
    gameMode: "pvp",
    levelId: "arena_01"
);

// Add custom state data
MoonForgeErrorTracker.Instance.SetGameStateData("boss_phase", 2);
MoonForgeErrorTracker.Instance.SetGameStateData("player_health", 45);
```

### Capture Handled Exceptions

For try/catch blocks where you want to report but not crash:

```csharp
try
{
    // Risky operation
}
catch (Exception ex)
{
    MoonForgeErrorTracker.Instance.CaptureException(ex, ErrorLevel.Warning);
}
```

### Capture Network Errors

Track API failures:

```csharp
MoonForgeErrorTracker.Instance.CaptureNetworkError(
    url: "https://api.game.com/endpoint",
    method: "POST",
    statusCode: 500,
    errorMessage: "Internal Server Error",
    durationMs: 1234
);
```

### Send Custom Messages

Log important events:

```csharp
MoonForgeErrorTracker.Instance.CaptureMessage(
    "Player reached level cap",
    ErrorLevel.Info,
    new Dictionary<string, string> { { "player_level", "100" } }
);
```

---

## Configuration Reference

Access via **MoonForge > Setup Error Tracking** or **Edit > Project Settings > MoonForge**

### Basic Settings

| Option | Default | Description |
|--------|---------|-------------|
| `enabled` | true | Master switch for error tracking |
| `enableInEditor` | false | Track errors in Unity Editor |
| `debugMode` | false | Show debug logs in Console |

### Capture Settings

| Option | Default | Description |
|--------|---------|-------------|
| `captureUnhandledExceptions` | true | Auto-capture unhandled exceptions |
| `captureLogErrors` | true | Capture Debug.LogError calls |
| `captureNativeCrashes` | true | Capture iOS/Android native crashes |
| `trackSceneChanges` | true | Track scene changes as breadcrumbs |

### Performance Settings

| Option | Default | Description |
|--------|---------|-------------|
| `enableBatching` | true | Batch errors for efficiency |
| `batchSize` | 10 | Errors per batch |
| `maxBreadcrumbs` | 100 | Max breadcrumbs to retain |
| `enableOfflineStorage` | true | Store errors when offline |

### Privacy Settings

| Option | Default | Description |
|--------|---------|-------------|
| `scrubSensitiveData` | true | Auto-scrub passwords, tokens, etc. |

---

## How It Works

### Auto-Initialization

MoonForge uses Unity's `[RuntimeInitializeOnLoadMethod]` to automatically start tracking before your first scene loads. This means:

- ✅ **No manual GameObject setup needed**
- ✅ **Errors caught from the very first frame**
- ✅ **Survives scene transitions** (uses `DontDestroyOnLoad`)
- ✅ **Graceful shutdown** on app quit

### Session Management

Sessions are managed automatically:
- Session ID generated on game start
- Errors flushed on app pause
- Queued errors saved to disk on quit
- Stored errors sent on next launch

### Complex Scene Management

If your game uses a custom scene loading system:

```csharp
// MoonForge handles this automatically, but you can verify:
if (MoonForgeAutoInitializer.IsInitialized)
{
    Debug.Log("MoonForge is ready!");
}
```

---

## Troubleshooting

### Errors not appearing in dashboard

1. Check your **Game ID** is correct (UUID format)
2. Enable **"Enable in Editor"** for testing
3. Enable **"Debug Mode"** to see Console logs
4. Click **"Test Connection"** in the Setup Wizard

### "Settings not found" warning

Run **MoonForge > Setup Error Tracking** to create the settings file.

### Want to disable auto-initialization?

Set `enabled = false` in MoonForge Settings, then initialize manually when ready:

```csharp
MoonForgeAutoInitializer.Initialize();
```

---

## Requirements

- Unity 2021.3 or later (LTS recommended)
- iOS 12.0+
- Android API 21+

---

## Migration from Manual Setup

If you previously used the manual `ErrorTrackerConfig` approach:

1. Run **MoonForge > Setup Error Tracking**
2. Copy your Game ID to the new settings
3. Delete your old `ErrorTrackerConfig` asset
4. Remove your manual initialization script
5. MoonForge will now auto-initialize!

Your existing `SetUser()`, `AddBreadcrumb()`, etc. calls continue to work unchanged.

---

## License

MIT License - see LICENSE file for details.

# SuppressInvalidInviteErrors

A [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) mod for [Resonite](https://resonite.com/) that suppresses error messages from invalid invite requests on Headless server startup.

## Problem

When a Headless server restarts, it receives old invite requests from the SignalR server that were forwarded in previous sessions. These invite requests reference sessions that no longer exist, causing error spam in the logs and potentially sending error messages to users.

Related issue: https://github.com/Yellow-Dog-Man/Resonite-Issues/issues/4889

## Solution

This mod patches two methods in `InviteRequestManager` to silently ignore invalid/stale invite requests, preventing error spam on startup:

1. **`ForwardToAdmins`** - Silently ignores invite requests for worlds/sessions that no longer exist
2. **`ProcessGrantedInviteRequest`** - Silently ignores old granted invite requests that weren't forwarded in the current session

## Technical Details

### Error Locations in Decompiled Code

The errors occur in `FrooxEngine/InviteRequestManager.cs`:

#### 1. "Couldn't find hosted world for invite request" Error

**Location**: `InviteRequestManager.ForwardToAdmins()` method  
**Line**: ~144 in decompiled code  
**Code Path**:
```csharp
// Line 140: Check if world exists
CS$<>8__locals1.world = this.GetCorrespondingHostedWorld(CS$<>8__locals1.request);
if (CS$<>8__locals1.world == null)
{
    // Line 144: Error logged here
    defaultInterpolatedStringHandler.AppendLiteral("Couldn't find hosted world for invite request: ");
    defaultInterpolatedStringHandler.AppendFormatted<InviteRequest>(CS$<>8__locals1.request);
    UniLog.Warning(defaultInterpolatedStringHandler.ToStringAndClear(), false);
    // Line 147: Task.Run that may send error messages to users
    Task.Run<bool>(delegate { ... });
    return;
}
```

**Issue**: When an old invite request references a session that no longer exists, `GetCorrespondingHostedWorld()` returns `null`, triggering the warning log and a Task that may send error messages to users.

#### 2. "Received granted invite request that has not been forwarded in this session" Warning

**Location**: `InviteRequestManager.ProcessGrantedInviteRequest()` method  
**Line**: ~59 in decompiled code  
**Code Path**:
```csharp
// Line 57: Check if request was forwarded in this session
if (!this._forwardedInviteRequests.TryGetValue(CS$<>8__locals1.request.InviteRequestId, out CS$<>8__locals1.state))
{
    // Line 59: Warning logged here
    string text = "Received granted invite request that has not been forwarded in this session: ";
    InviteRequest request2 = CS$<>8__locals1.request;
    UniLog.Warning(text + ((request2 != null) ? request2.ToString() : null), false);
    return;
}
```

**Issue**: When the Headless restarts, the `_forwardedInviteRequests` dictionary is empty (not persisted), so old granted invite requests from SignalR are not found, causing warning spam.

### Patch Implementation

The mod uses Harmony `Prefix` patches to intercept these methods before they execute:

- **`ForwardToAdmins` patch**: Checks if the world exists using `GetCorrespondingHostedWorld()`. If `null`, returns `false` to skip the original method entirely.
- **`ProcessGrantedInviteRequest` patch**: Uses reflection to access the private `_forwardedInviteRequests` dictionary and checks if the request ID exists. If not found, returns `false` to skip the original method.

Both patches only skip execution for invalid/stale requests, allowing valid invite requests to be processed normally. The patches also check configuration settings to allow users to enable/disable each suppression feature independently.

## Configuration

The mod provides two configuration options that can be toggled in the ResoniteModLoader configuration:

- **`SuppressForwardToAdminsErrors`** (default: `true`) - Suppresses "Couldn't find hosted world for invite request" errors when invite requests reference non-existent sessions
- **`SuppressProcessGrantedInviteWarnings`** (default: `true`) - Suppresses "Received granted invite request that has not been forwarded in this session" warnings for old invite requests

Both options are enabled by default. You can disable them if you want to see the original error messages for debugging purposes.

## Screenshots
<!-- If your mod has visible effects in the game, attach some images or video of it in-use here! Otherwise remove this section -->

## Installation

1. Install [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader).
2. Place `SuppressInvalidInviteErrors.dll` into your `rml_mods` folder. This folder should be at `C:\Program Files (x86)\Steam\steamapps\common\Resonite\rml_mods` for a default install. You can create it if it's missing, or if you launch the game once with ResoniteModLoader installed it will create this folder for you.
3. Start the game. If you want to verify that the mod is working you can check your Resonite logs - the error messages should no longer appear.
4. (Optional) Configure the mod settings using ResoniteModLoader's configuration system if you want to adjust the suppression behavior.

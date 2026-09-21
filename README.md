# AppSpike SDK for Unity

**The free Firebase Remote Config alternative.**

> **Firebase Remote Config is going paid.** Google's usage-based pricing took effect on
> September 1, 2026. Existing free-plan (Spark) projects hit enforcement on
> **December 1, 2026**: past 100K daily fetches they get a 30-day grace period and are
> then throttled. Existing Blaze projects are billed automatically from
> **February 1, 2027**. The dates come from
> [Firebase's own pricing schedule](https://firebase.google.com/docs/remote-config/pricing).
> The [migration schedule below](#when-to-migrate) fits inside that window.


[AppSpike Remote Config](https://appspike.dev/remote-config) for Unity games. **Free**
remote configuration, feature flags, and staged rollouts evaluated **locally on-device**:
fetch/activate, in-app defaults, conditional targeting, percent rollouts, custom signals.
AppSpike Remote Config is also a drop-in replacement for Firebase Remote Config, with the
**same public API as Firebase Remote Config for Unity**, minus the per-request server
round-trips, the fetch limits, and the usage fees.

[Product](https://appspike.dev/remote-config) · [Docs](https://appspike.dev/docs/remote-config)

## Installation

Requires Unity **2021.3+**. Add both packages to `Packages/manifest.json` (Unity's package
manager cannot resolve git-to-git dependencies, so the core package must be listed
explicitly, first):

```json
{
  "dependencies": {
    "dev.appspike.sdk-core": "https://github.com/pydetech/appspike-sdk-unity-dist.git?path=dev.appspike.sdk-core#1.4.5",
    "dev.appspike.remote-config": "https://github.com/pydetech/appspike-sdk-unity-dist.git?path=dev.appspike.remote-config#1.4.5",
    "com.unity.nuget.newtonsoft-json": "3.2.1"
  }
}
```

Or via **Window → Package Manager → + → Add package from git URL…**, adding the
`dev.appspike.sdk-core` URL first, then `dev.appspike.remote-config`.

## Quick start

Never used Unity beyond opening it? Follow all six steps. They end with values from your
own config template on screen.

**1. Register your app.** Create your app at [console.appspike.dev](https://console.appspike.dev)
and copy its `pk_live_…` API key.

**2. Install the packages.** Add the two git URLs from [Installation](#installation) above,
`dev.appspike.sdk-core` first.

**3. Get a runnable script.** Pick one:

- **A. Open the bundled project.** Clone this repo, then in Unity Hub choose
  **Add → Add project from disk** and select [`SampleApp/`](SampleApp) (Unity 2021.3+). Its
  `Packages/manifest.json` already lists both packages, so step 2 is done for you. The script
  is [`SampleApp/Assets/RemoteConfigSample.cs`](SampleApp/Assets/RemoteConfigSample.cs).
- **B. Import the sample into your own project.** **Window → Package Manager**, select
  **AppSpike Remote Config** in the package list, open its **Samples** tab, and press
  **Import** next to **Remote Config Showcase**. Unity copies `RemoteConfigShowcase.cs` into
  `Assets/Samples/AppSpike Remote Config/<version>/Remote Config Showcase/`.

**4. Put the script in a scene.** Open a scene, or **File → New Scene**. Then
**GameObject → Create Empty**, select the new GameObject in the Hierarchy, press
**Add Component** in the Inspector, and pick **Remote Config Sample** (option A) or
**Remote Config Showcase** (option B).

**5. Set the API key.** With the GameObject selected, the component shows an **Api Key**
field pre-filled with `YOUR_API_KEY`. Paste your `pk_live_…` key over it. (The field is
declared in [`SampleApp/Assets/RemoteConfigSample.cs`](SampleApp/Assets/RemoteConfigSample.cs)
for option A or the imported `RemoteConfigShowcase.cs` for option B, so you can also set it
in code.)

**6. Press Play.** An on-screen panel appears with an **Initialize** button. Press
**Initialize**, then **Fetch & Activate**. If the Api Key field is still `YOUR_API_KEY`,
Initialize stops with an on-screen message telling you where to paste the key. Save the
scene to keep it.

### The same flow in your own code

```csharp
using System.Collections.Generic;
using AppSpike;
using AppSpike.RemoteConfig;
using UnityEngine;

public class Bootstrap : MonoBehaviour
{
    private async void Start()
    {
        var remoteConfig = AppSpikeRemoteConfig.DefaultInstance;

        // In-app defaults — served until a fetched config is activated
        await remoteConfig.SetDefaultsAsync(new Dictionary<string, object>
        {
            { "welcome_message", "Hello" },
            { "feature_enabled", false },
            { "max_retries", 3L },
        });

        // One API key, no config files
        var result = await AppSpikeSdk.InitializeAsync(
            "YOUR_API_KEY",
            new IAppSpikeModule[] { remoteConfig });
        if (result.IsError) return;

        // Fetch and activate (12h cache by default)
        try
        {
            await remoteConfig.FetchAndActivateAsync();
        }
        catch (RemoteConfigFetchException e)
        {
            // A failed fetch is an ordinary outcome — offline, or a backoff window.
            // Your defaults (or the last activated config) stay in place.
            Debug.LogWarning($"Fetch skipped: {e.Message}");
        }

        // Read values — REMOTE → DEFAULT → zero-value precedence
        Debug.Log(remoteConfig.GetValue("welcome_message").StringValue);
        Debug.Log(remoteConfig.GetBoolean("feature_enabled"));
        Debug.Log(remoteConfig.GetLong("max_retries"));
    }
}
```

### Targeting with custom signals

```csharp
await remoteConfig.SetCustomSignalsAsync(new CustomSignals.Builder()
    .Put("tier", "gold")
    .Put("session_count", 12L)
    .Build());
// Signals apply at the next fetch + activate; force one immediately:
await remoteConfig.FetchAsync(TimeSpan.Zero);
await remoteConfig.ActivateAsync();
```

### Settings, info, updates

```csharp
await remoteConfig.SetConfigSettingsAsync(new ConfigSettings
{
    MinimumFetchIntervalInMilliseconds = 3_600_000,   // 1h
    FetchTimeoutInMilliseconds = 30_000,
});

remoteConfig.OnConfigUpdateListener += (sender, args) =>
    Debug.Log("changed: " + string.Join(", ", args.UpdatedKeys));

var info = remoteConfig.Info;   // FetchTime, LastFetchStatus, LastFetchFailureReason
```

## Why AppSpike Remote Config?

- **A free, direct Firebase Remote Config replacement.** The lifecycle and the accessors
  are the same, and nothing meters your fetches. Firebase Remote Config bills $0.06 per
  10K fetches past 100K/day. AppSpike Remote Config is free at any scale.
- **The API is drop-in familiar.** `DefaultInstance`, `FetchAndActivateAsync()`,
  `GetValue()`, `ConfigSettings`, `ConfigInfo`, `OnConfigUpdateListener`. If you have used
  Firebase Remote Config in Unity, you already know this SDK.
- **On-device evaluation.** The published config template is served from a CDN, and
  conditions (country, language, app version, percent rollout, custom signals, time
  windows) are evaluated locally. Custom signals never leave the device.
- **Offline-first.** Activated values and in-app defaults are persisted. A device that
  never comes back online keeps serving the last activated config.
- **Battle tested.** It already serves millions of users in PokeRaid and PokeTrade.

## Migrating from Firebase Remote Config

### Feature comparison

| Feature | Firebase Remote Config | AppSpike Remote Config |
|---|---|---|
| Firebase-compatible Unity API | ✅ | ✅ |
| In-app defaults, fetch/activate lifecycle | ✅ | ✅ |
| Conditional targeting (country, language, version, platform…) | ✅ | ✅ |
| Percent rollouts | ✅ | ✅ Groups stay stable across fetches |
| Custom signals | Uploaded with every fetch | Never leave the device |
| Price at scale | 100K fetches/day free, then $0.06 per 10K | Free, no fetch metering |
| Config import | ❌ No import path from other providers | ✅ One-click import from Firebase |
| Version history & rollback | ✅ | ✅ |
| Real-time config updates | ✅ Real-time Remote Config | ✅ (push setup required) |
| A/B testing | ✅ Firebase A/B Testing | ✅ Via percentage conditions |
| Analytics audience targeting | ✅ Google Analytics audiences | ❌ Use custom signals instead |
| Works alongside other AppSpike modules (KMP, iOS SDKs share the backend) | n/a | ✅ |

### When to migrate

The two SDKs run side by side in the same app, so nothing forces a single cutover day. Two dates bound the plan: existing Spark projects face throttling enforcement from December 1, 2026, and existing Blaze projects are billed from February 1, 2027.

1. **Today.** Register your app at [console.appspike.dev](https://console.appspike.dev), import your Firebase Remote Config template, and publish. Nothing in your app changes yet.
2. **Next development cycle.** Make the code changes below in a branch. Debug builds can run both SDKs together and compare values.
3. **Before the cutover release.** Finish any in-flight percentage rollouts and experiments on Firebase Remote Config. Rollout groups are re-randomized on AppSpike, so a mid-rollout user can change groups. If your template changed since step 1, import it again.
4. **The cutover release.** Ship the swap as a normal app release. Keep your in-app defaults registered. They cover every device that has not fetched yet.
5. **After the rollout.** Once the release has reached most of your fleet, remove the Firebase Remote Config package from the project.

### Step-by-step

**1. Move your config template.** In the [AppSpike console](https://console.appspike.dev),
register your app, import your Firebase Remote Config template (Firebase export upload is
supported: parameters, conditions, percent rollouts), review it, and publish. Your template
exists on the AppSpike side before any project change.

**2. Replace the packages.** In `Packages/manifest.json`:

```jsonc
// Remove
"com.google.firebase.remote-config": "…",

// Add
"dev.appspike.sdk-core": "https://github.com/pydetech/appspike-sdk-unity-dist.git?path=dev.appspike.sdk-core#1.4.5",
"dev.appspike.remote-config": "https://github.com/pydetech/appspike-sdk-unity-dist.git?path=dev.appspike.remote-config#1.4.5",
"com.unity.nuget.newtonsoft-json": "3.2.1"
```

Drop the External Dependency Manager entries that served Firebase Remote Config.

**3. Update the using directives.** These cover every step below.

```csharp
// Before
using Firebase.RemoteConfig;

// After
using AppSpike;
using AppSpike.RemoteConfig;
```

`AppSpikeSdk`, `IAppSpikeModule` and `InitResult` come from `AppSpike`. Everything else
(`AppSpikeRemoteConfig`, `ConfigSettings`, `ConfigValue`, `CustomSignals`,
`RemoteConfigFetchException`) comes from `AppSpike.RemoteConfig`. `using Firebase.RemoteConfig;`
goes.

**4. Add initialization.** `AppSpikeSdk.InitializeAsync` is added once at startup. AppSpike
takes one API key and no config files.

```csharp
// Before
var remoteConfig = FirebaseRemoteConfig.DefaultInstance;

// After
var remoteConfig = AppSpikeRemoteConfig.DefaultInstance;
var result = await AppSpikeSdk.InitializeAsync(
    "YOUR_API_KEY",
    new IAppSpikeModule[] { remoteConfig });
if (result.IsError) return;
```

**5. Defaults.** The Firebase call compiles verbatim.

```csharp
// Before and after — the same method and the same dictionary
await remoteConfig.SetDefaultsAsync(new Dictionary<string, object>
{
    { "welcome_message", "Hello" },
    { "feature_enabled", false },
    { "max_retries", 3L },
});
```

**6. Settings.** The Firebase struct compiles verbatim.

```csharp
// Before and after — the same struct, the same field names
await remoteConfig.SetConfigSettingsAsync(new ConfigSettings
{
    MinimumFetchIntervalInMilliseconds = 3_600_000,   // 1h
    FetchTimeoutInMilliseconds = 30_000,
});
```

**7. Fetch / activate.** Same names, same `Task`s.

```csharp
// Before and after — the same
await remoteConfig.FetchAsync();
await remoteConfig.FetchAsync(TimeSpan.Zero);
var changed = await remoteConfig.ActivateAsync();
await remoteConfig.FetchAndActivateAsync();
```

The one real edit is the failure path. Firebase Unity reports a failed fetch through a faulted
`Task` plus `Info.LastFetchStatus` / `Info.LastFetchFailureReason`. AppSpike throws a typed
`RemoteConfigFetchException` (and `RemoteConfigThrottledException` while the consecutive-failure
backoff window is open). A failed fetch is an ordinary outcome (offline, or a backoff window),
so catch it and keep serving:

```csharp
try
{
    await remoteConfig.FetchAndActivateAsync();
}
catch (RemoteConfigFetchException e)
{
    Debug.LogWarning($"Fetch skipped: {e.Message}");
}
```

**8. Read values.** Replace the receiver and keep the calls.

```csharp
// Before and after — the same
remoteConfig.GetValue("welcome_message").StringValue;
remoteConfig.GetValue("feature_enabled").BooleanValue;
remoteConfig.GetValue("max_retries").LongValue;
remoteConfig.GetValue("price_multiplier").DoubleValue;
remoteConfig.GetValue("key").Source;
remoteConfig.Keys;
remoteConfig.AllValues;
remoteConfig.GetKeysByPrefix("feature_");
remoteConfig.Info;
```

`ConfigValue`'s accessors never throw here: an unconvertible value reads as the zero value
(`""`, `false`, `0`, `0.0`) instead of raising `FormatException`. AppSpike also adds
`GetString` / `GetBoolean` / `GetLong` / `GetDouble` (Firebase Android's naming), which fall
through to your in-app default when the remote value doesn't convert for that type.

**9. Custom signals.** The Firebase call compiles verbatim.

```csharp
// Before and after — the same method and the same dictionary
await remoteConfig.SetCustomSignalsAsync(new Dictionary<string, object>
{
    { "tier", "gold" },
    { "session_count", 12L },
});

// Or the builder (AppSpike addition, Firebase Android's shape)
await remoteConfig.SetCustomSignalsAsync(new CustomSignals.Builder()
    .Put("tier", "gold")
    .Put("session_count", 12L)
    .Build());
```

(`SetCustomSignalsAsync` reached the Firebase Unity SDK in 13.16.0. On an older Firebase
version there is nothing to port, because this is new capability.)

Firebase uploads signals with every fetch and evaluates them on its servers. AppSpike keeps
them on the device and evaluates locally. They apply at the next fetch + activate. To force
one, call `FetchAsync(TimeSpan.Zero)` followed by `ActivateAsync()`.

**10. Update listeners.** The Firebase event compiles verbatim.

```csharp
// Before and after — the same event and the same EventArgs
remoteConfig.OnConfigUpdateListener += (sender, args) =>
    Debug.Log("changed: " + string.Join(", ", args.UpdatedKeys));
```

### API mapping reference

The public API is intentionally identical, so a migration is mostly renames:

| | Firebase | AppSpike |
|---|---|---|
| Package | `com.google.firebase.remote-config` (+ app, .tgz/UPM) | the two git URLs above |
| Setup | `google-services.json` / `GoogleService-Info.plist` + `FirebaseApp.CheckAndFixDependenciesAsync()` | `AppSpikeSdk.InitializeAsync(apiKey, modules)`, one API key and no config files |
| Instance | `FirebaseRemoteConfig.DefaultInstance` | `AppSpikeRemoteConfig.DefaultInstance` |
| Namespace | `Firebase.RemoteConfig` | `AppSpike.RemoteConfig` |
| Defaults | `SetDefaultsAsync(dict)` | identical |
| Fetch | `FetchAsync()` / `FetchAndActivateAsync()` / `ActivateAsync()` | identical |
| Read | `GetValue(key).StringValue` etc. | identical |
| Settings | `ConfigSettings` struct | identical |
| Info | `Info` (`ConfigInfo`) | identical |
| Signals | `SetCustomSignalsAsync(dict)` (Firebase Unity 13.16.0+) | identical, plus a `CustomSignals.Builder` |
| Updates | `OnConfigUpdateListener` event | identical |
| Errors | `FirebaseException` | `RemoteConfigFetchException` / `RemoteConfigThrottledException` |

Behavioral differences worth knowing:

1. **`ConfigValue` accessors never throw.** An unconvertible value reads as the zero value
   (`""`, `false`, `0`, `0.0`) instead of raising `FormatException`.
2. **Typed getters** `GetString` / `GetBoolean` / `GetLong` / `GetDouble` (Firebase Android
   naming) are included. A remote value that does not convert for the requested type falls
   through to your in-app default.
3. **Custom signals stay on the device.** Evaluation is local, and nothing is uploaded.

### AI migration prompt

Paste this into your AI coding assistant to migrate an existing project:

```
Migrate this Unity project from Firebase Remote Config to AppSpike Remote Config.

1. In Packages/manifest.json remove com.google.firebase.remote-config and add:
   "dev.appspike.sdk-core": "https://github.com/pydetech/appspike-sdk-unity-dist.git?path=dev.appspike.sdk-core#1.4.5",
   "dev.appspike.remote-config": "https://github.com/pydetech/appspike-sdk-unity-dist.git?path=dev.appspike.remote-config#1.4.5",
   "com.unity.nuget.newtonsoft-json": "3.2.1"
2. Replace `using Firebase.RemoteConfig;` with `using AppSpike.RemoteConfig;` and add
   `using AppSpike;` where initialization happens.
3. Add, before any Remote Config usage:
   await AppSpikeSdk.InitializeAsync("<API_KEY>",
       new IAppSpikeModule[] { AppSpikeRemoteConfig.DefaultInstance });
4. Replace FirebaseRemoteConfig.DefaultInstance with AppSpikeRemoteConfig.DefaultInstance.
   FetchAsync, ActivateAsync, FetchAndActivateAsync, SetDefaultsAsync,
   SetConfigSettingsAsync, SetCustomSignalsAsync, GetValue, Keys, AllValues,
   GetKeysByPrefix, Info, and the OnConfigUpdateListener event keep their exact names and
   signatures, as do the ConfigSettings fields MinimumFetchIntervalInMilliseconds and
   FetchTimeoutInMilliseconds.
5. Replace catch blocks on FirebaseException around fetch calls with
   RemoteConfigFetchException (and RemoteConfigThrottledException for throttling).
6. Drop the External Dependency Manager entries that served Firebase Remote Config.
Do not change any parameter keys or default values.
```

## Requirements

- Unity 2021.3 LTS or newer (2022.3 recommended)
- Android API 21+ / iOS 12+ / WebGL
- `com.unity.nuget.newtonsoft-json` 3.x

## Related SDKs

- Kotlin Multiplatform / Android: [appspike-sdk-kmp-dist](https://github.com/pydetech/appspike-sdk-kmp-dist)
- Native iOS (Swift): [appspike-sdk-ios-dist](https://github.com/pydetech/appspike-sdk-ios-dist)

## License

Proprietary. See [LICENSE](LICENSE). Copyright © Pyde Technologies LTD.

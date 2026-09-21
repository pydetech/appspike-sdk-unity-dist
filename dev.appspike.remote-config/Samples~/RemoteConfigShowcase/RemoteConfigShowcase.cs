using System;
using System.Collections.Generic;
using System.Linq;
using AppSpike;
using AppSpike.RemoteConfig;
using UnityEngine;

namespace AppSpike.Samples
{
    /// <summary>
    /// End-to-end Remote Config usage with an on-screen IMGUI panel: setup → fetch
    /// controls → fetch settings → custom signals → typed getters → fetch info, plus a
    /// second screen listing every key/value with its source and a prefix filter. Attach
    /// to any GameObject in a scene and paste your API key into the Api Key field in the
    /// Inspector.
    /// </summary>
    public class RemoteConfigShowcase : MonoBehaviour
    {
        /// <summary>
        /// The unedited value of <see cref="apiKey"/>. Initialize refuses to run while the
        /// field still holds it.
        /// </summary>
        private const string PlaceholderApiKey = "YOUR_API_KEY";

        // The one place the API key lives. It is an edit-time Inspector field, not a
        // runtime text box: a shipping game passes its key at build time, so the sample
        // models that.
        [SerializeField]
        private string apiKey = PlaceholderApiKey;

        private AppSpikeRemoteConfig _remoteConfig;
        private bool _initialized;
        private bool _busy;
        private bool _premiumSignalSet;
        private string _status = "Set the Api Key field in the Inspector, then press Initialize.";
        private string _signalKey = "tier";
        private string _signalValue = "silver";
        private string _prefixFilter = "";
        private bool _showAllValues;
        private Vector2 _mainScroll;
        private Vector2 _allValuesScroll;
        private GUIStyle _labelStyle;
        private GUIStyle _headerStyle;

        private void Awake()
        {
            _remoteConfig = AppSpikeRemoteConfig.DefaultInstance;
        }

        private void OnGUI()
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, wordWrap = true };
                _headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold };
                GUI.skin.button.fontSize = 28;
                GUI.skin.textField.fontSize = 28;
            }

            // Keep every control reachable while the mobile keyboard is open: the panel is a
            // scroll view whose bottom is padded by the keyboard's on-screen height.
            var keyboardHeight = TouchScreenKeyboard.visible ? TouchScreenKeyboard.area.height : 0f;
            var panel = new Rect(20, 20, Screen.width - 40, Screen.height - 40 - keyboardHeight);

            GUILayout.BeginArea(panel);
            if (_showAllValues)
            {
                DrawAllValuesScreen();
            }
            else
            {
                DrawMainScreen();
            }
            GUILayout.EndArea();
        }

        private void DrawMainScreen()
        {
            _mainScroll = GUILayout.BeginScrollView(_mainScroll);
            GUILayout.Label("AppSpike Remote Config Showcase", _headerStyle);

            // Setup: the key is set at edit time, so the panel only reports whether it is set.
            GUILayout.Label(
                apiKey == PlaceholderApiKey || apiKey.Length == 0
                    ? "API key: not set — paste it into the Api Key field of this component in the Inspector."
                    : "API key: set in the Inspector.",
                _labelStyle);

            GUI.enabled = !_busy && !_initialized;
            if (GUILayout.Button("Initialize"))
            {
                RunGuarded(InitializeAsync);
            }

            GUI.enabled = !_busy && _initialized;
            if (GUILayout.Button("Fetch & Activate"))
            {
                RunGuarded(() => FetchAndActivateAsync(bypassCache: false));
            }
            if (GUILayout.Button("Bypass Cache & Activate"))
            {
                RunGuarded(() => FetchAndActivateAsync(bypassCache: true));
            }
            GUI.enabled = !_busy;
            if (GUILayout.Button("Reset"))
            {
                RunGuarded(ResetAsync);
            }
            GUI.enabled = !_busy && _initialized;
            if (GUILayout.Button("Show All Values"))
            {
                _showAllValues = true;
            }
            GUI.enabled = true;

            GUILayout.Space(10);
            GUILayout.Label(_status, _labelStyle);

            if (_initialized)
            {
                DrawFetchSettingsSection();
                DrawCustomSignalsSection();

                GUILayout.Space(10);
                GUILayout.Label("Typed getters", _headerStyle);
                GUILayout.Label(
                    "GetString(welcome_message) = " + _remoteConfig.GetString("welcome_message"),
                    _labelStyle);
                GUILayout.Label(
                    "GetBoolean(feature_enabled) = " + _remoteConfig.GetBoolean("feature_enabled"),
                    _labelStyle);
                GUILayout.Label("GetLong(max_retries) = " + _remoteConfig.GetLong("max_retries"), _labelStyle);
                GUILayout.Label(
                    "GetDouble(price_multiplier) = " + _remoteConfig.GetDouble("price_multiplier"),
                    _labelStyle);

                // ConfigValue exposes the raw value with per-accessor coercion and its origin.
                var retries = _remoteConfig.GetValue("max_retries");
                GUILayout.Label(
                    "GetValue(max_retries).LongValue = " + retries.LongValue +
                    "  (" + SourceLabel(retries.Source) + ")",
                    _labelStyle);

                // byte[] defaults round-trip byte-exact through ByteArrayValue.
                var payload = _remoteConfig.GetValue("binary_payload").ByteArrayValue.ToArray();
                GUILayout.Label(
                    "GetValue(binary_payload).ByteArrayValue = " + BitConverter.ToString(payload),
                    _labelStyle);

                GUILayout.Space(10);
                GUILayout.Label("Fetch info", _headerStyle);
                var info = _remoteConfig.Info;
                GUILayout.Label(
                    "last fetch: " + info.LastFetchStatus + " at " + info.FetchTime + " UTC",
                    _labelStyle);
                if (info.LastFetchStatus == LastFetchStatus.Failure)
                {
                    GUILayout.Label("failure reason: " + info.LastFetchFailureReason, _labelStyle);
                }
                if (info.LastFetchFailureReason == FetchFailureReason.Throttled)
                {
                    GUILayout.Label("throttled until: " + info.ThrottledEndTime + " UTC", _labelStyle);
                }
                GUILayout.Label("AppSpikeSdk.IsInitialized = " + AppSpikeSdk.IsInitialized, _labelStyle);
            }
            GUILayout.EndScrollView();
        }

        private void DrawFetchSettingsSection()
        {
            GUILayout.Space(10);
            GUILayout.Label("Fetch settings", _headerStyle);

            // ConfigSettings reads back whatever SetConfigSettingsAsync last applied; the
            // pair is persisted, so it survives a restart.
            var settings = _remoteConfig.ConfigSettings;
            GUILayout.Label(
                "min fetch interval " + settings.MinimumFetchIntervalInMilliseconds / 1000 +
                "s, fetch timeout " + settings.FetchTimeoutInMilliseconds / 1000 + "s",
                _labelStyle);

            GUI.enabled = !_busy;

            // A zero minimum fetch interval makes every FetchAsync hit the network instead of
            // serving the cached template — the usual development setting, and the way a new
            // custom signal is applied without waiting out the interval.
            if (GUILayout.Button("Development Settings (0s interval, 10s timeout)"))
            {
                RunGuarded(() => ApplyConfigSettingsAsync(new ConfigSettings
                {
                    MinimumFetchIntervalInMilliseconds = 0UL,
                    FetchTimeoutInMilliseconds = 10_000UL,
                }));
            }

            // ConfigSettings.Defaults is the production pair: a 12h interval and a 60s timeout.
            if (GUILayout.Button("Restore Default Settings (12h interval, 60s timeout)"))
            {
                RunGuarded(() => ApplyConfigSettingsAsync(ConfigSettings.Defaults));
            }

            GUI.enabled = true;
        }

        private void DrawCustomSignalsSection()
        {
            GUILayout.Space(10);
            GUILayout.Label("Custom signals", _headerStyle);

            // Free-form entry: any key and any string value, so every custom_signal
            // condition in the published template can be exercised from the running sample.
            GUILayout.Label("Signal key", _labelStyle);
            _signalKey = GUILayout.TextField(_signalKey);
            GUILayout.Label("Signal value", _labelStyle);
            _signalValue = GUILayout.TextField(_signalValue);

            GUI.enabled = !_busy && _signalKey.Length > 0;
            if (GUILayout.Button("Set Signal"))
            {
                RunGuarded(() => ApplySignalAsync(_signalKey, _signalValue));
            }
            if (GUILayout.Button("Remove Signal"))
            {
                RunGuarded(() => ApplySignalAsync(_signalKey, null));
            }

            // Convenience preset on top of the free-form entry above.
            GUI.enabled = !_busy;
            if (GUILayout.Button(_premiumSignalSet ? "Clear \"premium\" Signal" : "Set \"premium\" Signal"))
            {
                RunGuarded(TogglePremiumSignalAsync);
            }

            GUI.enabled = true;
        }

        private void DrawAllValuesScreen()
        {
            GUILayout.Label("All Key/Values", _headerStyle);
            if (GUILayout.Button("Back"))
            {
                _showAllValues = false;
            }

            // GetKeysByPrefix narrows the key set; an empty filter lists everything via Keys.
            GUILayout.Label("Filter by key prefix", _labelStyle);
            _prefixFilter = GUILayout.TextField(_prefixFilter);

            _allValuesScroll = GUILayout.BeginScrollView(_allValuesScroll);
            var keys = (string.IsNullOrEmpty(_prefixFilter)
                    ? _remoteConfig.Keys
                    : _remoteConfig.GetKeysByPrefix(_prefixFilter))
                .OrderBy(key => key)
                .ToList();
            if (keys.Count == 0)
            {
                GUILayout.Label("No values yet — set defaults or fetch and activate first.", _labelStyle);
            }
            foreach (var key in keys)
            {
                var value = _remoteConfig.GetValue(key);
                GUILayout.Label(
                    key + " = " + value.StringValue + "  (" + SourceLabel(value.Source) + ")",
                    _labelStyle);
            }
            GUILayout.EndScrollView();
        }

        /// <summary>
        /// ValueSource is Firebase-shaped (StaticValue / RemoteValue / DefaultValue); every
        /// AppSpike sample shows the same lowercase label so the platforms read alike.
        /// </summary>
        private static string SourceLabel(ValueSource source)
        {
            switch (source)
            {
                case ValueSource.RemoteValue:
                    return "remote";
                case ValueSource.DefaultValue:
                    return "default";
                default:
                    return "static";
            }
        }

        private async void RunGuarded(Func<System.Threading.Tasks.Task> action)
        {
            _busy = true;
            try
            {
                await action();
            }
            catch (RemoteConfigFetchException fetchException)
            {
                _status = "Fetch failed, serving cached/default values: " + fetchException.Message;
            }
            catch (Exception exception)
            {
                _status = "Error: " + exception.Message;
            }
            finally
            {
                _busy = false;
            }
        }

        private async System.Threading.Tasks.Task InitializeAsync()
        {
            if (apiKey.Length == 0 || apiKey == PlaceholderApiKey)
            {
                _status = "No API key: the Api Key field is still \"" + PlaceholderApiKey +
                    "\". Select this GameObject, paste your pk_live_… key into the Api Key " +
                    "field in the Inspector (serialized by RemoteConfigShowcase.cs), then " +
                    "press Play again.";
                return;
            }

            _status = "Initializing…";

            // Fetch settings: keep the production 12h minimum fetch interval, tighten the
            // fetch timeout. Applied before initialize; persisted across launches.
            await _remoteConfig.SetConfigSettingsAsync(new ConfigSettings
            {
                MinimumFetchIntervalInMilliseconds = ConfigSettings.DefaultMinimumFetchIntervalInMilliseconds,
                FetchTimeoutInMilliseconds = 30_000UL,
            });

            await SetSampleDefaultsAsync();

            // Targeting signals for custom_signal conditions (evaluated on-device, never
            // transmitted). Builder overload; see the Custom signals section for the
            // dictionary overload and removal.
            await _remoteConfig.SetCustomSignalsAsync(new CustomSignals.Builder()
                .Put("tier", "gold")
                .Put("session_count", 12L)
                .Build());

            var initResult = await AppSpikeSdk.InitializeAsync(
                apiKey,
                new IAppSpikeModule[] { _remoteConfig });
            if (initResult.IsError)
            {
                _status = "AppSpike initialize failed: " + ((InitResult.Error)initResult).Message;
                return;
            }

            // Resolves once the module has its session and cached values are loaded,
            // handing back the same metadata as the Info property.
            var info = await _remoteConfig.EnsureInitializedAsync();

            _initialized = true;
            _status = "Initialized (last fetch: " + info.LastFetchStatus +
                "). Fetch to load the published template.";
        }

        /// <summary>
        /// The sample's in-app defaults: served until a fetched template is activated, and
        /// as fallback for keys the template does not define.
        /// </summary>
        private System.Threading.Tasks.Task SetSampleDefaultsAsync()
        {
            return _remoteConfig.SetDefaultsAsync(new Dictionary<string, object>
            {
                { "welcome_message", "Hello from defaults" },
                { "feature_enabled", false },
                { "max_retries", 3L },
                { "price_multiplier", 1.0 },

                // Platform extra: byte[] is a Unity/.NET-only default type and round-trips
                // byte-exact through ConfigValue.ByteArrayValue.
                { "binary_payload", new byte[] { 0x41, 0x70, 0x70 } },
            });
        }

        private async System.Threading.Tasks.Task ResetAsync()
        {
            // Reset clears fetched and activated values, defaults, custom signals and
            // settings; the sample re-registers its defaults straight away, so the
            // all-values screen shows the defaults set with (default) sources and no
            // remote rows.
            _remoteConfig.Reset();
            await SetSampleDefaultsAsync();
            _premiumSignalSet = false;
            _status = "Remote Config state cleared; sample defaults re-applied.";
        }

        private async System.Threading.Tasks.Task ApplyConfigSettingsAsync(ConfigSettings settings)
        {
            // Settings can be changed at any time, not just before initialize; the new pair
            // takes effect on the next fetch.
            await _remoteConfig.SetConfigSettingsAsync(settings);
            _status = "Fetch settings applied: min interval " +
                settings.MinimumFetchIntervalInMilliseconds + "ms, timeout " +
                settings.FetchTimeoutInMilliseconds + "ms.";
        }

        private async System.Threading.Tasks.Task ApplySignalAsync(string key, string value)
        {
            // Dictionary overload of SetCustomSignalsAsync; a null value removes the key.
            await _remoteConfig.SetCustomSignalsAsync(new Dictionary<string, object>
            {
                { key, value },
            });

            // A signal only changes the evaluated template at the next non-throttled
            // fetch, so bypass the minimum fetch interval and activate right away.
            await _remoteConfig.FetchAsync(TimeSpan.Zero);
            var changed = await _remoteConfig.ActivateAsync();
            _status = (value == null
                    ? "Signal " + key + " removed"
                    : "Signal " + key + "=" + value + " applied") +
                "; fetched and activated, values changed: " + changed;
        }

        private async System.Threading.Tasks.Task TogglePremiumSignalAsync()
        {
            await ApplySignalAsync("premium", _premiumSignalSet ? null : "yes");
            _premiumSignalSet = !_premiumSignalSet;
        }

        private async System.Threading.Tasks.Task FetchAndActivateAsync(bool bypassCache)
        {
            if (bypassCache)
            {
                // TimeSpan.Zero ignores the minimum fetch interval, so the fetch always
                // hits the server instead of serving the cached template.
                _status = "Fetching (cache bypassed)…";
                await _remoteConfig.FetchAsync(TimeSpan.Zero);
                var changed = await _remoteConfig.ActivateAsync();
                _status = "Bypass fetch finished, values changed: " + changed;
            }
            else
            {
                _status = "Fetching…";
                var changed = await _remoteConfig.FetchAndActivateAsync();
                _status = "FetchAndActivate finished, values changed: " + changed;
            }
        }
    }
}

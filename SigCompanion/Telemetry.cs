using Mixpanel;
using SigCompanion.Models;
using SigCompanion.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SigCompanion
{
    public static class Telemetry
    {
        private static ConcurrentDictionary<string, DateTime> exceptionLastTrackedTimes = new ConcurrentDictionary<string, DateTime>();
        private static Func<AppSettings>? _settingsProvider;
        private static DateTime _lastDayEventTime = DateTime.MinValue;
        private static System.Threading.Timer? _newDayTimer;
        private static bool? _shouldLogExceptionCache;
        private static DateTime _shouldLogExceptionCacheTime = DateTime.MinValue;
        public static void TrackUser()
        {
            try
            {
                var mc = new MixpanelClient(Keys.MixpanelToken);
                mc.PeopleSetAsync(new
                {
                    DistinctId = TelemetryIdentity.UserId,
                    Locale = CultureInfo.CurrentCulture.Name,
            });
            }
            catch
            {
                // Ignore errors
            }
        }

        public static void TrackException(Exception ex)
        {
            string exceptionKey = ex.Message + ":" + ex.GetType().ToString();
            if (exceptionLastTrackedTimes.TryGetValue(exceptionKey, out var lastTracked) && (DateTime.UtcNow - lastTracked).TotalSeconds < 300)
            {
                // Don't track the same exception more than once every 300 seconds
                return;
            }
            exceptionLastTrackedTimes[exceptionKey] = DateTime.UtcNow;

            Task.Run(async () =>
            {
                try
                {
                    // Check if telemetry is disabled for this version
                    if (await ShouldLogException())
                    {
                        await TrackEventToMixpanel("Exception", BuildExceptionMetadata(ex, true));
                    }
                }
                catch
                {
                    // Ignore errors
                }
            });
        }

        private static async Task<bool> ShouldLogException()
        {
            if (_shouldLogExceptionCache.HasValue && (DateTime.UtcNow - _shouldLogExceptionCacheTime).TotalHours < 8)
            {
                return _shouldLogExceptionCache.Value;
            }

            try
            {
                using (var client = new HttpClient())
                {
                    var json = await client.GetStringAsync("https://tomsapps2.blob.core.windows.net/sig-companion/disable-exception-telemetry-version.json");
                    var versions = JsonSerializer.Deserialize<string[]>(json);

                    if (versions.Contains(AppVersion) || versions.Contains("*"))
                    {
                        _shouldLogExceptionCache = false;
                    }
                    else
                    {
                        _shouldLogExceptionCache = true;
                    }
                    _shouldLogExceptionCacheTime = DateTime.UtcNow;
                    return _shouldLogExceptionCache.Value;
                }
            }
            catch
            {
                return true;
            }
        }

        public static void TrackUnhandledException(Exception ex)
        {
            TrackEventToMixpanelSafe("Exception", BuildExceptionMetadata(ex, false));
        }

        private static Dictionary<string, string> BuildExceptionMetadata(Exception ex, bool handled)
        {
            // Split stack trace into 256 character segments as this is the limit for Mixpanel strings
            var fullStackTrace = ex.StackTrace ?? string.Empty;
            var stackTrace1 = fullStackTrace.Length > 256 ? fullStackTrace.Substring(0, 256) : fullStackTrace;
            var stackTrace2 = fullStackTrace.Length > 256 ? fullStackTrace.Substring(256) : null;
            return new Dictionary<string, string>()
            {
                { "Type", ex.GetType().ToString() },
                { "Message", ex.Message },
                { "StackTrace1", stackTrace1 },
                { "StackTrace2", stackTrace2 },
                { "IsHandled", handled.ToString() }
            };
        }

        public static void TrackEvent(string eventName, IDictionary<string, string> metadata)
        {
            TrackEventToMixpanelSafe(eventName, metadata);
        }

        public static void TrackEvent(string eventName)
        {
            TrackEventToMixpanelSafe(eventName, null);
        }

        public static void StartSession(Func<AppSettings> settingsProvider)
        {
            _settingsProvider = settingsProvider;
            _lastDayEventTime = DateTime.UtcNow;
            TrackUser();
            TrackEventToMixpanelSafe("SessionStart", BuildSettingsMetadata(settingsProvider()));
            _newDayTimer ??= new System.Threading.Timer(_ => CheckNewDay(), null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
        }

        private static void CheckNewDay()
        {
            try
            {
                if (_settingsProvider is null || DateTime.UtcNow - _lastDayEventTime < TimeSpan.FromHours(24))
                {
                    return;
                }
                _lastDayEventTime = DateTime.UtcNow;
                TrackEventToMixpanelSafe("NewDay", BuildSettingsMetadata(_settingsProvider()));
            }
            catch
            {
                // Ignore errors
            }
        }

        private static Dictionary<string, string> BuildSettingsMetadata(AppSettings settings)
        {
            return new Dictionary<string, string>()
            {
                { "UseModbus", settings.UseModbus.ToString() },
                { "ShowClock", settings.ShowClock.ToString() },
                { "CyclePagesWhenIdle", settings.CyclePagesWhenIdle.ToString() },
                { "ChartData", settings.ChartData },
                { "Region", settings.Region ?? string.Empty },
                { "SavePassword", settings.SavePassword.ToString() }
            };
        }

        private static void TrackEventToMixpanelSafe(string eventName, IDictionary<string, string> metadata)
        {
            try
            {
                Task.Run(async () => await TrackEventToMixpanel(eventName, metadata));
            }
            catch
            {

            }
        }

        private static async Task TrackEventToMixpanel(string eventName, IDictionary<string, string> metadata)
        {
            var mc = new MixpanelClient(Keys.MixpanelToken);
            dynamic expando = new ExpandoObject();
            IDictionary<string, object> dict = expando;

            dict["DistinctId"] = TelemetryIdentity.UserId;
            dict["AppVersion"] = AppVersion;
            dict["OSVersion"] = TelemetryIdentity.OSVersion;
            dict["Platform"] = TelemetryIdentity.Platform;

            if (metadata != null)
            {
                foreach (var kvp in metadata)
                {
                    dict[kvp.Key] = kvp.Value;
                }
            }
#if DEBUG
            Debug.WriteLine($"Telemetry: {eventName} {string.Join(", ", dict.Select(kvp => $"{kvp.Key}: {kvp.Value}"))}");
#else
            await mc.TrackAsync(eventName, dict);
#endif
        }

        public static string AppVersion
        {
            get
            {
                var assembly = typeof(App).Assembly;
                string version = "";

                // Reads InformationalVersion (supports semantic versioning like "1.2.0-beta.1")
                var infoVersion = assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion;

                if (!string.IsNullOrEmpty(infoVersion))
                {
                    // Strip the git commit hash suffix added automatically by .NET 8+ SDKs (e.g., "1.2.0+abc1234")
                    var plusIndex = infoVersion.IndexOf('+');
                    version = plusIndex > 0 ? infoVersion[..plusIndex] : infoVersion;
                }
                else
                {
                    version = assembly.GetName().Version?.ToString(3) ?? "1.0.0";
                }

                return version;
            }
        }

    }
}

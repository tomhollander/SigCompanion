using System;
using System.IO;

namespace SigCompanion.Services;

public static class TelemetryIdentity
{
    private static readonly Lazy<string> _userId = new(LoadOrCreateUserId);

    public static string UserId => _userId.Value;

    public static string Platform
    {
        get
        {
            if (OperatingSystem.IsBrowser()) return "Browser";
            if (OperatingSystem.IsAndroid()) return "Android";
            if (OperatingSystem.IsIOS()) return "iOS";
            if (OperatingSystem.IsWindows()) return "Windows";
            if (OperatingSystem.IsMacOS()) return "macOS";
            if (OperatingSystem.IsLinux()) return "Linux";
            return Environment.OSVersion.Platform.ToString();
        }
    }

    public static string OSVersion => Environment.OSVersion.VersionString;

    private static string LoadOrCreateUserId()
    {
        var newId = Guid.NewGuid().ToString("N");
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SigCompanion");
            var path = Path.Combine(directory, "telemetry-id.txt");

            if (File.Exists(path))
            {
                var existing = File.ReadAllText(path).Trim();
                if (Guid.TryParse(existing, out _))
                {
                    return existing;
                }
            }

            Directory.CreateDirectory(directory);
            File.WriteAllText(path, newId);
        }
        catch
        {
            // Storage unavailable (e.g. browser): use an in-memory id for this session
        }
        return newId;
    }
}

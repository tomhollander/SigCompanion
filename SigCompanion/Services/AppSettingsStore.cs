using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SigCompanion.Models;

namespace SigCompanion.Services;

public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    private readonly string _settingsPath;

    public AppSettingsStore()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SigCompanion");

        Directory.CreateDirectory(directory);
        _settingsPath = Path.Combine(directory, "appsettings.dat");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppSettings();
        }

        try
        {
            var protectedBytes = File.ReadAllBytes(_settingsPath);
            if (protectedBytes.Length == 0)
            {
                return new AppSettings();
            }

            var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_settingsPath, protectedBytes);
    }
}

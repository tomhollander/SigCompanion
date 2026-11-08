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
            var fileBytes = File.ReadAllBytes(_settingsPath);
            if (fileBytes.Length == 0)
            {
                return new AppSettings();
            }

            var bytes = OperatingSystem.IsWindows()
                ? ProtectedData.Unprotect(fileBytes, null, DataProtectionScope.CurrentUser)
                : DecryptFallback(fileBytes);

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

        // DPAPI is Windows-only; other platforms use AES-GCM with a key embedded in the app.
        var fileBytes = OperatingSystem.IsWindows()
            ? ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser)
            : EncryptFallback(bytes);

        File.WriteAllBytes(_settingsPath, fileBytes);
    }

    // Obfuscation only: the key ships with the app, so this deters casual reading of the file
    // but does not protect against anyone who has the binary or source.
    private static readonly byte[] FallbackKey =
        SHA256.HashData(Encoding.UTF8.GetBytes("SigCompanion-settings-obfuscation-v1"));

    private const int NonceSize = 12;
    private const int TagSize = 16;

    private static byte[] EncryptFallback(byte[] plainBytes)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var cipherBytes = new byte[plainBytes.Length];

        using var aes = new AesGcm(FallbackKey, TagSize);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var result = new byte[NonceSize + TagSize + cipherBytes.Length];
        nonce.CopyTo(result, 0);
        tag.CopyTo(result, NonceSize);
        cipherBytes.CopyTo(result, NonceSize + TagSize);
        return result;
    }

    private static byte[] DecryptFallback(byte[] fileBytes)
    {
        if (fileBytes.Length < NonceSize + TagSize)
        {
            throw new InvalidDataException("Settings file is too short.");
        }

        var nonce = fileBytes[..NonceSize];
        var tag = fileBytes[NonceSize..(NonceSize + TagSize)];
        var cipherBytes = fileBytes[(NonceSize + TagSize)..];
        var plainBytes = new byte[cipherBytes.Length];

        using var aes = new AesGcm(FallbackKey, TagSize);
        aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
        return plainBytes;
    }
}

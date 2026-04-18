using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WpfMarkdownEditor.Wpf.Services;

public sealed record ProviderConfig(string ProviderName)
{
    // Baidu fields
    public string? AppId { get; init; }
    public string? SecretKey { get; init; }

    // OpenAI-compatible fields
    public string? ApiEndpoint { get; init; }
    public string? ApiKey { get; init; }
    public string? ModelName { get; init; }

    // Derived
    public bool IsComplete => ProviderName switch
    {
        "Baidu" => !string.IsNullOrEmpty(AppId) && !string.IsNullOrEmpty(SecretKey),
        "OpenAI" => !string.IsNullOrEmpty(ApiEndpoint) && !string.IsNullOrEmpty(ApiKey),
        _ => false
    };
}

public sealed class TranslationSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _configPath;

    public TranslationSettingsService(string baseDir)
    {
        _configPath = Path.Combine(baseDir, "translation.json");
    }

    public ProviderConfig? LoadConfig(string providerName)
    {
        var data = LoadRaw();
        if (data == null || !data.TryGetValue(providerName, out var encrypted))
            return null;

        var json = TryDecrypt(encrypted);
        if (json == null)
            return null;
        return JsonSerializer.Deserialize<ProviderConfig>(json, JsonOptions);
    }

    public void SaveConfig(ProviderConfig config)
    {
        var data = LoadRaw() ?? new Dictionary<string, string>();
        var json = JsonSerializer.Serialize(config, JsonOptions);
        data[config.ProviderName] = Encrypt(json);
        SaveRaw(data);
    }

    public string? GetActiveProvider()
    {
        var data = LoadRaw();
        if (data == null || !data.TryGetValue("_active", out var name))
            return null;
        return name;
    }

    public void SetActiveProvider(string providerName)
    {
        var data = LoadRaw() ?? new Dictionary<string, string>();
        data["_active"] = providerName;
        SaveRaw(data);
    }

    private Dictionary<string, string>? LoadRaw()
    {
        if (!File.Exists(_configPath))
            return null;
        var json = File.ReadAllText(_configPath);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
    }

    private void SaveRaw(Dictionary<string, string> data)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(_configPath, json);
    }

    private static string Encrypt(string plainText)
    {
        var bytes = Encoding.UTF8.GetBytes(plainText);
        var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    private static string? TryDecrypt(string encryptedText)
    {
        try
        {
            var bytes = Convert.FromBase64String(encryptedText);
            var decrypted = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}

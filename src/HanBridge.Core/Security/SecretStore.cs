using System.Text.Json;

namespace HanBridge.Core.Security;

public sealed class SecretStore
{
    private readonly string _path;
    private readonly object _gate = new();
    private readonly Dictionary<string, string> _secrets;

    public SecretStore(string path)
    {
        _path = path;
        _secrets = Load(path);
    }

    public string? GetSecret(string providerId)
    {
        lock (_gate)
        {
            return _secrets.TryGetValue(providerId, out var value) ? value : null;
        }
    }

    public bool HasSecret(string providerId) => !string.IsNullOrWhiteSpace(GetSecret(providerId));

    public void SetSecret(string providerId, string? secret)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(secret))
            {
                _secrets.Remove(providerId);
            }
            else
            {
                _secrets[providerId] = secret.Trim();
            }

            Save();
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var json = JsonSerializer.SerializeToUtf8Bytes(_secrets);
        var protectedBytes = DpapiProtector.Protect(json);
        var temporaryPath = _path + ".tmp";
        File.WriteAllBytes(temporaryPath, protectedBytes);
        File.Move(temporaryPath, _path, true);
    }

    private static Dictionary<string, string> Load(string path)
    {
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var protectedBytes = File.ReadAllBytes(path);
            var json = DpapiProtector.Unprotect(protectedBytes);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
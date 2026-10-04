using System.Text.Json;

namespace HanBridge.Core.Storage;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _path;
    private readonly object _gate = new();

    public SettingsStore(string path)
    {
        _path = path;
        Current = Load();
    }

    public AppSettings Current { get; private set; }

    public AppSettings Reload()
    {
        lock (_gate)
        {
            Current = Load();
            return Current;
        }
    }

    public void Save(AppSettings settings)
    {
        lock (_gate)
        {
            Normalize(settings);
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            var temporaryPath = _path + ".tmp";
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _path, true);
            Current = settings;
        }
    }

    private AppSettings Load()
    {
        if (!File.Exists(_path))
        {
            var defaults = ProviderPresets.CreateDefault();
            Save(defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(_path);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
                           ?? ProviderPresets.CreateDefault();
            Normalize(settings);
            return settings;
        }
        catch
        {
            return ProviderPresets.CreateDefault();
        }
    }

    private static void Normalize(AppSettings settings)
    {
        settings.Providers ??= ProviderPresets.CreateDefaults();

        foreach (var preset in ProviderPresets.CreateDefaults())
        {
            if (settings.Providers.All(provider => !provider.Id.Equals(preset.Id, StringComparison.OrdinalIgnoreCase)))
            {
                settings.Providers.Add(preset);
            }
        }

        if (settings.Providers.All(provider => !provider.Id.Equals(settings.ActiveProviderId, StringComparison.OrdinalIgnoreCase)))
        {
            settings.ActiveProviderId = ProviderPresets.DeepSeekId;
        }

        if (string.IsNullOrWhiteSpace(settings.OpenCodeSessionId))
        {
            settings.OpenCodeSessionId = Guid.NewGuid().ToString("N");
        }
    }
}
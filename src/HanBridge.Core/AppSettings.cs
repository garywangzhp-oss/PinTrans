namespace HanBridge.Core;

public sealed class AppSettings
{
    public bool TranslationEnabled { get; set; } = true;
    public string ActiveProviderId { get; set; } = ProviderPresets.DeepSeekId;
    public int DebounceMilliseconds { get; set; } = 250;
    public int RequestTimeoutSeconds { get; set; } = 8;
    public int MaxRequestsPerMinute { get; set; } = 60;
    public int MaxSourceHanCharacters { get; set; } = 200;
    public string ProxyMode { get; set; } = "system";
    public string? ProxyUrl { get; set; }
    public string OpenCodeSessionId { get; set; } = Guid.NewGuid().ToString("N");
    public List<ProviderSettings> Providers { get; set; } = ProviderPresets.CreateDefaults();
}

public sealed class ProviderSettings
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}

public static class ProviderPresets
{
    public const string DeepSeekId = "deepseek";
    public const string OpenCodeGoId = "opencode-go";
    public const string CustomId = "custom";

    public static List<ProviderSettings> CreateDefaults() =>
    [
        new()
        {
            Id = DeepSeekId,
            DisplayName = "DeepSeek",
            Endpoint = "https://api.deepseek.com/chat/completions",
            Model = "deepseek-flash"
        },
        new()
        {
            Id = OpenCodeGoId,
            DisplayName = "OpenCode Go",
            Endpoint = "https://opencode.ai/zen/go/v1/chat/completions",
            Model = "deepseek-v4.1-flash"
        },
        new()
        {
            Id = CustomId,
            DisplayName = "Custom OpenAI-compatible",
            Endpoint = string.Empty,
            Model = string.Empty,
            Enabled = false
        }
    ];

    public static IReadOnlyList<string> GetModelPresets(string providerId)
    {
        if (providerId.Equals(OpenCodeGoId, StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                "deepseek-v4.1-flash",
                "glm-5.3-flash",
                "mimo-v2.6-flash",
                "kimi-k3",
                "longcat-2.0"
            ];
        }

        if (providerId.Equals(DeepSeekId, StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                "deepseek-flash",
                "deepseek-v4-pro"
            ];
        }

        return [];
    }

    public static AppSettings CreateDefault() => new();
}
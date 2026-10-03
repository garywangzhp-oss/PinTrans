using HanBridge.Core;
using HanBridge.Core.Storage;

namespace HanBridge.Tests;

public sealed class ProviderAndSettingsTests
{
    [Fact]
    public void ProviderPresets_UseCurrentDefaults()
    {
        var providers = ProviderPresets.CreateDefaults();

        var deepSeek = Assert.Single(providers, provider => provider.Id == ProviderPresets.DeepSeekId);
        Assert.Equal("https://api.deepseek.com/chat/completions", deepSeek.Endpoint);
        Assert.Equal("deepseek-flash", deepSeek.Model);

        var openCode = Assert.Single(providers, provider => provider.Id == ProviderPresets.OpenCodeGoId);
        Assert.Equal("https://opencode.ai/zen/go/v1/chat/completions", openCode.Endpoint);
        Assert.Equal("deepseek-v4.1-flash", openCode.Model);
    }

    [Fact]
    public void SettingsStore_CreatesDefaultsAndRoundTripsValues()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        var store = new SettingsStore(path);

        Assert.True(store.Current.TranslationEnabled);
        Assert.Equal(ProviderPresets.DeepSeekId, store.Current.ActiveProviderId);

        store.Current.ActiveProviderId = ProviderPresets.OpenCodeGoId;
        store.Current.ProxyMode = "direct";
        store.Save(store.Current);

        var reloaded = new SettingsStore(path);
        Assert.Equal(ProviderPresets.OpenCodeGoId, reloaded.Current.ActiveProviderId);
        Assert.Equal("direct", reloaded.Current.ProxyMode);
    }
}
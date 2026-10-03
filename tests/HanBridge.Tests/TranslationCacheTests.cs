using System.Text;
using HanBridge.Core;
using HanBridge.Core.Logging;
using HanBridge.Core.Security;
using HanBridge.Core.Storage;
using HanBridge.Core.Translation;

namespace HanBridge.Tests;

public sealed class TranslationCacheTests
{
    [Fact]
    public async Task Cache_RoundTripsEncryptedValues()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "cache.db");
        var cache = new TranslationCache(path);
        var key = TranslationCache.CreateKey("你好世界", "deepseek", "deepseek-flash", "prompt-v1");

        await cache.SetAsync(
            key,
            "你好世界",
            "Hello world",
            "deepseek",
            "deepseek-flash",
            "prompt-v1",
            CancellationToken.None);

        var cached = await cache.GetAsync(key, CancellationToken.None);
        Assert.NotNull(cached);
        Assert.Equal("Hello world", cached.Translation);

        var databaseBytes = await File.ReadAllBytesAsync(path);
        Assert.False(ContainsBytes(databaseBytes, Encoding.UTF8.GetBytes("你好世界")));
        Assert.False(ContainsBytes(databaseBytes, Encoding.UTF8.GetBytes("Hello world")));
    }

    [Fact]
    public async Task TranslationService_ReportsMissingSecret()
    {
        using var temp = new TemporaryDirectory();
        var paths = new AppPaths(temp.Path);
        var store = new SettingsStore(paths.SettingsFile);
        var secrets = new SecretStore(paths.SecretsFile);
        var cache = new TranslationCache(paths.CacheFile);
        var logger = new SafeLogger(paths.LogDirectory);
        var service = new TranslationService(
            store,
            secrets,
            cache,
            logger,
            new OpenAiCompatibleClient());

        var outcome = await service.TranslateAsync("你好世界", CancellationToken.None);

        Assert.Equal("error", outcome.Status);
        Assert.Equal("not_configured", outcome.ErrorCode);
    }

    private static bool ContainsBytes(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0)
        {
            return true;
        }

        for (var index = 0; index <= haystack.Length - needle.Length; index++)
        {
            if (haystack.AsSpan(index, needle.Length).SequenceEqual(needle))
            {
                return true;
            }
        }

        return false;
    }
}
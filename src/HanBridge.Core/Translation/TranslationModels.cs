namespace HanBridge.Core.Translation;

public static class TranslationModes
{
    public const string Chinese = "chinese";
    public const string Pinyin = "pinyin";
}

public sealed record CachedTranslation(string Translation, DateTimeOffset LastUsedUtc);

public sealed record TranslationOutcome(
    string Status,
    string? Translation,
    string? ErrorCode,
    long ElapsedMilliseconds,
    bool CacheHit);
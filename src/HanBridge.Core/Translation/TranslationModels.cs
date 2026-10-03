namespace HanBridge.Core.Translation;

public sealed record CachedTranslation(string Translation, DateTimeOffset LastUsedUtc);

public sealed record TranslationOutcome(
    string Status,
    string? Translation,
    string? ErrorCode,
    long ElapsedMilliseconds,
    bool CacheHit);
using System.Diagnostics;
using HanBridge.Core.Logging;
using HanBridge.Core.Security;
using HanBridge.Core.Storage;

namespace HanBridge.Core.Translation;

public sealed class TranslationService
{
    private readonly SettingsStore _settingsStore;
    private readonly SecretStore _secretStore;
    private readonly TranslationCache _cache;
    private readonly SafeLogger _logger;
    private readonly OpenAiCompatibleClient _client;
    private readonly TranslationRateLimiter _rateLimiter = new();
    private readonly object _failureGate = new();

    private int _consecutiveFailures;
    private DateTimeOffset _pausedUntilUtc = DateTimeOffset.MinValue;

    public TranslationService(
        SettingsStore settingsStore,
        SecretStore secretStore,
        TranslationCache cache,
        SafeLogger logger,
        OpenAiCompatibleClient client)
    {
        _settingsStore = settingsStore;
        _secretStore = secretStore;
        _cache = cache;
        _logger = logger;
        _client = client;
    }

    public async Task<TranslationOutcome> TranslateAsync(string sourceText, CancellationToken cancellationToken)
    {
        var started = Stopwatch.StartNew();
        var settings = _settingsStore.Current;

        if (!settings.TranslationEnabled)
        {
            return Outcome("skipped", "translation_off", started, false);
        }

        if (DateTimeOffset.UtcNow < _pausedUntilUtc)
        {
            return Outcome("error", "temporarily_paused", started, false);
        }

        if (HostFieldGuard.IsPasswordFieldFocused())
        {
            return Outcome("skipped", "password-field", started, false);
        }

        if (!LanguageGuards.ShouldTranslate(sourceText, settings.MaxSourceHanCharacters, out var guardReason))
        {
            return Outcome("skipped", guardReason, started, false);
        }

        var provider = settings.Providers.FirstOrDefault(item =>
            item.Id.Equals(settings.ActiveProviderId, StringComparison.OrdinalIgnoreCase));
        if (provider is null || !provider.Enabled)
        {
            return Outcome("error", "provider_unavailable", started, false);
        }

        var apiKey = _secretStore.GetSecret(provider.Id);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Outcome("error", "not_configured", started, false);
        }

        var cacheKey = TranslationCache.CreateKey(
            sourceText,
            provider.Id,
            provider.Model,
            OpenAiCompatibleClient.CurrentPromptVersion);

        try
        {
            var cached = await _cache.GetAsync(cacheKey, cancellationToken);
            if (cached is not null)
            {
                await _cache.TouchAsync(cacheKey, cancellationToken);
                return Outcome("ok", null, started, true, cached.Translation);
            }
        }
        catch (Exception exception)
        {
            _logger.Error("cache-read-failed", exception);
        }

        if (!_rateLimiter.TryAcquire(settings.MaxRequestsPerMinute, DateTimeOffset.UtcNow))
        {
            return Outcome("error", "rate_limited", started, false);
        }

        var completion = await _client.CompleteAsync(provider, apiKey, sourceText, settings, cancellationToken);
        if (!completion.IsSuccess || string.IsNullOrWhiteSpace(completion.Content))
        {
            RegisterFailure(completion.ErrorCode ?? "request_failed");
            _logger.Warn($"translation-failed provider={provider.Id} model={provider.Model} code={completion.ErrorCode ?? "unknown"} status={completion.StatusCode}");
            return Outcome("error", completion.ErrorCode ?? "request_failed", started, false);
        }

        RegisterSuccess();

        try
        {
            await _cache.SetAsync(
                cacheKey,
                sourceText,
                completion.Content,
                provider.Id,
                provider.Model,
                OpenAiCompatibleClient.CurrentPromptVersion,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.Error("cache-write-failed", exception);
        }

        return Outcome("ok", null, started, false, completion.Content);
    }

    private void RegisterFailure(string errorCode)
    {
        lock (_failureGate)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= 3)
            {
                _pausedUntilUtc = DateTimeOffset.UtcNow.AddSeconds(60);
                _consecutiveFailures = 0;
                _logger.Warn($"translation-paused code={errorCode} duration_seconds=60");
            }
        }
    }

    private void RegisterSuccess()
    {
        lock (_failureGate)
        {
            _consecutiveFailures = 0;
            _pausedUntilUtc = DateTimeOffset.MinValue;
        }
    }

    private static TranslationOutcome Outcome(
        string status,
        string? errorCode,
        Stopwatch stopwatch,
        bool cacheHit,
        string? translation = null)
    {
        stopwatch.Stop();
        return new TranslationOutcome(status, translation, errorCode, stopwatch.ElapsedMilliseconds, cacheHit);
    }
}
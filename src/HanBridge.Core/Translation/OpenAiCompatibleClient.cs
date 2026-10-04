using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace HanBridge.Core.Translation;

public sealed class OpenAiCompatibleClient
{
    private const string PromptVersion = "2026-10-04.1";

    public static string CurrentPromptVersion => PromptVersion;

    public async Task<ProviderCompletion> CompleteAsync(
        ProviderSettings provider,
        string apiKey,
        string sourceText,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(provider.Endpoint))
        {
            return ProviderCompletion.Failure("endpoint_missing", 0);
        }

        if (string.IsNullOrWhiteSpace(provider.Model))
        {
            return ProviderCompletion.Failure("model_missing", 0);
        }

        Exception? lastException = null;
        ProviderCompletion? lastCompletion = null;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var completion = await SendOnceAsync(provider, apiKey, sourceText, settings, cancellationToken);
                if (completion.IsSuccess || !IsRetryable(completion.StatusCode))
                {
                    return completion;
                }

                lastCompletion = completion;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                lastException = exception;
            }

            if (attempt == 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500 + Random.Shared.Next(0, 300)), cancellationToken);
            }
        }

        if (lastException is not null)
        {
            return ProviderCompletion.Failure("network_error", 0);
        }

        return lastCompletion ?? ProviderCompletion.Failure("request_failed", 0);
    }

    private static async Task<ProviderCompletion> SendOnceAsync(
        ProviderSettings provider,
        string apiKey,
        string sourceText,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        using var handler = CreateHandler(settings);
        using var client = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, provider.Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (RequiresOpenCodeSession(provider))
        {
            request.Headers.TryAddWithoutValidation("x-opencode-session", settings.OpenCodeSessionId);
        }

        var payload = new Dictionary<string, object?>
        {
            ["model"] = provider.Model,
            ["temperature"] = 0.2,
            ["max_tokens"] = 512,
            ["messages"] = new object[]
            {
                new
                {
                    role = "system",
                    content =
                        "Translate the user's Chinese text into natural, concise, professional English. " +
                        "Preserve names, numbers, URLs, code, and existing English terms. " +
                        "Return only the translation, with no explanation, quotes, markdown, or prefix."
                },
                new
                {
                    role = "user",
                    content = sourceText
                }
            }
        };

        if (RequiresOpenCodeSession(provider))
        {
            payload["reasoning_effort"] = "none";
        }

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.RequestTimeoutSeconds, 2, 30)));

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        var body = await response.Content.ReadAsStringAsync(timeout.Token);
        var statusCode = (int)response.StatusCode;

        if (!response.IsSuccessStatusCode)
        {
            var providerError = TryExtractProviderError(body);
            var errorCode = providerError is null ? $"http_{statusCode}" : $"http_{statusCode}:{providerError}";
            return ProviderCompletion.Failure(errorCode, statusCode);
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var content = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
            {
                return ProviderCompletion.Failure("empty_response", statusCode);
            }

            return ProviderCompletion.Success(content.Trim(), statusCode);
        }
        catch (JsonException)
        {
            return ProviderCompletion.Failure("invalid_response", statusCode);
        }
        catch (KeyNotFoundException)
        {
            return ProviderCompletion.Failure("response_shape", statusCode);
        }
    }

    private static HttpClientHandler CreateHandler(AppSettings settings)
    {
        var handler = new HttpClientHandler
        {
            UseProxy = true
        };

        if (settings.ProxyMode.Equals("direct", StringComparison.OrdinalIgnoreCase))
        {
            handler.UseProxy = false;
        }
        else if (settings.ProxyMode.Equals("custom", StringComparison.OrdinalIgnoreCase)
                 && Uri.TryCreate(settings.ProxyUrl, UriKind.Absolute, out var proxyUri))
        {
            handler.Proxy = new WebProxy(proxyUri);
        }

        return handler;
    }

    private static bool IsRetryable(int statusCode) =>
        statusCode == 429 || statusCode >= 500;

    private static bool RequiresOpenCodeSession(ProviderSettings provider) =>
        provider.Id.Equals(ProviderPresets.OpenCodeGoId, StringComparison.OrdinalIgnoreCase)
        || provider.Endpoint.Contains("opencode.ai/zen/go/", StringComparison.OrdinalIgnoreCase);

    private static string? TryExtractProviderError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("error", out var error))
            {
                return null;
            }

            if (error.ValueKind == JsonValueKind.String)
            {
                return error.GetString();
            }

            if (error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("type", out var type)
                && type.ValueKind == JsonValueKind.String)
            {
                return type.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }
}

public sealed record ProviderCompletion(bool IsSuccess, string? Content, string? ErrorCode, int StatusCode)
{
    public static ProviderCompletion Success(string content, int statusCode) => new(true, content, null, statusCode);

    public static ProviderCompletion Failure(string errorCode, int statusCode) => new(false, null, errorCode, statusCode);
}
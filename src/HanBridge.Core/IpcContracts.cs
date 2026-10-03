using System.Text.Json.Serialization;

namespace HanBridge.Core;

public sealed class TranslationRequest
{
    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("source_text")]
    public string SourceText { get; set; } = string.Empty;

    [JsonPropertyName("created_utc")]
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TranslationResponse
{
    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("source_text")]
    public string SourceText { get; set; } = string.Empty;

    [JsonPropertyName("translation")]
    public string Translation { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "error";

    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("elapsed_ms")]
    public long ElapsedMilliseconds { get; set; }
}
using System.Security.Cryptography;
using System.Text;
using HanBridge.Core.Security;
using Microsoft.Data.Sqlite;

namespace HanBridge.Core.Translation;

public sealed class TranslationCache
{
    private readonly string _connectionString;

    public TranslationCache(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        }.ToString();

        Initialize();
    }

    public static string CreateKey(string sourceText, string providerId, string model, string promptVersion)
    {
        var material = $"{sourceText}\n{providerId}\n{model}\n{promptVersion}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(hash);
    }

    public async Task<CachedTranslation?> GetAsync(string cacheKey, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT translation_enc, last_used_utc FROM translations WHERE cache_key = $key;";
        command.Parameters.AddWithValue("$key", cacheKey);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var encrypted = reader.GetFieldValue<byte[]>(0);
        var translation = Encoding.UTF8.GetString(DpapiProtector.Unprotect(encrypted));
        var lastUsed = DateTimeOffset.Parse(reader.GetString(1));
        return new CachedTranslation(translation, lastUsed);
    }

    public async Task TouchAsync(string cacheKey, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE translations SET last_used_utc = $used WHERE cache_key = $key;";
        command.Parameters.AddWithValue("$used", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$key", cacheKey);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SetAsync(
        string cacheKey,
        string sourceText,
        string translation,
        string providerId,
        string model,
        string promptVersion,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO translations (
    cache_key, source_enc, translation_enc, provider_id, model, prompt_version, created_utc, last_used_utc
) VALUES (
    $key, $source, $translation, $provider, $model, $prompt, $created, $used
)
ON CONFLICT(cache_key) DO UPDATE SET
    source_enc = excluded.source_enc,
    translation_enc = excluded.translation_enc,
    provider_id = excluded.provider_id,
    model = excluded.model,
    prompt_version = excluded.prompt_version,
    last_used_utc = excluded.last_used_utc;";

        command.Parameters.AddWithValue("$key", cacheKey);
        command.Parameters.AddWithValue("$source", DpapiProtector.Protect(Encoding.UTF8.GetBytes(sourceText)));
        command.Parameters.AddWithValue("$translation", DpapiProtector.Protect(Encoding.UTF8.GetBytes(translation)));
        command.Parameters.AddWithValue("$provider", providerId);
        command.Parameters.AddWithValue("$model", model);
        command.Parameters.AddWithValue("$prompt", promptVersion);
        command.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$used", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM translations;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<long> GetSizeBytesAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT page_count * page_size FROM pragma_page_count(), pragma_page_size();";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result);
    }

    private void Initialize()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS translations (
    cache_key TEXT PRIMARY KEY,
    source_enc BLOB NOT NULL,
    translation_enc BLOB NOT NULL,
    provider_id TEXT NOT NULL,
    model TEXT NOT NULL,
    prompt_version TEXT NOT NULL,
    created_utc TEXT NOT NULL,
    last_used_utc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_translations_last_used ON translations(last_used_utc);";
        command.ExecuteNonQuery();
    }
}
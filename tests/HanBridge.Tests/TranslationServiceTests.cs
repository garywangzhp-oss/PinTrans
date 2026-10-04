using System.Net;
using System.Net.Sockets;
using System.Text;
using HanBridge.Core;
using HanBridge.Core.Logging;
using HanBridge.Core.Security;
using HanBridge.Core.Storage;
using HanBridge.Core.Translation;

namespace HanBridge.Tests;

public sealed class TranslationServiceTests
{
    [Fact]
    public async Task TranslationService_ReportsNetworkFailureWithoutThrowing()
    {
        using var temp = new TemporaryDirectory();
        var service = CreateService(temp.Path, "http://127.0.0.1:1/v1/chat/completions");

        var outcome = await service.TranslateAsync("你好世界", CancellationToken.None);

        Assert.Equal("error", outcome.Status);
        Assert.Contains(outcome.ErrorCode, new[] { "network_error", "request_failed" });
    }

    [Fact]
    public async Task TranslationService_ReportsUnauthorizedResponse()
    {
        using var temp = new TemporaryDirectory();
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serverTask = RunSingleResponseAsync(
            listener,
            "401 Unauthorized",
            """{"error":{"type":"invalid_api_key","message":"bad key"}}""");

        try
        {
            var service = CreateService(temp.Path, $"http://127.0.0.1:{port}/v1/chat/completions");
            var outcome = await service.TranslateAsync("你好世界", CancellationToken.None);

            Assert.Equal("error", outcome.Status);
            Assert.Equal("http_401:invalid_api_key", outcome.ErrorCode);
            await serverTask;
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task TranslationService_ExtractsJsonTranslationForOpenCode()
    {
        using var temp = new TemporaryDirectory();
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var payload = """
                      {"choices":[{"message":{"content":"{\"translation\":\"Hello, world\"}"}}]}
                      """;
        var serverTask = RunSingleResponseAsync(listener, "200 OK", payload);

        try
        {
            var service = CreateService(
                temp.Path,
                $"http://127.0.0.1:{port}/v1/chat/completions",
                ProviderPresets.OpenCodeGoId);
            var outcome = await service.TranslateAsync("你好，世界", CancellationToken.None);

            Assert.Equal("ok", outcome.Status);
            Assert.Equal("Hello, world", outcome.Translation);
            await serverTask;
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task TranslationService_AcceptsPinyinFallbackMode()
    {
        using var temp = new TemporaryDirectory();
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var payload = """
                      {"choices":[{"message":{"content":"{\"translation\":\"Switched\"}"}}]}
                      """;
        var serverTask = RunSingleResponseAsync(listener, "200 OK", payload);

        try
        {
            var service = CreateService(
                temp.Path,
                $"http://127.0.0.1:{port}/v1/chat/completions",
                ProviderPresets.OpenCodeGoId);
            var outcome = await service.TranslateAsync(
                "yiqiehuan",
                CancellationToken.None,
                TranslationModes.Pinyin);

            Assert.True(outcome.Status == "ok", $"status={outcome.Status}, error={outcome.ErrorCode}");
            Assert.Equal("Switched", outcome.Translation);
            await serverTask;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static TranslationService CreateService(
        string root,
        string endpoint,
        string providerId = ProviderPresets.DeepSeekId)
    {
        var paths = new AppPaths(root);
        var settingsStore = new SettingsStore(paths.SettingsFile);
        var provider = settingsStore.Current.Providers.Single(item => item.Id == providerId);
        provider.Endpoint = endpoint;
        provider.Model = "test-model";
        settingsStore.Current.ActiveProviderId = providerId;
        settingsStore.Current.ProxyMode = "direct";
        settingsStore.Current.RequestTimeoutSeconds = 2;
        settingsStore.Save(settingsStore.Current);

        var secrets = new SecretStore(paths.SecretsFile);
        secrets.SetSecret(provider.Id, "test-key");
        var cache = new TranslationCache(paths.CacheFile);
        var logger = new SafeLogger(paths.LogDirectory);
        return new TranslationService(settingsStore, secrets, cache, logger, new OpenAiCompatibleClient());
    }

    private static async Task RunSingleResponseAsync(TcpListener listener, string statusLine, string responseBody)
    {
        using var client = await listener.AcceptTcpClientAsync();
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);

        var contentLength = 0;
        while (await reader.ReadLineAsync() is { } line && line.Length > 0)
        {
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                _ = int.TryParse(line["Content-Length:".Length..].Trim(), out contentLength);
            }
        }

        if (contentLength > 0)
        {
            var requestBody = new char[contentLength];
            var offset = 0;
            while (offset < requestBody.Length)
            {
                var read = await reader.ReadAsync(requestBody.AsMemory(offset));
                if (read == 0)
                {
                    break;
                }
                offset += read;
            }
        }

        var body = Encoding.UTF8.GetBytes(responseBody);
        var header = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {statusLine}\r\n" +
            "Content-Type: application/json; charset=utf-8\r\n" +
            $"Content-Length: {body.Length}\r\n" +
            "Connection: close\r\n\r\n");
        await stream.WriteAsync(header);
        await stream.WriteAsync(body);
    }
}
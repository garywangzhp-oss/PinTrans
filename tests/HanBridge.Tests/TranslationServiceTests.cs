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

    private static TranslationService CreateService(string root, string endpoint)
    {
        var paths = new AppPaths(root);
        var settingsStore = new SettingsStore(paths.SettingsFile);
        var provider = settingsStore.Current.Providers.Single(item => item.Id == ProviderPresets.DeepSeekId);
        provider.Endpoint = endpoint;
        provider.Model = "test-model";
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
        while (await reader.ReadLineAsync() is { Length: > 0 })
        {
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
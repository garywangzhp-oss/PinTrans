using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using HanBridge.Core;
using HanBridge.Core.Ipc;
using HanBridge.Core.Logging;
using HanBridge.Core.Security;
using HanBridge.Core.Storage;
using HanBridge.Core.Translation;

namespace HanBridge.Tests;

public sealed class FileIpcIntegrationTests
{
    [Fact]
    public async Task IpcServer_TranslatesViaChatCompletionsProtocol()
    {
        using var temp = new TemporaryDirectory();
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var mockServer = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(shutdown.Token);
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
            while (await reader.ReadLineAsync(shutdown.Token) is { Length: > 0 })
            {
            }

            var json = JsonSerializer.Serialize(new
            {
                choices = new[]
                {
                    new
                    {
                        message = new { content = "Hello from the integration test." }
                    }
                }
            });

            var body = Encoding.UTF8.GetBytes(json);
            var header = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\n" +
                "Content-Type: application/json; charset=utf-8\r\n" +
                $"Content-Length: {body.Length}\r\n" +
                "Connection: close\r\n\r\n");
            await stream.WriteAsync(header, shutdown.Token);
            await stream.WriteAsync(body, shutdown.Token);
        }, shutdown.Token);

        try
        {
            var paths = new AppPaths(temp.Path);
            var settingsStore = new SettingsStore(paths.SettingsFile);
            var settings = settingsStore.Current;
            var provider = settings.Providers.Single(item => item.Id == ProviderPresets.DeepSeekId);
            provider.Endpoint = $"http://127.0.0.1:{port}/v1/chat/completions";
            provider.Model = "test-model";
            settings.ProxyMode = "direct";
            settingsStore.Save(settings);

            var secrets = new SecretStore(paths.SecretsFile);
            secrets.SetSecret(ProviderPresets.DeepSeekId, "test-key");
            var cache = new TranslationCache(paths.CacheFile);
            var logger = new SafeLogger(paths.LogDirectory);
            var service = new TranslationService(settingsStore, secrets, cache, logger, new OpenAiCompatibleClient());
            var ipc = new FileIpcServer(paths, service, logger, () => { });

            using var ipcShutdown = new CancellationTokenSource();
            var ipcTask = ipc.RunAsync(ipcShutdown.Token);
            var request = JsonSerializer.Serialize(new TranslationRequest
            {
                RequestId = "integration-test",
                SourceText = "你好，这是一次集成测试。"
            });
            await File.WriteAllTextAsync(paths.RequestFile, request, new UTF8Encoding(false));

            var deadline = DateTimeOffset.UtcNow.AddSeconds(8);
            while (!File.Exists(paths.ResponseFile) && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            Assert.True(File.Exists(paths.ResponseFile), "IPC response file was not produced.");
            var response = await File.ReadAllTextAsync(paths.ResponseFile);
            Assert.Contains("status=ok", response);
            Assert.Contains("translation=Hello from the integration test.", response);

            ipcShutdown.Cancel();
            ipc.CancelActiveRequest();
            await ipcTask;
            await mockServer;
        }
        finally
        {
            listener.Stop();
        }
    }
}
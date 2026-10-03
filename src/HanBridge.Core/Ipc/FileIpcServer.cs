using System.Text;
using System.Text.Json;
using HanBridge.Core.Logging;
using HanBridge.Core.Translation;

namespace HanBridge.Core.Ipc;

public sealed class FileIpcServer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AppPaths _paths;
    private readonly TranslationService _translationService;
    private readonly SafeLogger _logger;
    private readonly Action _refreshSignal;
    private readonly object _stateGate = new();
    private readonly HashSet<string> _seenRequestIds = new(StringComparer.Ordinal);
    private readonly Queue<string> _seenRequestOrder = new();

    private CancellationTokenSource? _activeRequestCancellation;
    private string _activeRequestId = string.Empty;

    public FileIpcServer(
        AppPaths paths,
        TranslationService translationService,
        SafeLogger logger,
        Action? refreshSignal = null)
    {
        _paths = paths;
        _translationService = translationService;
        _logger = logger;
        _refreshSignal = refreshSignal ?? RimeRefreshSignal.Send;
    }

    public event Action<BridgeStatus>? StatusChanged;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        Publish("starting", null);
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(25));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (!File.Exists(_paths.RequestFile))
                {
                    continue;
                }

                TranslationRequest? request;
                try
                {
                    var json = await ReadAllTextWithRetryAsync(_paths.RequestFile, cancellationToken);
                    request = JsonSerializer.Deserialize<TranslationRequest>(json, JsonOptions);
                    TryDelete(_paths.RequestFile);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.Error("ipc-request-read-failed", exception);
                    TryDelete(_paths.RequestFile);
                    continue;
                }

                if (request is null || string.IsNullOrWhiteSpace(request.RequestId) || string.IsNullOrWhiteSpace(request.SourceText))
                {
                    continue;
                }

                StartRequest(request);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        finally
        {
            Publish("stopped", null);
        }
    }

    public void CancelActiveRequest()
    {
        CancellationTokenSource? toCancel;
        lock (_stateGate)
        {
            toCancel = _activeRequestCancellation;
        }

        toCancel?.Cancel();
    }

    private void StartRequest(TranslationRequest request)
    {
        CancellationTokenSource cancellation;
        CancellationTokenSource? previous;
        lock (_stateGate)
        {
            if (!_seenRequestIds.Add(request.RequestId))
            {
                return;
            }

            _seenRequestOrder.Enqueue(request.RequestId);
            while (_seenRequestOrder.Count > 256)
            {
                _seenRequestIds.Remove(_seenRequestOrder.Dequeue());
            }

            previous = _activeRequestCancellation;
            cancellation = new CancellationTokenSource();
            _activeRequestCancellation = cancellation;
            _activeRequestId = request.RequestId;
        }

        previous?.Cancel();
        _ = ProcessRequestAsync(request, cancellation.Token);
    }

    private async Task ProcessRequestAsync(TranslationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            Publish("debouncing", null);
            await Task.Delay(250, cancellationToken);
            Publish("translating", null);

            var outcome = await _translationService.TranslateAsync(request.SourceText, cancellationToken);
            if (cancellationToken.IsCancellationRequested || !IsActiveRequest(request.RequestId))
            {
                return;
            }

            await WriteResponseAsync(request, outcome, cancellationToken);
            if (outcome.Status == "ok" && !string.IsNullOrWhiteSpace(outcome.Translation))
            {
                _refreshSignal();
                Publish("ready", null);
            }
            else if (outcome.Status == "error" && outcome.ErrorCode == "not_configured")
            {
                Publish("not_configured", null);
            }
            else if (outcome.Status == "error" && outcome.ErrorCode == "temporarily_paused")
            {
                Publish("paused", null);
            }
            else
            {
                Publish("idle", outcome.ErrorCode);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A newer composition superseded this request.
        }
        catch (Exception exception)
        {
            _logger.Error("ipc-processing-failed", exception);
            Publish("error", "processing_failed");
        }
        finally
        {
            lock (_stateGate)
            {
                if (_activeRequestId == request.RequestId)
                {
                    _activeRequestCancellation?.Dispose();
                    _activeRequestCancellation = null;
                    _activeRequestId = string.Empty;
                }
            }
        }
    }

    private bool IsActiveRequest(string requestId)
    {
        lock (_stateGate)
        {
            return string.Equals(_activeRequestId, requestId, StringComparison.Ordinal);
        }
    }

    private async Task WriteResponseAsync(
        TranslationRequest request,
        TranslationOutcome outcome,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.AppendLine("v1");
        builder.Append("request_id=").AppendLine(OneLine(request.RequestId));
        builder.Append("status=").AppendLine(OneLine(outcome.Status));
        builder.Append("source=").AppendLine(OneLine(request.SourceText));
        builder.Append("translation=").AppendLine(OneLine(outcome.Translation));
        builder.Append("error=").AppendLine(OneLine(outcome.ErrorCode));
        builder.Append("elapsed_ms=").AppendLine(outcome.ElapsedMilliseconds.ToString());

        var temporaryPath = _paths.ResponseFile + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, builder.ToString(), new UTF8Encoding(false), cancellationToken);
        File.Move(temporaryPath, _paths.ResponseFile, true);
    }

    private static async Task<string> ReadAllTextWithRetryAsync(string path, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    4096,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);

                using var reader = new StreamReader(stream, Encoding.UTF8, true);
                return await reader.ReadToEndAsync(cancellationToken);
            }
            catch (IOException) when (attempt < 2)
            {
                await Task.Delay(20, cancellationToken);
            }
        }

        throw new IOException($"Unable to read IPC request file '{path}'.");
    }

    private static string OneLine(string? text) =>
        (text ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // A newer request may already have replaced the file.
        }
    }


    private void Publish(string state, string? detail)
    {
        StatusChanged?.Invoke(new BridgeStatus(state, detail));
    }
}
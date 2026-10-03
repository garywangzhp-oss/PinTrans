namespace HanBridge.Core.Translation;

public sealed class TranslationRateLimiter
{
    private readonly Queue<DateTimeOffset> _requests = new();
    private readonly object _gate = new();

    public bool TryAcquire(int maximumRequestsPerMinute, DateTimeOffset now)
    {
        lock (_gate)
        {
            var cutoff = now.AddMinutes(-1);
            while (_requests.Count > 0 && _requests.Peek() < cutoff)
            {
                _requests.Dequeue();
            }

            if (_requests.Count >= maximumRequestsPerMinute)
            {
                return false;
            }

            _requests.Enqueue(now);
            return true;
        }
    }
}
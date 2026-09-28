using Zadatak34TriviaServer_Projekat2.Models;

namespace Zadatak34TriviaServer_Projekat2.Services;

public sealed class RequestQueue
{
    private readonly Queue<ClientRequest> _queue = new();
    private readonly object _queueLock = new();
    private readonly SemaphoreSlim _itemsAvailable = new(0);
    private readonly int _capacity;
    private readonly Logger _logger;
    private bool _stopped;

    public RequestQueue(int capacity, Logger logger)
    {
        _capacity = capacity;
        _logger = logger;
    }

    public bool TryEnqueue(ClientRequest request)
    {
        lock (_queueLock)
        {
            if (_stopped || _queue.Count >= _capacity) return false;
            _queue.Enqueue(request);
            _logger.Info($"Request {request.Id} dodat u red. Queue size={_queue.Count}");
        }
        _itemsAvailable.Release();
        return true;
    }

    public async Task<ClientRequest?> DequeueAsync(CancellationToken token)
    {
        await _itemsAvailable.WaitAsync(token);
        lock (_queueLock)
        {
            if (_queue.Count > 0) return _queue.Dequeue();
            if (_stopped) return null;
        }
        return null;
    }

    public void Stop()
    {
        lock (_queueLock) _stopped = true;
        _logger.Info("RequestQueue zaustavljen.");
    }
}

using System.Net;
using System.Text;
using Zadatak34TriviaServer_Projekat2.Models;

namespace Zadatak34TriviaServer_Projekat2.Services;

public sealed class TaskWorkerPool
{
    private readonly RequestQueue _queue;
    private readonly TriviaService _service;
    private readonly Logger _logger;
    private readonly ServerStats _stats;
    private readonly int _count;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _workers = new();

    public TaskWorkerPool(RequestQueue queue, TriviaService service, Logger logger,
        ServerStats stats, int count)
    {
        _queue = queue; _service = service; _logger = logger; _stats = stats; _count = count;
    }

    public Task StartAsync()
    {
        for (int i = 1; i <= _count; i++)
        {
            int workerNo = i;
            _workers.Add(Task.Run(() => WorkerLoopAsync(workerNo, _cts.Token)));
        }

        _logger.Info($"Pokrenuto {_count} worker Task-ova.");
        return Task.CompletedTask;
    }

    private async Task WorkerLoopAsync(int workerNo, CancellationToken token)
    {
        _logger.Info($"Worker task {workerNo} startovan.");

        while (!token.IsCancellationRequested)
        {
            try
            {
                var request = await _queue.DequeueAsync(token);
                if (request is null) continue;

                _stats.RequestReceived();
                _logger.Info($"Worker {workerNo} obrađuje request {request.Id}.");

                var processingTask = ProcessAndRespondAsync(request, workerNo);

                
                
                var continuation = processingTask.ContinueWith(
                    t =>
                    {
                        if (t.IsFaulted)
                            _logger.Error($"Request {request.Id} završen greškom: {t.Exception?.GetBaseException().Message}");
                        else if (t.IsCanceled)
                            _logger.Warning($"Request {request.Id} otkazan.");
                        else
                            _logger.Info($"Request {request.Id} task završen, HTTP {t.Result}.");
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);

                await processingTask;
                await continuation;
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.Error($"Worker {workerNo}: {ex}"); }
        }

        _logger.Info($"Worker task {workerNo} završen.");
    }

    private async Task<int> ProcessAndRespondAsync(ClientRequest request, int workerNo)
    {
        var result = await _service.ProcessRequestAsync(request);
        await SendResponseAsync(request.Context.Response, result.Body, result.StatusCode);

        if (result.StatusCode >= 200 && result.StatusCode < 400)
            _stats.RequestSucceeded();
        else
            _stats.RequestFailed();

        _logger.Info($"Worker {workerNo}: odgovor za request {request.Id} poslat.");
        return result.StatusCode;
    }

    private static async Task SendResponseAsync(HttpListenerResponse response, string body, int status)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        response.StatusCode = status;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.OutputStream.Close();
    }

    public async Task StopAsync()
    {
        _cts.Cancel();
        try { await Task.WhenAll(_workers); }
        catch (OperationCanceledException) { }
        _logger.Info("Task worker pool zaustavljen.");
    }
}

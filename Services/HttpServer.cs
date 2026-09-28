using System.Net;
using Zadatak34TriviaServer_Projekat2.Models;

namespace Zadatak34TriviaServer_Projekat2.Services;

public sealed class HttpServer
{
    private readonly string _prefix;
    private readonly RequestQueue _queue;
    private readonly Logger _logger;
    private readonly ServerStats _stats;
    private readonly HttpListener _listener = new();
    private readonly object _stateLock = new();
    private Thread? _receiver;
    private long _nextId;
    private bool _stopping;

    public HttpServer(string prefix, RequestQueue queue, Logger logger, ServerStats stats)
    {
        _prefix = prefix; _queue = queue; _logger = logger; _stats = stats;
        _listener.Prefixes.Add(prefix);
    }

    public void Start()
    {
        lock (_stateLock)
        {
            if (_listener.IsListening) return;
            _stopping = false;
            _listener.Start();

            
            _receiver = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "HTTP Receiver Thread"
            };
            _receiver.Start();
            _logger.Info($"HTTP server sluša na {_prefix}");
        }
    }

    private void ReceiveLoop()
    {
        while (true)
        {
            HttpListenerContext context;
            try { context = _listener.GetContext(); }
            catch (HttpListenerException) { if (_stopping) break; else break; }
            catch (ObjectDisposedException) { break; }

            var id = Interlocked.Increment(ref _nextId);
            var request = new ClientRequest(id, context);
            _logger.Info($"Primljen request {id}: {context.Request.HttpMethod} {context.Request.Url}");

            if (!_queue.TryEnqueue(request))
            {
                Send503(context.Response);
                _logger.Warning($"Request {id} odbijen: red je pun ili server završava.");
            }
        }
    }

    private static void Send503(HttpListenerResponse response)
    {
        try
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes("{error:Server je trenutno preopterećen.}");
            response.StatusCode = 503;
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.OutputStream.Close();
        }
        catch { }
    }

    public void Stop()
    {
        lock (_stateLock)
        {
            if (_stopping) return;
            _stopping = true;
        }

        try { _listener.Stop(); } catch { }
        try { _receiver?.Join(); } catch { }
        _logger.Info("HTTP server zaustavljen.");
    }
}

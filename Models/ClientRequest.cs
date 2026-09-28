using System.Net;

namespace Zadatak34TriviaServer_Projekat2.Models;

public sealed class ClientRequest
{
    public long Id { get; }
    public HttpListenerContext Context { get; }
    public DateTime ReceivedAt { get; }

    public ClientRequest(long id, HttpListenerContext context)
    {
        Id = id;
        Context = context;
        ReceivedAt = DateTime.Now;
    }
}

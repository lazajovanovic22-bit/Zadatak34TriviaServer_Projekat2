using System.Text;

namespace Zadatak34TriviaServer_Projekat2.Services;

public sealed class ServerStats
{
    private long _total, _success, _errors, _hits, _misses, _waits;

    public void RequestReceived() => Interlocked.Increment(ref _total);
    public void RequestSucceeded() => Interlocked.Increment(ref _success);
    public void RequestFailed() => Interlocked.Increment(ref _errors);
    public void CacheHit() => Interlocked.Increment(ref _hits);
    public void CacheMiss() => Interlocked.Increment(ref _misses);
    public void CacheWait() => Interlocked.Increment(ref _waits);

    public string CreateReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== SERVER STATS ===");
        sb.AppendLine($"Ukupno zahteva: {Interlocked.Read(ref _total)}");
        sb.AppendLine($"Uspešni: {Interlocked.Read(ref _success)}");
        sb.AppendLine($"Greške: {Interlocked.Read(ref _errors)}");
        sb.AppendLine($"Cache hit: {Interlocked.Read(ref _hits)}");
        sb.AppendLine($"Cache miss/load: {Interlocked.Read(ref _misses)}");
        sb.AppendLine($"Čekanja na isti resurs: {Interlocked.Read(ref _waits)}");
        return sb.ToString();
    }
}

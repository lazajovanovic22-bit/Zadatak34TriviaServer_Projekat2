using Zadatak34TriviaServer_Projekat2.Services;

namespace Zadatak34TriviaServer_Projekat2;

internal class Program
{
    private static async Task Main()
    {
        const string prefix = "http://localhost:8080/";
        const int workerCount = 4;

        var logger = new Logger("server.log");
        var stats = new ServerStats();
        var queue = new RequestQueue(1000, logger);
        var cache = new TriviaCache(TimeSpan.FromMinutes(2), logger);
        var service = new TriviaService(cache, logger, stats);
        var workers = new TaskWorkerPool(queue, service, logger, stats, workerCount);
        var server = new HttpServer(prefix, queue, logger, stats);

        Console.CancelKeyPress += (_, e) => { e.Cancel = true; server.Stop(); queue.Stop(); };

        server.Start();
        await workers.StartAsync();

        Console.WriteLine("Trivia Server - Projekat 2");
        Console.WriteLine($"Server: {prefix}");
        Console.WriteLine("Primer: http://localhost:8080/api.php?amount=23&type=boolean");
        Console.WriteLine("Status: http://localhost:8080/status");
        Console.WriteLine("Pritisni ENTER za zaustavljanje.");

        Console.ReadLine();

        server.Stop();
        queue.Stop();
        await workers.StopAsync();

        Console.WriteLine(stats.CreateReport());
        logger.Dispose();
    }
}

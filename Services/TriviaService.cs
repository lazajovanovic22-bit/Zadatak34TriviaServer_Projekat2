using System.Net;
using Newtonsoft.Json.Linq;
using Zadatak34TriviaServer_Projekat2.Models;

namespace Zadatak34TriviaServer_Projekat2.Services;

public sealed class TriviaService
{
    private readonly TriviaCache _cache;
    private readonly Logger _logger;
    private readonly ServerStats _stats;

    private static readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(15) };
    private const string ApiBase = "https://opentdb.com/api.php";

    public TriviaService(TriviaCache cache, Logger logger, ServerStats stats)
    {
        _cache = cache; _logger = logger; _stats = stats;
    }

    public async Task<(string Body, int StatusCode)> ProcessRequestAsync(ClientRequest request)
    {
        try
        {
            var req = request.Context.Request;

            if (!string.Equals(req.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
                return (Error("Dozvoljen je samo GET."), 405);

            if (string.Equals(req.Url?.AbsolutePath, "/status", StringComparison.OrdinalIgnoreCase))
                return (_stats.CreateReport(), 200);

            if (!string.Equals(req.Url?.AbsolutePath, "/api.php", StringComparison.OrdinalIgnoreCase))
                return (Error("Nepoznata putanja."), 404);

            if (!int.TryParse(req.QueryString["amount"], out var amount) || amount < 1 || amount > 50)
                return (Error("Parametar amount mora biti broj od 1 do 50."), 400);

            var type = req.QueryString["type"];
            if (type != "boolean" && type != "multiple")
                return (Error("Parametar type mora biti boolean ili multiple."), 400);

            var key = $"amount={amount}&type={type}";
            var result = await _cache.GetOrCreateAsync(key, () => FetchAsync(amount, type));

            if (result.Source == CacheResultSource.Hit) _stats.CacheHit();
            else if (result.Source == CacheResultSource.Loaded) _stats.CacheMiss();
            else _stats.CacheWait();

            return (result.Body, result.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.Error($"Request {request.Id}: mrežna greška - {ex.Message}");
            return (Error("Greška pri komunikaciji sa Open Trivia API servisom."), 502);
        }
        catch (TaskCanceledException)
        {
            _logger.Error($"Request {request.Id}: API timeout.");
            return (Error("Open Trivia API nije odgovorio na vreme."), 504);
        }
        catch (Exception ex)
        {
            _logger.Error($"Request {request.Id}: {ex}");
            return (Error("Interna greška servera."), 500);
        }
    }

    private async Task<(string Body, int StatusCode)> FetchAsync(int amount, string type)
    {
        var url = $"{ApiBase}?amount={amount}&type={Uri.EscapeDataString(type)}";
        _logger.Info($"API FETCH START: {url}");

        using var response = await _client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();

        var json = JObject.Parse(body);
        var code = json["response_code"]?.Value<int>();

        if (code is not 0 and not 1)
            throw new InvalidOperationException($"Open Trivia response_code={code}");

        _logger.Info($"API FETCH END: amount={amount}, type={type}, response_code={code}");
        return (body, 200);
    }

    private static string Error(string message) =>
        new JObject { ["error"] = message }.ToString();
}

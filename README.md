# Projekat 2 - Topic 34

C# console Web server za Open Trivia Database, sa Task/async-await obradom.

## Pokretanje
```text
dotnet restore
dotnet run
```

Server:
`http://localhost:8080/`

Primer:
`http://localhost:8080/api.php?amount=23&type=boolean`

Status:
`http://localhost:8080/status`

## Arhitektura

Browser -> HttpServer -> RequestQueue -> 4 Task worker-a -> TriviaService -> TriviaCache -> Open Trivia API.

### Thread
`HttpServer` koristi jedan klasičan `Thread` zato što `HttpListener.GetContext()` predstavlja dugotrajno blokirajuće čekanje. Tu Task nema posebnu korist.

### Task i async/await
Obrada koristi 4 worker Task-a. Mrežni poziv koristi:
`await HttpClient.GetAsync(...)`

Čitanje sadržaja:
`await response.Content.ReadAsStringAsync()`

Slanje odgovora:
`await response.OutputStream.WriteAsync(...)`

Dakle, I/O čekanja ne drže fizički Thread blokiranim.

### Kontrola paralelizma
Postoje 4 worker Task-a. Zato najviše 4 worker-a obrađuju zahteve u datom trenutku.

### Zajednički red
`Queue<ClientRequest>` je zaštićen `lock`-om. `SemaphoreSlim` signalizira da je zahtev dostupan, a `WaitAsync` omogućava asinhrono čekanje.

### Cache
Cache traje 2 minuta. Za svaki ključ postoji `CacheEntry`.

### Cache stampede
Za trenutno učitavane ključeve koristi se:
`Dictionary<string, TaskCompletionSource<CacheEntry>>`

Prvi task učitava podatke sa API-ja. Ostali taskovi rade `await` nad istim `TaskCompletionSource.Task` i ne šalju nove API zahteve.

Mrežni poziv se izvršava VAN cache lock-a.

`RunContinuationsAsynchronously` sprečava izvršavanje nastavaka direktno u kritičnom delu pri postavljanju rezultata.

### ContinueWith
`ContinueWith` je demonstriran posle glavnog `ProcessAndRespondAsync` taska za post-processing/logovanje njegovog završetka. `await` je zadržan kao glavni i čitljiviji način vođenja asinhronog toka.

### Thread-safe resursi
- Request queue: `lock`
- Cache: `lock`
- Logger: `lock`
- Brojači: `Interlocked`
- Cache stampede: `TaskCompletionSource`

### Greške
Obrađuju se loš HTTP metod, loša putanja, nevalidni parametri, API mrežna greška, timeout, loš JSON i neočekivane greške. Ako je red pun, vraća se HTTP 503.

## Ponašanje pod opterećenjem

Za mnogo različitih zahteva, oni čekaju u zajedničkom redu i obrađuju se preko 4 worker Task-a.

Za mnogo istih zahteva, samo prvi task radi Open Trivia API poziv; ostali asinhrono čekaju isti rezultat.

Kada cache istekne, sledeći zahtev ponovo učitava podatke.

## Odbrana

1. Klasičan Thread je ostavljen samo za prijemnu blokirajuću petlju.
2. Obrada je prebačena na Task worker-e.
3. `async/await` se koristi za I/O.
4. `SemaphoreSlim` omogućava asinhrono čekanje na red.
5. `lock` štiti kratke kritične sekcije.
6. Četiri worker Task-a kontrolišu paralelizam.
7. Cache traje 2 minuta.
8. `TaskCompletionSource` sprečava cache stampede.
9. Ostali taskovi čekaju preko `await`, bez blokiranja Thread-a.
10. `ContinueWith` je iskorišćen za post-processing nakon završetka obrade.
11. `Interlocked` štiti statistiku.
12. Logger ima `lock` zbog paralelnog pristupa.

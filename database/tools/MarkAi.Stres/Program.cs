// Stress test for a running API: for every scenario and every level, fires that many calls at once (all released together) and
// reports how the API held up: status codes, rate-limited calls (429), transport errors and timeouts, latency percentiles and
// throughput. Exits with 1 when a level breaks a threshold (--prag-erori, --prag-p95), with 2 when the setup fails.
// Only against a TEST API whose database has the volume data (date-volum): the signed-in scenarios use vol-elev00001, vol-elev00002, …
// Usage: dotnet run --project tools/MarkAi.Stres -c Release -- --url http://127.0.0.1:5082 [options]   (--ajutor lists them)
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

Optiuni o;
try { o = Optiuni.Citeste(args); }
catch (ArgumentException e) { Console.Error.WriteLine(e.Message); Console.Error.WriteLine(Optiuni.TextAjutor); return 2; }
if (o.Ajutor) { Console.WriteLine(Optiuni.TextAjutor); return 0; }

// The live stack (API on 5080, site on 3000) serves the demo; a stress run there slows it down for everyone using it.
if (o.Url.Port is 5080 or 3000 && !o.PermiteLive)
{
    Console.Error.WriteLine($"{o.Url} looks like the live API or site (port {o.Url.Port}). Start a test API on another port (see database/docs/teste.md), "
        + "or pass --permite-live if you really mean it.");
    return 2;
}

using var http = new HttpClient(new SocketsHttpHandler
{
    // Every call carries its own client's cookie; and "all at once" means one connection per call in flight.
    UseCookies = false, AllowAutoRedirect = false, MaxConnectionsPerServer = int.MaxValue,
    ConnectTimeout = o.Timeout, PooledConnectionLifetime = TimeSpan.FromMinutes(2),
}) { BaseAddress = o.Url, Timeout = Timeout.InfiniteTimeSpan };

var scenarii = Scenarii.Toate.Where(s => o.Scenarii.Length == 0 || o.Scenarii.Contains(s.Nume)).ToList();
var necunoscute = o.Scenarii.Except(Scenarii.Toate.Select(s => s.Nume)).ToList();
if (necunoscute.Count > 0)
{
    Console.Error.WriteLine($"Unknown scenario(s): {string.Join(", ", necunoscute)}. Known: {string.Join(", ", Scenarii.Toate.Select(s => s.Nume))}.");
    return 2;
}

Console.WriteLine($"Target {o.Url} · levels {string.Join(", ", o.Cereri)} calls · "
    + (o.Concurenta is int c ? $"at most {c} in flight" : "all at once") + $" · {o.Clienti} clients"
    + (o.IpuriDiferite ? " (each with its own X-Forwarded-For address)" : "") + $" · timeout {o.Timeout.TotalSeconds:0} s");
Context ctx;
try { ctx = await Context.PregatesteAsync(http, o, scenarii.Any(s => s.CuCont)); }
catch (Exception e) when (e is HttpRequestException or InvalidOperationException or TaskCanceledException or JsonException or KeyNotFoundException)
{
    Console.Error.WriteLine("Setup failed: " + e.Message);
    return 2;
}

var rezultate = new List<Rezultat>();
foreach (var s in scenarii)
{
    if (s.Nume == "lectie" && ctx.Lectii.Count == 0) { Console.WriteLine("lectie: skipped (no Lecții visible to the test accounts)."); continue; }
    Console.WriteLine();
    Console.WriteLine($"{s.Nume}: {s.Descriere}");
    foreach (var n in o.Cereri)
    {
        var r = await Ruleaza(s, n);
        rezultate.Add(r);
        Console.WriteLine("  " + r.Rand(o));
        if (r.Limitate > 0)
            Console.WriteLine($"  ↳ {r.Limitate} calls were refused by the rate limiter (429): raise Limite__* on the test API or use --ip-uri-diferite "
                + "to measure the API itself.");
        await Task.Delay(Scenarii.PauzaIntreTrepte);
    }
}

Console.WriteLine();
var picate = rezultate.Where(r => !r.Trece(o)).ToList();
Console.WriteLine(Rezultat.Tabel(rezultate, o, markdown: false));
// In CI, --rezumat "$GITHUB_STEP_SUMMARY" puts the same table on the run's page.
if (o.Rezumat is not null)
    await File.AppendAllTextAsync(o.Rezumat, $"## Test de stres ({o.Url})\n\n{Rezultat.Tabel(rezultate, o, markdown: true)}\n");
Console.WriteLine(picate.Count == 0
    ? $"All {rezultate.Count} levels passed (errors ≤ {o.PragErori:0.##} %, p95 ≤ {o.PragP95} ms)."
    : $"{picate.Count} of {rezultate.Count} levels failed: " + string.Join(", ", picate.Select(r => $"{r.Scenariu} × {r.Cereri}")) + ".");
return picate.Count == 0 ? 0 : 1;

async Task<Rezultat> Ruleaza(Scenariu s, int n)
{
    var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    using var limita = o.Concurenta is int maxim ? new SemaphoreSlim(maxim) : null;
    var sarcini = Enumerable.Range(0, n).Select(async i =>
    {
        await start.Task;
        if (limita is not null) await limita.WaitAsync();
        try
        {
            using var cerere = s.Cerere(ctx, i);
            ctx.Identifica(cerere, i);
            var ceas = Stopwatch.StartNew();
            using var cts = new CancellationTokenSource(o.Timeout);
            try
            {
                using var raspuns = await http.SendAsync(cerere, HttpCompletionOption.ResponseContentRead, cts.Token);
                return new Apel((int)raspuns.StatusCode, ceas.Elapsed.TotalMilliseconds, null);
            }
            catch (OperationCanceledException) { return new Apel(0, ceas.Elapsed.TotalMilliseconds, "timeout"); }
            catch (HttpRequestException e) { return new Apel(0, ceas.Elapsed.TotalMilliseconds, e.HttpRequestError.ToString()); }
        }
        finally { limita?.Release(); }
    }).ToArray();
    var total = Stopwatch.StartNew();
    start.SetResult();
    var apeluri = await Task.WhenAll(sarcini);
    return new Rezultat(s.Nume, n, total.Elapsed, apeluri);
}

sealed record Optiuni(Uri Url, int[] Cereri, int? Concurenta, string[] Scenarii, int Clienti, string Parola, bool IpuriDiferite,
    TimeSpan Timeout, double PragErori, int PragP95, bool PermiteLive, string? Rezumat, bool Ajutor)
{
    public static readonly string TextAjutor = """
        Options:
          --url <adresa>          the test API, e.g. http://127.0.0.1:5082 (required; never the live one on 5080)
          --cereri <n[,n…]>       calls per level, fired together; one level per number (default 100,500,1000)
          --concurenta <n>        at most n calls in flight (default: all of a level at once)
          --scenarii <a[,b…]>     which scenarios (default: all): materii, judete, organizatii, profesori, lectii, lectie,
                                  cautare, progres, eu, autentificare, mix
          --clienti <n>           simulated users: calls go round-robin over vol-elev00001…n (default 50)
          --parola <parola>       the date-volum password (default: MARKAI_PAROLA_TEST, else the date-test default)
          --ip-uri-diferite       give each client its own X-Forwarded-For address (only when connecting to the API directly
                                  from this machine: the API trusts that header from localhost only)
          --timeout <secunde>     per call (default 30)
          --prag-erori <procent>  a level fails above this share of errors: 5xx, other non-2xx except 429, timeouts,
                                  refused connections (default 1)
          --prag-p95 <ms>         a level fails when 95 % of the calls take longer than this (default 2000)
          --rezumat <fisier>      also append the results as a Markdown table to this file (CI: "$GITHUB_STEP_SUMMARY")
          --permite-live          allow port 5080/3000
        """;

    public static Optiuni Citeste(string[] args)
    {
        var parolaImplicita = Environment.GetEnvironmentVariable("MARKAI_PAROLA_TEST") is { Length: >= 8 } p ? p : "HackAttack";
        var o = new Optiuni(null!, [100, 500, 1000], null, [], 50, parolaImplicita, false, TimeSpan.FromSeconds(30), 1, 2000, false, null, false);
        for (var i = 0; i < args.Length; i++)
        {
            string Valoare() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value.");
            int Numar(string v, int minim = 1) => int.TryParse(v, out var x) && x >= minim ? x : throw new ArgumentException($"Not a number ≥ {minim}: {v}");
            o = args[i] switch
            {
                "--url" => o with { Url = Uri.TryCreate(Valoare().TrimEnd('/') + "/", UriKind.Absolute, out var u) ? u : throw new ArgumentException("--url is not a URL.") },
                "--cereri" => o with { Cereri = Valoare().Split(',', StringSplitOptions.RemoveEmptyEntries).Select(v => Numar(v)).ToArray() },
                "--concurenta" => o with { Concurenta = Numar(Valoare()) },
                "--scenarii" => o with { Scenarii = Valoare().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) },
                "--clienti" => o with { Clienti = Numar(Valoare()) },
                "--parola" => o with { Parola = Valoare() },
                "--ip-uri-diferite" => o with { IpuriDiferite = true },
                "--timeout" => o with { Timeout = TimeSpan.FromSeconds(Numar(Valoare())) },
                "--prag-erori" => o with { PragErori = double.TryParse(Valoare(), System.Globalization.CultureInfo.InvariantCulture, out var e) && e >= 0 ? e
                    : throw new ArgumentException("--prag-erori needs a percentage.") },
                "--prag-p95" => o with { PragP95 = Numar(Valoare()) },
                "--permite-live" => o with { PermiteLive = true },
                "--rezumat" => o with { Rezumat = Valoare() },
                "--ajutor" or "--help" or "-h" => o with { Ajutor = true },
                _ => throw new ArgumentException($"Unknown option {args[i]}."),
            };
        }
        if (o.Url is null && !o.Ajutor) throw new ArgumentException("--url is required.");
        return o;
    }
}

/// <summary>What the scenarios need, gathered before the first level: the subjects, some Lecții, and a cookie per client.</summary>
sealed class Context(Optiuni o)
{
    public List<int> Materii { get; } = [];
    public List<int> Lectii { get; } = [];
    private string[] cookies = [];

    public string Cont(int i) => $"vol-elev{i % o.Clienti + 1:00000}";
    public string? Cookie(int i) => cookies.Length == 0 ? null : cookies[i % cookies.Length];
    public string Parola => o.Parola;

    /// <summary>Marks a call as coming from its client: the client's own address (with --ip-uri-diferite).</summary>
    public void Identifica(HttpRequestMessage cerere, int i)
    {
        // 10.x.y.z, one per client; the API takes it from X-Forwarded-For only when the connection comes from localhost.
        var c = i % o.Clienti + 1;
        if (o.IpuriDiferite) cerere.Headers.Add("X-Forwarded-For", $"10.{c >> 16 & 255}.{c >> 8 & 255}.{c & 255}");
    }

    public static async Task<Context> PregatesteAsync(HttpClient http, Optiuni o, bool cuConturi)
    {
        var ctx = new Context(o);
        using (var r = await http.GetAsync("judete"))
            if (!r.IsSuccessStatusCode) throw new InvalidOperationException($"GET /judete answered {(int)r.StatusCode}; is {o.Url} the API?");
        foreach (var m in (await http.GetFromJsonAsync<JsonElement>("materii")).GetProperty("elemente").EnumerateArray())
            ctx.Materii.Add(m.GetProperty("id").GetInt32());
        if (ctx.Materii.Count == 0) throw new InvalidOperationException("No subjects: run seed and date-volum on the test database first.");
        if (!cuConturi) return ctx;

        // One sign-in per client, a few at a time (each one checks a password hash, which is slow on purpose). The sign-in limit
        // is per address, so without --ip-uri-diferite or a raised Limite__AutentificariPeMinut this waits for the next window.
        Console.Write($"Signing in {o.Clienti} clients ");
        ctx.cookies = new string[o.Clienti];
        using var cate = new SemaphoreSlim(8);
        await Task.WhenAll(Enumerable.Range(0, o.Clienti).Select(async i =>
        {
            await cate.WaitAsync();
            try { ctx.cookies[i] = await ctx.IntraAsync(http, i); Console.Write('.'); }
            finally { cate.Release(); }
        }));
        Console.WriteLine(" done.");

        // Lecții the first client can open, for the "lectie" scenario: the first page of every subject.
        foreach (var m in ctx.Materii)
        {
            using var cerere = new HttpRequestMessage(HttpMethod.Get, $"materii/{m}/parcursuri?pagina=1");
            cerere.Headers.Add("Cookie", ctx.cookies[0]);
            ctx.Identifica(cerere, 0);
            using var r = await http.SendAsync(cerere);
            if (!r.IsSuccessStatusCode) continue;
            foreach (var p in (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("elemente").EnumerateArray())
                ctx.Lectii.Add(p.GetProperty("id").GetInt32());
        }
        Console.WriteLine($"{ctx.Materii.Count} subjects, {ctx.Lectii.Count} Lecții to pick from.");
        return ctx;
    }

    private async Task<string> IntraAsync(HttpClient http, int i)
    {
        for (var incercare = 1; ; incercare++)
        {
            using var cerere = Scenarii.Autentificare(this, i);
            Identifica(cerere, i);
            using var r = await http.SendAsync(cerere);
            if (r.StatusCode == HttpStatusCode.TooManyRequests && incercare <= 10)
            {
                await Task.Delay(TimeSpan.FromSeconds(r.Headers.RetryAfter?.Delta?.TotalSeconds ?? 10));
                continue;
            }
            if (r.StatusCode == HttpStatusCode.Unauthorized)
                throw new InvalidOperationException($"{Cont(i)} cannot sign in: no volume data on this database (run date-volum), or a different password (--parola).");
            if (!r.IsSuccessStatusCode) throw new InvalidOperationException($"Signing in {Cont(i)} answered {(int)r.StatusCode}.");
            // Only the name=value part of each cookie goes back to the API.
            return string.Join("; ", r.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]));
        }
    }
}

sealed record Scenariu(string Nume, bool CuCont, string Descriere, Func<Context, int, HttpRequestMessage> Cerere);

static class Scenarii
{
    public static readonly TimeSpan PauzaIntreTrepte = TimeSpan.FromSeconds(2);
    // Words from the date-volum topics, so the searches find something.
    private static readonly string[] Cuvinte = ["functia", "romantismul", "celula", "algoritmi", "genetica", "optica", "progresii", "eseul", "grafuri", "clima"];

    private static T Una<T>(IReadOnlyList<T> lista) => lista[Random.Shared.Next(lista.Count)];

    private static HttpRequestMessage Get(string cale, Context? ctx = null, int i = 0)
    {
        var cerere = new HttpRequestMessage(HttpMethod.Get, cale);
        if (ctx?.Cookie(i) is { } cookie) cerere.Headers.Add("Cookie", cookie);
        return cerere;
    }

    public static HttpRequestMessage Autentificare(Context ctx, int i)
    {
        var cerere = new HttpRequestMessage(HttpMethod.Post, "autentificare") { Content = JsonContent.Create(new { identificator = ctx.Cont(i), parola = ctx.Parola }) };
        cerere.Headers.Add("X-MarkAi", "1"); // the API's CSRF header, required on every POST
        return cerere;
    }

    private static readonly Scenariu[] Citiri =
    [
        new("materii", false, "GET /materii, every subject with its Lecții counts (anonymous)", (_, _) => Get("materii")),
        new("judete", false, "GET /judete (anonymous; the cheapest call, a baseline)", (_, _) => Get("judete")),
        new("organizatii", false, "GET /organizatii, the approved organisations (anonymous)", (_, _) => Get("organizatii")),
        new("profesori", false, "GET /profesori?pagina=1, professors with upcoming sessions (anonymous)", (_, _) => Get("profesori?pagina=1")),
        new("lectii", true, "GET /materii/{id}/parcursuri?pagina=1, a subject's Lecții (signed in)",
            (c, i) => Get($"materii/{Una(c.Materii)}/parcursuri?pagina=1", c, i)),
        new("lectie", true, "GET /parcursuri/{id}, one Lecție (signed in)", (c, i) => Get($"parcursuri/{Una(c.Lectii)}", c, i)),
        new("cautare", true, "GET /cautare?q=…, search over Lecții and chapters (signed in)", (c, i) => Get($"cautare?q={Una(Cuvinte)}", c, i)),
        new("progres", true, "GET /progres, the student's progress (signed in)", (c, i) => Get("progres", c, i)),
        new("eu", true, "GET /utilizatori/me, the current account (signed in)", (c, i) => Get("utilizatori/me", c, i)),
    ];

    public static readonly Scenariu[] Toate =
    [
        .. Citiri,
        new("autentificare", false, "POST /autentificare, a fresh sign-in per call (password hashing: CPU-bound)", Autentificare),
        new("mix", true, "a random mix of every read above", (c, i) => Una(c.Lectii.Count > 0 ? Citiri : Citiri.Where(s => s.Nume != "lectie").ToArray()).Cerere(c, i)),
    ];
}

/// <summary>One call: its status (0 when it never got an answer), how long it took, and why it failed.</summary>
readonly record struct Apel(int Status, double Ms, string? Eroare);

sealed record Rezultat(string Scenariu, int Cereri, TimeSpan Durata, Apel[] Apeluri)
{
    public int Limitate => Apeluri.Count(a => a.Status == 429);
    /// <summary>Server errors, other refusals (except the rate limiter's) and calls that never got an answer.</summary>
    public int Erori => Apeluri.Count(a => a.Status is 0 or >= 300 and not 429);
    public double ProcentErori => 100.0 * Erori / Cereri;

    public double Percentila(double p)
    {
        var ms = Apeluri.Where(a => a.Status != 0).Select(a => a.Ms).Order().ToArray();
        return ms.Length == 0 ? double.NaN : ms[Math.Clamp((int)Math.Ceiling(p / 100 * ms.Length) - 1, 0, ms.Length - 1)];
    }

    /// <summary>Every level in one table: as text for the console, or as Markdown for a CI summary.</summary>
    public static string Tabel(IEnumerable<Rezultat> rezultate, Optiuni o, bool markdown)
    {
        string[] cap = ["scenario", "calls", "time (s)", "calls/s", "p50 ms", "p95 ms", "p99 ms", "max ms", "2xx", "429", "errors", "result"];
        var randuri = rezultate.Select(r => new[]
        {
            r.Scenariu, r.Cereri.ToString(), r.Durata.TotalSeconds.ToString("0.00"), (r.Cereri / r.Durata.TotalSeconds).ToString("0.0"),
            r.Percentila(50).ToString("0"), r.Percentila(95).ToString("0"), r.Percentila(99).ToString("0"), r.Percentila(100).ToString("0"),
            r.Apeluri.Count(a => a.Status is >= 200 and < 300).ToString(), r.Limitate.ToString(), $"{r.Erori} ({r.ProcentErori:0.#} %)",
            r.Trece(o) ? "ok" : "FAIL",
        }).ToList();
        if (markdown)
            return string.Join("\n", new[] { cap, cap.Select(_ => "---").ToArray() }.Concat(randuri).Select(c => "| " + string.Join(" | ", c) + " |")) + "\n";
        var latimi = cap.Select((c, i) => randuri.Select(x => x[i].Length).Prepend(c.Length).Max()).ToArray();
        string Rand(string[] c) => string.Join("  ", c.Select((x, i) => i == 0 ? x.PadRight(latimi[i]) : x.PadLeft(latimi[i])));
        return string.Join("\n", new[] { Rand(cap), new string('-', latimi.Sum() + 2 * (latimi.Length - 1)) }.Concat(randuri.Select(Rand)));
    }

    public bool Trece(Optiuni o) => ProcentErori <= o.PragErori && !(Percentila(95) > o.PragP95);

    public string Rand(Optiuni o)
    {
        var coduri = Apeluri.GroupBy(a => a.Status == 0 ? a.Eroare! : a.Status.ToString()).OrderBy(g => g.Key)
            .Select(g => $"{g.Key}×{g.Count()}");
        var verdict = Trece(o) ? "ok" : $"FAIL ({(ProcentErori > o.PragErori ? $"errors {ProcentErori:0.#} % > {o.PragErori:0.##} %" : $"p95 > {o.PragP95} ms")})";
        return $"{Cereri,6} calls  {Durata.TotalSeconds,6:0.00} s  {Cereri / Durata.TotalSeconds,7:0.0}/s  "
            + $"p50 {Percentila(50),6:0} ms  p95 {Percentila(95),6:0} ms  p99 {Percentila(99),6:0} ms  max {Percentila(100),6:0} ms  "
            + $"[{string.Join(" ", coduri)}]  {verdict}";
    }
}

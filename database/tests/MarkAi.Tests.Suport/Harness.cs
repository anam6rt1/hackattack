using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MarkAi.Api.Infrastructura;
using MarkAi.Api.Servicii;
using MarkAi.Api.Servicii.Mesagerie;
using MarkAi.Api.Setari;
using MarkAi.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace MarkAi.Tests.Suport;

/// <summary>
/// One isolated run of the real API: its own copy of the seeded test database (<see cref="BazaTest"/>), the pipeline from
/// <see cref="AplicatieMarkAi"/> on Kestrel (a free local port, so body-size limits, forwarded headers and SignalR behave as in
/// production), with fakes for email, SMS, the clock, the AI model and Twilio's REST API. Nothing reaches the real database,
/// AI providers, Twilio or SMTP: their settings are removed from the process first. Background jobs do not run by themselves;
/// a test runs one with <see cref="Serviciu{T}"/>().RuleazaOdataAsync, so its effects happen at a known point.
/// Harnesses run in parallel; only the Twilio ones (which need process-wide credentials) run alone.
/// </summary>
public sealed class Harness : IAsyncDisposable
{
    public const string ParolaTest = Fixturi.ParolaTest;
    /// <summary>Id of the fixture organisation "Liceul de Test" (approved, admin: director).</summary>
    public const int IdLiceu = Fixturi.IdLiceu;

    public WebApplication App { get; }
    public string Adresa { get; }
    public FakeMail Mail { get; }
    public FakeTimeProvider Ceas { get; }
    public FakeAi Ai { get; }
    public FakeTwilio Twilio { get; }
    /// <summary>Every email the app sent through its mail provider (codes, warnings, notifications).</summary>
    public FakeEmail Emailuri { get; }
    public FakeSms Sms { get; }
    /// <summary>Every log line the app wrote (Information and above), to check what is never logged.</summary>
    public JurnalCapturat Jurnal { get; }
    public string DirStocare { get; }

    private readonly string adminConexiune, numeBaza;
    private readonly bool cuTwilio;
    private readonly List<IDisposable> clienti = [];
    private static int cuTwilioActive;
    private static readonly Microsoft.Extensions.Caching.Memory.MemoryCache CacheEf = new(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());

    private Harness(WebApplication app, string adresa, FakeMail mail, FakeTimeProvider ceas, FakeAi ai, FakeTwilio twilio, JurnalCapturat jurnal,
        FakeEmail emailuri, FakeSms sms, string adminConexiune, string numeBaza, string dirStocare, bool cuTwilio)
        => (App, Adresa, Mail, Ceas, Ai, Twilio, Jurnal, Emailuri, Sms, this.adminConexiune, this.numeBaza, DirStocare, this.cuTwilio)
            = (app, adresa, mail, ceas, ai, twilio, jurnal, emailuri, sms, adminConexiune, numeBaza, dirStocare, cuTwilio);

    public T Serviciu<T>() where T : notnull => App.Services.GetRequiredService<T>();

    /// <summary>Runs a query or change directly on the test database.</summary>
    public async Task<T> Db<T>(Func<MarkAiDbContext, Task<T>> f)
    {
        using var scope = App.Services.CreateScope();
        return await f(scope.ServiceProvider.GetRequiredService<MarkAiDbContext>());
    }

    /// <summary>Runs a change directly on the test database.</summary>
    public Task Db(Func<MarkAiDbContext, Task> f) => Db(async db => { await f(db); return 0; });

    /// <summary>A test-only endpoint that fails with an internal message, to check that clients never see it.</summary>
    public const string RutaEroareInterna = "/__fals/eroare-interna";
    public const string MesajIntern = "Sequence contains no elements (detaliu intern)";

    // Settings the API reads from the environment; markai.env may be loaded in the shell that runs the tests.
    private static readonly string[] VariabileReale =
        ["MARKAI_CONNECTION_STRING", "MARKAI_JWT_KEY", "GEMINI_API_KEY", "ANTHROPIC_API_KEY", "TWILIO_ACCOUNT_SID", "TWILIO_AUTH_TOKEN"];
    private static readonly string[] PrefixeReale = ["Twilio__", "SetariEmail__", "Autentificare__", "Gemini__", "Claude__", "ConnectionStrings__", "Limite__", "Fisiere__", "Email__", "Sms__"];
    private static readonly Lazy<bool> MediuCurat = new(() =>
    {
        // No file watchers on appsettings.json: a watcher outlives its app and keeps it (all of it) in memory.
        Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");
        foreach (var v in VariabileReale) Environment.SetEnvironmentVariable(v, null);
        foreach (var v in Environment.GetEnvironmentVariables().Keys.Cast<string>().Where(k => PrefixeReale.Any(k.StartsWith)).ToList())
            Environment.SetEnvironmentVariable(v, null);
        return true;
    });

    /// <param name="ceasReal">Use the system clock and real background jobs (manual preview) instead of the fake ones.</param>
    /// <param name="cuTwilio">Configure Twilio with fake credentials, so outbound calls reach <see cref="FakeTwilio"/>. The Twilio
    /// credentials are process-wide environment variables, so such a harness must not run beside others (collection "Twilio").</param>
    /// <param name="setari">Extra configuration for this run (e.g. relaxed limits), applied last.</param>
    /// <param name="port">A fixed port (the manual preview); 0 = a free one.</param>
    /// <param name="faraAi">No AI model, as on a server without an AI key.</param>
    public static async Task<Harness> Porneste(bool ceasReal = false, bool cuTwilio = false, IReadOnlyDictionary<string, string?>? setari = null, int port = 0,
        bool faraAi = false, (string Conexiune, string Director)? existenta = null, Action<IServiceCollection>? configureazaServicii = null)
    {
        _ = MediuCurat.Value;
        if (cuTwilio && Interlocked.Increment(ref cuTwilioActive) == 1)
        {
            Environment.SetEnvironmentVariable("TWILIO_ACCOUNT_SID", FakeTwilio.Sid);
            Environment.SetEnvironmentVariable("TWILIO_AUTH_TOKEN", FakeTwilio.Token);
        }

        var (conexiune, adminConexiune, numeBaza) = existenta is { } veche
            ? (veche.Conexiune, "", "") : await BazaTest.CopieNoua();
        var dirStocare = existenta?.Director ?? Directory.CreateTempSubdirectory("markai-teste-").FullName;
        try
        {
            // Port 0: Kestrel picks a free port as it binds (a port chosen beforehand could be taken by a parallel test meanwhile).
            var adresa = "http://127.0.0.1:" + port;
            var builder = WebApplication.CreateBuilder();
            builder.Environment.EnvironmentName = "Development";
            builder.WebHost.UseUrls(adresa);
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Information for every logger, as in production; only the console stays quiet (below).
                ["Logging:LogLevel:Default"] = "Information",
                ["Stocare:Director"] = dirStocare,
                ["Twilio:ApiUrl"] = adresa + FakeTwilio.Cale,
                ["Twilio:Numar"] = cuTwilio ? "+15005550006" : "",
                ["Twilio:UrlPublic"] = cuTwilio ? FakeTwilio.UrlPublic : "",
            });
            if (setari is not null) builder.Configuration.AddInMemoryCollection(setari);
            var jurnal = new JurnalCapturat();
            // Only the captured log: the console is one shared, synchronous sink for every parallel app (a failed test prints the
            // captured lines instead). The manual preview keeps the console.
            if (!ceasReal) builder.Logging.ClearProviders();
            builder.Logging.AddProvider(jurnal);
            builder.Logging.AddFilter<Microsoft.Extensions.Logging.Console.ConsoleLoggerProvider>(null, LogLevel.Warning);
            AplicatieMarkAi.AdaugaServicii(builder, conexiune);
            // EF keeps one internal service provider (with the model and the compiled queries) per distinct logger factory and
            // memory cache, in a static cache that is never emptied. Each app has its own of both, so every test would rebuild
            // the model and leak it (about 3 MB per test). Shared ones make it one provider for the whole run; EF's own log
            // lines (SQL) are not needed by any test.
            builder.Services.ConfigureDbContext<MarkAiDbContext>(o => o.UseMemoryCache(CacheEf)
                .UseLoggerFactory(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance));
            var mail = new FakeMail(); var ceas = new FakeTimeProvider(DateTimeOffset.UtcNow); var ai = new FakeAi(); var twilio = new FakeTwilio();
            builder.Services.AddSingleton<IEmailConfirmare>(mail);
            builder.Services.AddSingleton<TimeProvider>(ceasReal ? TimeProvider.System : ceas);
            builder.Services.AddSingleton(new ModelAi(faraAi ? null : ai));
            var emailuri = new FakeEmail(); var sms = new FakeSms();
            // The manual preview keeps the app's own development provider, so codes show on the admin "Mesaje" page.
            if (!ceasReal)
            {
                builder.Services.AddSingleton<IExpeditorEmail>(emailuri);
                builder.Services.AddSingleton<IExpeditorSms>(sms);
            }
            // The manual preview runs the background jobs like the real app (file text extraction, AI queue, mail); tests run them on demand.
            if (!ceasReal) builder.Services.RemoveAll<IHostedService>();
            // The fake Twilio API is served by this same app, behind its CSRF check, which the real Twilio API does not have.
            builder.Services.AddHttpClient("twilio").AddHttpMessageHandler(() => new AntetCsrf());
            configureazaServicii?.Invoke(builder.Services);
            var app = builder.Build();
            AplicatieMarkAi.Configureaza(app);
            var middleware = CaptureazaMiddleware(app);
            twilio.Mapeaza(app);
            app.MapGet(RutaEroareInterna, () => { throw new InvalidOperationException(MesajIntern); });
            await app.StartAsync();
            if (port == 0)
            {
                adresa = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>().Features
                    .Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()!.Addresses.Single();
                // Read at each call, so the fake Twilio API's real address can be set once it is known.
                app.Configuration["Twilio:ApiUrl"] = adresa + FakeTwilio.Cale;
            }
            return new Harness(app, adresa, mail, ceas, ai, twilio, jurnal, emailuri, sms, adminConexiune, numeBaza, dirStocare, cuTwilio) { middleware = middleware };
        }
        catch
        {
            if (cuTwilio) Interlocked.Decrement(ref cuTwilioActive);
            if (numeBaza.Length > 0) { await BazaTest.Sterge(adminConexiune, numeBaza); Directory.Delete(dirStocare, true); }
            throw;
        }
    }

    /// <summary>A public platform Lecție (no organisation) created by the admin client; returns its id.</summary>
    public static async Task<int> LectiePlatforma(HttpClient admin, int materieId = 1, string titlu = "Lecție de test", string clasa = "Bac")
    {
        var r = await admin.PostAsJsonAsync("/parcursuri", new { materieId, titlu, clasa, vizibilitate = "public", organizatieId = (int?)null });
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    /// <summary>A chapter with one material in Lecție <paramref name="parcursId"/>; returns its id.</summary>
    public static async Task<int> Capitol(HttpClient autor, int parcursId, string titlu = "Luceafărul", string text = "„Luceafărul” de Mihai Eminescu a apărut în 1883.")
    {
        var r = await autor.PostAsJsonAsync("/cursuri", new
        {
            parcursId, titlu, capitol = "Romantismul", materiale = new[] { new { titlu = "Despre poem", text } },
        });
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    /// <summary>A signed-out browser: keeps cookies and sends the X-MarkAi header like the site does.</summary>
    public HttpClient Anonim() => Anonim(new CookieContainer());

    /// <summary>A client without cookies or the CSRF header (a cross-site form, a script).</summary>
    public HttpClient Brut() => Pastreaza(new HttpClient { BaseAddress = new Uri(Adresa) });

    private HttpClient Anonim(CookieContainer cookies)
    {
        var client = new HttpClient(new HttpClientHandler { CookieContainer = cookies }) { BaseAddress = new Uri(Adresa) };
        client.DefaultRequestHeaders.Add(Securitate.AntetCsrf, "1");
        return Pastreaza(client);
    }

    private HttpClient Pastreaza(HttpClient c) { lock (clienti) clienti.Add(c); return c; }

    /// <summary>A client holding the session cookie of <paramref name="username"/>.</summary>
    public async Task<HttpClient> Login(string username, string parola = ParolaTest) => (await LoginCuCookie(username, parola)).Client;

    /// <summary>Registers a student and confirms the email with the code from the fake mailbox; returns the signed-in client.</summary>
    public async Task<HttpClient> InregistreazaElev(string username, string? telefon = null)
    {
        var client = Anonim();
        var email = username + "@example.invalid";
        (await client.PostAsJsonAsync("/autentificare/inregistrare", new { nume = username + " Test", username, email, parola = ParolaTest })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/autentificare/confirma-email", new { email, cod = Emailuri.UltimulCod(email) })).EnsureSuccessStatusCode();
        if (telefon is not null) await VerificaTelefon(client, telefon);
        return client;
    }

    /// <summary>Verifies a phone number for the signed-in client with the code from the fake SMS provider.</summary>
    public async Task VerificaTelefon(HttpClient client, string telefon)
    {
        (await client.PostAsJsonAsync("/utilizatori/me/setari/telefon/cod", new { telefon })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/utilizatori/me/setari/telefon/confirma", new { telefon, cod = Sms.UltimulCod() })).EnsureSuccessStatusCode();
    }

    /// <summary>Like <see cref="Login"/>, also returning the cookies (a SignalR connection needs them).</summary>
    public async Task<(HttpClient Client, CookieContainer Cookies)> LoginCuCookie(string username, string parola = ParolaTest)
    {
        var cookies = new CookieContainer();
        var client = Anonim(cookies);
        var r = await client.PostAsJsonAsync("/autentificare", new { identificator = username, parola });
        r.EnsureSuccessStatusCode(); return (client, cookies);
    }

    /// <summary>A Triviador hub connection (not started) carrying these cookies.</summary>
    public HubConnection Hub(CookieContainer cookies) =>
        new HubConnectionBuilder().WithUrl(Adresa + "/hub/joc", o => o.Cookies = cookies).Build();

    private bool oprit;

    public async ValueTask DisposeAsync()
    {
        if (oprit) return;
        oprit = true;
        lock (clienti) foreach (var c in clienti) c.Dispose();
        // The app does not own its configuration; disposing it releases the providers (and anything they still hold).
        var configurare = App.Configuration as IDisposable;
        await App.StopAsync();
        OpresteLimitatoare();
        await App.DisposeAsync();
        configurare?.Dispose();
        // A Twilio test may start a second Twilio harness; the credentials stay until the last one stops.
        if (cuTwilio && Interlocked.Decrement(ref cuTwilioActive) == 0)
        {
            Environment.SetEnvironmentVariable("TWILIO_ACCOUNT_SID", null);
            Environment.SetEnvironmentVariable("TWILIO_AUTH_TOKEN", null);
        }
        // Only this database's pooled connections; other harnesses keep theirs.
        using (var c = new NpgsqlConnection(Conexiune)) NpgsqlConnection.ClearPool(c);
        if (numeBaza.Length > 0)
        {
            await BazaTest.Sterge(adminConexiune, numeBaza);
            try { Directory.Delete(DirStocare, true); } catch (DirectoryNotFoundException) { }
        }
    }

    private List<object> middleware = [];

    /// <summary>
    /// The middleware instances of the app's pipeline, captured as it is built. ASP.NET's rate-limiting middleware makes its own
    /// limiter with a repeating timer and never disposes it; the timer then keeps the whole stopped app in memory (and waking
    /// up). One app per process does not mind; hundreds of test apps in one process do, so <see cref="DisposeAsync"/> stops them.
    /// </summary>
    private static List<object> CaptureazaMiddleware(WebApplication app)
    {
        const System.Reflection.BindingFlags Ascuns = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var capturate = new List<object>();
        var constructor = (IApplicationBuilder)typeof(WebApplication).GetProperty("ApplicationBuilder", Ascuns)!.GetValue(app)!;
        var componente = (IList<Func<RequestDelegate, RequestDelegate>>)constructor.GetType().GetField("_components", Ascuns)!.GetValue(constructor)!;
        for (var i = 0; i < componente.Count; i++)
        {
            var original = componente[i];
            componente[i] = next =>
            {
                var d = original(next);
                if (d.Target is { } instanta) lock (capturate) capturate.Add(instanta);
                return d;
            };
        }
        return capturate;
    }

    private void OpresteLimitatoare()
    {
        const System.Reflection.BindingFlags Ascuns = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        lock (middleware)
            foreach (var m in middleware.Where(m => m.GetType().Name == "RateLimitingMiddleware"))
                foreach (var camp in m.GetType().GetFields(Ascuns).Where(f => typeof(IAsyncDisposable).IsAssignableFrom(f.FieldType)))
                    (camp.GetValue(m) as IDisposable)?.Dispose();
    }

    private string Conexiune => new NpgsqlConnectionStringBuilder(adminConexiune) { Database = numeBaza }.ConnectionString;
}

sealed class AntetCsrf : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage cerere, CancellationToken ct)
    {
        cerere.Headers.Add(Securitate.AntetCsrf, "1");
        return base.SendAsync(cerere, ct);
    }
}

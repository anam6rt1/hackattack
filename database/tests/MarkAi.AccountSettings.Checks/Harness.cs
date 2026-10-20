using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MarkAi.Api.Infrastructura;
using MarkAi.Api.Servicii;
using MarkAi.Api.Servicii.Mesagerie;
using MarkAi.Api.Setari;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

/// <summary>
/// One isolated run of the real API: a throwaway PostgreSQL database (migrated and seeded with fixtures), the
/// pipeline from <see cref="AplicatieMarkAi"/> with fakes for email, the clock, the AI model and Twilio's REST API,
/// and helpers to log in and check. Nothing reaches the real database, AI providers, Twilio or SMTP: their settings
/// are removed from the process first. Background jobs do not run by themselves; a check runs one with
/// <see cref="Serviciu{T}"/>().RuleazaOdataAsync, so its effects happen at a known point.
/// </summary>
public sealed class Harness : IAsyncDisposable
{
    public const string ParolaTest = "Test-demo-2026!";
    public const string Adresa = "http://127.0.0.1:5081";

    public WebApplication App { get; }
    public FakeMail Mail { get; }
    public FakeTimeProvider Ceas { get; }
    public FakeAi Ai { get; }
    public FakeTwilio Twilio { get; }
    /// <summary>Every email the app sent through its mail provider (codes, warnings, notifications).</summary>
    public FakeEmail Emailuri { get; }
    public FakeSms Sms { get; }
    /// <summary>Every log line the app wrote (Information and above), to check what is never logged.</summary>
    public JurnalCapturat Jurnal { get; }
    public int Numar { get; private set; }

    private readonly string adminConexiune, numeBaza, dirStocare;

    private Harness(WebApplication app, FakeMail mail, FakeTimeProvider ceas, FakeAi ai, FakeTwilio twilio, JurnalCapturat jurnal,
        FakeEmail emailuri, FakeSms sms, string adminConexiune, string numeBaza, string dirStocare)
        => (App, Mail, Ceas, Ai, Twilio, Jurnal, Emailuri, Sms, this.adminConexiune, this.numeBaza, this.dirStocare)
            = (app, mail, ceas, ai, twilio, jurnal, emailuri, sms, adminConexiune, numeBaza, dirStocare);

    public T Serviciu<T>() where T : notnull => App.Services.GetRequiredService<T>();

    /// <summary>Runs a query or change directly on the test database.</summary>
    public async Task<T> Db<T>(Func<MarkAiDbContext, Task<T>> f)
    {
        using var scope = App.Services.CreateScope();
        return await f(scope.ServiceProvider.GetRequiredService<MarkAiDbContext>());
    }

    /// <summary>A test-only endpoint that fails with an internal message, to check that clients never see it.</summary>
    public const string RutaEroareInterna = "/__fals/eroare-interna";
    public const string MesajIntern = "Sequence contains no elements (detaliu intern)";

    // Settings the API reads from the environment; markai.env may be loaded in the shell that runs the checks.
    private static readonly string[] VariabileReale =
        ["MARKAI_CONNECTION_STRING", "MARKAI_JWT_KEY", "GEMINI_API_KEY", "ANTHROPIC_API_KEY", "TWILIO_ACCOUNT_SID", "TWILIO_AUTH_TOKEN"];
    private static readonly string[] PrefixeReale = ["Twilio__", "SetariEmail__", "Autentificare__", "Gemini__", "Claude__", "ConnectionStrings__"];

    /// <param name="ceasReal">Use the system clock (manual preview) instead of the fake one.</param>
    /// <param name="cuTwilio">Configure Twilio with fake credentials, so outbound calls reach <see cref="FakeTwilio"/>.</param>
    /// <param name="setari">Extra configuration for this run (e.g. relaxed limits), applied last.</param>
    public static async Task<Harness> Porneste(bool ceasReal = false, bool cuTwilio = false, IReadOnlyDictionary<string, string?>? setari = null)
    {
        var adminConexiune = Environment.GetEnvironmentVariable("MARKAI_TEST_CONNECTION")
            ?? throw new Exception("Set MARKAI_TEST_CONNECTION to a test PostgreSQL server with CREATEDB permission.");
        foreach (var v in VariabileReale) Environment.SetEnvironmentVariable(v, null);
        foreach (var v in Environment.GetEnvironmentVariables().Keys.Cast<string>().Where(k => PrefixeReale.Any(k.StartsWith)).ToList())
            Environment.SetEnvironmentVariable(v, null);
        if (cuTwilio)
        {
            Environment.SetEnvironmentVariable("TWILIO_ACCOUNT_SID", FakeTwilio.Sid);
            Environment.SetEnvironmentVariable("TWILIO_AUTH_TOKEN", FakeTwilio.Token);
        }

        var numeBaza = "markai_checks_" + Guid.NewGuid().ToString("N");
        await using (var admin = new NpgsqlConnection(adminConexiune))
        {
            await admin.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE \"{numeBaza}\"", admin).ExecuteNonQueryAsync();
        }
        var conexiune = new NpgsqlConnectionStringBuilder(adminConexiune) { Database = numeBaza }.ConnectionString;
        var dirStocare = Directory.CreateTempSubdirectory("markai-checks-").FullName;
        try
        {
            await Populeaza(conexiune);

            var builder = WebApplication.CreateBuilder();
            builder.Environment.EnvironmentName = "Development";
            builder.WebHost.UseUrls(Adresa);
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Information for every logger, as in production; only the console stays quiet (below).
                ["Logging:LogLevel:Default"] = "Information",
                ["Stocare:Director"] = dirStocare,
                ["Twilio:ApiUrl"] = Adresa + FakeTwilio.Cale,
                ["Twilio:Numar"] = cuTwilio ? "+15005550006" : "",
                ["Twilio:UrlPublic"] = cuTwilio ? FakeTwilio.UrlPublic : "",
            });
            if (setari is not null) builder.Configuration.AddInMemoryCollection(setari);
            var jurnal = new JurnalCapturat();
            builder.Logging.AddProvider(jurnal);
            builder.Logging.AddFilter<Microsoft.Extensions.Logging.Console.ConsoleLoggerProvider>(null, LogLevel.Warning);
            AplicatieMarkAi.AdaugaServicii(builder, conexiune);
            var mail = new FakeMail(); var ceas = new FakeTimeProvider(DateTimeOffset.UtcNow); var ai = new FakeAi(); var twilio = new FakeTwilio();
            builder.Services.AddSingleton<IEmailConfirmare>(mail);
            builder.Services.AddSingleton<TimeProvider>(ceasReal ? TimeProvider.System : ceas);
            builder.Services.AddSingleton(new ModelAi(ai));
            var emailuri = new FakeEmail(); var sms = new FakeSms();
            // The manual preview keeps the app's own development provider, so codes show on the admin "Mesaje" page.
            if (!ceasReal)
            {
                builder.Services.AddSingleton<IExpeditorEmail>(emailuri);
                builder.Services.AddSingleton<IExpeditorSms>(sms);
            }
            builder.Services.RemoveAll<IHostedService>();
            // The fake Twilio API is served by this same app, behind its CSRF check, which the real Twilio API does not have.
            builder.Services.AddHttpClient("twilio").AddHttpMessageHandler(() => new AntetCsrf());
            var app = builder.Build();
            AplicatieMarkAi.Configureaza(app);
            twilio.Mapeaza(app);
            app.MapGet(RutaEroareInterna, () => { throw new InvalidOperationException(MesajIntern); });
            await app.StartAsync();
            return new Harness(app, mail, ceas, ai, twilio, jurnal, emailuri, sms, adminConexiune, numeBaza, dirStocare);
        }
        catch
        {
            await StergeBaza(adminConexiune, numeBaza);
            Directory.Delete(dirStocare, true);
            throw;
        }
    }

    // Fixtures: the three roles, three subjects, and student george, approved teacher irina, administrator admin.
    private static async Task Populeaza(string conexiune)
    {
        var optiuni = new DbContextOptionsBuilder<MarkAiDbContext>()
            .UseNpgsql(conexiune, x => x.MigrationsHistoryTable("__EFMigrationsHistory", "mark_ai")).Options;
        await using var db = new MarkAiDbContext(optiuni);
        await db.Database.MigrateAsync();
        var roles = new[] { new Rol { Nume = "elev" }, new Rol { Nume = "profesor" }, new Rol { Nume = "admin" } };
        var subjects = new[] { new Materie { Denumire = "Limba și literatura română" }, new Materie { Denumire = "Matematică" }, new Materie { Denumire = "Biologie" } };
        db.AddRange(roles); db.AddRange(subjects); await db.SaveChangesAsync();
        foreach (var (username, fullName, role) in new[] { ("george", "George Pavel", 0), ("irina", "Irina Marin", 1), ("admin", "Administrator", 2) })
        {
            var u = new Utilizator { Nume = fullName, Username = username, Email = username + "@example.invalid", Telefon = role == 1 ? "+40722123456" : null,
                EmailConfirmatLa = DateTime.UtcNow };
            u.ParolaHash = Parole.Hash(u, ParolaTest);
            u.Roluri.Add(new UtilizatorRol { RolId = roles[role].Id });
            if (role == 1) u.ProfilMentor = new ProfilMentor { StareVerificare = "aprobat", ActivatLa = DateTime.UtcNow, Descriere = "Profesor de limba română, cu 8 ani de experiență. Învățăm cu răbdare și exemple clare.", PozaUrl = "fixture-photo.png", DovadaCalificareUrl = "fixture-diploma.pdf", Materii = new List<MentorMaterie> { new() { MaterieId = subjects[0].Id } } };
            db.Add(u);
        }
        await db.SaveChangesAsync();

        // A few SIRUTA rows (the full register takes seconds to load): Bucharest with two sectors, Alba with two localities.
        db.AddRange(new Judet { Id = 40, Nume = "București" }, new Judet { Id = 1, Nume = "Alba" });
        db.AddRange(
            new Localitate { Id = 179141, JudetId = 40, Nume = "Sectorul 1", UatId = 179132, UatNume = "Municipiul București", Urban = true, Cautare = "sectorul 1" },
            new Localitate { Id = 179150, JudetId = 40, Nume = "Sectorul 2", UatId = 179132, UatNume = "Municipiul București", Urban = true, Cautare = "sectorul 2" },
            new Localitate { Id = 1026, JudetId = 1, Nume = "Alba Iulia", UatId = 1017, UatNume = "Municipiul Alba Iulia", Urban = true, Cautare = "alba iulia" },
            new Localitate { Id = 1035, JudetId = 1, Nume = "Bărăbanț", UatId = 1017, UatNume = "Municipiul Alba Iulia", Urban = true, Cautare = "barabant" });
        // An approved organisation run by "director" (organisation admin and professor), with irina as an approved professor.
        var director = new Utilizator { Nume = "Dana Director", Username = "director", Email = "director@example.invalid", Telefon = "+40722000111",
            EmailConfirmatLa = DateTime.UtcNow, JudetId = 40 };
        director.ParolaHash = Parole.Hash(director, ParolaTest);
        director.Roluri.Add(new UtilizatorRol { Rol = await db.Roluri.SingleAsync(r => r.Nume == "organizatie") });
        director.Roluri.Add(new UtilizatorRol { RolId = roles[1].Id });
        director.ProfilMentor = new ProfilMentor { StareVerificare = "aprobat", ActivatLa = DateTime.UtcNow, Descriere = "Directoarea liceului." };
        db.Add(director);
        await db.SaveChangesAsync();
        var liceu = new Organizatie { Nume = "Liceul de Test", Adresa = "Strada Școlii 1", JudetId = 40, LocalitateId = 179141, Stare = Organizatie.Aprobata,
            AdminId = director.Id, CreatLa = DateTime.UtcNow, DecisLa = DateTime.UtcNow };
        db.Add(liceu);
        await db.SaveChangesAsync();
        var irina = await db.Utilizatori.SingleAsync(u => u.Username == "irina");
        db.AddRange(
            new MembruOrganizatie { OrganizatieId = liceu.Id, UtilizatorId = director.Id, Rol = MembruOrganizatie.RolProfesor, Stare = MembruOrganizatie.Aprobat, CerutLa = DateTime.UtcNow },
            new MembruOrganizatie { OrganizatieId = liceu.Id, UtilizatorId = irina.Id, Rol = MembruOrganizatie.RolProfesor, Stare = MembruOrganizatie.Aprobat, CerutLa = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    /// <summary>Id of the fixture organisation "Liceul de Test" (approved, admin: director).</summary>
    public const int IdLiceu = 1;

    /// <summary>A public platform Lecție (no organisation) created by the admin client; returns its id.</summary>
    public static async Task<int> LectiePlatforma(HttpClient admin, int materieId = 1, string titlu = "Lecție de test", string clasa = "Bac")
    {
        var r = await admin.PostAsJsonAsync("/parcursuri", new { materieId, titlu, clasa, vizibilitate = "public", organizatieId = (int?)null });
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    public void Check(bool condition, string label)
    {
        if (!condition) throw new Exception("FAIL: " + label);
        Console.WriteLine("PASS: " + label); Numar++;
    }

    /// <summary>A signed-out browser: keeps cookies and sends the X-MarkAi header like the site does.</summary>
    public HttpClient Anonim() => Anonim(new CookieContainer());

    private static HttpClient Anonim(CookieContainer cookies)
    {
        var client = new HttpClient(new HttpClientHandler { CookieContainer = cookies }) { BaseAddress = new Uri(Adresa) };
        client.DefaultRequestHeaders.Add(Securitate.AntetCsrf, "1");
        return client;
    }

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

    public async ValueTask DisposeAsync()
    {
        await App.StopAsync(); await App.DisposeAsync();
        await StergeBaza(adminConexiune, numeBaza);
        Directory.Delete(dirStocare, true);
    }

    private static async Task StergeBaza(string adminConexiune, string numeBaza)
    {
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(adminConexiune); await admin.OpenAsync();
        await new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{numeBaza}\" WITH (FORCE)", admin).ExecuteNonQueryAsync();
    }
}

public sealed class FakeMail : IEmailConfirmare
{
    public string Code = "", Email = ""; public bool Fail;
    public Task Trimite(string email, string cod, string actiune, CancellationToken ct)
    { if (Fail) throw new EmailNeconfiguratException(); Email = email; Code = cod; return Task.CompletedTask; }
}

/// <summary>The AI model: answers with <see cref="Raspunde"/> (default: an empty JSON object) and records every call.</summary>
public sealed class FakeAi : IModelAi
{
    public string Nume => "Fake";
    public Func<ScopAi, string, IReadOnlyList<MesajAi>, string> Raspunde { get; set; } = (_, _, _) => "{}";
    public ConcurrentQueue<(ScopAi Scop, string Sistem, IReadOnlyList<MesajAi> Mesaje)> Apeluri { get; } = new();

    public Task<string> JsonAsync(ScopAi scop, string sistem, IReadOnlyList<MesajAi> mesaje, Dictionary<string, JsonElement> schema,
        int maxTokens, CancellationToken ct)
    {
        Apeluri.Enqueue((scop, sistem, mesaje));
        return Task.FromResult(Raspunde(scop, sistem, mesaje));
    }
}

/// <summary>
/// Twilio's REST API for outbound calls, served by the test app itself under <see cref="Cale"/>.
/// Records each call's form and answers like Twilio (201 with a CallSid), or with <see cref="Eroare"/> when set.
/// </summary>
public sealed class FakeTwilio
{
    public const string Cale = "/__fals/twilio";
    public const string Sid = "ACfa4e000000000000000000000000000";
    public const string Token = "fake-twilio-auth-token";
    public const string UrlPublic = "https://markai.test/api";

    /// <summary>X-Twilio-Signature for a POST to <paramref name="cale"/> (path + query) with this form, as Twilio computes it.</summary>
    public static string Semnatura(string cale, IReadOnlyDictionary<string, string> form)
    {
        var sb = new System.Text.StringBuilder(UrlPublic + cale);
        foreach (var kv in form.OrderBy(k => k.Key, StringComparer.Ordinal)) sb.Append(kv.Key).Append(kv.Value);
        return Convert.ToBase64String(System.Security.Cryptography.HMACSHA1.HashData(
            System.Text.Encoding.UTF8.GetBytes(Token), System.Text.Encoding.UTF8.GetBytes(sb.ToString())));
    }

    public ConcurrentQueue<Dictionary<string, string>> Apeluri { get; } = new();
    public (int Status, int Cod, string Mesaj)? Eroare { get; set; }

    public void Mapeaza(WebApplication app) =>
        app.MapPost(Cale + "/2010-04-01/Accounts/{sid}/Calls.json", async (string sid, HttpRequest req) =>
        {
            var form = await req.ReadFormAsync();
            Apeluri.Enqueue(form.ToDictionary(x => x.Key, x => x.Value.ToString()));
            if (Eroare is var (status, cod, mesaj)) return Results.Json(new { code = cod, message = mesaj, status }, statusCode: status);
            return Results.Json(new { sid = "CA" + Guid.NewGuid().ToString("N"), status = "queued" }, statusCode: 201);
        }).AllowAnonymous();
}

/// <summary>Keeps every formatted log line in memory.</summary>
public sealed class JurnalCapturat : ILoggerProvider
{
    public ConcurrentQueue<string> Linii { get; } = new();
    public ILogger CreateLogger(string categorie) => new Logger(this);
    public void Dispose() { }

    private sealed class Logger(JurnalCapturat jurnal) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel nivel) => true;
        public void Log<TState>(LogLevel nivel, EventId id, TState state, Exception? ex, Func<TState, Exception?, string> format) =>
            jurnal.Linii.Enqueue(format(state, ex) + (ex is null ? "" : " " + ex));
    }
}

sealed class AntetCsrf : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage cerere, CancellationToken ct)
    {
        cerere.Headers.Add(Securitate.AntetCsrf, "1");
        return base.SendAsync(cerere, ct);
    }
}

/// <summary>The mail provider: keeps every email; <see cref="Esueaza"/> makes it fail like an unreachable server.</summary>
public sealed class FakeEmail : IExpeditorEmail
{
    public string Nume => "fals";
    public bool Esueaza { get; set; }
    public ConcurrentQueue<Email> Trimise { get; } = new();

    public Task TrimiteAsync(Email mesaj, CancellationToken ct)
    {
        if (Esueaza) throw new TrimitereEsuata("Serverul de email nu răspunde.");
        Trimise.Enqueue(mesaj);
        return Task.CompletedTask;
    }

    public string UltimulCod(string catre) => System.Text.RegularExpressions.Regex.Match(
        Trimise.Last(m => m.Catre.Equals(catre, StringComparison.OrdinalIgnoreCase)).Text, @"\b\d{6}\b").Value;
}

public sealed class FakeSms : IExpeditorSms
{
    public string Nume => "fals";
    public ConcurrentQueue<Sms> Trimise { get; } = new();

    public Task TrimiteAsync(Sms mesaj, CancellationToken ct) { Trimise.Enqueue(mesaj); return Task.CompletedTask; }

    public string UltimulCod() => System.Text.RegularExpressions.Regex.Match(Trimise.Last().Text, @"\b\d{6}\b").Value;
}

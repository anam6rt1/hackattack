using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using MarkAi.Api.Servicii.Triviador;
using MarkAi.Data.Entities;
using MarkAi.Tests.Suport;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace MarkAi.Tests.Integration.Triviador;

/// <summary>A student with a live connection to the game hub, remembering every state the server sent.</summary>
public sealed class Jucator : IAsyncDisposable
{
    public required HubConnection Hub { get; init; }
    public required HttpClient Http { get; init; }
    public required string Id { get; init; }
    public ConcurrentQueue<string> Mesaje { get; } = new();
    private readonly object _lacat = new();
    private JsonElement _stare;
    public JsonElement Stare { get { lock (_lacat) return _stare; } set { lock (_lacat) _stare = value.Clone(); } }

    public static async Task<Jucator> Conecteaza(Harness h, string username)
    {
        var (http, cookies) = await h.LoginCuCookie(username);
        var id = (await http.GetJson("/utilizatori/me")).Int("id").ToString();
        var hub = h.Hub(cookies);
        var j = new Jucator { Hub = hub, Http = http, Id = id };
        hub.On<JsonElement>("stare", s => { j.Mesaje.Enqueue(s.GetRawText()); j.Stare = s; });
        await hub.StartAsync();
        return j;
    }

    public async Task<JsonElement> Intra(string cod) => Stare = await Hub.InvokeAsync<JsonElement>("Intra", cod);

    public async Task<JsonElement> Asteapta(Func<JsonElement, bool> conditie, string ce)
    {
        for (var i = 0; i < 250; i++)
        {
            var s = Stare;
            if (s.ValueKind == JsonValueKind.Object && conditie(s)) return s;
            await Task.Delay(20);
        }
        var text = Stare.ValueKind == JsonValueKind.Undefined ? "(none)" : Stare.GetRawText();
        throw new ShouldAssertException($"Timed out waiting for: {ce}. Last state: {text[..Math.Min(text.Length, 800)]}");
    }

    /// <summary>The state stays as it is for a moment (an intent that must be ignored).</summary>
    public async Task<JsonElement> Neschimbat() { await Task.Delay(300); return Stare; }

    public async ValueTask DisposeAsync() { await Hub.DisposeAsync(); Http.Dispose(); }
}

/// <summary>
/// Triviador played through real SignalR connections. The subject has a Lecție "Romantismul" with two chapters, six
/// multiple-choice questions (the right option is always <see cref="Bun"/>) and two estimates (1883 and 1920).
/// </summary>
public abstract class TestTriviador : TestCuHarness
{
    protected override IReadOnlyDictionary<string, string?> Setari => new Dictionary<string, string?>
    {
        ["Limite:AutentificariPeMinut"] = "200", ["Limite:InregistrariPeOra"] = "100", ["Limite:EmailuriPeOra"] = "100",
    };

    protected const string Bun = "Răspunsul bun";
    private readonly List<Jucator> jucatori = [];
    protected MotorTriviador Motor => h.Serviciu<MotorTriviador>();

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        using var admin = await h.Login("admin");
        var lectie = await Harness.LectiePlatforma(admin, titlu: "Romantismul");
        foreach (var (titlu, capitol) in new[] { ("Luceafărul", "Romantismul"), ("Ion", "Romanul interbelic") })
            await admin.Post("/cursuri", new { parcursId = lectie, titlu, capitol, materiale = new[] { new { titlu = "Material", text = "„Luceafărul” a apărut în 1883, „Ion” în 1920." } } })
                .Status(HttpStatusCode.Created);
        await h.Db(async db =>
        {
            for (var i = 1; i <= 6; i++)
                db.Add(new Intrebare { MaterieId = 1, ParcursId = lectie, Text = $"Întrebarea {i}",
                    Variante = [new() { Text = "Altă variantă A" }, new() { Text = Bun, Corecta = true }, new() { Text = "Altă variantă B" }, new() { Text = "Altă variantă C" }] });
            db.Add(new Intrebare { MaterieId = 1, ParcursId = lectie, Text = "În ce an a apărut „Luceafărul”?", Tip = Intrebare.Estimare, RaspunsNumeric = 1883 });
            db.Add(new Intrebare { MaterieId = 1, ParcursId = lectie, Text = "În ce an a apărut „Ion”?", Tip = Intrebare.Estimare, RaspunsNumeric = 1920 });
            await db.SaveChangesAsync();
        });
    }

    public override async ValueTask DisposeAsync()
    {
        foreach (var j in jucatori) await j.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>A signed-in student (registered first unless it is a fixture) connected to the hub.</summary>
    protected async Task<Jucator> Conecteaza(string username)
    {
        if (username is not ("george")) (await h.InregistreazaElev(username)).Dispose();
        var j = await Jucator.Conecteaza(h, username);
        jucatori.Add(j);
        return j;
    }

    protected static async Task<string> Creeaza(Jucator gazda, int insule = 9, int maxJucatori = 2, string? clasa = null) =>
        (await gazda.Http.Post("/trivia/camere", new { materieIds = new[] { 1 }, insule, maxJucatori, clasa }).Json(HttpStatusCode.Created)).Str("cod");

    /// <summary>A started two-player game: george (host, first turn) against maria.</summary>
    protected async Task<(Jucator George, Jucator Maria, string Cod, JsonElement Stare)> JocInDoi(int insule = 9)
    {
        var george = await Conecteaza("george");
        var maria = await Conecteaza("maria");
        var cod = await Creeaza(george, insule);
        await george.Intra(cod);
        await maria.Intra(cod);
        await george.Asteapta(x => x.GetProperty("jucatori").GetArrayLength() == 2, "second player seated");
        await george.Hub.InvokeAsync("Porneste");
        var s = await george.Asteapta(x => x.Str("faza") == "alegere", "game started");
        await maria.Asteapta(x => x.Str("faza") == "alegere", "maria sees the start");
        return (george, maria, cod, s);
    }

    protected static int IndexBun(JsonElement s) => s.GetProperty("intrebare").GetProperty("v").EnumerateArray().Select(v => v.GetString()).ToList().IndexOf(Bun);

    protected static List<string> Tinte(JsonElement s) => s.GetProperty("tinte").EnumerateArray().Select(x => x.GetString()!).ToList();

    protected static IEnumerable<string> Vecini(JsonElement s, string insula) => s.GetProperty("harta").GetProperty("poduri").EnumerateArray()
        .Select(p => (p[0].GetString()!, p[1].GetString()!)).Where(p => p.Item1 == insula || p.Item2 == insula)
        .Select(p => p.Item1 == insula ? p.Item2 : p.Item1);

    protected static string Cetate(JsonElement s, int loc) => s.GetProperty("jucatori")[loc].Str("cetate");

    protected static int Insule(JsonElement s, string id) =>
        s.GetProperty("proprietari").EnumerateObject().Count(p => p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() == id);

    protected void Avanseaza(TimeSpan cat) => h.Ceas.Advance(cat);

    protected static async Task Refuzat(Func<Task> f) =>
        await Should.ThrowAsync<Exception>(async () =>
        {
            try { await f(); }
            catch (HubException ex) { throw new InvalidOperationException(ex.Message, ex); }
        });
}

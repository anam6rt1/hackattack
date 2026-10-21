using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MarkAi.Api.Servicii;
using MarkAi.Api.Servicii.Triviador;
using MarkAi.Data.Entities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Triviador refereed by the server, played through real SignalR connections: the host is whoever created the room,
/// turns and deadlines are enforced, answers never reach a client before the result, bots play, XP is awarded once.
/// </summary>
static class VerificariTriviador
{
    const string Bun = "Răspunsul bun";

    public static async Task Ruleaza(Harness h)
    {
        await PregatesteIntrebari(h);
        await VerificaHarti(h);
        using var anonim = h.Anonim();
        foreach (var u in new[] { "maria", "ion" }) (await h.InregistreazaElev(u)).Dispose();

        await using var george = await Jucator.Conecteaza(h, "george");
        await using var maria = await Jucator.Conecteaza(h, "maria");
        await using var ion = await Jucator.Conecteaza(h, "ion");

        // The answer pack is gone; rooms are validated and created by the server.
        h.Check((await george.Http.GetAsync("/trivia/intrebari?materieId=1")).StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            "question pack with answers no longer served");
        h.Check((await george.Http.PostAsJsonAsync("/trivia/camere", new { materieIds = new[] { 1 }, insule = 8 })).StatusCode == HttpStatusCode.BadRequest
            && (await george.Http.PostAsJsonAsync("/trivia/camere", new { materieIds = new[] { 1 }, insule = 31 })).StatusCode == HttpStatusCode.BadRequest
            && (await george.Http.PostAsJsonAsync("/trivia/camere", new { materieIds = new[] { 1 }, maxJucatori = 5 })).StatusCode == HttpStatusCode.BadRequest,
            "island count (9–30) and players (2–4) validated");
        var cod = await Creeaza(george, insule: 9, maxJucatori: 3);
        h.Check(cod.Length == 6, "room code made by the server");

        // Lobby: the creator is the host, identities come from the login.
        var s = await george.Intra(cod);
        h.Check(s.Str("faza") == "lobby" && s.Str("gazdaId") == george.Id, "creator is the host");
        await maria.Intra(cod);
        s = await george.Asteapta(x => x.GetProperty("jucatori").GetArrayLength() == 2, "second player seated");
        h.Check(s.GetProperty("harta").GetProperty("insule").EnumerateArray().Any(i => i.Str("nume") == "Romantismul"), "islands named after the subject's Lecții");
        h.Check(await Esueaza(() => maria.Hub.InvokeAsync("Porneste")), "only the host can start");
        h.Check(await Esueaza(() => maria.Hub.InvokeAsync("Trimite", "stare", new { faza = "final" })), "clients can no longer broadcast game state");

        await george.Hub.InvokeAsync("Porneste");
        s = await george.Asteapta(x => x.Str("faza") == "alegere", "game started");
        h.Check(s.GetProperty("jucatori")[0].Str("id") == george.Id && s.Int("tura") == 0, "first seat plays first");
        var cetateMaria = s.GetProperty("jucatori")[1].Str("cetate");

        // Turn and target rules.
        var tinte = s.GetProperty("tinte").EnumerateArray().Select(x => x.GetString()!).ToList();
        await maria.Hub.InvokeAsync("Ataca", tinte[0]);
        await george.Hub.InvokeAsync("Ataca", cetateMaria);
        await Task.Delay(300);
        h.Check(george.Stare.Str("faza") == "alegere", "attack out of turn or on a castle ignored");
        await george.Hub.InvokeAsync("Ataca", tinte[0]);
        s = await george.Asteapta(x => x.Str("faza") == "intrebare", "question open");
        h.Check(s.GetProperty("rezultat").ValueKind == JsonValueKind.Null && !s.GetProperty("intrebare").TryGetProperty("corect", out _),
            "open question carries no answer");
        await maria.Hub.InvokeAsync("Raspunde", 0);
        await Task.Delay(300);
        h.Check(!george.Stare.GetProperty("raspunsuri").TryGetProperty(maria.Id, out _), "only the duel's players can answer");
        var corect = IndexBun(s);
        await george.Hub.InvokeAsync("Raspunde", corect);
        s = await george.Asteapta(x => x.Str("faza") == "rezultat", "result shown");
        h.Check(s.GetProperty("proprietari").Str(tinte[0]) == george.Id && s.GetProperty("rezultat").Int("corect") == corect,
            "correct answer conquers the free island; answer revealed with the result");

        // Deadlines are the server's.
        h.Ceas.Advance(MotorTriviador.PauzaRezultat + TimeSpan.FromMilliseconds(100));
        await maria.Asteapta(x => x.Str("faza") == "alegere" && x.Int("tura") == 1, "next player's turn");
        h.Ceas.Advance(MotorTriviador.TimpAlegere + TimeSpan.FromSeconds(1));
        s = await george.Asteapta(x => x.Int("tura") == 0 && x.Int("runda") == 2, "turn passes after the choosing time");
        h.Check(s.GetProperty("jurnal").EnumerateArray().Any(r => r.Str("text").Contains("n-a ales nimic la timp")), "missed turn logged");

        tinte = s.GetProperty("tinte").EnumerateArray().Select(x => x.GetString()!).ToList();
        await george.Hub.InvokeAsync("Ataca", tinte.First(t => s.GetProperty("proprietari").Str(t) is null));
        s = await george.Asteapta(x => x.Str("faza") == "intrebare", "second question");
        h.Ceas.Advance(MotorTriviador.TimpIntrebare + TimeSpan.FromSeconds(1.1));
        s = await george.Asteapta(x => x.Str("faza") == "rezultat", "question closed by the server's deadline");
        await george.Hub.InvokeAsync("Raspunde", IndexBun(s));
        await Task.Delay(300);
        h.Check(george.Stare.GetProperty("rezultat").GetProperty("alegeri").EnumerateObject().Count() == 0, "answer after the deadline ignored");

        // Duel: both right → estimate; a tie keeps the island with the defender.
        h.Ceas.Advance(MotorTriviador.PauzaRezultat + TimeSpan.FromMilliseconds(100));
        s = await maria.Asteapta(x => x.Str("faza") == "alegere" && x.Int("tura") == 1, "maria's turn");
        var vecin = Vecini(s, cetateMaria).First(v => v != s.GetProperty("jucatori")[0].Str("cetate"));
        await h.Serviciu<MotorTriviador>().PentruVerificariAsync(cod, insula: vecin, proprietar: george.Id);
        await maria.Hub.InvokeAsync("Ataca", vecin);
        s = await maria.Asteapta(x => x.Str("faza") == "intrebare", "duel question");
        h.Check(s.GetProperty("atac").Str("aparator") == george.Id, "attacking an owned island is a duel");
        await maria.Hub.InvokeAsync("Raspunde", IndexBun(s));
        await george.Hub.InvokeAsync("Raspunde", IndexBun(s));
        s = await george.Asteapta(x => x.Str("faza") == "rezultat", "duel result");
        h.Check(s.GetProperty("rezultat").Str("text").Contains("estimare"), "both right leads to the estimate");
        h.Ceas.Advance(MotorTriviador.PauzaRezultat + TimeSpan.FromMilliseconds(100));
        s = await george.Asteapta(x => x.Str("faza") == "estimare", "estimate open");
        h.Check(s.GetProperty("intrebare").GetProperty("v").ValueKind == JsonValueKind.Null && s.GetProperty("rezultat").ValueKind == JsonValueKind.Null,
            "open estimate carries no answer");
        await maria.Hub.InvokeAsync("Estimeaza", 1900);
        await george.Hub.InvokeAsync("Estimeaza", 1900);
        s = await george.Asteapta(x => x.Str("faza") == "rezultat", "estimate result");
        h.Check(s.GetProperty("rezultat").Str("castigator") == george.Id && s.GetProperty("proprietari").Str(vecin) == george.Id
            && s.GetProperty("rezultat").Int("corect") is 1883 or 1920, "a tie at the estimate keeps the island with the defender");

        // End of the game: XP from the server, once.
        await h.Serviciu<MotorTriviador>().PentruVerificariAsync(cod, runda: s.Int("runde"));
        h.Ceas.Advance(MotorTriviador.PauzaRezultat + TimeSpan.FromSeconds(1));
        s = await george.Asteapta(x => x.Str("faza") == "final", "game over");
        var insule = (string id) => s.GetProperty("proprietari").EnumerateObject().Count(p => p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() == id);
        var max = Math.Max(insule(george.Id), insule(maria.Id));
        var xpGeorge = insule(george.Id) * MotorTriviador.XpInsula + (insule(george.Id) == max ? MotorTriviador.XpCastig : 0);
        h.Check(s.GetProperty("xp").Int(george.Id) == xpGeorge && !s.GetProperty("cuBoti").GetBoolean(), "XP: 10 per island, 50 for the winner");
        var randuri = await h.Db(db => db.EvenimenteRecompensa.Where(e => e.TipEveniment == "triviador").Select(e => new { e.UtilizatorId, e.XpAcordat }).ToListAsync());
        h.Check(randuri.Count(r => r.UtilizatorId.ToString() == george.Id) == 1 && randuri.Single(r => r.UtilizatorId.ToString() == george.Id).XpAcordat == xpGeorge,
            "XP saved once for the game");
        h.Check(await Esueaza(() => maria.Hub.InvokeAsync("JocNou")), "only the host starts a new game");
        await george.Hub.InvokeAsync("JocNou");
        await maria.Asteapta(x => x.Str("faza") == "lobby", "back to the lobby");

        // Bots: one person against a hard bot; the bot plays its turns; XP is 30% lower.
        var codBoti = await Creeaza(ion, insule: 12, maxJucatori: 2);
        await ion.Intra(codBoti);
        h.Check(await Esueaza(() => ion.Hub.InvokeAsync("AdaugaBot", "imposibil")), "unknown bot level refused");
        await ion.Hub.InvokeAsync("AdaugaBot", "greu");
        s = await ion.Asteapta(x => x.GetProperty("jucatori").GetArrayLength() == 2, "bot seated");
        h.Check(s.GetProperty("jucatori")[1].GetProperty("bot").GetBoolean() && s.GetProperty("cuBoti").GetBoolean(), "bot fills a seat");
        h.Check(await Esueaza(() => ion.Hub.InvokeAsync("AdaugaBot", "usor")), "no bot beyond the room's seats");
        await ion.Hub.InvokeAsync("Porneste");
        s = await ion.Asteapta(x => x.Str("faza") == "alegere", "game with a bot started");
        await ion.Hub.InvokeAsync("Ataca", s.GetProperty("tinte")[0].GetString());
        s = await ion.Asteapta(x => x.Str("faza") == "intrebare", "question");
        await ion.Hub.InvokeAsync("Raspunde", IndexBun(s));
        await ion.Asteapta(x => x.Str("faza") == "rezultat", "result");
        h.Ceas.Advance(MotorTriviador.PauzaRezultat + TimeSpan.FromMilliseconds(100));
        await ion.Asteapta(x => x.Str("faza") == "alegere" && x.Int("tura") == 1, "bot's turn");
        h.Ceas.Advance(TimeSpan.FromSeconds(3.1));
        s = await ion.Asteapta(x => x.Str("faza") == "intrebare", "bot chose an island");
        h.Check(s.GetProperty("atac").Str("atacator") == "bot1", "bot attacks on its own");
        h.Ceas.Advance(TimeSpan.FromSeconds(8.1));
        s = await ion.Asteapta(x => x.Str("faza") == "rezultat", "bot answered");
        h.Check(s.GetProperty("rezultat").GetProperty("alegeri").TryGetProperty("bot1", out _), "bot answers within its delay");
        await h.Serviciu<MotorTriviador>().PentruVerificariAsync(codBoti, runda: s.Int("runde"));
        h.Ceas.Advance(MotorTriviador.PauzaRezultat + TimeSpan.FromMilliseconds(100));
        s = await ion.Asteapta(x => x.Str("faza") == "final", "bot game over");
        var insuleIon = s.GetProperty("proprietari").EnumerateObject().Count(p => p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() == ion.Id);
        var insuleBot = s.GetProperty("proprietari").EnumerateObject().Count(p => p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() == "bot1");
        var asteptat = (int)Math.Round((insuleIon * MotorTriviador.XpInsula + (insuleIon >= insuleBot ? MotorTriviador.XpCastig : 0)) * MotorTriviador.FactorBoti);
        h.Check(s.GetProperty("xp").Int(ion.Id) == asteptat && !s.GetProperty("xp").TryGetProperty("bot1", out _), "XP is 30% lower with bots; bots get none");

        // Seats, spectators and a new host when the host leaves.
        var codMic = await Creeaza(maria, insule: 9, maxJucatori: 2);
        await maria.Intra(codMic);
        await george.Intra(codMic);
        await ion.Intra(codMic);
        s = await maria.Asteapta(x => x.GetProperty("spectatori").GetArrayLength() == 1, "third person watches");
        h.Check(s.GetProperty("spectatori")[0].Str("id") == ion.Id, "people beyond the seats are spectators");
        await maria.Hub.StopAsync();
        s = await george.Asteapta(x => x.GetProperty("jucatori").EnumerateArray().All(j => j.Str("id") != maria.Id), "leaving shows at once");
        h.Check(s.Str("gazdaId") == maria.Id, "the host keeps the role during the grace period");
        h.Ceas.Advance(MotorTriviador.GratiePlecare + TimeSpan.FromMilliseconds(100));
        s = await george.Asteapta(x => x.Str("gazdaId") == george.Id, "host handed over");
        h.Check(s.GetProperty("jucatori").GetArrayLength() == 2 && s.GetProperty("spectatori").GetArrayLength() == 0,
            "after the grace period the next person is host and the spectator takes the free seat"
            + (s.GetProperty("jucatori").GetArrayLength() == 2 && s.GetProperty("spectatori").GetArrayLength() == 0 ? "" : ": " + s.GetProperty("jucatori").GetRawText() + " SPEC " + s.GetProperty("spectatori").GetRawText() + " J " + s.GetProperty("jurnal").GetRawText()));
        h.Check(s.GetProperty("jurnal").EnumerateArray().Any(r => r.Str("text").Contains("este acum gazda")), "everyone is told who the new host is");

        // A reload (reconnecting within the grace period) changes nothing.
        await george.Hub.StopAsync();
        await using var georgeDinNou = await Jucator.Conecteaza(h, "george");
        await georgeDinNou.Intra(codMic);
        h.Ceas.Advance(MotorTriviador.GratiePlecare + TimeSpan.FromMilliseconds(100));
        await Task.Delay(300);
        s = georgeDinNou.Stare;
        h.Check(s.Str("gazdaId") == george.Id && !s.GetProperty("jurnal").EnumerateArray().Any(r => r.Str("text").Contains("George Pavel a ieșit")),
            "a reload within the grace period keeps the host and the seat");

        // Host alone with a bot leaves: no person is left, so the room is wiped.
        var codSolo = await Creeaza(ion, insule: 9, maxJucatori: 2);
        await ion.Intra(codSolo);
        await ion.Hub.InvokeAsync("AdaugaBot", "mediu");
        await ion.Asteapta(x => x.Str("cod") == codSolo && x.GetProperty("jucatori").GetArrayLength() == 2, "host and bot in the room");
        await ion.Hub.StopAsync();
        // The server handles the disconnect a moment after StopAsync returns, and only then starts the grace timer
        // on the fake clock; so the clock is moved until the room is gone (or the check gives up).
        var stearsa = false;
        for (var i = 0; i < 20 && !stearsa; i++)
        {
            h.Ceas.Advance(MotorTriviador.GratiePlecare + TimeSpan.FromMilliseconds(100));
            await Task.Delay(150);
            stearsa = !h.Serviciu<MotorTriviador>().Exista(codSolo);
        }
        h.Check(stearsa && await Esueaza(() => georgeDinNou.Hub.InvokeAsync<JsonElement>("Intra", codSolo)), "a room with no person left is wiped");

        // Room limit per host (whatever rooms are still open from above).
        HttpStatusCode refuz = HttpStatusCode.OK;
        for (var i = 0; i < MotorTriviador.MaxCamerePeGazda + 1 && refuz == HttpStatusCode.OK; i++)
        {
            var r = await ion.Http.PostAsJsonAsync("/trivia/camere", new { materieIds = new[] { 1 } });
            if (!r.IsSuccessStatusCode) refuz = r.StatusCode;
        }
        h.Check(refuz == HttpStatusCode.Conflict, "at most 3 open rooms per host");

        // Nothing sent to any player ever had the answer of an open question.
        var toate = new[] { george, maria, ion, georgeDinNou }.SelectMany(j => j.Mesaje).Select(m => JsonDocument.Parse(m).RootElement).ToList();
        h.Check(toate.Count > 20 && toate.Where(m => m.Str("faza") is "intrebare" or "estimare").All(m => m.GetProperty("rezultat").ValueKind == JsonValueKind.Null),
            $"no answer in any of the {toate.Count} states sent during open questions");
    }

    static async Task PregatesteIntrebari(Harness h)
    {
        using var admin = await h.Login("admin");
        var lectie = await Harness.LectiePlatforma(admin, titlu: "Romantismul");
        foreach (var (titlu, capitol) in new[] { ("Luceafărul", "Romantismul"), ("Ion", "Romanul interbelic") })
            (await admin.PostAsJsonAsync("/cursuri", new { parcursId = lectie, titlu, capitol,
                materiale = new[] { new { titlu = "Material", text = "„Luceafărul” a apărut în 1883, „Ion” în 1920." } } })).EnsureSuccessStatusCode();
        h.Ai.Raspunde = (_, _, _) => JsonSerializer.Serialize(new
        {
            grila = Enumerable.Range(1, 6).Select(i => new { text = $"Întrebarea {i}", variante = new[] { "Altă variantă A", Bun, "Altă variantă B", "Altă variantă C" }, corect = 1, explicatie = "-" }),
            estimari = new[] { new { text = "În ce an a apărut „Luceafărul”?", raspuns = 1883 }, new { text = "În ce an a apărut „Ion”?", raspuns = 1920 } },
        });
        (await admin.PostAsJsonAsync($"/parcursuri/{lectie}/genereaza-intrebari", new { numar = 6 })).EnsureSuccessStatusCode();
        await h.Serviciu<GeneratorIntrebari>().RuleazaOdataAsync(CancellationToken.None);
    }

    static Task VerificaHarti(Harness h)
    {
        var bune = Enumerable.Range(GeneratorHarta.MinInsule, GeneratorHarta.MaxInsule - GeneratorHarta.MinInsule + 1).All(n =>
        {
            var harta = GeneratorHarta.Genereaza(n, [], new Random(n));
            var cetati = harta.Cetati(4);
            return harta.Insule.Count == n && harta.Distante(harta.Insule[0].Id).Count == n
                && harta.Insule.Select(i => (i.X, i.Y)).Distinct().Count() == n && cetati.Distinct().Count() == 4;
        });
        h.Check(bune, "every map size 9–30 is connected, with 4 separate castles");
        return Task.CompletedTask;
    }

    static async Task<string> Creeaza(Jucator gazda, int insule, int maxJucatori)
    {
        var r = await gazda.Http.PostAsJsonAsync("/trivia/camere", new { materieIds = new[] { 1 }, insule, maxJucatori });
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).Str("cod");
    }

    static int IndexBun(JsonElement s) => s.GetProperty("intrebare").GetProperty("v").EnumerateArray().Select(v => v.GetString()).ToList().IndexOf(Bun);

    static IEnumerable<string> Vecini(JsonElement s, string insula) => s.GetProperty("harta").GetProperty("poduri").EnumerateArray()
        .Select(p => (p[0].GetString()!, p[1].GetString()!)).Where(p => p.Item1 == insula || p.Item2 == insula)
        .Select(p => p.Item1 == insula ? p.Item2 : p.Item1);

    static async Task<bool> Esueaza(Func<Task> f)
    {
        try { await f(); return false; } catch (HubException) { return true; } catch (InvalidOperationException) { return true; }
    }

    static string Str(this JsonElement e, string p) => e.GetProperty(p).GetString()!;
    static int Int(this JsonElement e, string p) => e.GetProperty(p).GetInt32();

    /// <summary>A student with a live connection to the game hub, remembering every state the server sent.</summary>
    sealed class Jucator : IAsyncDisposable
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
            var id = (await http.GetFromJsonAsync<JsonElement>("/utilizatori/me")).GetProperty("id").GetInt32().ToString();
            var hub = new HubConnectionBuilder().WithUrl(Harness.Adresa + "/hub/joc", o => o.Cookies = cookies).Build();
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
            throw new Exception($"FAIL (timeout): {ce}. Last state: {Stare.GetRawText()[..Math.Min(Stare.GetRawText().Length, 600)]}");
        }

        public async ValueTask DisposeAsync() { await Hub.DisposeAsync(); Http.Dispose(); }
    }
}

using System.Collections.Concurrent;
using MarkAi.Api.Hubs;
using MarkAi.Api.Infrastructura;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Servicii.Triviador;

/// <summary>Clasa: only Lecții of this grade (IX–XII or Bac); null = all. Public/organisation visibility is ignored on purpose.</summary>
public sealed record SetariCamera(IReadOnlyList<int> MaterieIds, int Insule, int MaxJucatori, string? Clasa = null);

public sealed record InsulaPublica(string Id, string Nume, string Autor, int X, int Y, int Seed, string Semn);
public sealed record HartaPublica(int Latime, int Inaltime, IReadOnlyList<InsulaPublica> Insule, IReadOnlyList<string[]> Poduri);
public sealed record JucatorJoc(string Id, string Nume, string Culoare, string? Cetate, bool Bot, string? Dificultate, bool Conectat);
public sealed record IntrebareJoc(int Nr, string T, IReadOnlyList<string>? V, int Durata);
public sealed record AtacJoc(string Atacator, string? Aparator, string Insula);
public sealed record RezultatJoc(string Tip, int Corect, IReadOnlyDictionary<string, int> Alegeri, string? Castigator, string Text);
public sealed record RandJurnal(string Culoare, string Text);

/// <summary>
/// What every player sees. It never contains the correct answer of an open question: <see cref="Rezultat"/> (with the answer)
/// exists only in the "rezultat" phase. <see cref="TermenLa"/> and <see cref="Acum"/> are server times (Unix ms), so a
/// client can show the countdown without trusting its own clock.
/// </summary>
public sealed record StareJoc(string Cod, string Faza, string GazdaId, string Materie, HartaPublica Harta, int MaxJucatori,
    IReadOnlyList<JucatorJoc> Jucatori, IReadOnlyList<JucatorJoc> Spectatori, IReadOnlyDictionary<string, string?> Proprietari,
    int Tura, int Runda, int Runde, IReadOnlyList<RandJurnal> Jurnal, AtacJoc? Atac, IntrebareJoc? Intrebare,
    IReadOnlyDictionary<string, bool> Raspunsuri, RezultatJoc? Rezultat, IReadOnlyList<string> Tinte, long? TermenLa, long Acum,
    IReadOnlyDictionary<string, int>? Xp, bool CuBoti);

/// <summary>
/// Triviador, refereed by the server. Rooms live in memory (one API instance); each has a lock, and every change is
/// broadcast to the room as a <see cref="StareJoc"/>. Players only send intents (attack, answer, estimate), checked here
/// against the turn, phase and deadline. Timers and bots run on <see cref="TimeProvider"/>, so checks can move time.
/// </summary>
public sealed class MotorTriviador(IServiceScopeFactory scopes, IHubContext<JocHub> hub, TimeProvider ceas, ILogger<MotorTriviador> log)
{
    public const int MaxCamere = 200, MaxCamerePeGazda = 3, MinJucatori = 2, MaxJucatoriCamera = 4;
    public static readonly TimeSpan TimpAlegere = TimeSpan.FromSeconds(30), TimpIntrebare = TimeSpan.FromSeconds(15),
        PauzaRezultat = TimeSpan.FromMilliseconds(2800), TimpInchidere = TimeSpan.FromMinutes(10);
    /// <summary>A leave takes effect after this, so a page reload or a short network drop costs neither the seat, the host role nor the room.</summary>
    public static readonly TimeSpan GratiePlecare = TimeSpan.FromSeconds(5);
    private static readonly string[] Culori = ["#e5483b", "#7a52d8", "#e0469a", "#f08a24"];
    private const string Alfabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public const int XpInsula = 10, XpCastig = 50;
    /// <summary>Games with bots give 30% less XP (practice still counts, but playing people is worth more).</summary>
    public const double FactorBoti = 0.7;

    private readonly ConcurrentDictionary<string, Camera> _camere = new();
    private readonly ConcurrentDictionary<string, (string Cod, int UtilizatorId)> _conexiuni = new();

    public static string Grup(string cod) => "joc:" + cod;

    /// <summary>The room a connection is in, if any.</summary>
    public string? CodConexiune(string conexiune) => _conexiuni.TryGetValue(conexiune, out var x) ? x.Cod : null;

    // ---------------------------------------------------------------- rooms

    public async Task<string> CreeazaAsync(int gazdaId, SetariCamera s, CancellationToken ct)
    {
        var materieIds = s.MaterieIds.Distinct().ToList();
        if (materieIds.Count is 0 or > 10) throw new CerereInvalida("Alege între una și zece materii.");
        if (s.Insule is < GeneratorHarta.MinInsule or > GeneratorHarta.MaxInsule)
            throw new CerereInvalida($"Harta are între {GeneratorHarta.MinInsule} și {GeneratorHarta.MaxInsule} de insule.");
        if (s.MaxJucatori is < MinJucatori or > MaxJucatoriCamera) throw new CerereInvalida($"O cameră are între {MinJucatori} și {MaxJucatoriCamera} jucători.");
        var clasa = string.IsNullOrWhiteSpace(s.Clasa) ? null : s.Clasa.Trim();
        if (clasa is not null && !Parcurs.Clase.Contains(clasa)) throw new CerereInvalida("Clasa trebuie să fie IX, X, XI, XII sau Bac.");
        Curata();
        if (_camere.Count >= MaxCamere) throw new Conflict("Sunt prea multe camere deschise acum. Încearcă din nou în câteva minute.");
        if (_camere.Values.Count(c => c.GazdaId == gazdaId) >= MaxCamerePeGazda)
            throw new Conflict($"Ai deja {MaxCamerePeGazda} camere deschise. Folosește una dintre ele sau așteaptă să se închidă.");

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarkAiDbContext>();
        var materii = await db.Materii.AsNoTracking().Where(m => materieIds.Contains(m.Id)).OrderBy(m => m.Ordine)
            .Select(m => new { m.Id, m.Denumire }).ToListAsync(ct);
        if (materii.Count != materieIds.Count) throw new CerereInvalida("Materie inexistentă.");
        // One island per published Lecție of the chosen subjects (and grade); its quiz questions are asked there.
        var lectii = await db.Set<Parcurs>().AsNoTracking()
            .Where(p => materieIds.Contains(p.MaterieId) && p.Publicat && (clasa == null || p.Clasa == clasa))
            .OrderBy(p => p.MaterieId).ThenBy(p => p.Ordine)
            .Select(p => new { p.Id, p.Titlu, p.MaterieId, Capitole = p.Capitole.Count(c => c.Publicat) }).ToListAsync(ct);
        var rnd = new Random();
        var capitole = lectii
            .Select(p => (p.Titlu, materii.Count > 1 ? materii.First(m => m.Id == p.MaterieId).Denumire : $"{p.Capitole} {(p.Capitole == 1 ? "capitol" : "capitole")}",
                new[] { p.Id }))
            .OrderBy(_ => rnd.Next()).ToList();
        var pachet = await PachetIntrebari.IncarcaAsync(db, materieIds, clasa is null ? null : lectii.Select(p => p.Id).ToList(), ct);
        var harta = GeneratorHarta.Genereaza(s.Insule, capitole, rnd);

        while (true)
        {
            var cod = new string(Enumerable.Range(0, 6).Select(_ => Alfabet[rnd.Next(Alfabet.Length)]).ToArray());
            var camera = new Camera(cod, gazdaId, string.Join(", ", materii.Select(m => m.Denumire)) + (clasa switch { null => "", "Bac" => " · Bac", _ => $" · clasa a {clasa}-a" }), s.MaxJucatori, harta, pachet, rnd, Acum());
            if (_camere.TryAdd(cod, camera))
            {
                // Closed if nobody ever joins.
                camera.TimerInchidere = ceas.CreateTimer(_ => _ = InchideDacaGoalaAsync(camera), null, TimpInchidere, Timeout.InfiniteTimeSpan);
                return cod;
            }
        }
    }

    public async Task<StareJoc> IntraAsync(string conexiune, string cod, int utilizatorId, string nume)
    {
        var camera = _camere.GetValueOrDefault((cod ?? "").Trim().ToUpperInvariant())
            ?? throw new HubException("Camera nu există sau s-a închis.");
        await IeseAsync(conexiune); // one room per connection
        return await Executa(camera, () =>
        {
            var nou = !camera.Membri.TryGetValue(utilizatorId, out var membru);
            if (nou) camera.Membri[utilizatorId] = membru = new Membru(nume, Acum());
            membru!.Conexiuni.Add(conexiune);
            _conexiuni[conexiune] = (camera.Cod, utilizatorId);
            // Back within the grace period (a reload, a short drop): as if they never left.
            if (camera.TimerePlecare.Remove(utilizatorId, out var plecare)) plecare.Dispose();
            camera.TimerInchidere?.Dispose(); camera.TimerInchidere = null;
            if (nou) Jurnal(camera, utilizatorId.ToString(), $"{nume} a intrat în cameră");
            else if (membru.Plecat) { membru.Plecat = false; Jurnal(camera, utilizatorId.ToString(), $"{nume} a revenit în cameră"); }
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// A connection closed (tab closed, page left, network lost). The person's last connection starts the grace period;
    /// <see cref="PlecareAsync"/> then applies the leave. Everyone sees at once that the player is offline.
    /// </summary>
    public async Task IeseAsync(string conexiune)
    {
        if (!_conexiuni.TryRemove(conexiune, out var x) || !_camere.TryGetValue(x.Cod, out var camera)) return;
        await camera.Lacat.WaitAsync();
        try
        {
            if (camera.Inchisa || !camera.Membri.TryGetValue(x.UtilizatorId, out var membru)) return;
            membru.Conexiuni.Remove(conexiune);
            if (membru.Conexiuni.Count == 0 && !camera.TimerePlecare.ContainsKey(x.UtilizatorId))
                camera.TimerePlecare[x.UtilizatorId] = ceas.CreateTimer(_ => _ = PlecareAsync(camera, x.UtilizatorId), null, GratiePlecare, Timeout.InfiniteTimeSpan);
            await DupaSchimbareAsync(camera);
        }
        finally { camera.Lacat.Release(); }
    }

    /// <summary>
    /// The grace period is over and the person did not come back. In the lobby their seat is freed (during a game they keep
    /// it and their turns pass). If they were the host, the first person still in the room (seated players first) becomes
    /// host; if no person is left (bots do not count), the room is wiped.
    /// </summary>
    private async Task PlecareAsync(Camera camera, int utilizatorId)
    {
        await camera.Lacat.WaitAsync();
        try
        {
            if (camera.Inchisa || !camera.Membri.TryGetValue(utilizatorId, out var membru) || membru.Conexiuni.Count > 0) return;
            if (camera.TimerePlecare.Remove(utilizatorId, out var t)) t.Dispose();

            var asezati = camera.Jucatori.Select(j => j.Id).ToHashSet();
            var ramasi = camera.Membri.Where(m => m.Value.Conexiuni.Count > 0)
                .OrderBy(m => asezati.Contains(m.Key.ToString()) ? 0 : 1).ThenBy(m => m.Value.IntratLa).ToList();
            // Someone else may also be within their own grace period; the room waits for them.
            if (ramasi.Count == 0 && camera.TimerePlecare.Count == 0) { Sterge(camera); return; }

            if (camera.Faza == "lobby") camera.Membri.Remove(utilizatorId); else membru.Plecat = true;
            Jurnal(camera, utilizatorId.ToString(), $"{membru.Nume} a ieșit din cameră");
            if (camera.GazdaId == utilizatorId && ramasi.Count > 0)
            {
                camera.GazdaId = ramasi[0].Key;
                Jurnal(camera, ramasi[0].Key.ToString(), $"{ramasi[0].Value.Nume} este acum gazda camerei");
            }
            await DupaSchimbareAsync(camera);
        }
        catch (Exception ex) { log.LogError(ex, "Triviador leave failed in room {Cod}", camera.Cod); }
        finally { camera.Lacat.Release(); }
    }

    /// <summary>Closes a room for good: its timers stop and its code no longer works. Called under the room's lock.</summary>
    private void Sterge(Camera camera)
    {
        camera.Inchisa = true;
        AnuleazaTimere(camera);
        camera.TimerInchidere?.Dispose();
        foreach (var t in camera.TimerePlecare.Values) t.Dispose();
        camera.TimerePlecare.Clear();
        _camere.TryRemove(camera.Cod, out _);
        foreach (var c in _conexiuni.Where(c => c.Value.Cod == camera.Cod).Select(c => c.Key).ToList()) _conexiuni.TryRemove(c, out _);
    }

    // A room nobody ever joined (created, then the page was closed).
    private async Task InchideDacaGoalaAsync(Camera camera)
    {
        await camera.Lacat.WaitAsync();
        try { if (!camera.Inchisa && camera.Membri.Values.All(m => m.Conexiuni.Count == 0)) Sterge(camera); }
        finally { camera.Lacat.Release(); }
    }

    // A safety net besides the per-room timers: drops rooms empty for longer than the closing delay.
    private void Curata()
    {
        foreach (var c in _camere.Values.Where(c => c.Goala && Acum() - c.Activitate > (long)TimpInchidere.TotalMilliseconds * 2))
            _camere.TryRemove(c.Cod, out _);
    }

    // ---------------------------------------------------------------- lobby

    public Task AdaugaBotAsync(string conexiune, string dificultate) => CaGazda(conexiune, camera =>
    {
        if (camera.Faza != "lobby") throw new HubException("Boții se adaugă înainte de start.");
        if (!Dificultati.ContainsKey(dificultate)) throw new HubException("Dificultate necunoscută.");
        if (Locuri(camera).Count >= camera.MaxJucatori) throw new HubException("Camera e plină.");
        camera.NumarBoti++;
        camera.Boti.Add(new Bot("bot" + camera.NumarBoti, $"Robot {camera.NumarBoti} ({Dificultati[dificultate].Eticheta})", dificultate));
        return Task.CompletedTask;
    });

    public Task ScoateBotAsync(string conexiune, string botId) => CaGazda(conexiune, camera =>
    {
        if (camera.Faza != "lobby") throw new HubException("Boții se pot scoate doar înainte de start.");
        camera.Boti.RemoveAll(b => b.Id == botId);
        return Task.CompletedTask;
    });

    public Task PornesteAsync(string conexiune) => CaGazda(conexiune, camera =>
    {
        if (camera.Faza != "lobby") throw new HubException("Jocul a început deja.");
        var locuri = Locuri(camera);
        if (locuri.Count < MinJucatori) throw new HubException("Este nevoie de cel puțin doi jucători (poți adăuga un robot).");
        var cetati = camera.Harta.Cetati(locuri.Count);
        camera.Jucatori = locuri.Select((l, i) => l with { Culoare = Culori[i], Cetate = cetati[i] }).ToList();
        camera.Proprietari = camera.Harta.Insule.ToDictionary(i => i.Id, _ => (string?)null);
        foreach (var j in camera.Jucatori) camera.Proprietari[j.Cetate!] = j.Id;
        var libere = camera.Harta.Insule.Count - camera.Jucatori.Count;
        camera.Runde = (int)Math.Ceiling(libere / (double)camera.Jucatori.Count) + 2;
        (camera.Tura, camera.Runda, camera.Nr) = (0, 1, 0);
        camera.Jurnal.Clear();
        camera.Xp = null;
        camera.JocId = Guid.NewGuid();
        Jurnal(camera, camera.Jucatori[0].Id, "Bătălia a început!");
        IncepeTura(camera);
        return Task.CompletedTask;
    });

    /// <summary>After a game, back to the lobby with the same room, map and bots.</summary>
    public Task JocNouAsync(string conexiune) => CaGazda(conexiune, camera =>
    {
        if (camera.Faza != "final") throw new HubException("Jocul nu s-a terminat.");
        camera.Faza = "lobby";
        camera.Jucatori = [];
        camera.Proprietari = [];
        (camera.Atac, camera.Intrebare, camera.Rezultat, camera.TermenLa, camera.Xp) = (null, null, null, null, null);
        // Members who left during the game no longer hold a seat.
        foreach (var gol in camera.Membri.Where(m => m.Value.Conexiuni.Count == 0).Select(m => m.Key).ToList()) camera.Membri.Remove(gol);
        return Task.CompletedTask;
    });

    // ---------------------------------------------------------------- player intents

    public Task AtacaAsync(string conexiune, string insula) => CaJucator(conexiune, (camera, jucator) =>
    {
        if (camera.Faza == "alegere" && camera.Jucatori[camera.Tura].Id == jucator && Tinte(camera, jucator).Contains(insula))
            Ataca(camera, jucator, insula);
    });

    public Task RaspundeAsync(string conexiune, int varianta) => CaJucator(conexiune, (camera, jucator) =>
    {
        if (camera.Faza == "intrebare" && camera.Secreta is IntrebareGrila q && varianta >= 0 && varianta < q.Variante.Count)
            Raspunde(camera, jucator, varianta);
    });

    public Task EstimeazaAsync(string conexiune, int valoare) => CaJucator(conexiune, (camera, jucator) =>
    {
        if (camera.Faza == "estimare") Raspunde(camera, jucator, valoare);
    });

    /// <summary>Integration checks only: whether a room is still open, without joining it.</summary>
    internal bool Exista(string cod) => _camere.ContainsKey(cod);

    /// <summary>For checks: the island names and room title of an open room.</summary>
    internal (string Materie, string[] Insule)? Descriere(string cod) =>
        _camere.TryGetValue(cod, out var c) ? (c.Materie, c.Harta.Insule.Select(i => i.Nume).ToArray()) : null;

    /// <summary>Integration checks only (InternalsVisibleTo): arranges the board, e.g. an island owner or the round, then publishes.</summary>
    internal Task PentruVerificariAsync(string cod, string? insula = null, string? proprietar = null, int? runda = null)
    {
        var c = _camere[cod];
        return Executa(c, () =>
        {
            if (insula is not null) c.Proprietari[insula] = proprietar;
            if (runda is { } r) c.Runda = r;
            return Task.CompletedTask;
        });
    }

    // ---------------------------------------------------------------- game flow (always under the room's lock)

    private void IncepeTura(Camera c)
    {
        if (c.Runda > c.Runde) { Final(c); return; }
        (c.Faza, c.Atac, c.Rezultat, c.Intrebare) = ("alegere", null, null, null);
        var curent = c.Jucatori[c.Tura];
        if (Tinte(c, curent.Id).Count == 0)
        {
            Jurnal(c, curent.Id, $"{curent.Nume} nu are ce ataca");
            Programeaza(c, TimeSpan.FromSeconds(1.5), Urmatorul);
            return;
        }
        // Gone for good (past the grace period): the turn passes quickly. Someone reloading the page still gets their turn.
        if (!curent.Bot && (!c.Membri.TryGetValue(int.Parse(curent.Id), out var m) || m.Plecat))
        {
            Jurnal(c, curent.Id, $"{curent.Nume} nu e în cameră și pierde tura");
            Programeaza(c, TimeSpan.FromSeconds(2), Urmatorul);
            return;
        }
        Programeaza(c, TimpAlegere, cam => { Jurnal(cam, curent.Id, $"{curent.Nume} n-a ales nimic la timp"); Urmatorul(cam); });
        if (curent.Bot) ProgrameazaBot(c, Intarziere(c, 1.5, 3), cam =>
        {
            var tinte = Tinte(cam, curent.Id);
            var libere = tinte.Where(t => cam.Proprietari[t] is null).ToList();
            var alese = libere.Count > 0 && cam.Rnd.NextDouble() < 0.75 ? libere : tinte;
            Ataca(cam, curent.Id, alese[cam.Rnd.Next(alese.Count)]);
        });
    }

    private void Urmatorul(Camera c)
    {
        if (++c.Tura >= c.Jucatori.Count) { c.Tura = 0; c.Runda++; }
        IncepeTura(c);
    }

    private void Ataca(Camera c, string atacator, string insula)
    {
        c.Atac = new AtacJoc(atacator, c.Proprietari[insula], insula);
        var q = c.Pachet.Alege(c.Pachet.Grila, Insula(c, insula).ParcursIds, x => x.Id, x => x.ParcursId, c.Rnd);
        DeschideIntrebare(c, "intrebare", q, new IntrebareJoc(++c.Nr, q.Text, q.Variante, (int)TimpIntrebare.TotalSeconds));
    }

    private void DeschideIntrebare(Camera c, string faza, object secreta, IntrebareJoc publica)
    {
        (c.Faza, c.Secreta, c.Intrebare, c.Rezultat) = (faza, secreta, publica, null);
        c.Alegeri.Clear();
        // One extra second of grace for the network.
        Programeaza(c, TimpIntrebare + TimeSpan.FromSeconds(1), faza == "intrebare" ? InchideGrila : InchideEstimare);
        foreach (var bot in Participanti(c).Select(id => c.Jucatori.First(j => j.Id == id)).Where(j => j.Bot))
        {
            var d = Dificultati[bot.Dificultate!];
            ProgrameazaBot(c, Intarziere(c, 2, 8), cam => Raspunde(cam, bot.Id, secreta switch
            {
                IntrebareGrila g => cam.Rnd.NextDouble() < d.Corect ? g.Corect
                    : Enumerable.Range(0, g.Variante.Count).Where(i => i != g.Corect).OrderBy(_ => cam.Rnd.Next()).First(),
                IntrebareEstimare e => (int)Math.Round(e.Raspuns + (e.Raspuns == 0 ? 10 : Math.Abs(e.Raspuns)) * d.Abatere * (cam.Rnd.NextDouble() * 2 - 1)),
                _ => 0,
            }));
        }
    }

    private void Raspunde(Camera c, string jucator, int alegere)
    {
        if (!Participanti(c).Contains(jucator) || c.Alegeri.ContainsKey(jucator)) return;
        c.Alegeri[jucator] = alegere;
        if (Participanti(c).All(c.Alegeri.ContainsKey))
        {
            if (c.Faza == "intrebare") InchideGrila(c); else InchideEstimare(c);
        }
    }

    private void InchideGrila(Camera c)
    {
        if (c.Faza != "intrebare" || c.Secreta is not IntrebareGrila q || c.Atac is not { } atac) return;
        var nume = Insula(c, atac.Insula).Nume;
        bool Corect(string? id) => id is not null && c.Alegeri.TryGetValue(id, out var a) && a == q.Corect;
        string? castigator = null; var text = ""; var urmeazaEstimare = false;
        if (atac.Aparator is null)
        {
            if (Corect(atac.Atacator)) { castigator = atac.Atacator; text = $"Corect! {Nume(c, atac.Atacator)} a cucerit {nume}."; }
            else text = $"Greșit. {nume} rămâne liberă.";
        }
        else if (Corect(atac.Atacator) && Corect(atac.Aparator)) { urmeazaEstimare = true; text = "Amândoi ați răspuns corect! Urmează întrebarea de estimare."; }
        else if (Corect(atac.Atacator)) { castigator = atac.Atacator; text = $"{Nume(c, atac.Atacator)} câștigă duelul și cucerește {nume}!"; }
        else { castigator = atac.Aparator; text = $"{Nume(c, atac.Aparator)} a apărat {nume}."; }

        if (castigator == atac.Atacator)
        {
            c.Proprietari[atac.Insula] = atac.Atacator;
            Jurnal(c, atac.Atacator, $"{Nume(c, atac.Atacator)} a cucerit {nume}");
        }
        else if (!urmeazaEstimare)
            Jurnal(c, atac.Aparator ?? atac.Atacator, atac.Aparator is not null ? $"{Nume(c, atac.Aparator)} a apărat {nume}" : $"{Nume(c, atac.Atacator)} a greșit la {nume}");

        (c.Faza, c.Rezultat) = ("rezultat", new RezultatJoc("grila", q.Corect, new Dictionary<string, int>(c.Alegeri), castigator, text));
        Programeaza(c, PauzaRezultat, urmeazaEstimare ? PornesteEstimare : Urmatorul);
    }

    private void PornesteEstimare(Camera c)
    {
        var e = c.Pachet.Alege(c.Pachet.Estimari, Insula(c, c.Atac!.Insula).ParcursIds, x => x.Id, x => x.ParcursId, c.Rnd);
        DeschideIntrebare(c, "estimare", e, new IntrebareJoc(++c.Nr, e.Text, null, (int)TimpIntrebare.TotalSeconds));
    }

    private void InchideEstimare(Camera c)
    {
        if (c.Faza != "estimare" || c.Secreta is not IntrebareEstimare e || c.Atac is not { Aparator: { } aparator } atac) return;
        var nume = Insula(c, atac.Insula).Nume;
        double Dist(string id) => c.Alegeri.TryGetValue(id, out var v) ? Math.Abs((double)v - e.Raspuns) : double.PositiveInfinity;
        // A perfect tie keeps the island with the defender.
        string text;
        if (Dist(atac.Atacator) < Dist(aparator))
        {
            c.Proprietari[atac.Insula] = atac.Atacator;
            Jurnal(c, atac.Atacator, $"{Nume(c, atac.Atacator)} a cucerit {nume} la estimare");
            text = $"{Nume(c, atac.Atacator)} a fost mai aproape și cucerește {nume}!";
        }
        else
        {
            Jurnal(c, aparator, $"{Nume(c, aparator)} a apărat {nume} la estimare");
            text = $"{Nume(c, aparator)} a fost mai aproape și păstrează {nume}.";
        }
        var castigator = c.Proprietari[atac.Insula] == atac.Atacator ? atac.Atacator : aparator;
        (c.Faza, c.Rezultat) = ("rezultat", new RezultatJoc("estimare", e.Raspuns, new Dictionary<string, int>(c.Alegeri), castigator, text));
        Programeaza(c, PauzaRezultat + TimeSpan.FromMilliseconds(800), Urmatorul);
    }

    private void Final(Camera c)
    {
        (c.Faza, c.Atac, c.Intrebare, c.Rezultat, c.TermenLa) = ("final", null, null, null, null);
        AnuleazaTimere(c);
        var insule = c.Jucatori.ToDictionary(j => j.Id, j => c.Proprietari.Values.Count(p => p == j.Id));
        var maxim = insule.Values.Max();
        var cuBoti = c.Jucatori.Any(j => j.Bot);
        c.Xp = c.Jucatori.Where(j => !j.Bot).ToDictionary(j => j.Id,
            j => (int)Math.Round((insule[j.Id] * XpInsula + (insule[j.Id] == maxim ? XpCastig : 0)) * (cuBoti ? FactorBoti : 1)));
        var castigatori = c.Jucatori.Where(j => insule[j.Id] == maxim).Select(j => j.Nume).ToList();
        Jurnal(c, c.Jucatori.First(j => insule[j.Id] == maxim).Id,
            castigatori.Count == 1 ? $"{castigatori[0]} a câștigat bătălia!" : $"Egalitate: {string.Join(" și ", castigatori)}");
        c.DeAcordatXp = true;
    }

    // ---------------------------------------------------------------- helpers

    private sealed record Nivel(string Eticheta, double Corect, double Abatere);
    private static readonly Dictionary<string, Nivel> Dificultati = new()
    {
        ["usor"] = new("ușor", 0.5, 0.30), ["mediu"] = new("mediu", 0.7, 0.15), ["greu"] = new("greu", 0.9, 0.05),
    };

    private static InsulaHarta Insula(Camera c, string id) => c.Harta.Insule.First(i => i.Id == id);
    private static string Nume(Camera c, string id) => c.Jucatori.First(j => j.Id == id).Nume;
    private static bool Conectat(Camera c, int utilizatorId) => c.Membri.TryGetValue(utilizatorId, out var m) && m.Conexiuni.Count > 0;
    private static IEnumerable<string> Participanti(Camera c) => c.Atac is { } a ? new[] { a.Atacator, a.Aparator }.OfType<string>() : [];
    private TimeSpan Intarziere(Camera c, double min, double max) => TimeSpan.FromSeconds(min + c.Rnd.NextDouble() * (max - min));
    private long Acum() => ceas.GetUtcNow().ToUnixTimeMilliseconds();

    private static void Jurnal(Camera c, string jucatorId, string text)
    {
        var culoare = c.Jucatori.FirstOrDefault(j => j.Id == jucatorId)?.Culoare ?? "#1b8ad3";
        c.Jurnal.Insert(0, new RandJurnal(culoare, text));
        if (c.Jurnal.Count > 8) c.Jurnal.RemoveAt(8);
    }

    /// <summary>Islands a player may attack: joined by a bridge to one of theirs, not theirs, and not someone's castle.</summary>
    private static List<string> Tinte(Camera c, string jucator)
    {
        var cetati = c.Jucatori.Select(j => j.Cetate).ToHashSet();
        return c.Proprietari.Where(p => p.Value == jucator).SelectMany(p => c.Harta.Vecini(p.Key))
            .Where(v => c.Proprietari[v] != jucator && !cetati.Contains(v)).Distinct().ToList();
    }

    /// <summary>The seats in the lobby: connected people in the order they came, then the bots.</summary>
    private static List<JucatorJoc> Locuri(Camera c)
    {
        var oameni = c.Membri.Where(m => m.Value.Conexiuni.Count > 0).OrderBy(m => m.Value.IntratLa)
            .Take(Math.Max(0, c.MaxJucatori - c.Boti.Count))
            .Select(m => new JucatorJoc(m.Key.ToString(), m.Value.Nume, "", null, false, null, true));
        return [.. oameni, .. c.Boti.Select(b => new JucatorJoc(b.Id, b.Nume, "", null, true, b.Dificultate, true))];
    }

    /// <summary>Replaces the room's phase timer (and drops pending bot actions): only the newest scheduled step can run.</summary>
    private void Programeaza(Camera c, TimeSpan dupa, Action<Camera> pas)
    {
        AnuleazaTimere(c);
        var generatie = c.Generatie;
        c.TermenLa = Acum() + (long)dupa.TotalMilliseconds;
        c.Timer = ceas.CreateTimer(_ => _ = RuleazaAsync(c, generatie, pas), null, dupa, Timeout.InfiniteTimeSpan);
    }

    private void ProgrameazaBot(Camera c, TimeSpan dupa, Action<Camera> pas)
    {
        var generatie = c.Generatie;
        c.TimereBoti.Add(ceas.CreateTimer(_ => _ = RuleazaAsync(c, generatie, pas), null, dupa, Timeout.InfiniteTimeSpan));
    }

    private static void AnuleazaTimere(Camera c)
    {
        c.Generatie++;
        c.Timer?.Dispose(); c.Timer = null;
        foreach (var t in c.TimereBoti) t.Dispose();
        c.TimereBoti.Clear();
    }

    private async Task RuleazaAsync(Camera c, int generatie, Action<Camera> pas)
    {
        await c.Lacat.WaitAsync();
        try
        {
            if (c.Inchisa || c.Generatie != generatie) return;
            pas(c);
            await DupaSchimbareAsync(c);
        }
        catch (Exception ex) { log.LogError(ex, "Triviador step failed in room {Cod}", c.Cod); }
        finally { c.Lacat.Release(); }
    }

    private async Task<StareJoc> Executa(Camera c, Func<Task> actiune)
    {
        await c.Lacat.WaitAsync();
        try
        {
            if (c.Inchisa) throw new HubException("Camera s-a închis.");
            await actiune();
            return await DupaSchimbareAsync(c);
        }
        finally { c.Lacat.Release(); }
    }

    private async Task CaGazda(string conexiune, Func<Camera, Task> actiune)
    {
        var (camera, utilizatorId) = CameraDe(conexiune);
        await Executa(camera, () => camera.GazdaId == utilizatorId ? actiune(camera) : throw new HubException("Doar gazda poate face asta."));
    }

    private async Task CaJucator(string conexiune, Action<Camera, string> actiune)
    {
        var (camera, utilizatorId) = CameraDe(conexiune);
        await Executa(camera, () => { actiune(camera, utilizatorId.ToString()); return Task.CompletedTask; });
    }

    private (Camera, int) CameraDe(string conexiune) =>
        _conexiuni.TryGetValue(conexiune, out var x) && _camere.TryGetValue(x.Cod, out var camera)
            ? (camera, x.UtilizatorId)
            : throw new HubException("Intră mai întâi într-o cameră.");

    // Awards the game's XP once (unique key per game and player), then tells everyone in the room.
    private async Task<StareJoc> DupaSchimbareAsync(Camera c)
    {
        c.Activitate = Acum();
        c.Goala = c.Membri.Values.All(m => m.Conexiuni.Count == 0);
        if (c.DeAcordatXp)
        {
            c.DeAcordatXp = false;
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<MarkAiDbContext>();
                foreach (var (id, xp) in c.Xp!.Where(x => x.Value > 0))
                    await db.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT INTO mark_ai.eveniment_recompensa (utilizator_id, xp_acordat, puncte_materie, tip_eveniment, cheie_unica)
                        VALUES ({int.Parse(id)}, {xp}, 0, 'triviador', {$"triviador:{c.JocId:N}:utilizator:{id}"})
                        ON CONFLICT (cheie_unica) DO NOTHING
                        """);
            }
            catch (Exception ex) { log.LogError(ex, "Could not award Triviador XP in room {Cod}", c.Cod); }
        }
        var stare = Stare(c);
        await hub.Clients.Group(Grup(c.Cod)).SendAsync("stare", stare);
        return stare;
    }

    private StareJoc Stare(Camera c)
    {
        var inJoc = c.Faza != "lobby";
        var locuri = inJoc ? c.Jucatori.Select(j => j.Bot ? j : j with { Conectat = Conectat(c, int.Parse(j.Id)) }).ToList() : Locuri(c);
        if (!inJoc)
        {
            var cetati = locuri.Count >= MinJucatori ? c.Harta.Cetati(locuri.Count) : [];
            locuri = locuri.Select((l, i) => l with { Culoare = Culori[i], Cetate = i < cetati.Count ? cetati[i] : null }).ToList();
        }
        var asezati = locuri.Select(l => l.Id).ToHashSet();
        var spectatori = c.Membri.Where(m => m.Value.Conexiuni.Count > 0 && !asezati.Contains(m.Key.ToString())).OrderBy(m => m.Value.IntratLa)
            .Select(m => new JucatorJoc(m.Key.ToString(), m.Value.Nume, "#8a8f98", null, false, null, true)).ToList();
        var curent = c.Faza == "alegere" ? c.Jucatori[c.Tura].Id : null;
        return new StareJoc(c.Cod, c.Faza, c.GazdaId.ToString(), c.Materie,
            new HartaPublica(c.Harta.Latime, c.Harta.Inaltime, c.Harta.Insule.Select(i => new InsulaPublica(i.Id, i.Nume, i.Autor, i.X, i.Y, i.Seed, i.Semn)).ToList(), c.Harta.Poduri),
            c.MaxJucatori, locuri, spectatori, inJoc ? new Dictionary<string, string?>(c.Proprietari) : locuri.Where(l => l.Cetate is not null).ToDictionary(l => l.Cetate!, l => (string?)l.Id),
            c.Tura, c.Runda, c.Runde, c.Jurnal.ToList(), c.Atac, c.Intrebare,
            c.Alegeri.Keys.ToDictionary(k => k, _ => true), c.Faza == "rezultat" ? c.Rezultat : null,
            curent is null ? [] : Tinte(c, curent), c.Faza is "lobby" or "final" ? null : c.TermenLa, Acum(),
            c.Faza == "final" ? c.Xp : null, inJoc ? c.Jucatori.Any(j => j.Bot) : c.Boti.Count > 0);
    }

    // ---------------------------------------------------------------- state

    private sealed class Membru(string nume, long intratLa)
    {
        public string Nume { get; } = nume;
        public long IntratLa { get; } = intratLa;
        public HashSet<string> Conexiuni { get; } = [];
        /// <summary>Left during a game (after the grace period); keeps the seat and may come back.</summary>
        public bool Plecat { get; set; }
    }

    private sealed record Bot(string Id, string Nume, string Dificultate);

    private sealed class Camera(string cod, int gazdaId, string materie, int maxJucatori, HartaJoc harta, PachetIntrebari pachet, Random rnd, long acum)
    {
        public string Cod { get; } = cod;
        public int GazdaId { get; set; } = gazdaId;
        public string Materie { get; } = materie;
        public int MaxJucatori { get; } = maxJucatori;
        public HartaJoc Harta { get; } = harta;
        public PachetIntrebari Pachet { get; } = pachet;
        public Random Rnd { get; } = rnd;
        public SemaphoreSlim Lacat { get; } = new(1, 1);
        public bool Inchisa { get; set; }
        // Read without the lock (room sweep), so only ever written as whole values under it.
        public volatile bool Goala = true;
        public long Activitate { get; set; } = acum;

        public Dictionary<int, Membru> Membri { get; } = [];
        public List<Bot> Boti { get; } = [];
        public int NumarBoti { get; set; }

        public string Faza { get; set; } = "lobby";
        public List<JucatorJoc> Jucatori { get; set; } = [];
        public Dictionary<string, string?> Proprietari { get; set; } = [];
        public int Tura { get; set; }
        public int Runda { get; set; }
        public int Runde { get; set; }
        public int Nr { get; set; }
        public List<RandJurnal> Jurnal { get; } = [];
        public AtacJoc? Atac { get; set; }
        public IntrebareJoc? Intrebare { get; set; }
        /// <summary>The open question with its answer (<see cref="IntrebareGrila"/> or <see cref="IntrebareEstimare"/>); never sent.</summary>
        public object? Secreta { get; set; }
        public Dictionary<string, int> Alegeri { get; } = [];
        public RezultatJoc? Rezultat { get; set; }
        public long? TermenLa { get; set; }
        public Dictionary<string, int>? Xp { get; set; }
        public bool DeAcordatXp { get; set; }
        public Guid JocId { get; set; }

        public int Generatie { get; set; }
        public ITimer? Timer { get; set; }
        public List<ITimer> TimereBoti { get; } = [];
        public ITimer? TimerInchidere { get; set; }
        public Dictionary<int, ITimer> TimerePlecare { get; } = [];
    }
}

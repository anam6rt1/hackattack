using System.Text.Json;
using MarkAi.Api.Infrastructura;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Servicii;

public record StareGenerare(string Stare, string? Mesaj, int IntrebariGenerate, DateTime Actualizat);

/// <summary>Durable queue/status in PostgreSQL. The pilot runs one API instance and one generator.</summary>
public sealed class CoadaIntrebari(IServiceScopeFactory scopes)
{
    public StareGenerare? Stare(int parcursId)
    {
        using var scope = scopes.CreateScope();
        return scope.ServiceProvider.GetRequiredService<MarkAiDbContext>().Set<GenerareAi>().AsNoTracking()
            .Where(g => g.ParcursId == parcursId).OrderByDescending(g => g.Id)
            .Select(g => new StareGenerare(g.Stare, g.Mesaj, g.IntrebariGenerate, g.Actualizat ?? g.CreatLa)).FirstOrDefault();
    }
    public bool InLucru(int parcursId) => Stare(parcursId)?.Stare is "in_asteptare" or "in_lucru";
    internal void Actualizeaza(int id, StareGenerare stare)
    {
        using var scope = scopes.CreateScope();
        scope.ServiceProvider.GetRequiredService<MarkAiDbContext>().Set<GenerareAi>().Where(g => g.Id == id)
            .ExecuteUpdate(s => s.SetProperty(g => g.Stare, stare.Stare).SetProperty(g => g.Mesaj, stare.Mesaj)
                .SetProperty(g => g.IntrebariGenerate, stare.IntrebariGenerate).SetProperty(g => g.Actualizat, stare.Actualizat));
    }
    internal bool IncearcaCiteste(out (int Id, int ParcursId, int Numar) cerere)
    {
        using var scope = scopes.CreateScope();
        var g = scope.ServiceProvider.GetRequiredService<MarkAiDbContext>().Set<GenerareAi>().AsNoTracking()
            .Where(g => g.Stare == "in_asteptare" || g.Stare == "in_lucru").OrderBy(g => g.Id).FirstOrDefault();
        cerere = g is null ? default : (g.Id, g.ParcursId, g.Numar);
        return g is not null;
    }
}
/// <summary>AI questions the user may still ask for in the last 24 hours: their own budget, capped by what the platform has left.</summary>
public record BugetAi(int Ramase, int PeZi);

/// <summary>
/// Queues an AI question generation a professor asked for. Spending is limited by questions asked, not by clicks: each account
/// (admins too) may ask for <c>Limite:IntrebariAiPeZi</c> questions in 24 hours, and the whole platform for
/// <c>Limite:IntrebariAiPeZiTotal</c>. Questions are generated only on request, never automatically.
/// </summary>
public static class LimitaGenerariAi
{
    public const int MaxIntrebariPeCerere = 20;

    public static async Task<BugetAi> BugetAsync(MarkAiDbContext db, IConfiguration cfg, TimeProvider ceas, int uid, CancellationToken ct)
    {
        var peZi = cfg.GetValue("Limite:IntrebariAiPeZi", 30);
        var total = cfg.GetValue("Limite:IntrebariAiPeZiTotal", 300);
        var deLa = ceas.GetUtcNow().UtcDateTime.AddHours(-24);
        var recente = db.Set<GenerareAi>().Where(g => g.CreatLa >= deLa);
        var ale = await recente.Where(g => g.UtilizatorId == uid).SumAsync(g => g.Numar, ct);
        var toate = await recente.SumAsync(g => g.Numar, ct);
        return new BugetAi(Math.Max(0, Math.Min(peZi - ale, total - toate)), peZi);
    }

    public static async Task PuneAsync(MarkAiDbContext db, CoadaIntrebari coada, IConfiguration cfg, TimeProvider ceas, int uid, int parcursId, int numar,
        CancellationToken ct)
    {
        if (numar is < 1 or > MaxIntrebariPeCerere) throw new CerereInvalida($"Poți genera între 1 și {MaxIntrebariPeCerere} întrebări odată.");
        if (coada.InLucru(parcursId)) throw new Conflict("Întrebările acestei lecții se generează deja. Așteaptă să se termine.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // One budget check at a time, so two requests at once cannot both spend the last questions.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext('mark_ai.generare_ai'))", ct);
        if (await db.Set<GenerareAi>().AnyAsync(g => g.ParcursId == parcursId && (g.Stare == "in_asteptare" || g.Stare == "in_lucru"), ct)) throw new Conflict("Întrebările acestei lecții se generează deja. Așteaptă să se termine.");
        var buget = await BugetAsync(db, cfg, ceas, uid, ct);
        if (buget.Ramase == 0)
            throw new Conflict("Ai folosit întrebările AI disponibile în ultimele 24 de ore. Poți scrie sau edita întrebările manual sau reîncerca mâine.");
        if (numar > buget.Ramase)
            throw new Conflict($"Îți mai rămân {buget.Ramase} întrebări AI în ultimele 24 de ore. Cere cel mult {buget.Ramase}.");
        db.Add(new GenerareAi { UtilizatorId = uid, ParcursId = parcursId, Numar = numar, Stare = "in_asteptare", CreatLa = ceas.GetUtcNow().UtcDateTime });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

    }
}

/// <summary>
/// Generates quiz questions for a Lecție with the AI model, from the notes of all its chapters and the text of its files.
/// New questions are ADDED to the Lecție's quiz (the professor edits or deletes any of them afterwards); the model is shown the
/// existing ones so it does not repeat them, and asked for a few more than requested so that the ones about the document itself
/// or too close to another (<see cref="CalitateIntrebari"/>) can be dropped. Estimation questions (Triviador's tie-breaks) are
/// topped up to <c>Claude:IntrebariEstimare</c> per Lecție.
/// </summary>
public sealed class GeneratorIntrebari(CoadaIntrebari coada, IServiceScopeFactory scopes, IConfiguration cfg, ModelAi ai, ILogger<GeneratorIntrebari> log)
    : BackgroundService
{
    private readonly SemaphoreSlim procesare = new(1, 1);
    private const string Sistem = """
        Ești profesor de liceu în România și pregătești elevi pentru examenul de bacalaureat.
        Scrii întrebări de verificare în limba română, cu diacritice, despre materia din materialul primit: faptele, ideile, operele,
        personajele, formulele și conceptele lui. Nu adăuga informații care nu sunt în material.
        Întrebările sunt despre materie, nu despre material: nu întreba despre capitole, titluri, fișiere sau notițe și nu scrie
        „conform materialului”, „din material” sau „în textul de mai sus”. Elevul trebuie să poată răspunde și fără să fi văzut
        documentul. Titlurile de capitole și de fișiere din material sunt doar pentru organizare.
        Fiecare întrebare verifică o altă idee: nu pune două întrebări despre același fapt, nici cu alte cuvinte, și acoperă părți
        cât mai diferite ale materialului.
        Întrebările grilă au exact 4 variante plauzibile, o singură variantă corectă, iar poziția variantei corecte variază.
        Explicația are 1–2 propoziții și spune de ce răspunsul este corect.
        Nu repeta și nu reformula întrebările care există deja.
        Întrebările de estimare cer un număr întreg (de exemplu un an) care apare explicit în material;
        dacă materialul nu conține astfel de numere, lista de estimări rămâne goală.
        """;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested) { await RuleazaOdataAsync(ct); await Task.Delay(1000, ct); }
    }

    /// <summary>Processes the Lecții queued so far, then returns. The integration checks call this instead of running the service.</summary>
    public async Task RuleazaOdataAsync(CancellationToken ct)
    {
        await procesare.WaitAsync(ct);
        try { while (coada.IncearcaCiteste(out var cerere)) await ProceseazaAsync(cerere.Id, cerere.ParcursId, cerere.Numar, ct); }
        finally { procesare.Release(); }
    }

    private async Task ProceseazaAsync(int jobId, int parcursId, int numar, CancellationToken ct)
    {
        coada.Actualizeaza(jobId, new StareGenerare("in_lucru", null, 0, DateTime.UtcNow));
        try
        {
            var (n, mesaj) = await GenereazaAsync(jobId, parcursId, numar, ct);
            coada.Actualizeaza(jobId, new StareGenerare("gata", mesaj, n, DateTime.UtcNow));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (ex is EroareGenerare) log.LogWarning("AI question generation for Lecție {ParcursId}: {Mesaj}", parcursId, ex.Message);
            else log.LogError(ex, "AI question generation failed for Lecție {ParcursId}", parcursId);
            coada.Actualizeaza(jobId, new StareGenerare("eroare", ex is EroareGenerare ? ex.Message : "Generarea a eșuat. Încearcă din nou.", 0, DateTime.UtcNow));
        }
    }

    /// <summary>A failure the professor can act on; its message is shown in the editor.</summary>
    private sealed class EroareGenerare(string mesaj) : Exception(mesaj);

    /// <returns>The number of multiple-choice questions added, and a note for the editor when fewer than asked were added.</returns>
    private async Task<(int, string?)> GenereazaAsync(int jobId, int parcursId, int numar, CancellationToken ct)
    {
        var model = ai.Activ ?? throw new EroareGenerare("Generarea AI nu este configurată (setează ANTHROPIC_API_KEY sau GEMINI_API_KEY pe server).");

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarkAiDbContext>();
        var lectie = await db.Set<Parcurs>().AsNoTracking().Where(p => p.Id == parcursId)
            .Select(p => new { p.Id, p.MaterieId, p.Titlu, Materie = p.Materie.Denumire }).SingleOrDefaultAsync(ct);
        if (lectie is null) return (0, null);

        var material = await MaterialAsync(db, parcursId, ct);
        if (string.IsNullOrWhiteSpace(material))
            throw new EroareGenerare("Lecția nu are materiale (notițe în capitole sau fișiere cu text) din care să fie generate întrebări.");

        var existente = await db.Set<Intrebare>().AsNoTracking().Where(q => q.ParcursId == parcursId && q.Activa)
            .OrderByDescending(q => q.Id).Select(q => new { q.Tip, q.Text }).Take(200).ToListAsync(ct);
        var estimari = Math.Max(0, cfg.GetValue("Claude:IntrebariEstimare", 2) - existente.Count(q => q.Tip == Intrebare.Estimare));
        // A few more than asked, so dropping a near-duplicate does not mean fewer questions (only the asked number is kept).
        var cerute = Math.Min(numar + Math.Max(2, numar / 2), numar * 2);
        var deEvitat = existente.Count == 0 ? "" : $"""

            Întrebări care există deja (nu le repeta):
            {string.Join("\n", existente.Select(q => "- " + q.Text))}
            """;
        var prompt = $"""
            Materie: {lectie.Materie}
            Lecție: {lectie.Titlu}

            <material>
            {material}
            </material>
            {deEvitat}

            Scrie {cerute} întrebări grilă și cel mult {estimari} întrebări de estimare despre materia din acest material.
            """;

        var text = await model.JsonAsync(ScopAi.Intrebari, Sistem, [new MesajAi(true, prompt)], Schema, 16000, ct);
        var rezultat = JsonSerializer.Deserialize<RezultatAi>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        // A reply without the lists (e.g. "{}") counts as no questions, not as a crash.
        var corecte = (rezultat?.Grila ?? []).OfType<GrilaAi>().Where(q => q.Variante?.Count == 4 && q.Corect is >= 0 and <= 3
            && !string.IsNullOrWhiteSpace(q.Text) && q.Variante.All(v => !string.IsNullOrWhiteSpace(v))).ToList();
        if (corecte.Count == 0) throw new EroareGenerare("Modelul AI nu a propus nicio întrebare validă. Încearcă din nou.");
        var (valide, asemanatoare, despreDocument) = CalitateIntrebari.Filtreaza(corecte, q => q.Text, existente.Select(q => q.Text), numar);
        if (valide.Count == 0)
            throw new EroareGenerare("Întrebările propuse de modelul AI semănau prea mult cu cele existente sau erau despre material, nu despre materie. Încearcă din nou.");
        var estimariNoi = CalitateIntrebari.Filtreaza((rezultat?.Estimari ?? []).OfType<EstimareAi>().Where(e => !string.IsNullOrWhiteSpace(e.Text)),
            e => e.Text, existente.Select(q => q.Text), estimari).Pastrate;

        foreach (var q in valide)
            db.Add(new Intrebare
            {
                MaterieId = lectie.MaterieId, ParcursId = parcursId, Text = q.Text.Trim(), Explicatie = q.Explicatie?.Trim(), Sursa = Intrebare.SursaAi,
                Variante = q.Variante!.Select((v, i) => new VariantaIntrebare { Text = v.Trim(), Corecta = i == q.Corect }).ToList(),
            });
        foreach (var e in estimariNoi)
            db.Add(new Intrebare { MaterieId = lectie.MaterieId, ParcursId = parcursId, Text = e.Text.Trim(), Tip = Intrebare.Estimare, RaspunsNumeric = e.Raspuns, Sursa = Intrebare.SursaAi });
        // Commit generated questions and completed job together: a restart cannot add them twice.
        var job = await db.Set<GenerareAi>().SingleAsync(g => g.Id == jobId, ct);
        job.Stare = "gata"; job.IntrebariGenerate = valide.Count; job.Actualizat = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        if (valide.Count == numar) return (valide.Count, null);
        var motive = new List<string>();
        if (asemanatoare > 0) motive.Add(asemanatoare == 1 ? "una semăna prea mult cu o întrebare existentă" : $"{asemanatoare} semănau prea mult cu întrebări existente");
        if (despreDocument > 0) motive.Add(despreDocument == 1 ? "una era despre material, nu despre materie" : $"{despreDocument} erau despre material, nu despre materie");
        return (valide.Count, $"Au fost adăugate {valide.Count} din {numar} cerute"
            + (motive.Count > 0 ? $": dintre cele propuse, {string.Join(", iar ", motive)}." : ": modelul AI n-a propus mai multe valide.")
            + " Poți genera din nou sau scrie restul manual.");
    }

    // The notes of every chapter (in the recommended order), then the extracted text of the Lecție's files, within
    // Claude:MaxCaractereMaterial (120 000) in total; each file within Fisiere:MaxCaractereText.
    private async Task<string> MaterialAsync(MarkAiDbContext db, int parcursId, CancellationToken ct)
    {
        var ramase = cfg.GetValue("Claude:MaxCaractereMaterial", 120_000);
        var text = new System.Text.StringBuilder();
        void Adauga(string titlu, string? continut)
        {
            if (string.IsNullOrWhiteSpace(continut) || ramase <= 0) return;
            var parte = continut.Length <= ramase ? continut : continut[..ramase];
            text.Append($"\n\n# {titlu}\n{parte}");
            ramase -= parte.Length;
        }

        var capitole = await db.Curs.AsNoTracking().Where(c => c.ParcursId == parcursId).OrderBy(c => c.Ordine).ThenBy(c => c.Id).ToListAsync(ct);
        foreach (var c in capitole)
        {
            var notite = string.Join("\n\n", c.Materiale.Select(m => $"## {m.Titlu}\n{m.Text}"));
            if (!string.IsNullOrWhiteSpace(c.Continut)) notite += "\n\n" + c.Continut;
            Adauga($"Capitol: {c.Titlu}", notite);
        }
        var fisiere = await db.Set<FisierLectie>().AsNoTracking().Where(f => f.ParcursId == parcursId && f.StareText == FisierLectie.Gata)
            .OrderBy(f => f.Id).Select(f => new { f.Nume, f.TextExtras }).ToListAsync(ct);
        foreach (var f in fisiere) Adauga($"Fișier: {f.Nume}", f.TextExtras);
        return text.ToString().Trim();
    }

    private static readonly Dictionary<string, JsonElement> Schema = new()
    {
        ["type"] = JsonSerializer.SerializeToElement("object"),
        ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
        ["required"] = JsonSerializer.SerializeToElement(new[] { "grila", "estimari" }),
        ["properties"] = JsonSerializer.SerializeToElement(new Dictionary<string, object>
        {
            ["grila"] = new
            {
                type = "array",
                items = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new[] { "text", "variante", "corect", "explicatie" },
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["text"] = new { type = "string" },
                        ["variante"] = new { type = "array", items = new { type = "string" } },
                        ["corect"] = new { type = "integer", description = "Indexul (0–3) variantei corecte" },
                        ["explicatie"] = new { type = "string" },
                    },
                },
            },
            ["estimari"] = new
            {
                type = "array",
                items = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new[] { "text", "raspuns" },
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["text"] = new { type = "string" },
                        ["raspuns"] = new { type = "integer" },
                    },
                },
            },
        }),
    };

    private sealed record RezultatAi(List<GrilaAi?>? Grila, List<EstimareAi?>? Estimari);
    private sealed record GrilaAi(string Text, List<string>? Variante, int Corect, string? Explicatie);
    private sealed record EstimareAi(string Text, int Raspuns);
}

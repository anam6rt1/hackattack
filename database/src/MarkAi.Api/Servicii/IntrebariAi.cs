using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using MarkAi.Data;
using MarkAi.Data.Trivia;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Servicii;

public record StareGenerare(string Stare, string? Mesaj, int IntrebariGenerate, DateTime Actualizat);

/// <summary>Courses waiting for AI question generation, plus the latest status of each (in memory).</summary>
public sealed class CoadaIntrebari
{
    private readonly Channel<int> _coada = Channel.CreateUnbounded<int>();
    private readonly ConcurrentDictionary<int, StareGenerare> _stari = new();

    public void Adauga(int cursId)
    {
        _stari[cursId] = new StareGenerare("in_asteptare", null, 0, DateTime.UtcNow);
        _coada.Writer.TryWrite(cursId);
    }

    public StareGenerare? Stare(int cursId) => _stari.GetValueOrDefault(cursId);

    internal void Actualizeaza(int cursId, StareGenerare stare) => _stari[cursId] = stare;

    internal IAsyncEnumerable<int> Citeste(CancellationToken ct) => _coada.Reader.ReadAllAsync(ct);

    internal bool IncearcaCiteste(out int cursId) => _coada.Reader.TryRead(out cursId);
}

/// <summary>
/// Generates quiz questions for a course with Claude when the course is created or its content changes.
/// Previous AI questions are deactivated (not deleted, since past trivia rounds may reference them);
/// questions written by people are left untouched.
/// </summary>
public sealed class GeneratorIntrebari(CoadaIntrebari coada, IServiceScopeFactory scopes, IConfiguration cfg, ModelAi ai, ILogger<GeneratorIntrebari> log)
    : BackgroundService
{
    private const string Sistem = """
        Ești profesor de liceu în România și pregătești elevi pentru examenul de bacalaureat.
        Scrii întrebări de verificare în limba română, cu diacritice, strict pe baza materialului primit.
        Întrebările grilă au exact 4 variante plauzibile, o singură variantă corectă, iar poziția variantei corecte variază.
        Explicația are 1–2 propoziții și spune de ce răspunsul este corect.
        Întrebările de estimare cer un număr întreg (de exemplu un an) care apare explicit în material;
        dacă materialul nu conține astfel de numere, lista de estimări rămâne goală.
        """;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var cursId in coada.Citeste(ct)) await ProceseazaAsync(cursId, ct);
    }

    /// <summary>Processes the courses queued so far, then returns. The integration checks call this instead of running the service.</summary>
    public async Task RuleazaOdataAsync(CancellationToken ct)
    {
        while (coada.IncearcaCiteste(out var cursId)) await ProceseazaAsync(cursId, ct);
    }

    private async Task ProceseazaAsync(int cursId, CancellationToken ct)
    {
        coada.Actualizeaza(cursId, new StareGenerare("in_lucru", null, 0, DateTime.UtcNow));
        try
        {
            var n = await GenereazaAsync(cursId, ct);
            coada.Actualizeaza(cursId, new StareGenerare("gata", null, n, DateTime.UtcNow));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "AI question generation failed for course {CursId}", cursId);
            coada.Actualizeaza(cursId, new StareGenerare("eroare", ex is ConfigurareLipsa ? ex.Message : "Generarea a eșuat. Încearcă din nou.", 0, DateTime.UtcNow));
        }
    }

    private sealed class ConfigurareLipsa(string mesaj) : Exception(mesaj);

    private async Task<int> GenereazaAsync(int cursId, CancellationToken ct)
    {
        var model = ai.Activ ?? throw new ConfigurareLipsa("Generarea AI nu este configurată (setează ANTHROPIC_API_KEY sau GEMINI_API_KEY pe server).");

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarkAiDbContext>();
        var curs = await db.Curs.Include(c => c.Materie).AsNoTracking().SingleOrDefaultAsync(c => c.Id == cursId, ct);
        if (curs is null) return 0;

        var material = string.Join("\n\n", curs.Materiale.Select(m => $"## {m.Titlu}\n{m.Text}"));
        if (!string.IsNullOrWhiteSpace(curs.Continut)) material += "\n\n" + curs.Continut;
        if (string.IsNullOrWhiteSpace(material)) throw new ConfigurareLipsa("Cursul nu are materiale din care să fie generate întrebări.");

        var grila = cfg.GetValue("Claude:IntrebariGrila", 8);
        var estimari = cfg.GetValue("Claude:IntrebariEstimare", 2);
        var prompt = $"""
            Materie: {curs.Materie.Denumire}
            Capitol: {curs.Capitol ?? "-"}
            Curs: {curs.Titlu}

            <material>
            {material}
            </material>

            Scrie {grila} întrebări grilă și cel mult {estimari} întrebări de estimare despre acest material.
            """;

        var text = await model.JsonAsync(ScopAi.Intrebari, Sistem, [new MesajAi(true, prompt)], Schema, 16000, ct);
        var rezultat = JsonSerializer.Deserialize<RezultatAi>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Răspuns gol.");

        var valide = rezultat.Grila.Where(q => q.Variante.Count == 4 && q.Corect is >= 0 and <= 3
            && !string.IsNullOrWhiteSpace(q.Text) && q.Variante.All(v => !string.IsNullOrWhiteSpace(v))).ToList();
        if (valide.Count == 0) throw new InvalidOperationException("Nicio întrebare validă.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Set<TriviaQuestion>().Where(q => q.CourseId == cursId && q.Source == "ai" && q.Active)
            .ExecuteUpdateAsync(s => s.SetProperty(q => q.Active, false), ct);
        foreach (var q in valide)
        {
            var intrebare = new TriviaQuestion { SubjectId = curs.MaterieId, CourseId = cursId, Text = q.Text.Trim(), Explanation = q.Explicatie?.Trim(), Source = "ai" };
            db.Add(intrebare);
            await db.SaveChangesAsync(ct);
            db.AddRange(q.Variante.Select((v, i) => new TriviaOption { QuestionId = intrebare.Id, Text = v.Trim(), IsCorrect = i == q.Corect }));
        }
        foreach (var e in rezultat.Estimari.Where(e => !string.IsNullOrWhiteSpace(e.Text)).Take(estimari))
            db.Add(new TriviaQuestion { SubjectId = curs.MaterieId, CourseId = cursId, Text = e.Text.Trim(), Kind = "estimare", NumericAnswer = e.Raspuns, Source = "ai" });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return valide.Count;
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

    private sealed record RezultatAi(List<GrilaAi> Grila, List<EstimareAi> Estimari);
    private sealed record GrilaAi(string Text, List<string> Variante, int Corect, string? Explicatie);
    private sealed record EstimareAi(string Text, int Raspuns);
}

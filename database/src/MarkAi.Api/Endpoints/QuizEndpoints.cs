using MarkAi.Api.Infrastructura;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Endpoints;

public record IncercareQuizDto(int Id, int Total, int Necesare, int Raspunse, IReadOnlyList<IntrebarePublica> Intrebari);
public record RaspunsQuiz(int IntrebareId, int VariantaId);
public record CorectieQuiz(int IntrebareId, int VariantaAleasaId, int VariantaCorectaId, string? Explicatie);
public record FinalQuiz(bool Promovat, int Corecte, int Total, int Necesare, int XpCastigat, DateTime? PromovatLa, IReadOnlyList<CorectieQuiz> Corectii);
public record RezultatRaspuns(bool Corect, int Ramase, FinalQuiz? Final);

/// <summary>
/// The Lecție quiz (questions about all its chapters and files). An attempt is a fixed random set of questions; each answer only says whether it was right, and the
/// correct options are revealed when the attempt ends. Passing needs <see cref="Prag"/> of the answers, and a new attempt
/// can start only after a short pause, so guessing through many attempts is slow and unrewarding.
/// </summary>
public static class QuizEndpoints
{
    /// <summary>Questions per attempt (<c>Limite:IntrebariPeTest</c>, 10 by default); fewer when the Lecție has fewer.</summary>
    public static int IntrebariPeIncercare(IConfiguration cfg) => Math.Clamp(cfg.GetValue("Limite:IntrebariPeTest", 10), 1, 50);
    /// <summary>An unfinished attempt is resumed (not replaced) for this long.</summary>
    private static readonly TimeSpan ValabilitateIncercare = TimeSpan.FromMinutes(30);

    /// <summary>Correct answers needed to pass: 80%, rounded up (8 of 10, 4 of 5).</summary>
    public static int Prag(int total) => (int)Math.Ceiling(total * 0.8);

    public static void Mapeaza(WebApplication app)
    {
        var g = app.MapGroup("/parcursuri/{id:int}/quiz").WithTags("Quiz").RequireAuthorization(Politici.Elev);

        g.MapPost("", async (int id, HttpContext h, MarkAiDbContext db, AccesContinut acces, IConfiguration cfg, TimeProvider ceas, CancellationToken ct) =>
        {
            await AsiguraLectieAsync(acces, h, id, ct);
            var uid = h.User.Id();
            var acum = ceas.GetUtcNow().UtcDateTime;

            var ultima = await db.Set<IncercareQuiz>().Where(i => i.UtilizatorId == uid && i.ParcursId == id)
                .OrderByDescending(i => i.InceputLa).FirstOrDefaultAsync(ct);
            if (ultima is { FinalizataLa: null } && acum - ultima.InceputLa < ValabilitateIncercare)
                return await Dto(db, ultima, ct);
            var pauza = TimeSpan.FromSeconds(cfg.GetValue("Limite:PauzaQuizSecunde", 60));
            if (ultima is not null && acum - ultima.InceputLa < pauza)
                throw new Conflict($"Poți începe un test nou peste {Math.Ceiling((pauza - (acum - ultima.InceputLa)).TotalSeconds)} secunde.");

            var disponibile = await IntrebariValide(db, id).Select(q => q.Id).ToListAsync(ct);
            if (disponibile.Count == 0) throw new Conflict("Lecția nu are încă întrebări.");
            var alese = disponibile.OrderBy(_ => Random.Shared.Next()).Take(IntrebariPeIncercare(cfg)).ToArray();
            var incercare = new IncercareQuiz { UtilizatorId = uid, ParcursId = id, IntrebariIds = alese, Raspunsuri = new int[alese.Length], InceputLa = acum };
            db.Add(incercare);
            await db.SaveChangesAsync(ct);
            return await Dto(db, incercare, ct);
        });

        g.MapGet("/{incercareId:int}/stare", async (int id, int incercareId, HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) =>
        {
            await AsiguraLectieAsync(acces, h, id, ct);
            var i = await db.Set<IncercareQuiz>().AsNoTracking().SingleOrDefaultAsync(i => i.Id == incercareId && i.ParcursId == id && i.UtilizatorId == h.User.Id(), ct)
                ?? throw new KeyNotFoundException("Testul nu există.");
            h.Response.Headers.CacheControl = "no-store";
            return (object)new { i.FinalizataLa, Raspunsuri = i.IntrebariIds.Select((q, n) => new { IntrebareId = q, VariantaId = i.Raspunsuri[n] }).ToArray() };
        });

        g.MapPost("/{incercareId:int}/raspuns", async (int id, int incercareId, RaspunsQuiz c, HttpContext h, MarkAiDbContext db,
            AccesContinut acces, ProgresService progres, TimeProvider ceas, CancellationToken ct) =>
        {
            // Access is checked again: the student may have left the organisation since the attempt started.
            await AsiguraLectieAsync(acces, h, id, ct);
            var incercare = await db.Set<IncercareQuiz>()
                .SingleOrDefaultAsync(i => i.Id == incercareId && i.ParcursId == id && i.UtilizatorId == h.User.Id(), ct)
                ?? throw new KeyNotFoundException("Testul nu există.");
            if (incercare.FinalizataLa is not null) throw new Conflict("Testul s-a încheiat. Începe unul nou.");
            var pozitie = Array.IndexOf(incercare.IntrebariIds, c.IntrebareId);
            if (pozitie < 0) throw new CerereInvalida("Întrebarea nu face parte din acest test.");
            if (incercare.Raspunsuri[pozitie] != 0) throw new Conflict("Ai răspuns deja la această întrebare.");

            var variante = await db.Set<VariantaIntrebare>().AsNoTracking().Where(o => o.IntrebareId == c.IntrebareId).ToListAsync(ct);
            if (variante.All(o => o.Id != c.VariantaId)) throw new CerereInvalida("Varianta nu aparține întrebării.");
            // A new array, so EF sees the change to the column.
            incercare.Raspunsuri = incercare.Raspunsuri.Select((r, i) => i == pozitie ? c.VariantaId : r).ToArray();
            var corect = variante.Any(o => o.Id == c.VariantaId && o.Corecta);
            if (!corect) await GreseliEndpoints.NoteazaAsync(db, h.User.Id(), c.IntrebareId, ceas.GetUtcNow().UtcDateTime, ct);
            db.Add(new RaspunsStatistica { IncercareId = incercare.Id, IntrebareId = c.IntrebareId, UtilizatorId = h.User.Id(), Corect = corect, RaspunsLa = ceas.GetUtcNow().UtcDateTime });
            var ramase = incercare.Raspunsuri.Count(r => r == 0);
            if (ramase > 0)
            {
                await db.SaveChangesAsync(ct);
                return new RezultatRaspuns(corect, ramase, null);
            }

            // Last answer: score from the stored answers (not a running count), then reveal the corrections.
            var ids = incercare.IntrebariIds;
            var corecte = await db.Set<VariantaIntrebare>().AsNoTracking().Where(o => ids.Contains(o.IntrebareId) && o.Corecta)
                .ToDictionaryAsync(o => o.IntrebareId, o => o.Id, ct);
            var explicatii = await db.Set<Intrebare>().AsNoTracking().Where(q => ids.Contains(q.Id))
                .ToDictionaryAsync(q => q.Id, q => q.Explicatie, ct);
            incercare.Corecte = ids.Select((q, i) => corecte.GetValueOrDefault(q) == incercare.Raspunsuri[i]).Count(x => x);
            incercare.FinalizataLa = ceas.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);

            var necesare = Prag(ids.Length);
            var promovat = incercare.Corecte >= necesare;
            RezultatProgres? p = promovat ? await progres.PromoveazaAsync(incercare.UtilizatorId, id, ct) : null;
            var corectii = ids.Select((q, i) => new CorectieQuiz(q, incercare.Raspunsuri[i], corecte.GetValueOrDefault(q), explicatii.GetValueOrDefault(q))).ToList();
            return new RezultatRaspuns(corect, 0, new FinalQuiz(promovat, incercare.Corecte, ids.Length, necesare, p?.XpCastigat ?? 0,
                p?.PromovatLa, corectii));
        });
    }

    // The quiz is for published Lecții the student may see; otherwise 404 (an outsider is not told it exists).
    private static async Task AsiguraLectieAsync(AccesContinut acces, HttpContext h, int parcursId, CancellationToken ct)
    {
        var a = await acces.PentruAsync(h.User, ct);
        if (!await acces.Parcursuri(a).AnyAsync(p => p.Id == parcursId && p.Publicat, ct)) throw new KeyNotFoundException("Lecția nu există.");
    }

    /// <summary>The Lecție's active multiple-choice questions with exactly one correct option among at least two.</summary>
    /// <remarks>"A correct option and a wrong one" is the same rule: the unique index <c>intrebare_varianta_o_corecta</c> allows at most one
    /// correct option, and two EXISTS probes cost less than counting the options (the same check is used by simulations, mistakes,
    /// offline packages and Triviador; homework keeps the counts, see TemeEndpoints).</remarks>
    public static IQueryable<Intrebare> IntrebariValide(MarkAiDbContext db, int parcursId) =>
        db.Set<Intrebare>().Where(q => q.ParcursId == parcursId && q.Activa && q.Tip == Intrebare.Grila
            && q.Variante.Any(o => o.Corecta) && q.Variante.Any(o => !o.Corecta));

    private static async Task<IncercareQuizDto> Dto(MarkAiDbContext db, IncercareQuiz incercare, CancellationToken ct)
    {
        var ids = incercare.IntrebariIds;
        var intrebari = await db.Set<Intrebare>().AsNoTracking().Where(q => ids.Contains(q.Id)).ToDictionaryAsync(q => q.Id, ct);
        var variante = (await db.Set<VariantaIntrebare>().AsNoTracking().Where(o => ids.Contains(o.IntrebareId)).OrderBy(o => o.Id).ToListAsync(ct))
            .ToLookup(o => o.IntrebareId);
        // Questions answered already are left out when an attempt is resumed; no option says which one is correct.
        var deAfisat = ids.Where((q, i) => incercare.Raspunsuri[i] == 0 && intrebari.ContainsKey(q))
            .Select(q => new IntrebarePublica(q, intrebari[q].Text, variante[q].Select(o => new VariantaPublica(o.Id, o.Text)).ToList()))
            .ToList();
        return new IncercareQuizDto(incercare.Id, ids.Length, Prag(ids.Length), incercare.Raspunsuri.Count(r => r != 0), deAfisat);
    }
}

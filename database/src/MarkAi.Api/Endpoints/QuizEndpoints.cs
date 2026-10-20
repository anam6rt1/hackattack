using MarkAi.Api.Infrastructura;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using MarkAi.Data.Trivia;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Endpoints;

public record IncercareQuizDto(int Id, int Total, int Necesare, int Raspunse, IReadOnlyList<IntrebarePublica> Intrebari);
public record RaspunsQuiz(int IntrebareId, int VariantaId);
public record CorectieQuiz(int IntrebareId, int VariantaAleasaId, int VariantaCorectaId, string? Explicatie);
public record FinalQuiz(bool Promovat, int Corecte, int Total, int Necesare, int XpCastigat, ProgresCursDto? Progres, IReadOnlyList<CorectieQuiz> Corectii);
public record RezultatRaspuns(bool Corect, int Ramase, FinalQuiz? Final);

/// <summary>
/// The lesson quiz. An attempt is a fixed random set of questions; each answer only says whether it was right, and the
/// correct options are revealed when the attempt ends. Passing needs <see cref="Prag"/> of the answers, and a new attempt
/// can start only after a short pause, so guessing through many attempts is slow and unrewarding.
/// </summary>
public static class QuizEndpoints
{
    public const int IntrebariPeIncercare = 5;
    /// <summary>An unfinished attempt is resumed (not replaced) for this long.</summary>
    private static readonly TimeSpan ValabilitateIncercare = TimeSpan.FromMinutes(30);

    /// <summary>Correct answers needed to pass: 80%, rounded up (4 of 5).</summary>
    public static int Prag(int total) => (int)Math.Ceiling(total * 0.8);

    public static void Mapeaza(WebApplication app)
    {
        var g = app.MapGroup("/cursuri/{id:int}/quiz").WithTags("Quiz").RequireAuthorization(Politici.Elev);

        g.MapPost("", async (int id, HttpContext h, MarkAiDbContext db, IConfiguration cfg, TimeProvider ceas, CancellationToken ct) =>
        {
            if (!await db.Curs.AnyAsync(c => c.Id == id && c.Publicat, ct)) throw new KeyNotFoundException("Lecția nu există.");
            var uid = h.User.Id();
            var acum = ceas.GetUtcNow().UtcDateTime;

            var ultima = await db.Set<IncercareQuiz>().Where(i => i.UtilizatorId == uid && i.CursId == id)
                .OrderByDescending(i => i.InceputLa).FirstOrDefaultAsync(ct);
            if (ultima is { FinalizataLa: null } && acum - ultima.InceputLa < ValabilitateIncercare)
                return await Dto(db, ultima, ct);
            var pauza = TimeSpan.FromSeconds(cfg.GetValue("Limite:PauzaQuizSecunde", 60));
            if (ultima is not null && acum - ultima.InceputLa < pauza)
                throw new Conflict($"Poți începe un test nou peste {Math.Ceiling((pauza - (acum - ultima.InceputLa)).TotalSeconds)} secunde.");

            var disponibile = await IntrebariValide(db, id).Select(q => q.Id).ToListAsync(ct);
            if (disponibile.Count == 0) throw new Conflict("Lecția nu are încă întrebări.");
            var alese = disponibile.OrderBy(_ => Random.Shared.Next()).Take(IntrebariPeIncercare).ToArray();
            var incercare = new IncercareQuiz { UtilizatorId = uid, CursId = id, IntrebariIds = alese, Raspunsuri = new int[alese.Length], InceputLa = acum };
            db.Add(incercare);
            await db.SaveChangesAsync(ct);
            return await Dto(db, incercare, ct);
        });

        g.MapPost("/{incercareId:int}/raspuns", async (int id, int incercareId, RaspunsQuiz c, HttpContext h, MarkAiDbContext db,
            ProgresService progres, TimeProvider ceas, CancellationToken ct) =>
        {
            var incercare = await db.Set<IncercareQuiz>()
                .SingleOrDefaultAsync(i => i.Id == incercareId && i.CursId == id && i.UtilizatorId == h.User.Id(), ct)
                ?? throw new KeyNotFoundException("Testul nu există.");
            if (incercare.FinalizataLa is not null) throw new Conflict("Testul s-a încheiat. Începe unul nou.");
            var pozitie = Array.IndexOf(incercare.IntrebariIds, c.IntrebareId);
            if (pozitie < 0) throw new CerereInvalida("Întrebarea nu face parte din acest test.");
            if (incercare.Raspunsuri[pozitie] != 0) throw new Conflict("Ai răspuns deja la această întrebare.");

            var variante = await db.Set<TriviaOption>().AsNoTracking().Where(o => o.QuestionId == c.IntrebareId).ToListAsync(ct);
            if (variante.All(o => o.Id != c.VariantaId)) throw new CerereInvalida("Varianta nu aparține întrebării.");
            // A new array, so EF sees the change to the column.
            incercare.Raspunsuri = incercare.Raspunsuri.Select((r, i) => i == pozitie ? c.VariantaId : r).ToArray();
            var corect = variante.Any(o => o.Id == c.VariantaId && o.IsCorrect);
            var ramase = incercare.Raspunsuri.Count(r => r == 0);
            if (ramase > 0)
            {
                await db.SaveChangesAsync(ct);
                return new RezultatRaspuns(corect, ramase, null);
            }

            // Last answer: score from the stored answers (not a running count), then reveal the corrections.
            var ids = incercare.IntrebariIds;
            var corecte = await db.Set<TriviaOption>().AsNoTracking().Where(o => ids.Contains(o.QuestionId) && o.IsCorrect)
                .ToDictionaryAsync(o => o.QuestionId, o => o.Id, ct);
            var explicatii = await db.Set<TriviaQuestion>().AsNoTracking().Where(q => ids.Contains(q.Id))
                .ToDictionaryAsync(q => q.Id, q => q.Explanation, ct);
            incercare.Corecte = ids.Select((q, i) => corecte.GetValueOrDefault(q) == incercare.Raspunsuri[i]).Count(x => x);
            incercare.FinalizataLa = ceas.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);

            var necesare = Prag(ids.Length);
            var promovat = incercare.Corecte >= necesare;
            RezultatProgres? p = promovat ? await progres.MarcheazaAsync(incercare.UtilizatorId, id, ActivitateCurs.Quiz, ct) : null;
            var corectii = ids.Select((q, i) => new CorectieQuiz(q, incercare.Raspunsuri[i], corecte.GetValueOrDefault(q), explicatii.GetValueOrDefault(q))).ToList();
            return new RezultatRaspuns(corect, 0, new FinalQuiz(promovat, incercare.Corecte, ids.Length, necesare, p?.XpCastigat ?? 0,
                p is null ? null : new ProgresCursDto(p.VideoVazut, p.MaterialeCitite, p.QuizPromovat, p.CompletatLa), corectii));
        });
    }

    // Active multiple-choice questions with exactly one correct option among at least two.
    private static IQueryable<TriviaQuestion> IntrebariValide(MarkAiDbContext db, int cursId) =>
        db.Set<TriviaQuestion>().Where(q => q.CourseId == cursId && q.Active && q.Kind == "grila"
            && db.Set<TriviaOption>().Count(o => o.QuestionId == q.Id) >= 2
            && db.Set<TriviaOption>().Count(o => o.QuestionId == q.Id && o.IsCorrect) == 1);

    private static async Task<IncercareQuizDto> Dto(MarkAiDbContext db, IncercareQuiz incercare, CancellationToken ct)
    {
        var ids = incercare.IntrebariIds;
        var intrebari = await db.Set<TriviaQuestion>().AsNoTracking().Where(q => ids.Contains(q.Id)).ToDictionaryAsync(q => q.Id, ct);
        var variante = (await db.Set<TriviaOption>().AsNoTracking().Where(o => ids.Contains(o.QuestionId)).OrderBy(o => o.Id).ToListAsync(ct))
            .ToLookup(o => o.QuestionId);
        // Questions answered already are left out when an attempt is resumed; no option says which one is correct.
        var deAfisat = ids.Where((q, i) => incercare.Raspunsuri[i] == 0 && intrebari.ContainsKey(q))
            .Select(q => new IntrebarePublica(q, intrebari[q].Text, variante[q].Select(o => new VariantaPublica(o.Id, o.Text)).ToList()))
            .ToList();
        return new IncercareQuizDto(incercare.Id, ids.Length, Prag(ids.Length), incercare.Raspunsuri.Count(r => r != 0), deAfisat);
    }
}

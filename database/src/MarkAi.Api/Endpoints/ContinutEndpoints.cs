using System.Security.Claims;
using MarkAi.Api.Infrastructura;
using MarkAi.Api.Servicii;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using MarkAi.Data.Trivia;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Endpoints;

public record MaterieDto(int Id, string? Cod, string Denumire, string? Descriere, int Ordine, int NumarCursuri, int CursuriCompletate);
public record ProgresCursDto(bool VideoVazut, bool MaterialeCitite, bool QuizPromovat, DateTime? CompletatLa);
public record CursRezumat(int Id, int MaterieId, string Titlu, string? Descriere, string? Capitol, int Ordine, bool AreVideo, ProgresCursDto? Progres);
public record CursDetaliu(int Id, int MaterieId, string Materie, string Titlu, string? Descriere, string? Capitol, int Ordine,
    string? VideoUrl, IReadOnlyList<SectiuneMaterial> Materiale, string? Continut, bool Publicat, ProgresCursDto? Progres);
public record VariantaPublica(int Id, string Text);
public record IntrebarePublica(int Id, string Text, IReadOnlyList<VariantaPublica> Variante);
public record MarcareActivitate(string Activitate);

public record MaterieEditare(string Denumire, string? Descriere, string? Cod, int? Ordine);
public record CursEditare(int MaterieId, string Titlu, string? Descriere, string? Capitol, int? Ordine, string? VideoUrl,
    List<SectiuneMaterial>? Materiale, string? Continut, bool? Publicat);
public record VariantaEditare(string Text, bool Corecta);
public record IntrebareEditare(string Text, string? Explicatie, bool Activa, List<VariantaEditare>? Variante, int? RaspunsNumeric);
public record VariantaCompleta(int Id, string Text, bool Corecta);
public record IntrebareCompleta(int Id, string Tip, string Text, string? Explicatie, string Sursa, bool Activa, int? RaspunsNumeric,
    IReadOnlyList<VariantaCompleta> Variante);

public static class ContinutEndpoints
{
    public static void Mapeaza(WebApplication app)
    {
        var g = app.MapGroup("").WithTags("Conținut").RequireAuthorization();

        // Anonymous too: the professor sign-up form lists the subjects.
        g.MapGet("/materii", async (HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var uid = h.User.Identity?.IsAuthenticated == true ? h.User.Id() : 0;
            var materii = await db.Materii.AsNoTracking().OrderBy(m => m.Ordine).ThenBy(m => m.Denumire)
                .Select(m => new MaterieDto(m.Id, m.Cod, m.Denumire, m.Descriere, m.Ordine,
                    m.Cursuri.Count(c => c.Publicat),
                    m.Cursuri.Count(c => c.Publicat && c.Progrese.Any(p => p.UtilizatorId == uid && p.QuizPromovat))))
                .ToListAsync(ct);
            return Lista<MaterieDto>.Din(materii);
        }).AllowAnonymous();

        g.MapGet("/materii/{id:int}", async (int id, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var uid = h.User.Id();
            return await db.Materii.AsNoTracking().Where(m => m.Id == id)
                .Select(m => new MaterieDto(m.Id, m.Cod, m.Denumire, m.Descriere, m.Ordine, m.Cursuri.Count(c => c.Publicat),
                    m.Cursuri.Count(c => c.Publicat && c.Progrese.Any(p => p.UtilizatorId == uid && p.QuizPromovat))))
                .SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Materia nu există.");
        });

        g.MapGet("/materii/{id:int}/cursuri", async (int id, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var uid = h.User.Id();
            var cursuri = await db.Curs.AsNoTracking().Where(c => c.MaterieId == id && c.Publicat)
                .OrderBy(c => c.Ordine).ThenBy(c => c.Id)
                .Select(c => new CursRezumat(c.Id, c.MaterieId, c.Titlu, c.Descriere, c.Capitol, c.Ordine, c.VideoUrl != null,
                    c.Progrese.Where(p => p.UtilizatorId == uid)
                        .Select(p => new ProgresCursDto(p.VideoVazut, p.MaterialeCitite, p.QuizPromovat, p.CompletatLa)).FirstOrDefault()))
                .ToListAsync(ct);
            return Lista<CursRezumat>.Din(cursuri);
        });

        g.MapGet("/cursuri/{id:int}", async (int id, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var curs = await Detaliu(db, id, h.User.Id(), ct);
            if (!curs.Publicat && !await PoateEdita(db, h.User, curs.MaterieId, ct)) throw new KeyNotFoundException("Cursul nu există.");
            return curs;
        });

        // The lesson quiz lives in QuizEndpoints: answers are checked there and never sent ahead.

        g.MapPost("/cursuri/{id:int}/progres", async (int id, MarcareActivitate c, HttpContext h, ProgresService progres, CancellationToken ct) =>
        {
            var activitate = c.Activitate switch
            {
                "video" => ActivitateCurs.Video,
                "materiale" => ActivitateCurs.Materiale,
                _ => throw new CerereInvalida("Activitatea trebuie să fie „video” sau „materiale”."),
            };
            var p = await progres.MarcheazaAsync(h.User.Id(), id, activitate, ct);
            return new ProgresCursDto(p.VideoVazut, p.MaterialeCitite, p.QuizPromovat, p.CompletatLa);
        }).RequireAuthorization(Politici.Elev);

        MapeazaEditare(app);
    }

    private static void MapeazaEditare(WebApplication app)
    {
        var e = app.MapGroup("").WithTags("Editare conținut").RequireAuthorization(Politici.Continut);

        e.MapPost("/materii", async (MaterieEditare c, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var m = new Materie();
            Aplica(m, c);
            // The professor who creates a subject teaches it, so they can add its lessons.
            if (!h.User.EsteAdmin()) m.Mentori.Add(new MentorMaterie { MentorId = h.User.Id() });
            db.Materii.Add(m);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/materii/{m.Id}", new MaterieDto(m.Id, m.Cod, m.Denumire, m.Descriere, m.Ordine, 0, 0));
        });

        e.MapPut("/materii/{id:int}", async (int id, MaterieEditare c, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            await AsiguraEditare(db, h.User, id, ct);
            var m = await db.Materii.FindAsync([id], ct) ?? throw new KeyNotFoundException("Materia nu există.");
            Aplica(m, c);
            await db.SaveChangesAsync(ct);
            return new MaterieDto(m.Id, m.Cod, m.Denumire, m.Descriere, m.Ordine, 0, 0);
        });

        // Everything the current editor may edit, published or not.
        e.MapGet("/continut/cursuri", async (int? materieId, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var q = db.Curs.AsNoTracking().AsQueryable();
            if (!h.User.EsteAdmin())
            {
                var uid = h.User.Id();
                q = q.Where(c => c.Materie.Mentori.Any(m => m.MentorId == uid));
            }
            if (materieId is not null) q = q.Where(c => c.MaterieId == materieId);
            var cursuri = await q.OrderBy(c => c.MaterieId).ThenBy(c => c.Ordine)
                .Select(c => new { c.Id, c.MaterieId, Materie = c.Materie.Denumire, c.Titlu, c.Capitol, c.Ordine, c.Publicat,
                    Intrebari = db.Set<TriviaQuestion>().Count(x => x.CourseId == c.Id && x.Active) })
                .ToListAsync(ct);
            return new { elemente = cursuri, total = cursuri.Count };
        });

        e.MapPost("/cursuri", async (CursEditare c, HttpContext h, MarkAiDbContext db, CoadaIntrebari coada, IConfiguration cfg, CancellationToken ct) =>
        {
            await AsiguraEditare(db, h.User, c.MaterieId, ct);
            var curs = new Curs { CreatDeId = h.User.Id() };
            Aplica(curs, c);
            curs.Ordine = c.Ordine ?? (await db.Curs.Where(x => x.MaterieId == c.MaterieId).MaxAsync(x => (int?)x.Ordine, ct) ?? 0) + 1;
            db.Curs.Add(curs);
            await db.SaveChangesAsync(ct);
            await PuneLaGenerare(db, coada, cfg, h.User, curs.Id, laCerere: false, ct);
            return Results.Created($"/cursuri/{curs.Id}", await Detaliu(db, curs.Id, h.User.Id(), ct));
        });

        e.MapPut("/cursuri/{id:int}", async (int id, CursEditare c, HttpContext h, MarkAiDbContext db, CoadaIntrebari coada, IConfiguration cfg, CancellationToken ct) =>
        {
            var curs = await db.Curs.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Cursul nu există.");
            await AsiguraEditare(db, h.User, curs.MaterieId, ct);
            if (c.MaterieId != curs.MaterieId) throw new CerereInvalida("Un curs nu poate fi mutat la altă materie.");
            var inainte = Amprenta(curs);
            Aplica(curs, c);
            if (c.Ordine is not null) curs.Ordine = c.Ordine.Value;
            await db.SaveChangesAsync(ct);
            // Questions follow the content: regenerate only when the learning material changed.
            if (Amprenta(curs) != inainte) await PuneLaGenerare(db, coada, cfg, h.User, curs.Id, laCerere: false, ct);
            return await Detaliu(db, curs.Id, h.User.Id(), ct);
        });

        e.MapPost("/cursuri/{id:int}/regenereaza-intrebari", async (int id, HttpContext h, MarkAiDbContext db, CoadaIntrebari coada, IConfiguration cfg, CancellationToken ct) =>
        {
            var materieId = await db.Curs.Where(x => x.Id == id).Select(x => (int?)x.MaterieId).SingleOrDefaultAsync(ct)
                ?? throw new KeyNotFoundException("Cursul nu există.");
            await AsiguraEditare(db, h.User, materieId, ct);
            await PuneLaGenerare(db, coada, cfg, h.User, id, laCerere: true, ct);
            return Results.Accepted($"/cursuri/{id}/generare", coada.Stare(id));
        }).RequireRateLimiting(LimiteCereri.Ai);

        e.MapGet("/cursuri/{id:int}/generare", (int id, CoadaIntrebari coada) =>
            coada.Stare(id) ?? new StareGenerare("necunoscuta", null, 0, DateTime.UtcNow));

        e.MapGet("/cursuri/{id:int}/intrebari/toate", async (int id, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var materieId = await db.Curs.Where(x => x.Id == id).Select(x => (int?)x.MaterieId).SingleOrDefaultAsync(ct)
                ?? throw new KeyNotFoundException("Cursul nu există.");
            await AsiguraEditare(db, h.User, materieId, ct);
            var intrebari = await db.Set<TriviaQuestion>().AsNoTracking().Where(q => q.CourseId == id)
                .OrderByDescending(q => q.Active).ThenBy(q => q.Id).ToListAsync(ct);
            var ids = intrebari.Select(q => q.Id).ToList();
            var variante = (await db.Set<TriviaOption>().AsNoTracking().Where(o => ids.Contains(o.QuestionId)).OrderBy(o => o.Id).ToListAsync(ct))
                .ToLookup(o => o.QuestionId);
            return Lista<IntrebareCompleta>.Din(intrebari.Select(q => Completa(q, variante[q.Id])).ToList());
        });

        e.MapPut("/intrebari/{id:int}", async (int id, IntrebareEditare c, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var q = await db.Set<TriviaQuestion>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Întrebarea nu există.");
            await AsiguraEditare(db, h.User, q.SubjectId, ct);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            q.Text = Validare.Obligatoriu(c.Text, "Întrebare", 1000);
            q.Explanation = Validare.Optional(c.Explicatie, 2000);
            q.Active = c.Activa;
            // An edited AI question is now owned by a person and survives regeneration.
            q.Source = "manual";
            if (q.Kind == "estimare") q.NumericAnswer = c.RaspunsNumeric ?? throw new CerereInvalida("Răspunsul numeric este obligatoriu.");
            else if (c.Variante is not null)
            {
                if (c.Variante.Count is < 2 or > 6 || c.Variante.Count(v => v.Corecta) != 1)
                    throw new CerereInvalida("O întrebare grilă are 2–6 variante și exact una corectă.");
                // Bank options are not referenced by played rounds (rounds keep their own copies).
                await db.Set<TriviaOption>().Where(o => o.QuestionId == id).ExecuteDeleteAsync(ct);
                db.AddRange(c.Variante.Select(v => new TriviaOption { QuestionId = id, Text = Validare.Obligatoriu(v.Text, "Variantă", 500), IsCorrect = v.Corecta }));
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Completa(q, await db.Set<TriviaOption>().AsNoTracking().Where(o => o.QuestionId == id).OrderBy(o => o.Id).ToListAsync(ct));
        });
    }

    private static IntrebareCompleta Completa(TriviaQuestion q, IEnumerable<TriviaOption> variante) =>
        new(q.Id, q.Kind, q.Text, q.Explanation, q.Source, q.Active, q.NumericAnswer,
            variante.Select(o => new VariantaCompleta(o.Id, o.Text, o.IsCorrect)).ToList());

    /// <summary>
    /// Queues AI question generation, within the editor's daily limit (admins are not limited). Over the limit, an explicit
    /// "regenerate" is refused; an automatic one after an edit is skipped and the editor sees why in the generation status.
    /// </summary>
    private static async Task PuneLaGenerare(MarkAiDbContext db, CoadaIntrebari coada, IConfiguration cfg, ClaimsPrincipal user, int cursId,
        bool laCerere, CancellationToken ct)
    {
        var uid = user.Id();
        if (!user.EsteAdmin())
        {
            var limita = cfg.GetValue("Limite:GenerariAiPeZi", 10);
            var deLa = DateTime.UtcNow.AddHours(-24);
            if (await db.Set<GenerareAi>().CountAsync(g => g.UtilizatorId == uid && g.CreatLa >= deLa, ct) >= limita)
            {
                var mesaj = $"Ai folosit cele {limita} generări de întrebări AI din ultimele 24 de ore. Poți edita întrebările manual sau reîncerca mâine.";
                if (laCerere) throw new Conflict(mesaj);
                coada.Refuza(cursId, mesaj);
                return;
            }
        }
        db.Add(new GenerareAi { UtilizatorId = uid, CursId = cursId, CreatLa = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        coada.Adauga(cursId);
    }

    private static string Amprenta(Curs c) =>
        $"{c.Titlu}\u0001{c.Continut}\u0001{string.Join('\u0002', c.Materiale.Select(m => m.Titlu + "\u0003" + m.Text))}";

    private static void Aplica(Materie m, MaterieEditare c)
    {
        m.Denumire = Validare.Obligatoriu(c.Denumire, "Denumire", 120);
        m.Descriere = Validare.Optional(c.Descriere, 1000);
        var cod = Validare.Optional(c.Cod, 60)?.ToLowerInvariant();
        if (cod is not null && !cod.All(ch => char.IsAsciiLetterLower(ch) || char.IsAsciiDigit(ch) || ch == '-'))
            throw new CerereInvalida("Codul poate conține doar litere mici, cifre și „-”.");
        m.Cod = cod;
        if (c.Ordine is not null) m.Ordine = c.Ordine.Value;
    }

    private static void Aplica(Curs curs, CursEditare c)
    {
        curs.MaterieId = c.MaterieId;
        curs.Titlu = Validare.Obligatoriu(c.Titlu, "Titlu", 200);
        curs.Descriere = Validare.Optional(c.Descriere, 1000);
        curs.Capitol = Validare.Optional(c.Capitol, 120);
        curs.Continut = Validare.Optional(c.Continut, 50_000);
        var video = Validare.Optional(c.VideoUrl, 500);
        if (video is not null && !(Uri.TryCreate(video, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps))
            throw new CerereInvalida("Linkul video trebuie să fie o adresă https.");
        curs.VideoUrl = video;
        curs.Materiale = (c.Materiale ?? []).Select(m => new SectiuneMaterial
        {
            Titlu = Validare.Obligatoriu(m.Titlu, "Titlu secțiune", 200), Text = Validare.Obligatoriu(m.Text, "Text secțiune", 10_000),
        }).ToList();
        curs.Publicat = c.Publicat ?? true;
    }

    private static async Task<bool> PoateEdita(MarkAiDbContext db, ClaimsPrincipal u, int materieId, CancellationToken ct)
    {
        if (u.EsteAdmin()) return true;
        if (!u.EsteProfesor()) return false;
        var uid = u.Id();
        return await db.Set<MentorMaterie>().AnyAsync(m => m.MentorId == uid && m.MaterieId == materieId
            && m.Mentor.StareVerificare == RezervareService.MentorAprobat, ct);
    }

    private static async Task AsiguraEditare(MarkAiDbContext db, ClaimsPrincipal u, int materieId, CancellationToken ct)
    {
        if (!await db.Materii.AnyAsync(m => m.Id == materieId, ct)) throw new KeyNotFoundException("Materia nu există.");
        if (!await PoateEdita(db, u, materieId, ct)) throw new UnauthorizedAccessException();
    }

    private static async Task<CursDetaliu> Detaliu(MarkAiDbContext db, int id, int uid, CancellationToken ct)
    {
        var c = await db.Curs.AsNoTracking().Include(x => x.Materie).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Cursul nu există.");
        var p = await db.Set<ProgresCurs>().AsNoTracking().SingleOrDefaultAsync(x => x.CursId == id && x.UtilizatorId == uid, ct);
        return new CursDetaliu(c.Id, c.MaterieId, c.Materie.Denumire, c.Titlu, c.Descriere, c.Capitol, c.Ordine, c.VideoUrl, c.Materiale,
            c.Continut, c.Publicat, p is null ? null : new ProgresCursDto(p.VideoVazut, p.MaterialeCitite, p.QuizPromovat, p.CompletatLa));
    }
}

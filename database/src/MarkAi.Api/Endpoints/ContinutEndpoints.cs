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
public record CursRezumat(int Id, int MaterieId, int ParcursId, string Titlu, string? Descriere, string? Capitol, int Ordine, bool AreVideo, bool Publicat,
    ProgresCursDto? Progres);
public record CursDetaliu(int Id, int MaterieId, string Materie, int ParcursId, string Parcurs, string Clasa, string Titlu, string? Descriere, string? Capitol,
    int Ordine, string? VideoUrl, IReadOnlyList<SectiuneMaterial> Materiale, string? Continut, bool Publicat, ProgresCursDto? Progres);
/// <param name="Loc">The home organisation's locality and county, e.g. "Sectorul 1, București"; null for platform Lecții.</param>
/// <param name="InLocalitateaTa">The home organisation is in the user's own locality (sorted first under "local").</param>
public record ParcursRezumat(int Id, int MaterieId, string Titlu, string? Descriere, string Clasa, string? Autor, string? Organizatie, string Vizibilitate,
    bool Publicat, int Ordine, int NumarCapitole, int CapitoleCompletate, string? Loc, bool InLocalitateaTa);
public record CapitolGasit(int Id, string Titlu, string? Capitol, int ParcursId, string Parcurs, string Clasa, int MaterieId, string Materie);
public record RezultatCautare(IReadOnlyList<ParcursRezumat> Lectii, IReadOnlyList<CapitolGasit> Capitole);
public record ParcursDetaliu(int Id, int MaterieId, string Materie, string Titlu, string? Descriere, string Clasa, string? Autor, string? Organizatie,
    string Vizibilitate, bool Publicat, bool PoateEdita, IReadOnlyList<CursRezumat> Capitole);
public record VariantaPublica(int Id, string Text);
public record IntrebarePublica(int Id, string Text, IReadOnlyList<VariantaPublica> Variante);
public record MarcareActivitate(string Activitate);

public record MaterieEditare(string Denumire, string? Descriere, string? Cod, int? Ordine);
public record ParcursEditare(int MaterieId, string Titlu, string? Descriere, string Clasa, string Vizibilitate, int? OrganizatieId,
    List<int>? Partajari, bool? Publicat, int? Ordine);
public record OrganizatieAutor(int Id, string Nume, bool PoatePublicaPublic);
public record ParcursEditabil(int Id, int MaterieId, string Materie, string Titlu, string? Descriere, string Clasa, string Vizibilitate, bool Publicat,
    int Ordine, int? AutorId, string? Autor, int? OrganizatieId, string? Organizatie, IReadOnlyList<int> Partajari, int NumarCapitole,
    bool PoatePublicaPublic, bool PoateMuta, IReadOnlyList<OrganizatieAutor> OrganizatiiPosibile);
public record OrdineCapitole(List<int> CursIds);
public record CursEditare(int ParcursId, string Titlu, string? Descriere, string? Capitol, int? Ordine, string? VideoUrl,
    List<SectiuneMaterial>? Materiale, string? Continut, bool? Publicat);
public record VariantaEditare(string Text, bool Corecta);
public record IntrebareEditare(string Text, string? Explicatie, bool Activa, List<VariantaEditare>? Variante, int? RaspunsNumeric);
public record VariantaCompleta(int Id, string Text, bool Corecta);
public record IntrebareCompleta(int Id, string Tip, string Text, string? Explicatie, string Sursa, bool Activa, int? RaspunsNumeric,
    IReadOnlyList<VariantaCompleta> Variante);

/// <summary>
/// Subjects (Materii), their Lecții (paths, <see cref="Parcurs"/>) and chapters (Capitole, <see cref="Curs"/>). Every read goes
/// through <see cref="AccesContinut"/>; editing a Lecție or its chapters is for its author, its home organisation's admin and
/// platform admins. Subjects are created and renamed by platform admins only.
/// </summary>
public static class ContinutEndpoints
{
    public static void Mapeaza(WebApplication app)
    {
        var g = app.MapGroup("").WithTags("Conținut").RequireAuthorization();

        // Anonymous too: the professor sign-up form lists the subjects (anonymous counts show only public content).
        g.MapGet("/materii", async (HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) =>
        {
            var numere = await Numere(acces, h.User, null, ct);
            var materii = await db.Materii.AsNoTracking().OrderBy(m => m.Ordine).ThenBy(m => m.Denumire).ToListAsync(ct);
            return Lista<MaterieDto>.Din(materii.Select(m => Dto(m, numere)).ToList());
        }).AllowAnonymous();

        g.MapGet("/materii/{id:int}", async (int id, HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) =>
        {
            var m = await db.Materii.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id, ct) ?? throw new KeyNotFoundException("Materia nu există.");
            return Dto(m, await Numere(acces, h.User, id, ct));
        });

        // The subject's Lecții the user may see. clasa: one or more of IX, X, XI, XII, Bac (comma-separated);
        // filtru: toate | organizatia-mea | public | local (see FiltreLectii); under "local", the user's own locality comes first.
        g.MapGet("/materii/{id:int}/parcursuri", async (int id, string? clasa, string? filtru, HttpContext h, MarkAiDbContext db, AccesContinut acces,
            CancellationToken ct) =>
        {
            var a = await acces.PentruAsync(h.User, ct);
            var loc = await LocUtilizator.PentruAsync(db, a.UtilizatorId, ct);
            var q = FiltreLectii.Aplica(acces.Parcursuri(a).AsNoTracking().Where(p => p.MaterieId == id), a, loc, filtru, clasa);
            var lista = await Rezumate(q, a.UtilizatorId, loc, ct);
            return Lista<ParcursRezumat>.Din(filtru == "local" ? lista.OrderByDescending(p => p.InLocalitateaTa).ToList() : lista);
        });

        // Lecții and chapters whose title or description has every word of q (without diacritics: "sarpe" finds "Șarpe"),
        // among what the user may see, with the same filters as the Lecții list; at most 30 of each.
        g.MapGet("/cautare", async (string? q, int? materieId, string? clasa, string? filtru, HttpContext h, MarkAiDbContext db, AccesContinut acces,
            CancellationToken ct) =>
        {
            var termeni = TermeniCautare.Din(q);
            var a = await acces.PentruAsync(h.User, ct);
            var loc = await LocUtilizator.PentruAsync(db, a.UtilizatorId, ct);
            var lectii = FiltreLectii.Aplica(acces.Parcursuri(a).AsNoTracking(), a, loc, filtru, clasa);
            if (materieId is not null) lectii = lectii.Where(p => p.MaterieId == materieId);
            var ids = lectii.Select(p => p.Id);
            var capitole = acces.Capitole(a).AsNoTracking().Where(c => ids.Contains(c.ParcursId));
            foreach (var t in termeni)
            {
                lectii = lectii.Where(p => EF.Functions.ILike(p.Cautare, t, "\\"));
                capitole = capitole.Where(c => EF.Functions.ILike(c.Cautare, t, "\\"));
            }
            var gasite = await Rezumate(lectii.OrderBy(p => p.Titlu).Take(30), a.UtilizatorId, loc, ct);
            var cap = await capitole.OrderBy(c => c.Titlu).Take(30)
                .Select(c => new CapitolGasit(c.Id, c.Titlu, c.Capitol, c.ParcursId, c.Parcurs.Titlu, c.Parcurs.Clasa, c.MaterieId, c.Materie.Denumire))
                .ToListAsync(ct);
            return new RezultatCautare(filtru == "local" ? gasite.OrderByDescending(p => p.InLocalitateaTa).ToList() : gasite, cap);
        });

        g.MapGet("/parcursuri/{id:int}", async (int id, HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) =>
        {
            var a = await acces.PentruAsync(h.User, ct);
            var p = await acces.Parcursuri(a).AsNoTracking().Where(p => p.Id == id)
                .Select(p => new { p.Id, p.MaterieId, Materie = p.Materie.Denumire, p.Titlu, p.Descriere, p.Clasa, Autor = p.Autor == null ? null : p.Autor.Nume,
                    Organizatie = p.Organizatie == null ? null : p.Organizatie.Nume, p.Vizibilitate, p.Publicat })
                .SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Lecția nu există.");
            var poateEdita = await acces.Editabile(a).AnyAsync(x => x.Id == id, ct);
            var uid = a.UtilizatorId;
            var capitole = await acces.Capitole(a).AsNoTracking().Where(c => c.ParcursId == id).OrderBy(c => c.Ordine).ThenBy(c => c.Id)
                .Select(c => new CursRezumat(c.Id, c.MaterieId, c.ParcursId, c.Titlu, c.Descriere, c.Capitol, c.Ordine, c.VideoUrl != null, c.Publicat,
                    c.Progrese.Where(x => x.UtilizatorId == uid)
                        .Select(x => new ProgresCursDto(x.VideoVazut, x.MaterialeCitite, x.QuizPromovat, x.CompletatLa)).FirstOrDefault()))
                .ToListAsync(ct);
            return new ParcursDetaliu(p.Id, p.MaterieId, p.Materie, p.Titlu, p.Descriere, p.Clasa, p.Autor, p.Organizatie, p.Vizibilitate, p.Publicat,
                poateEdita, capitole);
        });

        g.MapGet("/cursuri/{id:int}", async (int id, HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) =>
        {
            await acces.AsiguraCapitolVizibilAsync(h.User, id, ct);
            return await Detaliu(db, id, h.User.Id(), ct);
        });

        // The lesson quiz lives in QuizEndpoints: answers are checked there and never sent ahead.

        g.MapPost("/cursuri/{id:int}/progres", async (int id, MarcareActivitate c, HttpContext h, AccesContinut acces, ProgresService progres, CancellationToken ct) =>
        {
            var activitate = c.Activitate switch
            {
                "video" => ActivitateCurs.Video,
                "materiale" => ActivitateCurs.Materiale,
                _ => throw new CerereInvalida("Activitatea trebuie să fie „video” sau „materiale”."),
            };
            await acces.AsiguraCapitolVizibilAsync(h.User, id, ct);
            var p = await progres.MarcheazaAsync(h.User.Id(), id, activitate, ct);
            return new ProgresCursDto(p.VideoVazut, p.MaterialeCitite, p.QuizPromovat, p.CompletatLa);
        }).RequireAuthorization(Politici.Elev);

        MapeazaMaterii(app);
        MapeazaParcursuri(app);
        MapeazaCapitole(app);
    }

    private static void MapeazaMaterii(WebApplication app)
    {
        var e = app.MapGroup("").WithTags("Editare conținut").RequireAuthorization(Politici.Admin);

        e.MapPost("/materii", async (MaterieEditare c, MarkAiDbContext db, CancellationToken ct) =>
        {
            var m = new Materie();
            Aplica(m, c);
            db.Materii.Add(m);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/materii/{m.Id}", new MaterieDto(m.Id, m.Cod, m.Denumire, m.Descriere, m.Ordine, 0, 0));
        });

        e.MapPut("/materii/{id:int}", async (int id, MaterieEditare c, MarkAiDbContext db, CancellationToken ct) =>
        {
            var m = await db.Materii.FindAsync([id], ct) ?? throw new KeyNotFoundException("Materia nu există.");
            Aplica(m, c);
            await db.SaveChangesAsync(ct);
            return new MaterieDto(m.Id, m.Cod, m.Denumire, m.Descriere, m.Ordine, 0, 0);
        });
    }

    private static void MapeazaParcursuri(WebApplication app)
    {
        var e = app.MapGroup("").WithTags("Editare conținut").RequireAuthorization(Politici.Continut);

        // The organisations the current user may pick as the home of a new Lecție (platform admins may also pick none).
        e.MapGet("/continut/organizatii", async (HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) =>
            Lista<OrganizatieAutor>.Din(await OrganizatiiPosibile(db, acces, h.User, h.User.Id(), ct)));

        // Everything the current user may edit, published or not.
        e.MapGet("/continut/parcursuri", async (int? materieId, HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) =>
        {
            var a = await acces.PentruAsync(h.User, ct);
            var q = acces.Editabile(a).AsNoTracking();
            if (materieId is not null) q = q.Where(p => p.MaterieId == materieId);
            var ids = await q.OrderBy(p => p.MaterieId).ThenBy(p => p.Ordine).ThenBy(p => p.Titlu).Select(p => p.Id).ToListAsync(ct);
            var lista = new List<ParcursEditabil>();
            foreach (var id in ids) lista.Add(await Editabil(db, acces, h.User, id, ct));
            return Lista<ParcursEditabil>.Din(lista);
        });

        e.MapGet("/continut/parcursuri/{id:int}", async (int id, HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) =>
        {
            await acces.PentruEditareAsync(h.User, id, ct);
            return await Editabil(db, acces, h.User, id, ct);
        });

        e.MapPost("/parcursuri", async (ParcursEditare c, HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) =>
        {
            if (!await db.Materii.AnyAsync(m => m.Id == c.MaterieId, ct)) throw new CerereInvalida("Materia nu există.");
            var uid = h.User.Id();
            // Professors write Lecții in the subjects they teach (set in their profile); platform admins in any.
            if (!h.User.EsteAdmin() && !await db.Set<MentorMaterie>().AnyAsync(m => m.MentorId == uid && m.MaterieId == c.MaterieId, ct))
                throw new CerereInvalida("Poți crea lecții doar la materiile pe care le predai. Adaugă materia în profilul tău de profesor.");
            var p = new Parcurs { MaterieId = c.MaterieId, AutorId = uid, CreatLa = DateTime.UtcNow };
            await Aplica(p, c, h.User, db, acces, nou: true, ct);
            p.Ordine = c.Ordine ?? (await db.Set<Parcurs>().Where(x => x.MaterieId == c.MaterieId).MaxAsync(x => (int?)x.Ordine, ct) ?? 0) + 1;
            db.Add(p);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/parcursuri/{p.Id}", await Editabil(db, acces, h.User, p.Id, ct));
        });

        e.MapPut("/parcursuri/{id:int}", async (int id, ParcursEditare c, HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) =>
        {
            var p = await acces.PentruEditareAsync(h.User, id, ct);
            if (c.MaterieId != p.MaterieId) throw new CerereInvalida("O lecție nu poate fi mutată la altă materie.");
            await Aplica(p, c, h.User, db, acces, nou: false, ct);
            if (c.Ordine is not null) p.Ordine = c.Ordine.Value;
            await db.SaveChangesAsync(ct);
            return await Editabil(db, acces, h.User, p.Id, ct);
        });

        // Hard delete, with every chapter and the students' progress on them (XP already earned stays).
        e.MapDelete("/parcursuri/{id:int}", async (int id, HttpContext h, AccesContinut acces, StergereContinut stergere, CancellationToken ct) =>
        {
            await acces.PentruEditareAsync(h.User, id, ct);
            await stergere.StergeParcursAsync(id, ct);
            return Results.NoContent();
        });

        // The recommended chapter order: every chapter of the Lecție, in the new order.
        e.MapPut("/parcursuri/{id:int}/ordine", async (int id, OrdineCapitole c, HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) =>
        {
            await acces.PentruEditareAsync(h.User, id, ct);
            var capitole = await db.Curs.Where(x => x.ParcursId == id).ToListAsync(ct);
            if (c.CursIds is null || c.CursIds.Count != capitole.Count || c.CursIds.Distinct().Count() != capitole.Count
                || capitole.Any(x => !c.CursIds.Contains(x.Id)))
                throw new CerereInvalida("Trimite toate capitolele lecției, fiecare o singură dată.");
            foreach (var x in capitole) x.Ordine = c.CursIds.IndexOf(x.Id) + 1;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    private static void MapeazaCapitole(WebApplication app)
    {
        var e = app.MapGroup("").WithTags("Editare conținut").RequireAuthorization(Politici.Continut);

        e.MapGet("/continut/cursuri", async (int parcursId, HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) =>
        {
            await acces.PentruEditareAsync(h.User, parcursId, ct);
            var cursuri = await db.Curs.AsNoTracking().Where(c => c.ParcursId == parcursId).OrderBy(c => c.Ordine).ThenBy(c => c.Id)
                .Select(c => new { c.Id, c.ParcursId, c.MaterieId, c.Titlu, c.Capitol, c.Ordine, c.Publicat,
                    Intrebari = db.Set<TriviaQuestion>().Count(x => x.CourseId == c.Id && x.Active) })
                .ToListAsync(ct);
            return new { elemente = cursuri, total = cursuri.Count };
        });

        e.MapPost("/cursuri", async (CursEditare c, HttpContext h, MarkAiDbContext db, AccesContinut acces, CoadaIntrebari coada, IConfiguration cfg, CancellationToken ct) =>
        {
            var p = await acces.PentruEditareAsync(h.User, c.ParcursId, ct);
            var curs = new Curs { CreatDeId = h.User.Id(), MaterieId = p.MaterieId, ParcursId = p.Id };
            Aplica(curs, c);
            curs.Ordine = c.Ordine ?? (await db.Curs.Where(x => x.ParcursId == p.Id).MaxAsync(x => (int?)x.Ordine, ct) ?? 0) + 1;
            db.Curs.Add(curs);
            await db.SaveChangesAsync(ct);
            await PuneLaGenerare(db, coada, cfg, h.User, curs.Id, laCerere: false, ct);
            return Results.Created($"/cursuri/{curs.Id}", await Detaliu(db, curs.Id, h.User.Id(), ct));
        });

        e.MapPut("/cursuri/{id:int}", async (int id, CursEditare c, HttpContext h, MarkAiDbContext db, AccesContinut acces, CoadaIntrebari coada, IConfiguration cfg, CancellationToken ct) =>
        {
            var curs = await acces.CapitolPentruEditareAsync(h.User, id, ct);
            // A chapter may move to another Lecție of the same subject that the user may also edit.
            if (c.ParcursId != curs.ParcursId)
            {
                var alt = await acces.PentruEditareAsync(h.User, c.ParcursId, ct);
                if (alt.MaterieId != curs.MaterieId) throw new CerereInvalida("Un capitol poate fi mutat doar într-o lecție de la aceeași materie.");
                curs.ParcursId = alt.Id;
            }
            var inainte = Amprenta(curs);
            Aplica(curs, c);
            if (c.Ordine is not null) curs.Ordine = c.Ordine.Value;
            await db.SaveChangesAsync(ct);
            // Questions follow the content: regenerate only when the learning material changed.
            if (Amprenta(curs) != inainte) await PuneLaGenerare(db, coada, cfg, h.User, curs.Id, laCerere: false, ct);
            return await Detaliu(db, curs.Id, h.User.Id(), ct);
        });

        e.MapDelete("/cursuri/{id:int}", async (int id, HttpContext h, AccesContinut acces, StergereContinut stergere, CancellationToken ct) =>
        {
            await acces.CapitolPentruEditareAsync(h.User, id, ct);
            await stergere.StergeCapitoleAsync([id], ct);
            return Results.NoContent();
        });

        e.MapPost("/cursuri/{id:int}/regenereaza-intrebari", async (int id, HttpContext h, MarkAiDbContext db, AccesContinut acces, CoadaIntrebari coada, IConfiguration cfg, CancellationToken ct) =>
        {
            await acces.CapitolPentruEditareAsync(h.User, id, ct);
            await PuneLaGenerare(db, coada, cfg, h.User, id, laCerere: true, ct);
            return Results.Accepted($"/cursuri/{id}/generare", coada.Stare(id));
        }).RequireRateLimiting(LimiteCereri.Ai);

        e.MapGet("/cursuri/{id:int}/generare", async (int id, HttpContext h, AccesContinut acces, CoadaIntrebari coada, CancellationToken ct) =>
        {
            await acces.CapitolPentruEditareAsync(h.User, id, ct);
            return coada.Stare(id) ?? new StareGenerare("necunoscuta", null, 0, DateTime.UtcNow);
        });

        e.MapGet("/cursuri/{id:int}/intrebari/toate", async (int id, HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) =>
        {
            await acces.CapitolPentruEditareAsync(h.User, id, ct);
            var intrebari = await db.Set<TriviaQuestion>().AsNoTracking().Where(q => q.CourseId == id)
                .OrderByDescending(q => q.Active).ThenBy(q => q.Id).ToListAsync(ct);
            var ids = intrebari.Select(q => q.Id).ToList();
            var variante = (await db.Set<TriviaOption>().AsNoTracking().Where(o => ids.Contains(o.QuestionId)).OrderBy(o => o.Id).ToListAsync(ct))
                .ToLookup(o => o.QuestionId);
            return Lista<IntrebareCompleta>.Din(intrebari.Select(q => Completa(q, variante[q.Id])).ToList());
        });

        e.MapPut("/intrebari/{id:int}", async (int id, IntrebareEditare c, HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) =>
        {
            var q = await db.Set<TriviaQuestion>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Întrebarea nu există.");
            // Questions of a whole subject (no chapter) belong to the platform.
            if (q.CourseId is { } cursId) await acces.CapitolPentruEditareAsync(h.User, cursId, ct);
            else if (!h.User.EsteAdmin()) throw new UnauthorizedAccessException();
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

    // Chapters a student would see, per subject: (total, completed by the user).
    private static async Task<Dictionary<int, (int Total, int Completate)>> Numere(AccesContinut acces, ClaimsPrincipal u, int? materieId, CancellationToken ct)
    {
        var a = await acces.PentruAsync(u, ct);
        var uid = a.UtilizatorId;
        var q = acces.Capitole(a).AsNoTracking().Where(c => c.Publicat && c.Parcurs.Publicat);
        if (materieId is not null) q = q.Where(c => c.MaterieId == materieId);
        var randuri = await q.Select(c => new { c.MaterieId, Gata = c.Progrese.Any(p => p.UtilizatorId == uid && p.QuizPromovat) }).ToListAsync(ct);
        return randuri.GroupBy(r => r.MaterieId).ToDictionary(g => g.Key, g => (g.Count(), g.Count(r => r.Gata)));
    }

    private static async Task<List<ParcursRezumat>> Rezumate(IQueryable<Parcurs> q, int uid, LocUtilizator loc, CancellationToken ct)
    {
        var localitate = loc.LocalitateId;
        return await q.OrderBy(p => p.Ordine).ThenBy(p => p.Titlu)
            .Select(p => new ParcursRezumat(p.Id, p.MaterieId, p.Titlu, p.Descriere, p.Clasa, p.Autor == null ? null : p.Autor.Nume,
                p.Organizatie == null ? null : p.Organizatie.Nume, p.Vizibilitate, p.Publicat,
                p.Ordine, p.Capitole.Count(c => c.Publicat), p.Capitole.Count(c => c.Publicat && c.Progrese.Any(x => x.UtilizatorId == uid && x.QuizPromovat)),
                p.Organizatie == null ? null : (p.Organizatie.Localitate == null ? "" : p.Organizatie.Localitate.Nume + ", ") + p.Organizatie.Judet.Nume,
                localitate != null && p.Organizatie != null && p.Organizatie.LocalitateId == localitate))
            .ToListAsync(ct);
    }

    private static MaterieDto Dto(Materie m, Dictionary<int, (int Total, int Completate)> numere)
    {
        var (total, completate) = numere.GetValueOrDefault(m.Id);
        return new MaterieDto(m.Id, m.Cod, m.Denumire, m.Descriere, m.Ordine, total, completate);
    }

    private static async Task<List<OrganizatieAutor>> OrganizatiiPosibile(MarkAiDbContext db, AccesContinut acces, ClaimsPrincipal u, int? autorId, CancellationToken ct)
    {
        var aleAutorului = autorId is null ? [] : (await acces.OrganizatiiAutorAsync(autorId.Value, ct)).Select(o => new OrganizatieAutor(o.Id, o.Nume, o.PoatePublicaPublic)).ToList();
        if (!u.EsteAdmin()) return aleAutorului;
        // Platform admins may place a Lecție in any approved organisation.
        var toate = await db.Set<Organizatie>().AsNoTracking().Where(o => o.Stare == Organizatie.Aprobata).OrderBy(o => o.Nume).Select(o => new { o.Id, o.Nume }).ToListAsync(ct);
        return toate.Select(o => aleAutorului.FirstOrDefault(x => x.Id == o.Id) ?? new OrganizatieAutor(o.Id, o.Nume, true)).ToList();
    }

    private static async Task<ParcursEditabil> Editabil(MarkAiDbContext db, AccesContinut acces, ClaimsPrincipal u, int id, CancellationToken ct)
    {
        var p = await db.Set<Parcurs>().AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.Id, x.MaterieId, Materie = x.Materie.Denumire, x.Titlu, x.Descriere, x.Clasa, x.Vizibilitate, x.Publicat, x.Ordine, x.AutorId,
                Autor = x.Autor == null ? null : x.Autor.Nume, x.OrganizatieId, Organizatie = x.Organizatie == null ? null : x.Organizatie.Nume,
                Partajari = x.Partajari.Select(s => s.OrganizatieId).ToList(), Capitole = x.Capitole.Count })
            .SingleAsync(ct);
        // Only the author (or a platform admin) moves a Lecție to another home organisation, and only among the author's.
        var poateMuta = u.EsteAdmin() || (p.AutorId is not null && p.AutorId == u.Id());
        var posibile = await OrganizatiiPosibile(db, acces, u, p.AutorId, ct);
        return new ParcursEditabil(p.Id, p.MaterieId, p.Materie, p.Titlu, p.Descriere, p.Clasa, p.Vizibilitate, p.Publicat, p.Ordine, p.AutorId, p.Autor,
            p.OrganizatieId, p.Organizatie, p.Partajari, p.Capitole, await acces.PoatePublicaPublicAsync(p.AutorId, p.OrganizatieId, ct), poateMuta, posibile);
    }

    private static async Task Aplica(Parcurs p, ParcursEditare c, ClaimsPrincipal u, MarkAiDbContext db, AccesContinut acces, bool nou, CancellationToken ct)
    {
        p.Titlu = Validare.Obligatoriu(c.Titlu, "Titlu", 200);
        p.Descriere = Validare.Optional(c.Descriere, 1000);
        p.Clasa = Parcurs.Clase.Contains(c.Clasa) ? c.Clasa : throw new CerereInvalida("Clasa trebuie să fie IX, X, XI, XII sau Bac.");
        p.Vizibilitate = c.Vizibilitate is Parcurs.Public or Parcurs.DoarOrganizatie ? c.Vizibilitate
            : throw new CerereInvalida("Vizibilitatea trebuie să fie „public” sau „organizatie”.");
        p.Publicat = c.Publicat ?? true;

        var aleAutorului = p.AutorId is null ? [] : await acces.OrganizatiiAutorAsync(p.AutorId.Value, ct);
        if (nou || c.OrganizatieId != p.OrganizatieId)
        {
            if (!nou && !(u.EsteAdmin() || p.AutorId == u.Id()))
                throw new CerereInvalida("Doar autorul poate muta lecția în altă organizație.");
            if (c.OrganizatieId is null)
            {
                if (!u.EsteAdmin()) throw new CerereInvalida("Alege organizația lecției.");
            }
            else if (u.EsteAdmin() ? !await db.Set<Organizatie>().AnyAsync(o => o.Id == c.OrganizatieId && o.Stare == Organizatie.Aprobata, ct)
                     : aleAutorului.All(o => o.Id != c.OrganizatieId))
                throw new CerereInvalida("Poți alege doar o organizație aprobată din care faci parte.");
            p.OrganizatieId = c.OrganizatieId;
        }
        if (p.Vizibilitate == Parcurs.DoarOrganizatie && p.OrganizatieId is null)
            throw new CerereInvalida("O lecție vizibilă doar în organizație trebuie să aibă o organizație.");
        if (p.Vizibilitate == Parcurs.Public && !await acces.PoatePublicaPublicAsync(p.AutorId, p.OrganizatieId, ct))
            throw new Conflict("Organizația nu permite publicarea acestei lecții pentru toți. Poate fi vizibilă doar în organizație.");

        // View-only shares: only the author's other organisations (existing ones may always be kept or removed).
        var partajari = (c.Partajari ?? []).Distinct().Where(id => id != p.OrganizatieId).ToList();
        var permise = aleAutorului.Select(o => o.Id).Concat(p.Partajari.Select(s => s.OrganizatieId)).ToHashSet();
        if (partajari.Any(id => !permise.Contains(id)))
            throw new CerereInvalida("O lecție poate fi arătată doar în celelalte organizații ale autorului.");
        foreach (var s in p.Partajari.Where(s => !partajari.Contains(s.OrganizatieId)).ToList()) p.Partajari.Remove(s);
        foreach (var id in partajari.Where(id => p.Partajari.All(s => s.OrganizatieId != id)))
            p.Partajari.Add(new ParcursOrganizatie { OrganizatieId = id });
    }

    private static IntrebareCompleta Completa(TriviaQuestion q, IEnumerable<TriviaOption> variante) =>
        new(q.Id, q.Kind, q.Text, q.Explanation, q.Source, q.Active, q.NumericAnswer,
            variante.Select(o => new VariantaCompleta(o.Id, o.Text, o.IsCorrect)).ToList());

    private static Task PuneLaGenerare(MarkAiDbContext db, CoadaIntrebari coada, IConfiguration cfg, ClaimsPrincipal user, int cursId,
        bool laCerere, CancellationToken ct) =>
        LimitaGenerariAi.PuneAsync(db, coada, cfg, user.Id(), user.EsteAdmin(), cursId, laCerere, ct);

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

    private static async Task<CursDetaliu> Detaliu(MarkAiDbContext db, int id, int uid, CancellationToken ct)
    {
        var c = await db.Curs.AsNoTracking().Include(x => x.Materie).Include(x => x.Parcurs).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Lecția nu există.");
        var p = await db.Set<ProgresCurs>().AsNoTracking().SingleOrDefaultAsync(x => x.CursId == id && x.UtilizatorId == uid, ct);
        return new CursDetaliu(c.Id, c.MaterieId, c.Materie.Denumire, c.ParcursId, c.Parcurs.Titlu, c.Parcurs.Clasa, c.Titlu, c.Descriere, c.Capitol, c.Ordine,
            c.VideoUrl, c.Materiale, c.Continut, c.Publicat,
            p is null ? null : new ProgresCursDto(p.VideoVazut, p.MaterialeCitite, p.QuizPromovat, p.CompletatLa));
    }
}

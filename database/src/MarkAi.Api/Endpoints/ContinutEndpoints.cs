using System.Security.Claims;
using MarkAi.Api.Infrastructura;
using MarkAi.Api.Servicii;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Endpoints;

/// <param name="NumarLectii">Published Lecții the user may see; <paramref name="LectiiCompletate"/> = those whose quiz they passed.</param>
public record MaterieDto(int Id, string? Cod, string Denumire, string? Descriere, int Ordine, int NumarLectii, int LectiiCompletate);
/// <param name="PromovatLa">When the student passed the Lecție's quiz (the only thing that completes a Lecție).</param>
/// <param name="UltimulCursId">The chapter they opened last.</param>
public record ProgresLectieDto(DateTime? PromovatLa, int? UltimulCursId);
public record CursRezumat(int Id, int MaterieId, int ParcursId, string Titlu, string? Descriere, string? Capitol, int Ordine, bool AreVideo, bool Publicat);
public record CursDetaliu(int Id, int MaterieId, string Materie, int ParcursId, string Parcurs, string Clasa, string Titlu, string? Descriere, string? Capitol,
    int Ordine, string? VideoUrl, IReadOnlyList<SectiuneMaterial> Materiale, string? Continut, bool Publicat);
/// <param name="Loc">The home organisation's locality and county, e.g. "Sectorul 1, București"; null for platform Lecții.</param>
/// <param name="InLocalitateaTa">The home organisation is in the user's own locality (sorted first under "local").</param>
public record ParcursRezumat(int Id, int MaterieId, string Titlu, string? Descriere, string Clasa, string? Autor, string? Organizatie, string Vizibilitate,
    bool Publicat, int Ordine, int NumarCapitole, bool Completata, string? Loc, bool InLocalitateaTa);
public record CapitolGasit(int Id, string Titlu, string? Capitol, int ParcursId, string Parcurs, string Clasa, int MaterieId, string Materie);
public record RezultatCautare(IReadOnlyList<ParcursRezumat> Lectii, IReadOnlyList<CapitolGasit> Capitole);
/// <param name="IntrebariTest">Questions the Lecție's quiz can draw from (0: no quiz yet).</param>
public record ParcursDetaliu(int Id, int MaterieId, string Materie, string Titlu, string? Descriere, string Clasa, string? Autor, string? Organizatie,
    string Vizibilitate, bool Publicat, bool PoateEdita, int IntrebariTest, ProgresLectieDto? Progres, IReadOnlyList<CursRezumat> Capitole);
public record VariantaPublica(int Id, string Text);
public record IntrebarePublica(int Id, string Text, IReadOnlyList<VariantaPublica> Variante);

public record MaterieEditare(string Denumire, string? Descriere, string? Cod, int? Ordine);
public record ParcursEditare(int MaterieId, string Titlu, string? Descriere, string Clasa, string Vizibilitate, int? OrganizatieId,
    List<int>? Partajari, bool? Publicat, int? Ordine);
public record OrganizatieAutor(int Id, string Nume, bool PoatePublicaPublic);
public record ParcursEditabil(int Id, int MaterieId, string Materie, string Titlu, string? Descriere, string Clasa, string Vizibilitate, bool Publicat,
    int Ordine, int? AutorId, string? Autor, int? OrganizatieId, string? Organizatie, IReadOnlyList<int> Partajari, int NumarCapitole,
    int NumarIntrebari, bool PoatePublicaPublic, bool PoateMuta, IReadOnlyList<OrganizatieAutor> OrganizatiiPosibile);
public record OrdineCapitole(List<int> CursIds);
public record CursEditare(int ParcursId, string Titlu, string? Descriere, string? Capitol, int? Ordine, string? VideoUrl,
    List<SectiuneMaterial>? Materiale, string? Continut, bool? Publicat);
public record VariantaEditare(string Text, bool Corecta);
/// <param name="Tip">Only when adding a question: "grila" (default) or "estimare".</param>
public record IntrebareEditare(string Text, string? Explicatie, bool Activa, List<VariantaEditare>? Variante, int? RaspunsNumeric, string? Tip = null);
public record CerereGenerare(int Numar);
public record VariantaCompleta(int Id, string Text, bool Corecta);
public record IntrebareCompleta(int Id, string Tip, string Text, string? Explicatie, string Sursa, bool Activa, int? RaspunsNumeric,
    IReadOnlyList<VariantaCompleta> Variante);

/// <summary>
/// Subjects (Materii), their Lecții (paths, <see cref="Parcurs"/>), chapters (Capitole, <see cref="Curs"/>) and each Lecție's
/// quiz questions. Every read goes through <see cref="AccesContinut"/>; editing a Lecție, its chapters or its questions is for
/// its author, its home organisation's admin and platform admins. Subjects are created and renamed by platform admins only.
/// </summary>
public static class ContinutEndpoints
{
    public static void Mapeaza(WebApplication app)
    {
        var g = app.MapGroup("").WithTags("Conținut").RequireAuthorization();

        // Anonymous too: the professor sign-up form lists the subjects (anonymous counts show only public content). The counts are
        // computed in SQL. With pagina or marime (the Materiale overview), a page at a time (Pagini.Materii*, by role); without them
        // every subject, for the subject pickers (a short reference list).
        g.MapGet("/materii", async (int? pagina, int? marime, HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) =>
        {
            var a = await acces.PentruAsync(h.User, ct);
            var ordonate = db.Materii.AsNoTracking().OrderBy(m => m.Ordine).ThenBy(m => m.Denumire).ThenBy(m => m.Id);
            if (pagina is null && marime is null) return Lista<MaterieDto>.Din(await CuNumere(ordonate, acces, a).ToListAsync(ct));
            var lista = h.User.EsteAdmin() ? Pagini.MateriiAdmin : Pagini.MateriiElev;
            Pagini.Fereastra(lista, pagina, marime);
            var total = await ordonate.CountAsync(ct);
            return new Lista<MaterieDto>(await CuNumere(ordonate.Pagina(lista, pagina, marime), acces, a).ToListAsync(ct), total);
        }).AllowAnonymous();

        g.MapGet("/materii/{id:int}", async (int id, HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) =>
            await CuNumere(db.Materii.AsNoTracking().Where(m => m.Id == id), acces, await acces.PentruAsync(h.User, ct)).SingleOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Materia nu există."));

        // The subject's Lecții the user may see, a page at a time (pagina from 1, marime: Pagini.LectiiMaterie* by role; Total = all).
        // clasa: one or more of IX, X, XI, XII, Bac (comma-separated); filtru: toate | organizatia-mea | public | local (see
        // FiltreLectii); under "local", the user's own locality comes first; ascundeTerminate: without the ones the user passed.
        g.MapGet("/materii/{id:int}/parcursuri", async (int id, string? clasa, string? filtru, bool? ascundeTerminate, int? pagina, int? marime,
            HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) =>
        {
            var lista = h.User.EsteAdmin() ? Pagini.LectiiMaterieAdmin : Pagini.LectiiMaterieElev;
            Pagini.Fereastra(lista, pagina, marime);
            var a = await acces.PentruAsync(h.User, ct);
            var loc = await LocUtilizator.PentruAsync(db, a.UtilizatorId, ct);
            var q = FiltreLectii.Aplica(acces.Parcursuri(a).AsNoTracking().Where(p => p.MaterieId == id), a, loc, filtru, clasa);
            var uid = a.UtilizatorId;
            if (ascundeTerminate == true) q = q.Where(p => !p.Progrese.Any(x => x.UtilizatorId == uid && x.PromovatLa != null));
            var total = await q.CountAsync(ct);
            return new Lista<ParcursRezumat>(await Rezumate(Ordonate(q, filtru == "local" ? loc.LocalitateId : null).Pagina(lista, pagina, marime), uid, loc, ct), total);
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
            var gasite = await Rezumate((filtru == "local" ? Ordonate(lectii, loc.LocalitateId) : lectii.OrderBy(p => p.Titlu).ThenBy(p => p.Id)).Take(30),
                a.UtilizatorId, loc, ct);
            var cap = await capitole.OrderBy(c => c.Titlu).Take(30)
                .Select(c => new CapitolGasit(c.Id, c.Titlu, c.Capitol, c.ParcursId, c.Parcurs.Titlu, c.Parcurs.Clasa, c.MaterieId, c.Materie.Denumire))
                .ToListAsync(ct);
            return new RezultatCautare(gasite, cap);
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
                .Select(c => new CursRezumat(c.Id, c.MaterieId, c.ParcursId, c.Titlu, c.Descriere, c.Capitol, c.Ordine, c.VideoUrl != null, c.Publicat))
                .ToListAsync(ct);
            var progres = await db.Set<ProgresLectie>().AsNoTracking().Where(x => x.ParcursId == id && x.UtilizatorId == uid)
                .Select(x => new ProgresLectieDto(x.PromovatLa, x.UltimulCursId)).SingleOrDefaultAsync(ct);
            return new ParcursDetaliu(p.Id, p.MaterieId, p.Materie, p.Titlu, p.Descriere, p.Clasa, p.Autor, p.Organizatie, p.Vizibilitate, p.Publicat,
                poateEdita, await QuizEndpoints.IntrebariValide(db, id).CountAsync(ct), progres, capitole);
        });

        g.MapGet("/cursuri/{id:int}", async (int id, HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) =>
        {
            await acces.AsiguraCapitolVizibilAsync(h.User, id, ct);
            return await Detaliu(db, id, ct);
        });

        // The Lecție quiz lives in QuizEndpoints: answers are checked there and never sent ahead.

        // The student read this chapter (the client calls it once the notes, and the video if any, were opened). Completes nothing.
        g.MapPost("/cursuri/{id:int}/acces", async (int id, HttpContext h, AccesContinut acces, ProgresService progres, CancellationToken ct) =>
        {
            await acces.AsiguraCapitolVizibilAsync(h.User, id, ct);
            await progres.AcceseazaAsync(h.User.Id(), id, ct);
            return Results.NoContent();
        }).RequireAuthorization(Politici.Elev);

        MapeazaMaterii(app);
        MapeazaParcursuri(app);
        MapeazaCapitole(app);
        MapeazaIntrebari(app);
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
        // The Lecții the user may edit, a page at a time (pagina from 1, marime: Pagini.LectiiEditor* by role; Total = all that match),
        // built with a fixed number of queries per page. Filters, all in SQL: materieId, clasa, vizibilitate (public | organizatie),
        // publicat (false = drafts), q — words in the title or description, without diacritics.
        e.MapGet("/continut/parcursuri", async (int? materieId, string? clasa, string? vizibilitate, bool? publicat, string? q, int? pagina, int? marime,
            HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) =>
        {
            var lista = h.User.EsteAdmin() ? Pagini.LectiiEditorAdmin : Pagini.LectiiEditorProfesor;
            Pagini.Fereastra(lista, pagina, marime);
            if (!string.IsNullOrEmpty(clasa) && !Parcurs.Clase.Contains(clasa)) throw new CerereInvalida("Clasa trebuie să fie IX, X, XI, XII sau Bac.");
            if (vizibilitate is not (null or "" or Parcurs.Public or Parcurs.DoarOrganizatie))
                throw new CerereInvalida("Vizibilitatea trebuie să fie „public” sau „organizatie”.");
            var a = await acces.PentruAsync(h.User, ct);
            var lectii = acces.Editabile(a).AsNoTracking();
            if (materieId is not null) lectii = lectii.Where(p => p.MaterieId == materieId);
            if (!string.IsNullOrEmpty(clasa)) lectii = lectii.Where(p => p.Clasa == clasa);
            if (!string.IsNullOrEmpty(vizibilitate)) lectii = lectii.Where(p => p.Vizibilitate == vizibilitate);
            if (publicat is not null) lectii = lectii.Where(p => p.Publicat == publicat);
            foreach (var t in TermeniCautare.Optionali(q)) lectii = lectii.Where(p => EF.Functions.ILike(p.Cautare, t, "\\"));
            var total = await lectii.CountAsync(ct);
            var pag = lectii.OrderBy(p => p.MaterieId).ThenBy(p => p.Ordine).ThenBy(p => p.Titlu).ThenBy(p => p.Id).Pagina(lista, pagina, marime);
            return new Lista<ParcursEditabil>(await Editabile(db, h.User, pag, ct), total);
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

        // Hard delete, with every chapter, question and file and the students' progress (XP already earned stays).
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
                .Select(c => new { c.Id, c.ParcursId, c.MaterieId, c.Titlu, c.Capitol, c.Ordine, c.Publicat })
                .ToListAsync(ct);
            return new { elemente = cursuri, total = cursuri.Count };
        });

        e.MapPost("/cursuri", async (CursEditare c, HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) =>
        {
            var p = await acces.PentruEditareAsync(h.User, c.ParcursId, ct);
            var curs = new Curs { CreatDeId = h.User.Id(), MaterieId = p.MaterieId, ParcursId = p.Id };
            Aplica(curs, c);
            curs.Ordine = c.Ordine ?? (await db.Curs.Where(x => x.ParcursId == p.Id).MaxAsync(x => (int?)x.Ordine, ct) ?? 0) + 1;
            db.Curs.Add(curs);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/cursuri/{curs.Id}", await Detaliu(db, curs.Id, ct));
        });

        e.MapPut("/cursuri/{id:int}", async (int id, CursEditare c, HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) =>
        {
            var curs = await acces.CapitolPentruEditareAsync(h.User, id, ct);
            // A chapter may move to another Lecție of the same subject that the user may also edit.
            if (c.ParcursId != curs.ParcursId)
            {
                var alt = await acces.PentruEditareAsync(h.User, c.ParcursId, ct);
                if (alt.MaterieId != curs.MaterieId) throw new CerereInvalida("Un capitol poate fi mutat doar într-o lecție de la aceeași materie.");
                curs.ParcursId = alt.Id;
            }
            Aplica(curs, c);
            if (c.Ordine is not null) curs.Ordine = c.Ordine.Value;
            await db.SaveChangesAsync(ct);
            return await Detaliu(db, curs.Id, ct);
        });

        e.MapDelete("/cursuri/{id:int}", async (int id, HttpContext h, AccesContinut acces, StergereContinut stergere, CancellationToken ct) =>
        {
            await acces.CapitolPentruEditareAsync(h.User, id, ct);
            await stergere.StergeCapitoleAsync([id], ct);
            return Results.NoContent();
        });
    }

    /// <summary>
    /// A Lecție's quiz questions: generated by AI only when the professor asks (N at a time, added to the set), or written by
    /// hand; any of them can be edited or deleted. Questions of a whole subject (no Lecție, Triviador only) are the platform's.
    /// </summary>
    private static void MapeazaIntrebari(WebApplication app)
    {
        var e = app.MapGroup("").WithTags("Editare conținut").RequireAuthorization(Politici.Continut);

        e.MapGet("/continut/parcursuri/{id:int}/intrebari", async (int id, HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) =>
        {
            await acces.PentruEditareAsync(h.User, id, ct);
            var intrebari = await db.Set<Intrebare>().AsNoTracking().Include(q => q.Variante).Where(q => q.ParcursId == id)
                .OrderByDescending(q => q.Activa).ThenBy(q => q.Id).ToListAsync(ct);
            return Lista<IntrebareCompleta>.Din(intrebari.Select(Completa).ToList());
        });

        e.MapPost("/parcursuri/{id:int}/genereaza-intrebari", async (int id, CerereGenerare c, HttpContext h, MarkAiDbContext db, AccesContinut acces,
            CoadaIntrebari coada, IConfiguration cfg, TimeProvider ceas, CancellationToken ct) =>
        {
            await acces.PentruEditareAsync(h.User, id, ct);
            await LimitaGenerariAi.PuneAsync(db, coada, cfg, ceas, h.User.Id(), id, c.Numar, ct);
            return Results.Accepted($"/parcursuri/{id}/generare", coada.Stare(id));
        }).RequireRateLimiting(LimiteCereri.Ai);

        e.MapGet("/continut/buget-ai", (HttpContext h, MarkAiDbContext db, IConfiguration cfg, TimeProvider ceas, CancellationToken ct) =>
            LimitaGenerariAi.BugetAsync(db, cfg, ceas, h.User.Id(), ct));

        e.MapGet("/parcursuri/{id:int}/generare", async (int id, HttpContext h, AccesContinut acces, CoadaIntrebari coada, CancellationToken ct) =>
        {
            await acces.PentruEditareAsync(h.User, id, ct);
            return coada.Stare(id) ?? new StareGenerare("necunoscuta", null, 0, DateTime.UtcNow);
        });

        e.MapPost("/parcursuri/{id:int}/intrebari", async (int id, IntrebareEditare c, HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) =>
        {
            var p = await acces.PentruEditareAsync(h.User, id, ct);
            var tip = c.Tip ?? Intrebare.Grila;
            if (tip is not (Intrebare.Grila or Intrebare.Estimare)) throw new CerereInvalida("Tipul trebuie să fie „grila” sau „estimare”.");
            var q = new Intrebare { MaterieId = p.MaterieId, ParcursId = p.Id, Tip = tip, CreataLa = DateTime.UtcNow };
            Aplica(q, c, nou: true);
            db.Add(q);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/intrebari/{q.Id}", Completa(q));
        });

        e.MapPut("/intrebari/{id:int}", async (int id, IntrebareEditare c, HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) =>
        {
            var q = await IntrebarePentruEditareAsync(id, h, db, acces, ct);
            Aplica(q, c, nou: false);
            await db.SaveChangesAsync(ct);
            return Completa(q);
        });

        e.MapDelete("/intrebari/{id:int}", async (int id, HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) =>
        {
            var q = await IntrebarePentruEditareAsync(id, h, db, acces, ct);
            // Its options cascade. A quiz attempt in progress that drew it skips it (attempts keep only ids).
            db.Remove(q);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    private static async Task<Intrebare> IntrebarePentruEditareAsync(int id, HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct)
    {
        var q = await db.Set<Intrebare>().Include(x => x.Variante).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Întrebarea nu există.");
        if (q.ParcursId is { } parcursId) await acces.PentruEditareAsync(h.User, parcursId, ct);
        else if (!h.User.EsteAdmin()) throw new UnauthorizedAccessException();
        return q;
    }

    private static void Aplica(Intrebare q, IntrebareEditare c, bool nou)
    {
        q.Text = Validare.Obligatoriu(c.Text, "Întrebare", 1000);
        q.Explicatie = Validare.Optional(c.Explicatie, 2000);
        q.Activa = c.Activa;
        // An edited AI question is now owned by a person.
        q.Sursa = Intrebare.SursaManual;
        if (q.Tip == Intrebare.Estimare)
        {
            q.RaspunsNumeric = c.RaspunsNumeric ?? throw new CerereInvalida("Răspunsul numeric este obligatoriu.");
            return;
        }
        if (c.Variante is null)
        {
            if (nou) throw new CerereInvalida("O întrebare grilă are 2–6 variante și exact una corectă.");
            return;
        }
        if (c.Variante.Count is < 2 or > 6 || c.Variante.Count(v => v.Corecta) != 1)
            throw new CerereInvalida("O întrebare grilă are 2–6 variante și exact una corectă.");
        // Options are replaced as a whole; quiz attempts answered with the old ones are already scored.
        q.Variante.Clear();
        foreach (var v in c.Variante)
            q.Variante.Add(new VariantaIntrebare { Text = Validare.Obligatoriu(v.Text, "Variantă", 500), Corecta = v.Corecta });
    }

    // Published Lecții a student would see, per subject: (total, completed by the user).
    /// <summary>Subjects with the user's counts (published Lecții they may see, and those they passed), counted in SQL.</summary>
    private static IQueryable<MaterieDto> CuNumere(IQueryable<Materie> materii, AccesContinut acces, ContextAcces a)
    {
        var uid = a.UtilizatorId;
        var vizibile = acces.Parcursuri(a).Where(p => p.Publicat);
        return materii.Select(m => new MaterieDto(m.Id, m.Cod, m.Denumire, m.Descriere, m.Ordine,
            vizibile.Count(p => p.MaterieId == m.Id),
            vizibile.Count(p => p.MaterieId == m.Id && p.Progrese.Any(x => x.UtilizatorId == uid && x.PromovatLa != null))));
    }

    /// <summary>The Lecții list's order, in SQL: those in <paramref name="primaLocalitate"/> first (for "near me"), then the recommended order.</summary>
    private static IOrderedQueryable<Parcurs> Ordonate(IQueryable<Parcurs> q, int? primaLocalitate) =>
        (primaLocalitate is null ? q.OrderBy(p => 0) : q.OrderByDescending(p => p.Organizatie != null && p.Organizatie.LocalitateId == primaLocalitate))
            .ThenBy(p => p.Ordine).ThenBy(p => p.Titlu).ThenBy(p => p.Id);

    /// <param name="q">Already ordered (and paged); the order is kept.</param>
    private static async Task<List<ParcursRezumat>> Rezumate(IQueryable<Parcurs> q, int uid, LocUtilizator loc, CancellationToken ct)
    {
        var localitate = loc.LocalitateId;
        return await q
            .Select(p => new ParcursRezumat(p.Id, p.MaterieId, p.Titlu, p.Descriere, p.Clasa, p.Autor == null ? null : p.Autor.Nume,
                p.Organizatie == null ? null : p.Organizatie.Nume, p.Vizibilitate, p.Publicat,
                p.Ordine, p.Capitole.Count(c => c.Publicat), p.Progrese.Any(x => x.UtilizatorId == uid && x.PromovatLa != null),
                p.Organizatie == null ? null : (p.Organizatie.Localitate == null ? "" : p.Organizatie.Localitate.Nume + ", ") + p.Organizatie.Judet.Nume,
                localitate != null && p.Organizatie != null && p.Organizatie.LocalitateId == localitate))
            .ToListAsync(ct);
    }

    private static async Task<List<OrganizatieAutor>> OrganizatiiPosibile(MarkAiDbContext db, AccesContinut acces, ClaimsPrincipal u, int? autorId, CancellationToken ct) =>
        (await OrganizatiiPosibile(db, u, autorId is null ? [] : [autorId.Value], ct)).GetValueOrDefault(autorId ?? 0) ?? [];

    /// <summary>
    /// For each author, the organisations a Lecție of theirs may have as home: theirs (approved member as a professor, or admin),
    /// with whether that organisation lets them publish publicly; for a platform admin every approved organisation. Two or three
    /// queries whatever the number of authors. Key 0 = no author (platform Lecții).
    /// </summary>
    private static async Task<Dictionary<int, List<OrganizatieAutor>>> OrganizatiiPosibile(MarkAiDbContext db, ClaimsPrincipal u, IReadOnlyCollection<int> autori,
        CancellationToken ct)
    {
        var ca = await db.Set<MembruOrganizatie>().AsNoTracking()
            .Where(m => autori.Contains(m.UtilizatorId) && m.Rol == MembruOrganizatie.RolProfesor && m.Stare == MembruOrganizatie.Aprobat
                && m.Organizatie.Stare == Organizatie.Aprobata)
            .Select(m => new { m.UtilizatorId, m.OrganizatieId, m.Organizatie.Nume, m.PoatePublicaPublic }).ToListAsync(ct);
        var conduse = await db.Set<Organizatie>().AsNoTracking().Where(o => autori.Contains(o.AdminId) && o.Stare == Organizatie.Aprobata)
            .Select(o => new { o.AdminId, o.Id, o.Nume }).ToListAsync(ct);
        var toate = u.EsteAdmin()
            ? await db.Set<Organizatie>().AsNoTracking().Where(o => o.Stare == Organizatie.Aprobata).OrderBy(o => o.Nume).Select(o => new { o.Id, o.Nume }).ToListAsync(ct)
            : null;
        var rezultat = new Dictionary<int, List<OrganizatieAutor>>();
        foreach (var autor in autori.Append(0).Distinct())
        {
            var ale = ca.Where(m => m.UtilizatorId == autor).Select(m => new OrganizatieAutor(m.OrganizatieId, m.Nume, m.PoatePublicaPublic))
                .Concat(conduse.Where(o => o.AdminId == autor && ca.All(m => m.UtilizatorId != autor || m.OrganizatieId != o.Id)).Select(o => new OrganizatieAutor(o.Id, o.Nume, true)))
                .OrderBy(o => o.Nume).ToList();
            // Platform admins may place a Lecție in any approved organisation.
            rezultat[autor] = toate is null ? ale : toate.Select(o => ale.FirstOrDefault(x => x.Id == o.Id) ?? new OrganizatieAutor(o.Id, o.Nume, true)).ToList();
        }
        return rezultat;
    }

    private static async Task<ParcursEditabil> Editabil(MarkAiDbContext db, AccesContinut acces, ClaimsPrincipal u, int id, CancellationToken ct) =>
        (await Editabile(db, u, db.Set<Parcurs>().AsNoTracking().Where(x => x.Id == id), ct)).Single();

    /// <summary>
    /// The editor's view of these Lecții (a page of the list, or one), with a fixed number of queries: the Lecții, the authors'
    /// organisations, and the publishing switches turned off for them.
    /// </summary>
    private static async Task<List<ParcursEditabil>> Editabile(MarkAiDbContext db, ClaimsPrincipal u, IQueryable<Parcurs> q, CancellationToken ct)
    {
        var lectii = await q.Select(x => new { x.Id, x.MaterieId, Materie = x.Materie.Denumire, x.Titlu, x.Descriere, x.Clasa, x.Vizibilitate, x.Publicat, x.Ordine,
                x.AutorId, Autor = x.Autor == null ? null : x.Autor.Nume, x.OrganizatieId, Organizatie = x.Organizatie == null ? null : x.Organizatie.Nume,
                Partajari = x.Partajari.Select(s => s.OrganizatieId).ToList(), Capitole = x.Capitole.Count, Intrebari = x.Intrebari.Count(i => i.Activa) })
            .ToListAsync(ct);
        var autori = lectii.Where(p => p.AutorId != null).Select(p => p.AutorId!.Value).Distinct().ToList();
        var posibile = await OrganizatiiPosibile(db, u, autori, ct);
        // The home organisation switched off public publishing for the author (AccesContinut.PoatePublicaPublicAsync, for many at once).
        var oprite = (await db.Set<MembruOrganizatie>().AsNoTracking()
                .Where(m => autori.Contains(m.UtilizatorId) && m.Rol == MembruOrganizatie.RolProfesor && !m.PoatePublicaPublic)
                .Select(m => new { m.UtilizatorId, m.OrganizatieId }).ToListAsync(ct))
            .Select(m => (m.UtilizatorId, m.OrganizatieId)).ToHashSet();
        return lectii.Select(p => new ParcursEditabil(p.Id, p.MaterieId, p.Materie, p.Titlu, p.Descriere, p.Clasa, p.Vizibilitate, p.Publicat, p.Ordine,
            p.AutorId, p.Autor, p.OrganizatieId, p.Organizatie, p.Partajari, p.Capitole, p.Intrebari,
            p.OrganizatieId is null || p.AutorId is null || !oprite.Contains((p.AutorId.Value, p.OrganizatieId.Value)),
            // Only the author (or a platform admin) moves a Lecție to another home organisation, and only among the author's.
            u.EsteAdmin() || (p.AutorId is not null && p.AutorId == u.Id()),
            posibile.GetValueOrDefault(p.AutorId ?? 0) ?? [])).ToList();
    }

    private static async Task Aplica(Parcurs p, ParcursEditare c, ClaimsPrincipal u, MarkAiDbContext db, AccesContinut acces, bool nou, CancellationToken ct)
    {
        p.Titlu = Validare.Obligatoriu(c.Titlu, "Titlu", 200);
        p.Descriere = Validare.Optional(c.Descriere, 1000);
        p.Clasa = Parcurs.Clase.Contains(c.Clasa) ? c.Clasa : throw new CerereInvalida("Clasa trebuie să fie IX, X, XI, XII sau Bac.");
        var vecheVizibilitate = nou ? null : p.Vizibilitate;
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
        // An author who left (or was removed from) the home organisation stays the author, but the organisation's Lecție stays
        // the organisation's: only its admin (or the platform admin) may make an organisation-only one public.
        if (p.Vizibilitate == Parcurs.Public && vecheVizibilitate == Parcurs.DoarOrganizatie && p.OrganizatieId is { } casa
            && p.AutorId == u.Id() && !u.EsteAdmin() && aleAutorului.All(o => o.Id != casa))
            throw new Conflict("Lecția e doar a organizației și rămâne așa: nu mai faci parte din ea. Administratorul organizației o poate face publică.");
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

    private static IntrebareCompleta Completa(Intrebare q) =>
        new(q.Id, q.Tip, q.Text, q.Explicatie, q.Sursa, q.Activa, q.RaspunsNumeric,
            q.Variante.OrderBy(o => o.Id).Select(o => new VariantaCompleta(o.Id, o.Text, o.Corecta)).ToList());

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

    private static async Task<CursDetaliu> Detaliu(MarkAiDbContext db, int id, CancellationToken ct)
    {
        var c = await db.Curs.AsNoTracking().Include(x => x.Materie).Include(x => x.Parcurs).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Capitolul nu există.");
        return new CursDetaliu(c.Id, c.MaterieId, c.Materie.Denumire, c.ParcursId, c.Parcurs.Titlu, c.Parcurs.Clasa, c.Titlu, c.Descriere, c.Capitol, c.Ordine,
            c.VideoUrl, c.Materiale, c.Continut, c.Publicat);
    }
}

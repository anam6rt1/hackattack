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
            CoadaIntrebari coada, IConfiguration cfg, CancellationToken ct) =>
        {
            await acces.PentruEditareAsync(h.User, id, ct);
            await LimitaGenerariAi.PuneAsync(db, coada, cfg, h.User.Id(), id, c.Numar, ct);
            return Results.Accepted($"/parcursuri/{id}/generare", coada.Stare(id));
        }).RequireRateLimiting(LimiteCereri.Ai);

        e.MapGet("/continut/buget-ai", (HttpContext h, MarkAiDbContext db, IConfiguration cfg, CancellationToken ct) =>
            LimitaGenerariAi.BugetAsync(db, cfg, h.User.Id(), ct));

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
    private static async Task<Dictionary<int, (int Total, int Completate)>> Numere(AccesContinut acces, ClaimsPrincipal u, int? materieId, CancellationToken ct)
    {
        var a = await acces.PentruAsync(u, ct);
        var uid = a.UtilizatorId;
        var q = acces.Parcursuri(a).AsNoTracking().Where(p => p.Publicat);
        if (materieId is not null) q = q.Where(p => p.MaterieId == materieId);
        var randuri = await q.Select(p => new { p.MaterieId, Gata = p.Progrese.Any(x => x.UtilizatorId == uid && x.PromovatLa != null) }).ToListAsync(ct);
        return randuri.GroupBy(r => r.MaterieId).ToDictionary(g => g.Key, g => (g.Count(), g.Count(r => r.Gata)));
    }

    private static async Task<List<ParcursRezumat>> Rezumate(IQueryable<Parcurs> q, int uid, LocUtilizator loc, CancellationToken ct)
    {
        var localitate = loc.LocalitateId;
        return await q.OrderBy(p => p.Ordine).ThenBy(p => p.Titlu)
            .Select(p => new ParcursRezumat(p.Id, p.MaterieId, p.Titlu, p.Descriere, p.Clasa, p.Autor == null ? null : p.Autor.Nume,
                p.Organizatie == null ? null : p.Organizatie.Nume, p.Vizibilitate, p.Publicat,
                p.Ordine, p.Capitole.Count(c => c.Publicat), p.Progrese.Any(x => x.UtilizatorId == uid && x.PromovatLa != null),
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
                Partajari = x.Partajari.Select(s => s.OrganizatieId).ToList(), Capitole = x.Capitole.Count, Intrebari = x.Intrebari.Count(q => q.Activa) })
            .SingleAsync(ct);
        // Only the author (or a platform admin) moves a Lecție to another home organisation, and only among the author's.
        var poateMuta = u.EsteAdmin() || (p.AutorId is not null && p.AutorId == u.Id());
        var posibile = await OrganizatiiPosibile(db, acces, u, p.AutorId, ct);
        return new ParcursEditabil(p.Id, p.MaterieId, p.Materie, p.Titlu, p.Descriere, p.Clasa, p.Vizibilitate, p.Publicat, p.Ordine, p.AutorId, p.Autor,
            p.OrganizatieId, p.Organizatie, p.Partajari, p.Capitole, p.Intrebari, await acces.PoatePublicaPublicAsync(p.AutorId, p.OrganizatieId, ct), poateMuta, posibile);
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

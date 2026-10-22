using MarkAi.Api.Infrastructura;
using MarkAi.Api.Servicii;
using MarkAi.Api.Servicii.Mesagerie;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Endpoints;

public record JudetDto(int Id, string Nume);
public record LocalitateDto(int Id, string Nume, string UatNume, int JudetId);
public record OrganizatiePublica(int Id, string Nume, string Judet, string? Localitate);
public record CerereOrganizatie(int OrganizatieId);
public record DecizieMembru(string? Motiv);
public record SetarePublicare(bool PoatePublicaPublic);
public record DetaliiOrganizatie(string Nume, string Adresa, int JudetId, int? LocalitateId);
public record ApartenentaMea(int OrganizatieId, string Organizatie, string Rol, string Stare, DateTime CerutLa, DateTime? ExpiraLa, string? MotivRespingere);
/// <param name="ProfesorRetras">A professor whose approval the platform admin withdrew: the organisation can approve them again.</param>
public record MembruDto(int UtilizatorId, string Nume, string Username, string Email, string Rol, string Stare, DateTime CerutLa, DateTime? ExpiraLa,
    bool PoatePublicaPublic, string? Clasa, string? Descriere, bool AreDovada, IReadOnlyList<string> Materii, bool ProfesorRetras = false);
public record OrganizatieAdminDto(int Id, string Nume, string Adresa, int JudetId, string Judet, int? LocalitateId, string? Localitate, string Stare,
    string? MotivRespingere, DateTime CreatLa, int AdminId, string AdminNume, string AdminEmail, string? AdminTelefon, bool AdminEmailConfirmat,
    int Profesori, int Elevi, int Cereri);

public static class OrganizatiiEndpoints
{
    public static void Mapeaza(WebApplication app)
    {
        // Lists for the sign-up forms (anonymous).
        var publice = app.MapGroup("").WithTags("Organizații");
        publice.MapGet("/judete", async (MarkAiDbContext db, CancellationToken ct) =>
            Lista<JudetDto>.Din(await db.Set<Judet>().AsNoTracking().OrderBy(j => j.Nume).Select(j => new JudetDto(j.Id, j.Nume)).ToListAsync(ct)));

        // Search as you type: names starting with the text first, then towns before villages. At most 30 results.
        publice.MapGet("/judete/{id:int}/localitati", async (int id, string? q, MarkAiDbContext db, CancellationToken ct) =>
        {
            var text = ImportSiruta.PentruCautare((q ?? "").Trim());
            var lista = db.Set<Localitate>().AsNoTracking().Where(l => l.JudetId == id);
            if (text.Length > 0) lista = lista.Where(l => l.Cautare.Contains(text));
            return Lista<LocalitateDto>.Din(await lista.OrderBy(l => text.Length > 0 && l.Cautare.StartsWith(text) ? 0 : 1).ThenByDescending(l => l.Urban).ThenBy(l => l.Nume)
                .Take(30).Select(l => new LocalitateDto(l.Id, l.Nume, l.UatNume, l.JudetId)).ToListAsync(ct));
        });

        publice.MapGet("/localitati/{id:int}", async (int id, MarkAiDbContext db, CancellationToken ct) =>
            await db.Set<Localitate>().AsNoTracking().Where(l => l.Id == id).Select(l => new LocalitateDto(l.Id, l.Nume, l.UatNume, l.JudetId)).SingleOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Localitatea nu există."));

        publice.MapGet("/organizatii", async (int? judetId, string? q, MarkAiDbContext db, CancellationToken ct) =>
        {
            var lista = db.Set<Organizatie>().AsNoTracking().Where(o => o.Stare == Organizatie.Aprobata);
            if (judetId is not null) lista = lista.Where(o => o.JudetId == judetId);
            if (!string.IsNullOrWhiteSpace(q)) lista = lista.Where(o => EF.Functions.ILike(o.Nume, "%" + q.Trim() + "%"));
            return Lista<OrganizatiePublica>.Din(await lista.OrderBy(o => o.Nume).Take(100)
                .Select(o => new OrganizatiePublica(o.Id, o.Nume, o.Judet.Nume, o.Localitate == null ? null : o.Localitate.Nume)).ToListAsync(ct));
        });

        // The signed-in person's own memberships and requests.
        var eu = app.MapGroup("/utilizatori/me/organizatii").WithTags("Organizații").RequireAuthorization();
        eu.MapGet("", async (HttpContext h, MarkAiDbContext db, CancellationToken ct) => Lista<ApartenentaMea>.Din(await Apartenente(db, h.User.Id(), ct)));
        eu.MapPost("", async (CerereOrganizatie c, HttpContext h, MarkAiDbContext db, ServiciuOrganizatii org, CancellationToken ct) =>
        {
            var u = await db.Utilizatori.SingleAsync(x => x.Id == h.User.Id(), ct);
            var rol = h.User.EsteProfesor() ? MembruOrganizatie.RolProfesor : h.User.IsInRole(Roluri.Elev) ? MembruOrganizatie.RolElev
                : throw new CerereInvalida("Acest cont nu poate face parte dintr-o organizație.");
            await org.CereAsync(u, c.OrganizatieId, rol, ct);
            return Lista<ApartenentaMea>.Din(await Apartenente(db, u.Id, ct));
        });
        // Withdraws a request or leaves an organisation (an organisation's admin cannot leave their own).
        eu.MapDelete("/{organizatieId:int}", async (int organizatieId, HttpContext h, MarkAiDbContext db, ServiciuOrganizatii org, CancellationToken ct) =>
        {
            var uid = h.User.Id();
            if (await db.Set<Organizatie>().AnyAsync(o => o.Id == organizatieId && o.AdminId == uid, ct))
                throw new CerereInvalida("Administratorul nu poate ieși din propria organizație.");
            var m = await db.Set<MembruOrganizatie>().SingleOrDefaultAsync(x => x.OrganizatieId == organizatieId && x.UtilizatorId == uid, ct)
                ?? throw new KeyNotFoundException("Nu faci parte din această organizație.");
            db.Remove(m);
            await db.SaveChangesAsync(ct);
            if (m.Rol == MembruOrganizatie.RolProfesor) await org.SincronizeazaProfesorAsync(uid, ct);
            return Lista<ApartenentaMea>.Din(await Apartenente(db, uid, ct));
        });

        // The organisation's admin: details, members, decisions.
        var mea = app.MapGroup("/organizatia-mea").WithTags("Organizații").RequireAuthorization();
        mea.MapGet("", async (HttpContext h, MarkAiDbContext db, CancellationToken ct) => await DetaliiAdmin(db, (await OrganizatiaMea(db, h, ct)).Id, ct));
        mea.MapPut("", async (DetaliiOrganizatie c, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var o = await OrganizatiaMea(db, h, ct);
            (o.Nume, o.Adresa) = (Validare.Obligatoriu(c.Nume, "Numele organizației", 200), Validare.Obligatoriu(c.Adresa, "Adresa", 300));
            (o.JudetId, o.LocalitateId) = await ValideazaLocul(db, c.JudetId, c.LocalitateId, ct);
            await db.SaveChangesAsync(ct);
            return await DetaliiAdmin(db, o.Id, ct);
        });
        // q: words in the name, email or username, without diacritics; clasa (students) and materieId (professors) narrow it.
        mea.MapGet("/membri", async (string? rol, string? stare, string? q, string? clasa, int? materieId, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
            Lista<MembruDto>.Din(await Membri(db, (await OrganizatiaMea(db, h, ct)).Id, rol, stare, ct, q, clasa, materieId)));
        mea.MapPost("/membri/{uid:int}/aprobare", async (int uid, HttpContext h, MarkAiDbContext db, ServiciuOrganizatii org, CancellationToken ct) =>
        {
            var o = await OrganizatiaMea(db, h, ct);
            await org.DecideAsync(await Membru(db, o.Id, uid, ct), true, null, h.User.Id(), ct);
            return Results.NoContent();
        });
        mea.MapPost("/membri/{uid:int}/respingere", async (int uid, DecizieMembru c, HttpContext h, MarkAiDbContext db, ServiciuOrganizatii org, CancellationToken ct) =>
        {
            var o = await OrganizatiaMea(db, h, ct);
            await org.DecideAsync(await Membru(db, o.Id, uid, ct), false, c.Motiv, h.User.Id(), ct);
            return Results.NoContent();
        });
        // A professor whose approval the platform admin withdrew is approved again by an organisation they teach in (not their own
        // organisation's admin approving themselves).
        mea.MapPost("/membri/{uid:int}/reaprobare", async (int uid, HttpContext h, MarkAiDbContext db, ServiciuOrganizatii org, CancellationToken ct) =>
        {
            var o = await OrganizatiaMea(db, h, ct);
            if (uid == o.AdminId) throw new CerereInvalida("Nu te poți aproba din nou singur; o altă organizație sau administratorul platformei o poate face.");
            await org.ReaprobaAsync(uid, await Membru(db, o.Id, uid, ct), h.User.Id(), ct);
            return Results.NoContent();
        });
        mea.MapDelete("/membri/{uid:int}", async (int uid, HttpContext h, MarkAiDbContext db, ServiciuOrganizatii org, CancellationToken ct) =>
        {
            var o = await OrganizatiaMea(db, h, ct);
            if (uid == o.AdminId) throw new CerereInvalida("Nu te poți scoate pe tine din organizație.");
            var m = await Membru(db, o.Id, uid, ct);
            db.Remove(m);
            await db.SaveChangesAsync(ct);
            if (m.Rol == MembruOrganizatie.RolProfesor) await org.SincronizeazaProfesorAsync(uid, ct);
            return Results.NoContent();
        });
        // Whether a professor may publish courses publicly (used by the course pages, Phase 3).
        mea.MapPut("/membri/{uid:int}/publicare", async (int uid, SetarePublicare c, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var o = await OrganizatiaMea(db, h, ct);
            var m = await Membru(db, o.Id, uid, ct);
            if (m.Rol != MembruOrganizatie.RolProfesor) throw new CerereInvalida("Setarea se aplică doar profesorilor.");
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            m.PoatePublicaPublic = c.PoatePublicaPublic;
            await db.SaveChangesAsync(ct);
            // Switched off: the professor's Lecții whose home is this organisation become organisation-only (switching it
            // back on does not make them public again; the professor decides that per Lecție).
            if (!c.PoatePublicaPublic)
                await db.Set<Parcurs>().Where(p => p.AutorId == uid && p.OrganizatieId == o.Id && p.Vizibilitate == Parcurs.Public)
                    .ExecuteUpdateAsync(x => x.SetProperty(p => p.Vizibilitate, Parcurs.DoarOrganizatie), ct);
            await tx.CommitAsync(ct);
            return Results.NoContent();
        });
        // A professor's qualification document, for the admin of an organisation they asked to join.
        mea.MapGet("/membri/{uid:int}/dovada", async (int uid, HttpContext h, MarkAiDbContext db, Stocare stocare, CancellationToken ct) =>
        {
            var o = await OrganizatiaMea(db, h, ct);
            if ((await Membru(db, o.Id, uid, ct)).Rol != MembruOrganizatie.RolProfesor) return Results.NotFound();
            return stocare.Serveste(await db.ProfiluriMentor.Where(p => p.UtilizatorId == uid).Select(p => p.DovadaCalificareUrl).SingleOrDefaultAsync(ct),
                $"dovada-calificare-{uid}");
        });

        // Students' requests can also be decided by the organisation's approved professors.
        var elevi = app.MapGroup("/organizatii/{id:int}/elevi").WithTags("Organizații").RequireAuthorization();
        elevi.MapGet("", async (int id, string? stare, HttpContext h, MarkAiDbContext db, ServiciuOrganizatii org, CancellationToken ct) =>
        {
            if (!await org.PoateDecideAsync(h.User.Id(), id, MembruOrganizatie.RolElev, ct)) return Results.Forbid();
            return Results.Ok(Lista<MembruDto>.Din(await Membri(db, id, MembruOrganizatie.RolElev, stare, ct)));
        });
        elevi.MapPost("/{uid:int}/aprobare", (int id, int uid, HttpContext h, MarkAiDbContext db, ServiciuOrganizatii org, CancellationToken ct) =>
            DecideElev(id, uid, true, null, h, db, org, ct));
        elevi.MapPost("/{uid:int}/respingere", (int id, int uid, DecizieMembru c, HttpContext h, MarkAiDbContext db, ServiciuOrganizatii org, CancellationToken ct) =>
            DecideElev(id, uid, false, c.Motiv, h, db, org, ct));

        // Organisations a professor helps run (to decide on students), for the professor's pages.
        app.MapGet("/utilizatori/me/organizatii-predate", async (HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var uid = h.User.Id();
            return Lista<OrganizatiePublica>.Din(await db.Set<Organizatie>().AsNoTracking().Where(o => o.Stare == Organizatie.Aprobata
                    && (o.AdminId == uid || o.Membri.Any(m => m.UtilizatorId == uid && m.Rol == MembruOrganizatie.RolProfesor && m.Stare == MembruOrganizatie.Aprobat)))
                .OrderBy(o => o.Nume).Select(o => new OrganizatiePublica(o.Id, o.Nume, o.Judet.Nume, o.Localitate == null ? null : o.Localitate.Nume)).ToListAsync(ct));
        }).WithTags("Organizații").RequireAuthorization();

        // The platform admin approves new organisations (and with them, their admins).
        var admin = app.MapGroup("/admin/organizatii").WithTags("Administrare").RequireAuthorization(Politici.Admin);
        admin.MapGet("", async (string? stare, MarkAiDbContext db, CancellationToken ct) =>
        {
            var ids = await db.Set<Organizatie>().Where(o => stare == null || o.Stare == stare).OrderByDescending(o => o.CreatLa).Select(o => o.Id).Take(200).ToListAsync(ct);
            var lista = new List<OrganizatieAdminDto>();
            foreach (var id in ids) lista.Add(await DetaliiAdmin(db, id, ct));
            return Lista<OrganizatieAdminDto>.Din(lista);
        });
        admin.MapPost("/{id:int}/aprobare", async (int id, HttpContext h, MarkAiDbContext db, ServiciuOrganizatii org, Posta posta, TimeProvider ceas, CancellationToken ct) =>
        {
            var o = await db.Set<Organizatie>().Include(x => x.Admin).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Organizația nu există.");
            if (o.Admin.EmailConfirmatLa is null) throw new Conflict("Solicitantul nu și-a confirmat încă emailul.");
            (o.Stare, o.DecisDeId, o.DecisLa, o.MotivRespingere) = (Organizatie.Aprobata, h.User.Id(), ceas.GetUtcNow().UtcDateTime, null);
            // The admin also teaches in their organisation.
            if (!await db.Set<MembruOrganizatie>().AnyAsync(m => m.OrganizatieId == id && m.UtilizatorId == o.AdminId, ct))
                db.Add(new MembruOrganizatie { OrganizatieId = id, UtilizatorId = o.AdminId, Rol = MembruOrganizatie.RolProfesor, Stare = MembruOrganizatie.Aprobat,
                    CerutLa = o.CreatLa, DecisDeId = h.User.Id(), DecisLa = ceas.GetUtcNow().UtcDateTime });
            posta.Pune(Sabloane.Notificare(o.Admin.Email, "Organizația ta a fost aprobată", $"„{o.Nume}” a fost aprobată!",
                "Acum poți primi profesori și elevi în organizație și poți publica lecții."), "organizatie_aprobata");
            await db.SaveChangesAsync(ct);
            await org.SincronizeazaProfesorAsync(o.AdminId, ct);
            return Results.NoContent();
        });
        admin.MapPost("/{id:int}/respingere", async (int id, DecizieMembru c, HttpContext h, MarkAiDbContext db, ServiciuOrganizatii org, Posta posta, TimeProvider ceas, CancellationToken ct) =>
        {
            var o = await db.Set<Organizatie>().Include(x => x.Admin).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Organizația nu există.");
            if (o.Stare == Organizatie.Aprobata) throw new Conflict("O organizație aprobată nu mai poate fi respinsă.");
            (o.Stare, o.DecisDeId, o.DecisLa, o.MotivRespingere) = (Organizatie.Respinsa, h.User.Id(), ceas.GetUtcNow().UtcDateTime,
                Validare.Optional(c.Motiv, 500) ?? "Cererea nu a fost aprobată.");
            posta.Pune(Sabloane.Notificare(o.Admin.Email, "Cererea pentru organizație nu a fost aprobată", $"„{o.Nume}” nu a fost aprobată",
                $"Motiv: {o.MotivRespingere}", "Contul se șterge automat după 7 zile de la înregistrare."), "organizatie_respinsa");
            await db.SaveChangesAsync(ct);
            await org.SincronizeazaProfesorAsync(o.AdminId, ct);
            return Results.NoContent();
        });
    }

    private static async Task<IResult> DecideElev(int organizatieId, int uid, bool aprobat, string? motiv, HttpContext h, MarkAiDbContext db,
        ServiciuOrganizatii org, CancellationToken ct)
    {
        if (!await org.PoateDecideAsync(h.User.Id(), organizatieId, MembruOrganizatie.RolElev, ct)) return Results.Forbid();
        var m = await Membru(db, organizatieId, uid, ct);
        if (m.Rol != MembruOrganizatie.RolElev) return Results.Forbid();
        await org.DecideAsync(m, aprobat, motiv, h.User.Id(), ct);
        return Results.NoContent();
    }

    /// <summary>The approved organisation the signed-in person administers, or 403.</summary>
    private static async Task<Organizatie> OrganizatiaMea(MarkAiDbContext db, HttpContext h, CancellationToken ct) =>
        await db.Set<Organizatie>().SingleOrDefaultAsync(o => o.AdminId == h.User.Id() && o.Stare == Organizatie.Aprobata, ct)
        ?? throw new UnauthorizedAccessException();

    private static async Task<MembruOrganizatie> Membru(MarkAiDbContext db, int organizatieId, int uid, CancellationToken ct) =>
        await db.Set<MembruOrganizatie>().SingleOrDefaultAsync(m => m.OrganizatieId == organizatieId && m.UtilizatorId == uid, ct)
        ?? throw new KeyNotFoundException("Persoana nu face parte din organizație și nu are o cerere aici.");

    public static async Task<(int JudetId, int? LocalitateId)> ValideazaLocul(MarkAiDbContext db, int judetId, int? localitateId, CancellationToken ct)
    {
        if (!await db.Set<Judet>().AnyAsync(j => j.Id == judetId, ct)) throw new CerereInvalida("Alege județul.");
        if (localitateId is not null && !await db.Set<Localitate>().AnyAsync(l => l.Id == localitateId && l.JudetId == judetId, ct))
            throw new CerereInvalida("Localitatea nu este în județul ales.");
        return (judetId, localitateId);
    }

    private static Task<List<ApartenentaMea>> Apartenente(MarkAiDbContext db, int uid, CancellationToken ct) =>
        db.Set<MembruOrganizatie>().AsNoTracking().Where(m => m.UtilizatorId == uid).OrderByDescending(m => m.CerutLa)
            .Select(m => new ApartenentaMea(m.OrganizatieId, m.Organizatie.Nume, m.Rol, m.Stare, m.CerutLa, m.ExpiraLa, m.MotivRespingere)).ToListAsync(ct);

    private static async Task<List<MembruDto>> Membri(MarkAiDbContext db, int organizatieId, string? rol, string? stare, CancellationToken ct,
        string? text = null, string? clasa = null, int? materieId = null)
    {
        if (!string.IsNullOrEmpty(clasa) && !Parcurs.Clase.Contains(clasa)) throw new CerereInvalida("Clasa trebuie să fie IX, X, XI, XII sau Bac.");
        var cuvinte = CautareText.Normalizeaza(Validare.Optional(text, 100) ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var q = db.Set<MembruOrganizatie>().AsNoTracking().Where(m => m.OrganizatieId == organizatieId);
        if (!string.IsNullOrEmpty(rol)) q = q.Where(m => m.Rol == rol);
        if (!string.IsNullOrEmpty(stare)) q = q.Where(m => m.Stare == stare);
        if (!string.IsNullOrEmpty(clasa)) q = q.Where(m => m.Utilizator.Clasa == clasa);
        if (materieId is not null) q = q.Where(m => m.Utilizator.ProfilMentor != null && m.Utilizator.ProfilMentor.Materii.Any(x => x.MaterieId == materieId));
        var membri = await q.OrderBy(m => m.Stare == MembruOrganizatie.InAsteptare ? 0 : 1).ThenByDescending(m => m.CerutLa)
            .Select(m => new MembruDto(m.UtilizatorId, m.Utilizator.Nume, m.Utilizator.Username, m.Utilizator.Email, m.Rol, m.Stare, m.CerutLa, m.ExpiraLa,
                m.PoatePublicaPublic, m.Utilizator.Clasa,
                m.Utilizator.ProfilMentor == null ? null : m.Utilizator.ProfilMentor.Descriere,
                m.Utilizator.ProfilMentor != null && m.Utilizator.ProfilMentor.DovadaCalificareUrl != null,
                m.Utilizator.ProfilMentor == null ? new List<string>() : m.Utilizator.ProfilMentor.Materii.Select(x => x.Materie.Denumire).ToList(),
                m.Rol == MembruOrganizatie.RolProfesor && m.Utilizator.ProfilMentor != null && m.Utilizator.ProfilMentor.RetrasLa != null))
            .ToListAsync(ct);
        // One organisation's members: the words are matched here, the same way as everywhere else (CautareText).
        return cuvinte.Length == 0 ? membri
            : membri.Where(m => cuvinte.All(CautareText.Normalizeaza($"{m.Nume} {m.Email} {m.Username}").Contains)).ToList();
    }

    private static async Task<OrganizatieAdminDto> DetaliiAdmin(MarkAiDbContext db, int id, CancellationToken ct) =>
        await db.Set<Organizatie>().AsNoTracking().Where(o => o.Id == id)
            .Select(o => new OrganizatieAdminDto(o.Id, o.Nume, o.Adresa, o.JudetId, o.Judet.Nume, o.LocalitateId, o.Localitate == null ? null : o.Localitate.Nume,
                o.Stare, o.MotivRespingere, o.CreatLa, o.AdminId, o.Admin.Nume, o.Admin.Email, o.Admin.Telefon, o.Admin.EmailConfirmatLa != null,
                o.Membri.Count(m => m.Rol == MembruOrganizatie.RolProfesor && m.Stare == MembruOrganizatie.Aprobat),
                o.Membri.Count(m => m.Rol == MembruOrganizatie.RolElev && m.Stare == MembruOrganizatie.Aprobat),
                o.Membri.Count(m => m.Stare == MembruOrganizatie.InAsteptare)))
            .SingleAsync(ct);
}

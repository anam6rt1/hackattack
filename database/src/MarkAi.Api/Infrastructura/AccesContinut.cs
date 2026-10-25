using System.Linq.Expressions;
using System.Security.Claims;
using MarkAi.Data;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Infrastructura;

/// <summary>
/// Who is asking, as far as content access goes: the approved organisations they belong to (as a student, a professor or
/// the admin), and the approved organisations they run. Anonymous visitors have id 0 and no organisations.
/// </summary>
public sealed record ContextAcces(int UtilizatorId, bool Admin, int[] Organizatii, int[] Administrate)
{
    public static readonly ContextAcces Anonim = new(0, false, [], []);
}

/// <summary>
/// The one place that decides who may see and who may edit a Lecție (<see cref="Parcurs"/>) and its chapters. Every content
/// read goes through it (subject counts, paths, chapters, quiz, progress, the phone tutor); Triviador is the agreed exception.
/// <list type="bullet">
/// <item>See: published AND (public, OR its home or a shared organisation is one of the user's), OR the user may edit it.</item>
/// <item>Edit: a platform admin, the author, or the admin of its home organisation. Shares never give edit rights.</item>
/// <item>Publishing switch: when an organisation stops a professor from publishing publicly, that professor's Lecții whose home
/// is that organisation are organisation-only and cannot be made public again there.</item>
/// </list>
/// </summary>
public sealed class AccesContinut(MarkAiDbContext db)
{
    private ContextAcces? _ultimul; // one request usually asks several times for the same person

    public async Task<ContextAcces> PentruAsync(ClaimsPrincipal u, CancellationToken ct)
    {
        if (u.Identity?.IsAuthenticated != true) return ContextAcces.Anonim;
        var uid = u.Id();
        if (_ultimul?.UtilizatorId == uid) return _ultimul;
        return _ultimul = await PentruAsync(uid, u.EsteAdmin(), ct);
    }

    /// <summary>For a user id without a request (the phone tutor runs inside Twilio's webhook).</summary>
    public async Task<ContextAcces> PentruAsync(int uid, bool admin, CancellationToken ct)
    {
        // One round trip (UNION ALL): this runs on almost every signed-in request, and each query holds a pooled connection.
        var randuri = await db.Set<MembruOrganizatie>()
            .Where(m => m.UtilizatorId == uid && m.Stare == MembruOrganizatie.Aprobat && m.Organizatie.Stare == Organizatie.Aprobata)
            .Select(m => new { Id = m.OrganizatieId, Admin = false })
            .Concat(db.Set<Organizatie>().Where(o => o.AdminId == uid && o.Stare == Organizatie.Aprobata).Select(o => new { o.Id, Admin = true }))
            .ToListAsync(ct);
        var administrate = randuri.Where(r => r.Admin).Select(r => r.Id).Distinct().ToArray();
        return new ContextAcces(uid, admin, randuri.Select(r => r.Id).Distinct().ToArray(), administrate);
    }

    public static Expression<Func<Parcurs, bool>> PoateEdita(ContextAcces a)
    {
        var (uid, admin, administrate) = (a.UtilizatorId, a.Admin, a.Administrate);
        return p => admin || (uid != 0 && p.AutorId == uid) || (p.OrganizatieId != null && administrate.Contains(p.OrganizatieId.Value));
    }

    public static Expression<Func<Parcurs, bool>> Vizibil(ContextAcces a)
    {
        var (uid, admin, organizatii, administrate) = (a.UtilizatorId, a.Admin, a.Organizatii, a.Administrate);
        // The editing condition is repeated inline (EF cannot compose expressions); keep it equal to PoateEdita.
        return p => admin || (uid != 0 && p.AutorId == uid) || (p.OrganizatieId != null && administrate.Contains(p.OrganizatieId.Value))
            || (p.Publicat && (p.Vizibilitate == Parcurs.Public
                || (p.OrganizatieId != null && organizatii.Contains(p.OrganizatieId.Value))
                || p.Partajari.Any(s => organizatii.Contains(s.OrganizatieId))));
    }

    /// <summary>A chapter is visible when its Lecție is and the chapter is published, or when the user may edit the Lecție.</summary>
    public static Expression<Func<Curs, bool>> CapitolVizibil(ContextAcces a)
    {
        var (uid, admin, organizatii, administrate) = (a.UtilizatorId, a.Admin, a.Organizatii, a.Administrate);
        return c => admin || (uid != 0 && c.Parcurs.AutorId == uid) || (c.Parcurs.OrganizatieId != null && administrate.Contains(c.Parcurs.OrganizatieId.Value))
            || (c.Publicat && c.Parcurs.Publicat && (c.Parcurs.Vizibilitate == Parcurs.Public
                || (c.Parcurs.OrganizatieId != null && organizatii.Contains(c.Parcurs.OrganizatieId.Value))
                || c.Parcurs.Partajari.Any(s => organizatii.Contains(s.OrganizatieId))));
    }

    public IQueryable<Parcurs> Parcursuri(ContextAcces a) => db.Set<Parcurs>().Where(Vizibil(a));

    public IQueryable<Curs> Capitole(ContextAcces a) => db.Curs.Where(CapitolVizibil(a));

    public IQueryable<Parcurs> Editabile(ContextAcces a) => db.Set<Parcurs>().Where(PoateEdita(a));

    /// <summary>The chapter, if the user may see it; otherwise 404 (an outsider is not told it exists).</summary>
    public async Task AsiguraCapitolVizibilAsync(ClaimsPrincipal u, int cursId, CancellationToken ct)
    {
        var a = await PentruAsync(u, ct);
        if (!await Capitole(a).AnyAsync(c => c.Id == cursId, ct)) throw new KeyNotFoundException("Lecția nu există.");
    }

    /// <summary>The Lecție for editing: 404 when the user cannot even see it, 403 when they see it but may not edit it.</summary>
    public async Task<Parcurs> PentruEditareAsync(ClaimsPrincipal u, int parcursId, CancellationToken ct)
    {
        var a = await PentruAsync(u, ct);
        var p = await db.Set<Parcurs>().Include(x => x.Partajari).SingleOrDefaultAsync(x => x.Id == parcursId, ct);
        if (p is null || !await Parcursuri(a).AnyAsync(x => x.Id == parcursId, ct)) throw new KeyNotFoundException("Lecția nu există.");
        if (!await Editabile(a).AnyAsync(x => x.Id == parcursId, ct)) throw new UnauthorizedAccessException();
        return p;
    }

    /// <summary>The chapter for editing, with the same 404/403 rule as its Lecție.</summary>
    public async Task<Curs> CapitolPentruEditareAsync(ClaimsPrincipal u, int cursId, CancellationToken ct)
    {
        var parcursId = await db.Curs.Where(c => c.Id == cursId).Select(c => (int?)c.ParcursId).SingleOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Capitolul nu există.");
        await PentruEditareAsync(u, parcursId, ct);
        return await db.Curs.SingleAsync(c => c.Id == cursId, ct);
    }

    /// <summary>
    /// Organisations a person may pick as a Lecție's home: approved organisations where they are an approved professor or
    /// the admin. For each, whether that organisation lets them publish publicly.
    /// </summary>
    public async Task<List<(int Id, string Nume, bool PoatePublicaPublic)>> OrganizatiiAutorAsync(int autorId, CancellationToken ct)
    {
        var ca = await db.Set<MembruOrganizatie>()
            .Where(m => m.UtilizatorId == autorId && m.Rol == MembruOrganizatie.RolProfesor && m.Stare == MembruOrganizatie.Aprobat
                && m.Organizatie.Stare == Organizatie.Aprobata)
            .Select(m => new { m.OrganizatieId, m.Organizatie.Nume, m.PoatePublicaPublic }).ToListAsync(ct);
        var admin = await db.Set<Organizatie>().Where(o => o.AdminId == autorId && o.Stare == Organizatie.Aprobata)
            .Select(o => new { o.Id, o.Nume }).ToListAsync(ct);
        return ca.Select(m => (m.OrganizatieId, m.Nume, m.PoatePublicaPublic))
            .Concat(admin.Where(o => ca.All(m => m.OrganizatieId != o.Id)).Select(o => (o.Id, o.Nume, true)))
            .OrderBy(o => o.Item2).ToList();
    }

    /// <summary>False when the home organisation has switched off public publishing for the author.</summary>
    public async Task<bool> PoatePublicaPublicAsync(int? autorId, int? organizatieId, CancellationToken ct) =>
        organizatieId is null || autorId is null || !await db.Set<MembruOrganizatie>().AnyAsync(m => m.OrganizatieId == organizatieId
            && m.UtilizatorId == autorId && m.Rol == MembruOrganizatie.RolProfesor && !m.PoatePublicaPublic, ct);
}

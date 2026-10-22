using MarkAi.Api.Infrastructura;
using MarkAi.Api.Servicii.Mesagerie;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Servicii;

/// <summary>
/// Organisation memberships: requests, decisions, notifications, and the professor's approval state, which is derived from
/// them and kept in <see cref="ProfilMentor.StareVerificare"/>, so everything that already checks "approved professor" keeps
/// working: approved once an approved organisation approves them (or they run one), and from then on approved even when they
/// leave or are removed (plan 2); only a platform admin withdraws it (<see cref="RetrageAsync"/>), after which only an
/// organisation's approval given later counts.
/// </summary>
public sealed class ServiciuOrganizatii(MarkAiDbContext db, Posta posta, TimeProvider ceas)
{
    /// <summary>A request nobody decides on expires after this.</summary>
    public static readonly TimeSpan DurataCerere = TimeSpan.FromDays(7);

    private DateTime Acum() => ceas.GetUtcNow().UtcDateTime;

    public async Task SincronizeazaProfesorAsync(int utilizatorId, CancellationToken ct)
    {
        var p = await db.ProfiluriMentor.SingleOrDefaultAsync(x => x.UtilizatorId == utilizatorId, ct);
        if (p is null) return;
        var retras = p.RetrasLa;
        if (retras is null && p.StareVerificare == RezervareService.MentorAprobat) return;
        var membri = await db.Set<MembruOrganizatie>().Where(m => m.UtilizatorId == utilizatorId && m.Rol == MembruOrganizatie.RolProfesor)
            .Select(m => new { m.Stare, OrgStare = m.Organizatie.Stare, m.MotivRespingere, m.DecisLa }).ToListAsync(ct);
        var org = await db.Set<Organizatie>().Where(o => o.AdminId == utilizatorId).Select(o => new { o.Stare, o.DecisLa }).SingleOrDefaultAsync(ct);
        bool Dupa(DateTime? decis) => retras is null || decis > retras;
        if ((org?.Stare == Organizatie.Aprobata && Dupa(org.DecisLa))
            || membri.Any(m => m.Stare == MembruOrganizatie.Aprobat && m.OrgStare == Organizatie.Aprobata && Dupa(m.DecisLa)))
        {
            (p.StareVerificare, p.MotivRespingere, p.RetrasLa) = (RezervareService.MentorAprobat, null, null);
            p.ActivatLa ??= Acum();
        }
        // Withdrawn by the platform admin: stays so, with the admin's reason, until an organisation approves them again.
        else if (retras is not null) return;
        else if (org?.Stare == Organizatie.InAsteptare || membri.Any(m => m.Stare == MembruOrganizatie.InAsteptare))
            (p.StareVerificare, p.MotivRespingere) = (Endpoints.AutentificareEndpoints.StareInAsteptare, null);
        else
            (p.StareVerificare, p.MotivRespingere) = (Endpoints.AutentificareEndpoints.StareRespins,
                membri.Where(m => m.MotivRespingere != null).OrderByDescending(m => m.DecisLa).Select(m => m.MotivRespingere).FirstOrDefault()
                ?? "Nicio organizație nu a aprobat încă cererea ta. Poți cere să intri în altă organizație.");
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// A platform admin withdraws a professor's approval: refused with the reason (shown to them), and their upcoming sessions
    /// cancelled, bookings included, the way a professor cancels a session; the professor and the booked students are told.
    /// </summary>
    /// <returns>How many sessions were cancelled.</returns>
    public async Task<int> RetrageAsync(int profesorId, string? motiv, CancellationToken ct)
    {
        var p = await db.ProfiluriMentor.Include(x => x.Utilizator).SingleOrDefaultAsync(x => x.UtilizatorId == profesorId, ct)
            ?? throw new KeyNotFoundException("Profesorul nu există.");
        if (p.StareVerificare != RezervareService.MentorAprobat) throw new Conflict("Profesorul nu este aprobat acum.");
        var acum = Acum();
        (p.StareVerificare, p.MotivRespingere, p.RetrasLa) = (Endpoints.AutentificareEndpoints.StareRespins, Validare.Obligatoriu(motiv, "Motivul", 500), acum);
        var sedinte = await db.CamereTutoring.Include(s => s.Rezervari).ThenInclude(r => r.ComisionMentor).Include(s => s.Rezervari).ThenInclude(r => r.Elev)
            .Where(s => s.MentorId == profesorId && s.Status == RezervareService.SlotDeschis && s.InceputLa > acum).ToListAsync(ct);
        foreach (var s in sedinte)
        {
            s.Status = "anulata";
            foreach (var r in s.Rezervari.Where(r => r.Status == RezervareService.RezervareConfirmata))
            {
                r.Status = RezervareService.RezervareAnulata;
                if (r.ComisionMentor is not null) r.ComisionMentor.Status = "anulat";
                posta.Pune(Sabloane.Notificare(r.Elev.Email, "O ședință a fost anulată", $"„{s.Titlu}” a fost anulată",
                    $"Ședința cu {p.Utilizator.Nume} din {s.InceputLa:dd.MM.yyyy} nu mai are loc. Rezervarea ta a fost anulată."), "sedinta_anulata");
            }
        }
        posta.Pune(Sabloane.Notificare(p.Utilizator.Email, "Aprobarea ta de profesor a fost retrasă", "Aprobarea ta de profesor a fost retrasă",
            $"Motiv: {p.MotivRespingere}", "Ședințele tale viitoare au fost anulate. O organizație te poate aproba din nou."), "profesor_retras");
        await db.SaveChangesAsync(ct);
        return sedinte.Count;
    }

    /// <summary>
    /// Approves a withdrawn professor again: the platform admin directly (<paramref name="membru"/> null), or the admin of an
    /// organisation the professor is an approved member of (the approval is dated now, so it counts after the withdrawal).
    /// </summary>
    public async Task ReaprobaAsync(int profesorId, MembruOrganizatie? membru, int decidentId, CancellationToken ct)
    {
        var p = await db.ProfiluriMentor.Include(x => x.Utilizator).SingleOrDefaultAsync(x => x.UtilizatorId == profesorId, ct)
            ?? throw new KeyNotFoundException("Profesorul nu există.");
        if (p.RetrasLa is null) throw new Conflict("Aprobarea acestui profesor nu a fost retrasă.");
        if (membru is null) (p.StareVerificare, p.MotivRespingere, p.RetrasLa) = (RezervareService.MentorAprobat, null, null);
        else
        {
            if (membru.Rol != MembruOrganizatie.RolProfesor || membru.Stare != MembruOrganizatie.Aprobat)
                throw new CerereInvalida("Poți aproba din nou doar un profesor care face parte din organizație.");
            (membru.DecisLa, membru.DecisDeId) = (Acum(), decidentId);
        }
        posta.Pune(Sabloane.Notificare(p.Utilizator.Email, "Ești din nou profesor aprobat", "Ești din nou profesor aprobat",
            "Poți programa din nou ședințe, iar elevii te văd la Profesori."), "profesor_reaprobat");
        await db.SaveChangesAsync(ct);
        await SincronizeazaProfesorAsync(profesorId, ct);
    }

    /// <summary>Asks to join (or asks again after a refusal or expiry). Students may have only one organisation.</summary>
    public async Task<MembruOrganizatie> CereAsync(Utilizator u, int organizatieId, string rol, CancellationToken ct)
    {
        var org = await db.Set<Organizatie>().SingleOrDefaultAsync(o => o.Id == organizatieId && o.Stare == Organizatie.Aprobata, ct)
            ?? throw new CerereInvalida("Organizația nu există sau nu este încă aprobată.");
        if (rol == MembruOrganizatie.RolElev && await db.Set<MembruOrganizatie>().AnyAsync(m => m.UtilizatorId == u.Id && m.Rol == MembruOrganizatie.RolElev
                && m.OrganizatieId != organizatieId && (m.Stare == MembruOrganizatie.Aprobat || m.Stare == MembruOrganizatie.InAsteptare), ct))
            throw new Conflict("Ești deja într-o organizație sau ai o cerere în așteptare. Ieși din ea înainte să ceri alta.");
        var m = await db.Set<MembruOrganizatie>().SingleOrDefaultAsync(x => x.OrganizatieId == organizatieId && x.UtilizatorId == u.Id, ct);
        if (m is { Stare: MembruOrganizatie.Aprobat or MembruOrganizatie.InAsteptare })
            throw new Conflict(m.Stare == MembruOrganizatie.Aprobat ? "Faci deja parte din această organizație." : "Ai deja o cerere în așteptare la această organizație.");
        if (m is null) db.Add(m = new MembruOrganizatie { OrganizatieId = organizatieId, UtilizatorId = u.Id, Rol = rol });
        (m.Rol, m.Stare, m.CerutLa, m.ExpiraLa, m.AmintitLa, m.DecisDeId, m.DecisLa, m.MotivRespingere) =
            (rol, MembruOrganizatie.InAsteptare, Acum(), Acum() + DurataCerere, null, null, null, null);
        await db.SaveChangesAsync(ct);
        // People who have not confirmed their email yet are announced when they do (DupaConfirmareEmailAsync).
        if (u.EmailConfirmatLa is not null) await AnuntaCerereaAsync(org, u, rol, ct);
        if (rol == MembruOrganizatie.RolProfesor) await SincronizeazaProfesorAsync(u.Id, ct);
        return m;
    }

    public async Task DecideAsync(MembruOrganizatie m, bool aprobat, string? motiv, int decidentId, CancellationToken ct)
    {
        if (m.Stare != MembruOrganizatie.InAsteptare && aprobat == (m.Stare == MembruOrganizatie.Aprobat)) return;
        (m.Stare, m.DecisDeId, m.DecisLa, m.MotivRespingere) = (aprobat ? MembruOrganizatie.Aprobat : MembruOrganizatie.Respins,
            decidentId, Acum(), aprobat ? null : Validare.Optional(motiv, 500) ?? "Cererea nu a fost aprobată.");
        var membru = await db.Utilizatori.Where(u => u.Id == m.UtilizatorId).Select(u => new { u.Email, u.Nume }).SingleAsync(ct);
        var org = await db.Set<Organizatie>().Where(o => o.Id == m.OrganizatieId).Select(o => o.Nume).SingleAsync(ct);
        posta.Pune(aprobat
            ? Sabloane.Notificare(membru.Email, "Cererea ta a fost aprobată", $"Bun venit în {org}!",
                m.Rol == MembruOrganizatie.RolProfesor ? $"Cererea ta de profesor în „{org}” a fost aprobată." : $"Faci acum parte din „{org}” și vezi lecțiile organizației.")
            : Sabloane.Notificare(membru.Email, "Cererea ta nu a fost aprobată", $"Cererea ta în „{org}” nu a fost aprobată",
                $"Motiv: {m.MotivRespingere}", "Poți cere să intri în altă organizație din aplicație."), aprobat ? "membru_aprobat" : "membru_respins");
        await db.SaveChangesAsync(ct);
        if (m.Rol == MembruOrganizatie.RolProfesor) await SincronizeazaProfesorAsync(m.UtilizatorId, ct);
    }

    /// <summary>Who decides on a request: the organisation's admin for professors; for students also its approved professors.</summary>
    public async Task<bool> PoateDecideAsync(int utilizatorId, int organizatieId, string rol, CancellationToken ct) =>
        await db.Set<Organizatie>().AnyAsync(o => o.Id == organizatieId && o.AdminId == utilizatorId && o.Stare == Organizatie.Aprobata, ct)
        || (rol == MembruOrganizatie.RolElev && await db.Set<MembruOrganizatie>().AnyAsync(m => m.OrganizatieId == organizatieId && m.UtilizatorId == utilizatorId
            && m.Rol == MembruOrganizatie.RolProfesor && m.Stare == MembruOrganizatie.Aprobat && m.Organizatie.Stare == Organizatie.Aprobata, ct));

    /// <summary>Once an account's email is confirmed, the people who decide on its pending requests are told about them.</summary>
    public async Task DupaConfirmareEmailAsync(Utilizator u, CancellationToken ct)
    {
        var orgNoua = await db.Set<Organizatie>().SingleOrDefaultAsync(o => o.AdminId == u.Id && o.Stare == Organizatie.InAsteptare, ct);
        if (orgNoua is not null) await AnuntaAdminiiPlatformeiAsync($"Cerere nouă de organizație: {orgNoua.Nume}",
            $"{u.Nume} vrea să administreze organizația „{orgNoua.Nume}” ({orgNoua.Adresa}).", ct);
        var cereri = await db.Set<MembruOrganizatie>().Include(m => m.Organizatie)
            .Where(m => m.UtilizatorId == u.Id && m.Stare == MembruOrganizatie.InAsteptare).ToListAsync(ct);
        foreach (var m in cereri) await AnuntaCerereaAsync(m.Organizatie, u, m.Rol, ct);
    }

    private async Task AnuntaCerereaAsync(Organizatie org, Utilizator u, string rol, CancellationToken ct)
    {
        var admin = await db.Utilizatori.Where(x => x.Id == org.AdminId).Select(x => new { x.Email, x.Nume }).SingleAsync(ct);
        posta.Pune(Sabloane.Notificare(admin.Email, rol == MembruOrganizatie.RolProfesor ? "Un profesor cere să intre în organizație" : "Un elev cere să intre în organizație",
            $"Cerere nouă în „{org.Nume}”",
            $"{u.Nume} ({u.Email}) cere să intre ca {(rol == MembruOrganizatie.RolProfesor ? "profesor" : "elev")}. Cererea expiră în 7 zile dacă nu decide nimeni."),
            "cerere_membru");
        await db.SaveChangesAsync(ct);
    }

    public async Task AnuntaAdminiiPlatformeiAsync(string subiect, string text, CancellationToken ct)
    {
        var admini = await db.Utilizatori.Where(u => u.Roluri.Any(r => r.Rol.Nume == Roluri.Admin)).Select(u => u.Email).ToListAsync(ct);
        foreach (var a in admini) posta.Pune(Sabloane.Notificare(a, subiect, subiect, text), "cerere_organizatie");
        await db.SaveChangesAsync(ct);
    }
}

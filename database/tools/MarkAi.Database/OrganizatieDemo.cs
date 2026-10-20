using MarkAi.Data;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Creates (once) an approved demo organisation for the accounts that existed before organisations: the chosen account
/// becomes its admin (and a professor in it), every current professor joins it as approved, and every current student
/// joins it as an approved student (unless already in an organisation). Running it again only adds what is missing.
/// </summary>
static class OrganizatieDemo
{
    public static async Task Run(string connection, string adminUsername, string nume, int judetId, int? localitateId)
    {
        await using var db = DatabaseTools.Open(connection);
        var acum = DateTime.UtcNow;
        if (!await db.Set<Judet>().AnyAsync(j => j.Id == judetId))
            throw new InvalidOperationException($"County {judetId} not found. Run 'seed-localitati' first.");
        var admin = await db.Utilizatori.Include(u => u.Roluri).ThenInclude(r => r.Rol).Include(u => u.ProfilMentor)
            .SingleOrDefaultAsync(u => u.Username.ToLower() == adminUsername.ToLower())
            ?? throw new InvalidOperationException($"No user '{adminUsername}'.");

        var org = await db.Set<Organizatie>().SingleOrDefaultAsync(o => o.AdminId == admin.Id);
        if (org is null)
        {
            org = new Organizatie { Nume = nume, Adresa = "Adresă de completat", JudetId = judetId, LocalitateId = localitateId,
                Stare = Organizatie.Aprobata, AdminId = admin.Id, DecisLa = acum, CreatLa = acum };
            db.Add(org);
        }
        foreach (var rol in new[] { "organizatie", "profesor" }.Where(r => admin.Roluri.All(x => x.Rol.Nume != r)))
            admin.Roluri.Add(new UtilizatorRol { Rol = await db.Roluri.SingleAsync(x => x.Nume == rol) });
        admin.ProfilMentor ??= new ProfilMentor { Descriere = "Administratorul organizației.", StareVerificare = "aprobat", ActivatLa = acum };
        await db.SaveChangesAsync();

        var profesori = await db.ProfiluriMentor.ToListAsync();
        var membri = await db.Set<MembruOrganizatie>().Where(m => m.OrganizatieId == org.Id).Select(m => m.UtilizatorId).ToListAsync();
        var adaugatiP = 0;
        foreach (var p in profesori)
        {
            (p.StareVerificare, p.MotivRespingere) = ("aprobat", null);
            p.ActivatLa ??= acum;
            if (membri.Contains(p.UtilizatorId)) continue;
            db.Add(new MembruOrganizatie { OrganizatieId = org.Id, UtilizatorId = p.UtilizatorId, Rol = MembruOrganizatie.RolProfesor,
                Stare = MembruOrganizatie.Aprobat, CerutLa = acum, DecisLa = acum, DecisDeId = admin.Id });
            adaugatiP++;
        }
        // Students: accounts whose only role is "elev" and who are not yet in (or asking to join) an organisation.
        var elevi = await db.Utilizatori.Where(u => u.Roluri.All(r => r.Rol.Nume == "elev")
                && !u.Organizatii.Any(m => m.Rol == MembruOrganizatie.RolElev && (m.Stare == MembruOrganizatie.Aprobat || m.Stare == MembruOrganizatie.InAsteptare)))
            .Select(u => u.Id).ToListAsync();
        foreach (var id in elevi)
            db.Add(new MembruOrganizatie { OrganizatieId = org.Id, UtilizatorId = id, Rol = MembruOrganizatie.RolElev,
                Stare = MembruOrganizatie.Aprobat, CerutLa = acum, DecisLa = acum, DecisDeId = admin.Id });
        await db.SaveChangesAsync();
        Console.WriteLine($"Organisation #{org.Id} \"{org.Nume}\" (admin {admin.Username}): {adaugatiP} professor(s) and {elevi.Count} student(s) added.");
    }
}

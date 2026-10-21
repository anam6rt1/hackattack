using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Suport;

/// <summary>The seeded data every test database starts with (copied from one template, see <see cref="BazaTest"/>).</summary>
public static class Fixturi
{
    public const string ParolaTest = "Test-demo-2026!";
    /// <summary>Id of the fixture organisation "Liceul de Test" (approved, admin: director).</summary>
    public const int IdLiceu = 1;

    // Fixtures: the three roles, three subjects, and student george, approved teacher irina, administrator admin.
    public static async Task Populeaza(string conexiune)
    {
        var optiuni = new DbContextOptionsBuilder<MarkAiDbContext>()
            .UseNpgsql(conexiune, x => x.MigrationsHistoryTable("__EFMigrationsHistory", "mark_ai")).Options;
        await using var db = new MarkAiDbContext(optiuni);
        await db.Database.MigrateAsync();
        var roles = new[] { new Rol { Nume = "elev" }, new Rol { Nume = "profesor" }, new Rol { Nume = "admin" }, new Rol { Nume = "organizatie" } };
        var subjects = new[] { new Materie { Denumire = "Limba și literatura română" }, new Materie { Denumire = "Matematică" }, new Materie { Denumire = "Biologie" } };
        db.AddRange(roles); db.AddRange(subjects); await db.SaveChangesAsync();
        foreach (var (username, fullName, role) in new[] { ("george", "George Pavel", 0), ("irina", "Irina Marin", 1), ("admin", "Administrator", 2) })
        {
            var u = new Utilizator { Nume = fullName, Username = username, Email = username + "@example.invalid", Telefon = role == 1 ? "+40722123456" : null,
                EmailConfirmatLa = DateTime.UtcNow };
            u.ParolaHash = Parole.Hash(u, ParolaTest);
            u.Roluri.Add(new UtilizatorRol { RolId = roles[role].Id });
            if (role == 1) u.ProfilMentor = new ProfilMentor { StareVerificare = "aprobat", ActivatLa = DateTime.UtcNow, Descriere = "Profesor de limba română, cu 8 ani de experiență. Învățăm cu răbdare și exemple clare.", PozaUrl = "fixture-photo.png", DovadaCalificareUrl = "fixture-diploma.pdf", Materii = new List<MentorMaterie> { new() { MaterieId = subjects[0].Id } } };
            db.Add(u);
        }
        await db.SaveChangesAsync();

        // A few SIRUTA rows (the full register takes seconds to load): Bucharest with two sectors, Alba with two localities.
        db.AddRange(new Judet { Id = 40, Nume = "București" }, new Judet { Id = 1, Nume = "Alba" });
        db.AddRange(
            new Localitate { Id = 179141, JudetId = 40, Nume = "Sectorul 1", UatId = 179132, UatNume = "Municipiul București", Urban = true, Cautare = "sectorul 1" },
            new Localitate { Id = 179150, JudetId = 40, Nume = "Sectorul 2", UatId = 179132, UatNume = "Municipiul București", Urban = true, Cautare = "sectorul 2" },
            new Localitate { Id = 1026, JudetId = 1, Nume = "Alba Iulia", UatId = 1017, UatNume = "Municipiul Alba Iulia", Urban = true, Cautare = "alba iulia" },
            new Localitate { Id = 1035, JudetId = 1, Nume = "Bărăbanț", UatId = 1017, UatNume = "Municipiul Alba Iulia", Urban = true, Cautare = "barabant" });
        // An approved organisation run by "director" (organisation admin and professor), with irina as an approved professor.
        var director = new Utilizator { Nume = "Dana Director", Username = "director", Email = "director@example.invalid", Telefon = "+40722000111",
            EmailConfirmatLa = DateTime.UtcNow, JudetId = 40 };
        director.ParolaHash = Parole.Hash(director, ParolaTest);
        director.Roluri.Add(new UtilizatorRol { Rol = await db.Roluri.SingleAsync(r => r.Nume == "organizatie") });
        director.Roluri.Add(new UtilizatorRol { RolId = roles[1].Id });
        director.ProfilMentor = new ProfilMentor { StareVerificare = "aprobat", ActivatLa = DateTime.UtcNow, Descriere = "Directoarea liceului." };
        db.Add(director);
        await db.SaveChangesAsync();
        var liceu = new Organizatie { Nume = "Liceul de Test", Adresa = "Strada Școlii 1", JudetId = 40, LocalitateId = 179141, Stare = Organizatie.Aprobata,
            AdminId = director.Id, CreatLa = DateTime.UtcNow, DecisLa = DateTime.UtcNow };
        db.Add(liceu);
        await db.SaveChangesAsync();
        var irina = await db.Utilizatori.SingleAsync(u => u.Username == "irina");
        db.AddRange(
            new MembruOrganizatie { OrganizatieId = liceu.Id, UtilizatorId = director.Id, Rol = MembruOrganizatie.RolProfesor, Stare = MembruOrganizatie.Aprobat, CerutLa = DateTime.UtcNow },
            new MembruOrganizatie { OrganizatieId = liceu.Id, UtilizatorId = irina.Id, Rol = MembruOrganizatie.RolProfesor, Stare = MembruOrganizatie.Aprobat, CerutLa = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

}

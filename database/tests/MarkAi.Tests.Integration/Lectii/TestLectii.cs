using System.Net;
using System.Text.Json;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using MarkAi.Tests.Suport;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Lectii;

/// <summary>
/// Fixtures for the Lecție tests, on top of the harness ones: a second organisation (Centrul Alba, admin ovidiu) where irina also
/// teaches, professor petra in Liceul de Test, students elena (Liceul de Test) and radu (Centrul Alba); george is in none.
/// </summary>
public abstract class TestLectii : TestCuHarness
{
    protected override IReadOnlyDictionary<string, string?> Setari => new Dictionary<string, string?>
    {
        ["Limite:AutentificariPeMinut"] = "200", ["Limite:PauzaQuizSecunde"] = "0",
    };

    protected const int Liceu = Harness.IdLiceu;
    protected int Centru;
    protected HttpClient admin = null!, irina = null!, petra = null!, director = null!, ovidiu = null!, george = null!, elena = null!, radu = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        Centru = await Populeaza();
        (admin, irina, petra, director, ovidiu, george, elena, radu) = (await h.Login("admin"), await h.Login("irina"), await h.Login("petra"),
            await h.Login("director"), await h.Login("ovidiu"), await h.Login("george"), await h.Login("elena"), await h.Login("radu"));
    }

    private Task<int> Populeaza() => h.Db(async db =>
    {
        var roluri = await db.Roluri.ToDictionaryAsync(r => r.Nume, r => r.Id);
        Utilizator Cont(string username, string nume, params string[] rol)
        {
            var u = new Utilizator { Nume = nume, Username = username, Email = username + "@example.invalid", EmailConfirmatLa = DateTime.UtcNow };
            u.ParolaHash = Parole.Hash(u, Harness.ParolaTest);
            foreach (var r in rol) u.Roluri.Add(new UtilizatorRol { RolId = roluri[r] });
            if (rol.Contains("profesor"))
                u.ProfilMentor = new ProfilMentor { StareVerificare = "aprobat", ActivatLa = DateTime.UtcNow, Descriere = "Profesor de test.",
                    Materii = new List<MentorMaterie> { new() { MaterieId = 1 } } };
            db.Add(u);
            return u;
        }
        var ovidiu = Cont("ovidiu", "Ovidiu Organizator", "organizatie", "profesor");
        var petra = Cont("petra", "Petra Pop", "profesor");
        var elena = Cont("elena", "Elena Elev", "elev");
        var radu = Cont("radu", "Radu Elev", "elev");
        await db.SaveChangesAsync();
        var centru = new Organizatie { Nume = "Centrul Alba", Adresa = "Str. Unirii 2", JudetId = 1, Stare = Organizatie.Aprobata, AdminId = ovidiu.Id, CreatLa = DateTime.UtcNow };
        db.Add(centru);
        await db.SaveChangesAsync();
        var irina = await db.Utilizatori.SingleAsync(u => u.Username == "irina");
        MembruOrganizatie Membru(int org, int uid, string rol) => new() { OrganizatieId = org, UtilizatorId = uid, Rol = rol, Stare = MembruOrganizatie.Aprobat, CerutLa = DateTime.UtcNow };
        db.AddRange(Membru(centru.Id, irina.Id, MembruOrganizatie.RolProfesor), Membru(centru.Id, ovidiu.Id, MembruOrganizatie.RolProfesor),
            Membru(Liceu, petra.Id, MembruOrganizatie.RolProfesor), Membru(Liceu, elena.Id, MembruOrganizatie.RolElev),
            Membru(centru.Id, radu.Id, MembruOrganizatie.RolElev));
        await db.SaveChangesAsync();
        return centru.Id;
    });

    protected static object Lectie(string titlu, int? organizatieId, string clasa = "Bac", string vizibilitate = "public", int materieId = 1,
        int[]? partajari = null, bool publicat = true) =>
        new { materieId, titlu, clasa, vizibilitate, organizatieId, partajari = partajari ?? [], publicat };

    protected static async Task<int> Creeaza(HttpClient c, object lectie) => (await c.Post("/parcursuri", lectie).Json(HttpStatusCode.Created)).Int("id");

    protected static async Task<int> Capitol(HttpClient editor, int parcursId, string titlu, string text = "Material de test.") =>
        (await editor.Post("/cursuri", new { parcursId, titlu, materiale = new[] { new { titlu = "Notițe", text } } }).Json(HttpStatusCode.Created)).Int("id");

    protected static async Task<List<int>> Ids(HttpClient c, string url) => (await c.GetJson(url)).Lista().Select(e => e.Int("id")).ToList();

    protected static async Task<int> NumarLectii(HttpClient c, int materieId) =>
        (await c.GetJson("/materii")).Lista().Single(m => m.Int("id") == materieId).Int("numarLectii");

    protected static Task<JsonElement> Editor(HttpClient c, int id) => c.GetJson($"/continut/parcursuri/{id}");

    protected Task<int> Id(string username) => h.Db(db => db.Utilizatori.Where(u => u.Username == username).Select(u => u.Id).SingleAsync());

    /// <summary>The organisation-only Lecție of irina's, home Liceul de Test, grade XI, with two chapters and two questions.</summary>
    protected async Task<(int Lectie, int Cap1, int Cap2)> LectiePrivata()
    {
        var privata = await Creeaza(irina, Lectie("Ion — organizație", Liceu, clasa: "XI", vizibilitate: "organizatie"));
        var cap1 = await Capitol(irina, privata, "Ion: capitolul 1", "Glasul pământului și satul lui Ion");
        var cap2 = await Capitol(irina, privata, "Ion: capitolul 2", "Glasul iubirii și finalul romanului");
        await h.Db(async db =>
        {
            foreach (var text in new[] { "Cine e Ana?", "Cine e Florica?" })
                db.Add(new Intrebare { MaterieId = 1, ParcursId = privata, Text = text,
                    Variante = [new VariantaIntrebare { Text = "Răspunsul bun", Corecta = true }, new VariantaIntrebare { Text = "Alt răspuns" }] });
            await db.SaveChangesAsync();
        });
        return (privata, cap1, cap2);
    }

    protected async Task<int> LectiePlatforma()
    {
        var p = await Creeaza(admin, Lectie("Platformă Bac", null));
        await Capitol(admin, p, "Capitol platformă");
        return p;
    }
}

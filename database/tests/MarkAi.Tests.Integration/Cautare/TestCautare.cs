using System.Net;
using System.Text.Json;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using MarkAi.Tests.Suport;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Cautare;

/// <summary>
/// Places for the search and "near me" tests: irina teaches in Liceul de Test (Sectorul 1, București); ovidiu runs Centrul Alba
/// (Alba Iulia) and teaches at Școala Bărăbanț (bianca's); students george (no organisation, no place), elena (Liceul de Test,
/// lives in Sectorul 2), radu (Centrul Alba, lives in Alba Iulia).
/// </summary>
public abstract class TestCautare : TestCuHarness
{
    protected override IReadOnlyDictionary<string, string?> Setari => new Dictionary<string, string?>
    {
        ["Limite:AutentificariPeMinut"] = "200", ["Limite:CereriPeMinut"] = "2000", ["Limite:InregistrariPeOra"] = "100",
    };

    protected const int Bucuresti = 40, Sector1 = 179141, Sector2 = 179150, Alba = 1, AlbaIulia = 1026, Barabant = 1035, Liceu = Harness.IdLiceu;
    protected int Centru, ScoalaBarabant;
    protected HttpClient admin = null!, irina = null!, ovidiu = null!, george = null!, elena = null!, radu = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        (Centru, ScoalaBarabant) = await Populeaza();
        (admin, irina, ovidiu, george, elena, radu) = (await h.Login("admin"), await h.Login("irina"), await h.Login("ovidiu"),
            await h.Login("george"), await h.Login("elena"), await h.Login("radu"));
    }

    private Task<(int, int)> Populeaza() => h.Db(async db =>
    {
        var roluri = await db.Roluri.ToDictionaryAsync(r => r.Nume, r => r.Id);
        Utilizator Cont(string username, string nume, int? judet, int? localitate, params string[] rol)
        {
            var u = new Utilizator { Nume = nume, Username = username, Email = username + "@example.invalid", EmailConfirmatLa = DateTime.UtcNow,
                JudetId = judet, LocalitateId = localitate };
            u.ParolaHash = Parole.Hash(u, Harness.ParolaTest);
            foreach (var r in rol) u.Roluri.Add(new UtilizatorRol { RolId = roluri[r] });
            if (rol.Contains("profesor"))
                u.ProfilMentor = new ProfilMentor { StareVerificare = "aprobat", ActivatLa = DateTime.UtcNow, Descriere = "Profesor de test.",
                    Materii = new List<MentorMaterie> { new() { MaterieId = 1 } } };
            db.Add(u);
            return u;
        }
        var ovidiu = Cont("ovidiu", "Ovidiu Organizator", null, null, "organizatie", "profesor");
        var elena = Cont("elena", "Elena Elev", Bucuresti, Sector2, "elev");
        var radu = Cont("radu", "Radu Elev", Alba, AlbaIulia, "elev");
        var bianca = Cont("bianca", "Bianca Directoare", Alba, Barabant, "organizatie", "profesor");
        await db.SaveChangesAsync();
        var centru = new Organizatie { Nume = "Centrul Alba", Adresa = "Str. Unirii 2", JudetId = Alba, LocalitateId = AlbaIulia, Stare = Organizatie.Aprobata,
            AdminId = ovidiu.Id, CreatLa = DateTime.UtcNow };
        var barabant = new Organizatie { Nume = "Școala Bărăbanț", Adresa = "Str. Școlii 3", JudetId = Alba, LocalitateId = Barabant, Stare = Organizatie.Aprobata,
            AdminId = bianca.Id, CreatLa = DateTime.UtcNow };
        db.AddRange(centru, barabant);
        await db.SaveChangesAsync();
        MembruOrganizatie Membru(int org, int uid, string rol) => new() { OrganizatieId = org, UtilizatorId = uid, Rol = rol, Stare = MembruOrganizatie.Aprobat, CerutLa = DateTime.UtcNow };
        db.AddRange(Membru(Liceu, elena.Id, MembruOrganizatie.RolElev), Membru(centru.Id, radu.Id, MembruOrganizatie.RolElev),
            Membru(barabant.Id, ovidiu.Id, MembruOrganizatie.RolProfesor));
        await db.SaveChangesAsync();
        return (centru.Id, barabant.Id);
    });

    protected sealed record Gasite(List<int> Lectii, List<int> Capitole);
    protected sealed record LectieGasita(int Id, string? Loc, bool InLocalitateaTa);
    protected sealed record SlotGasit(int Id, string Tip, string? Loc, string? NotaLoc);
    protected sealed record ProfesorGasit(int Id, string? Loc, bool InLocalitateaTa, List<SlotGasit> Sloturi);

    protected static string? Loc(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null
        : string.Join(" · ", new[] { e.GetProperty("organizatie").GetString(),
            string.Join(", ", new[] { e.GetProperty("localitate").GetString(), e.GetProperty("judet").GetString() }.Where(x => x is not null)) }.Where(x => !string.IsNullOrEmpty(x)));

    protected static async Task<Gasite> Cauta(HttpClient c, string q, string extra = "")
    {
        var e = await c.GetJson($"/cautare?q={Uri.EscapeDataString(q)}{extra}");
        return new(e.Lista("lectii").Select(x => x.Int("id")).ToList(), e.Lista("capitole").Select(x => x.Int("id")).ToList());
    }

    protected static async Task<List<LectieGasita>> Lectii(HttpClient c, string url) =>
        (await c.GetJson(url)).Lista().Select(e => new LectieGasita(e.Int("id"), e.GetProperty("loc").GetString(), e.Bool("inLocalitateaTa"))).ToList();

    protected static async Task<List<ProfesorGasit>> Profesori(HttpClient c, string query = "") =>
        (await c.GetJson("/profesori" + query)).Lista().Select(p => new ProfesorGasit(p.Int("id"), Loc(p.GetProperty("loc")), p.Bool("inLocalitateaTa"),
            p.Lista("sloturi").Select(s => new SlotGasit(s.Int("id"), s.Str("tip"), Loc(s.GetProperty("loc")), s.GetProperty("notaLoc").GetString())).ToList())).ToList();

    protected static object Lectie(string titlu, int? organizatieId, string clasa = "Bac", string vizibilitate = "public", bool publicat = true, string? descriere = null) =>
        new { materieId = 1, titlu, descriere, clasa, vizibilitate, organizatieId, publicat };

    protected static object Slot(string titlu, DateTime inceputLa, string? tip = null, int? organizatieId = null, string? notaLoc = null, string? link = null,
        int capacitate = 5, bool proBono = true) =>
        new { materieId = 1, titlu, inceputLa, capacitate, proBono, linkIntalnire = link, tip, locOrganizatieId = organizatieId, notaLoc };

    protected static async Task<int> Creeaza(HttpClient c, object lectie) => (await c.Post("/parcursuri", lectie).Json(HttpStatusCode.Created)).Int("id");

    /// <summary>Creating a session returns the professor's dashboard; the new session is the one with the highest id.</summary>
    protected static async Task<int> CreeazaSlot(HttpClient c, object slot) =>
        (await c.Post("/profesori/me/sloturi", slot).Json(HttpStatusCode.Created)).Lista("sloturi").Select(s => s.GetProperty("slot").Int("id")).Max();

    protected static async Task<int> Capitol(HttpClient editor, int parcursId, string titlu, string? capitol = null) =>
        (await editor.Post("/cursuri", new { parcursId, titlu, capitol, materiale = new[] { new { titlu = "Notițe", text = "Material de test." } } })
            .Json(HttpStatusCode.Created)).Int("id");

    protected Task<int> Id(string username) => h.Db(db => db.Utilizatori.Where(u => u.Username == username).Select(u => u.Id).SingleAsync());

    protected static DateTime Maine => DateTime.UtcNow.AddDays(2);
}

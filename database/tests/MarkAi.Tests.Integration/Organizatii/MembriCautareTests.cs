using System.Net;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using MarkAi.Tests.Suport;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Organizatii;

/// <summary>
/// The organisation panel's member list: search (q: name, email or username, without diacritics) and filters (role, state,
/// grade, subject), together and on their own; an organisation admin only ever sees their own organisation. Liceul de Test has
/// director and irina (Română) as professors; added here: students Ștefania Ionescu (XI, approved), Andrei Pop (IX, pending),
/// Mara Lungu (XI, expired), Țibi Ene (XII, refused) and professor Radu Șerban (Matematică, approved).
/// </summary>
public sealed class MembriCautareTests : TestOrganizatii
{
    private HttpClient director = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        await h.Db(async db =>
        {
            var roluri = await db.Roluri.ToDictionaryAsync(r => r.Nume, r => r.Id);
            void Membru(string username, string nume, string rol, string stare, string? clasa = null, int? materie = null)
            {
                var u = new Utilizator { Nume = nume, Username = username, Email = $"{username}@scoala.example.invalid", EmailConfirmatLa = DateTime.UtcNow, Clasa = clasa };
                u.ParolaHash = Parole.Hash(u, Harness.ParolaTest);
                u.Roluri.Add(new UtilizatorRol { RolId = roluri[rol == MembruOrganizatie.RolElev ? "elev" : "profesor"] });
                if (materie is { } m)
                    u.ProfilMentor = new ProfilMentor { StareVerificare = "aprobat", ActivatLa = DateTime.UtcNow, Materii = new List<MentorMaterie> { new() { MaterieId = m } } };
                db.Add(new MembruOrganizatie { OrganizatieId = Liceu, Utilizator = u, Rol = rol, Stare = stare, CerutLa = DateTime.UtcNow });
            }
            Membru("stefania", "Ștefania Ionescu", MembruOrganizatie.RolElev, MembruOrganizatie.Aprobat, "XI");
            Membru("andrei", "Andrei Pop", MembruOrganizatie.RolElev, MembruOrganizatie.InAsteptare, "IX");
            Membru("mara", "Mara Lungu", MembruOrganizatie.RolElev, MembruOrganizatie.Expirat, "XI");
            Membru("tibi", "Țibi Ene", MembruOrganizatie.RolElev, MembruOrganizatie.Respins, "XII");
            Membru("radu.serban", "Radu Șerban", MembruOrganizatie.RolProfesor, MembruOrganizatie.Aprobat, materie: 2);
            await db.SaveChangesAsync();
        });
        director = await h.Login("director");
    }

    private async Task<List<string>> Membri(string query = "") =>
        (await director.GetJson("/organizatia-mea/membri" + query)).Lista().Select(m => m.Str("username")).Order().ToList();

    [Theory]
    [InlineData("stefania", "stefania")]
    [InlineData("Ștefania", "stefania")]
    [InlineData("ionescu", "stefania")]
    [InlineData("STEFANIA IONESCU", "stefania")]
    [InlineData("tibi", "tibi")]
    [InlineData("Ţibi", "tibi")] // cedilla
    [InlineData("serban", "radu.serban")]
    [InlineData("radu.serban", "radu.serban")]
    public async Task DupaNumeSauUtilizator(string q, string gasit) => (await Membri($"?q={Uri.EscapeDataString(q)}")).ShouldBe([gasit]);

    [Fact]
    public async Task DupaEmail()
    {
        (await Membri("?q=scoala.example")).Count.ShouldBe(5);
        (await Membri("?q=andrei%40scoala")).ShouldBe(["andrei"]);
    }

    [Fact]
    public async Task FaraTextSuntToti()
    {
        (await Membri()).ShouldBe(new[] { "andrei", "director", "irina", "mara", "radu.serban", "stefania", "tibi" }.Order());
        (await Membri("?q=%20%20")).Count.ShouldBe(7);
    }

    [Theory]
    [InlineData("?rol=elev", new[] { "andrei", "mara", "stefania", "tibi" })]
    [InlineData("?rol=profesor", new[] { "director", "irina", "radu.serban" })]
    [InlineData("?stare=in_asteptare", new[] { "andrei" })]
    [InlineData("?stare=expirat", new[] { "mara" })]
    [InlineData("?stare=respins", new[] { "tibi" })]
    [InlineData("?rol=elev&stare=aprobat", new[] { "stefania" })]
    [InlineData("?clasa=XI", new[] { "mara", "stefania" })]
    [InlineData("?clasa=XI&stare=aprobat", new[] { "stefania" })]
    [InlineData("?clasa=X", new string[0])]
    [InlineData("?materieId=1", new[] { "irina" })]
    [InlineData("?materieId=2", new[] { "radu.serban" })]
    [InlineData("?materieId=3", new string[0])]
    [InlineData("?q=a&rol=elev&clasa=XI", new[] { "mara", "stefania" })]
    [InlineData("?q=lungu&stare=aprobat", new string[0])]
    public async Task FiltreleSiCautareaImpreuna(string query, string[] gasiti) => (await Membri(query)).ShouldBe(gasiti.Order());

    [Fact]
    public async Task OClasaNecunoscutaEsteRefuzata()
    {
        await director.Get("/organizatia-mea/membri?clasa=XIII").Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnTextPreaLungEsteRefuzat()
    {
        await director.Get("/organizatia-mea/membri?q=" + new string('a', 101)).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AdminulAltreiOrganizatiiNuVedeMembriiAcesteia()
    {
        var (ovidiu, _) = await OrganizatieNoua(aprobata: true);
        var ai = (await ovidiu.GetJson("/organizatia-mea/membri?q=stefania")).Lista();
        ai.ShouldBeEmpty();
        (await ovidiu.GetJson("/organizatia-mea/membri")).Lista().Select(m => m.Str("username")).ShouldBe(["ovidiu"]);
    }

    [Fact]
    public async Task UnProfesorObisnuitNuAreListaOrganizatiei()
    {
        var irina = await h.Login("irina");
        await irina.Get("/organizatia-mea/membri?q=stefania").Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListaMembrilorArePagini()
    {
        await h.Db(async db =>
        {
            var rol = await db.Roluri.Where(r => r.Nume == "elev").Select(r => r.Id).SingleAsync();
            for (var i = 0; i < 60; i++)
            {
                var u = new Utilizator { Nume = $"Elev Mulți {i:00}", Username = $"multi{i:00}", Email = $"multi{i:00}@example.invalid", EmailConfirmatLa = DateTime.UtcNow, Clasa = "X" };
                u.ParolaHash = "x";
                u.Roluri.Add(new UtilizatorRol { RolId = rol });
                db.Add(new MembruOrganizatie { OrganizatieId = Liceu, Utilizator = u, Rol = MembruOrganizatie.RolElev, Stare = MembruOrganizatie.Aprobat, CerutLa = DateTime.UtcNow });
            }
            await db.SaveChangesAsync();
        });
        var p1 = await director.GetJson("/organizatia-mea/membri?rol=elev&clasa=X");
        (p1.Int("total"), p1.Lista().Count).ShouldBe((60, 50));
        var p2 = await director.GetJson("/organizatia-mea/membri?rol=elev&clasa=X&pagina=2");
        p2.Lista().Count.ShouldBe(10);
        p2.Lista().Select(m => m.Int("utilizatorId")).Intersect(p1.Lista().Select(m => m.Int("utilizatorId"))).ShouldBeEmpty();
        // "multi" and "5": 05, 15, 25, 35, 45 and 50–59.
        (await director.GetJson("/organizatia-mea/membri?q=multi%205&rol=elev")).Int("total").ShouldBe(15);
        await director.Get("/organizatia-mea/membri?pagina=0").Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnProfesorAlOrganizatieiVedeEleviiPePagini()
    {
        var irina = await h.Login("irina");
        var r = await irina.GetJson($"/organizatii/{Liceu}/elevi");
        r.Int("total").ShouldBe(r.Lista().Count);
    }
}

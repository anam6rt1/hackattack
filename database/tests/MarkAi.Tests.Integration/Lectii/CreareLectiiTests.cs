using System.Net;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Lectii;

/// <summary>Creating Lecții and chapters: subjects are the platform's; a home organisation of the author, a subject they teach, a valid grade.</summary>
public sealed class CreareLectiiTests : TestLectii
{
    [Fact]
    public async Task ProfesoriiNuCreeazaMaterii()
    {
        await irina.Post("/materii", new { denumire = "Filosofie" }).Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminulCreeazaSiRedenumesteMaterii()
    {
        var id = (await admin.Post("/materii", new { denumire = "Filosofie", cod = "filo" }).Json(HttpStatusCode.Created)).Int("id");
        (await admin.Put($"/materii/{id}", new { denumire = "Filozofie" }).Json()).Str("denumire").ShouldBe("Filozofie");
        await irina.Put($"/materii/{id}", new { denumire = "X" }).Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CodulMaterieiEsteScrisCuLitereMici()
    {
        (await admin.Post("/materii", new { denumire = "Filosofie", cod = "Filo" }).Json(HttpStatusCode.Created)).Str("cod").ShouldBe("filo");
    }

    [Theory]
    [InlineData("filo sofie")]
    [InlineData("filo_1")]
    public async Task CodulMaterieiCuAlteCaractereEsteRefuzat(string cod)
    {
        (await admin.Post("/materii", new { denumire = "Filosofie", cod }).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("litere mici");
    }

    [Fact]
    public async Task ProfesorulTrebuieSaAlegaOrganizatia()
    {
        (await irina.Post("/parcursuri", Lectie("Fără organizație", null)).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("Alege organizația");
    }

    [Fact]
    public async Task DoarOOrganizatieDinCareFaceParte()
    {
        (await petra.Post("/parcursuri", Lectie("Alt centru", Centru)).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("din care faci parte");
    }

    [Fact]
    public async Task DoarOMaterieCarePreda()
    {
        (await irina.Post("/parcursuri", Lectie("Mate", Liceu, materieId: 2)).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("materiile pe care le predai");
    }

    [Theory]
    [InlineData("VII")]
    [InlineData("xi")]
    [InlineData("")]
    public async Task ClasaTrebuieSaFieIxPanaLaXiiSauBac(string clasa)
    {
        (await irina.Post("/parcursuri", Lectie("Clasă greșită", Liceu, clasa: clasa)).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("IX, X, XI, XII sau Bac");
    }

    [Fact]
    public async Task VizibilitateaNecunoscutaEsteRefuzata()
    {
        await irina.Post("/parcursuri", Lectie("x", Liceu, vizibilitate: "secret")).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DoarOrganizatieCereOOrganizatie()
    {
        (await admin.Post("/parcursuri", Lectie("Doar org fără org", null, vizibilitate: "organizatie")).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("trebuie să aibă o organizație");
    }

    [Fact]
    public async Task AdminulPoateCreaLectiiDePlatforma()
    {
        var id = await Creeaza(admin, Lectie("Platformă", null));
        var l = await Editor(admin, id);
        l.EsteNull("organizatieId").ShouldBeTrue();
        l.Str("autor").ShouldBe("Administrator");
    }

    [Fact]
    public async Task AdminulPoateAlegeOriceOrganizatieAprobata()
    {
        await Creeaza(admin, Lectie("La centru", Centru));
        await admin.Post("/parcursuri", Lectie("Inexistentă", 999)).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ProfesorulCreeazaLectiaSiDevineAutor()
    {
        var id = await Creeaza(irina, Lectie("A Irinei", Liceu, clasa: "X"));
        var l = await Editor(irina, id);
        (l.Str("autor"), l.Int("organizatieId"), l.Str("clasa"), l.Str("vizibilitate")).ShouldBe(("Irina Marin", Liceu, "X", "public"));
        l.Bool("publicat").ShouldBeTrue();
    }

    [Fact]
    public async Task AdministratorulOrganizatieiPoateCreaInEa()
    {
        await Creeaza(ovidiu, Lectie("A lui Ovidiu", Centru));
    }

    [Fact]
    public async Task ElevulNuCreeazaLectii()
    {
        await george.Post("/parcursuri", Lectie("x", null)).Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ProfesorulNeaprobatNuCreeazaLectii()
    {
        await h.Db(db => db.ProfiluriMentor.Where(p => p.Utilizator.Username == "petra").ExecuteUpdateAsync(s => s.SetProperty(p => p.StareVerificare, "in_asteptare")));
        await petra.Post("/parcursuri", Lectie("x", Liceu)).Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OrganizatiilePosibileSuntAleAutorului()
    {
        var lista = (await irina.GetJson("/continut/organizatii")).Lista().Select(o => o.Int("id")).ToList();
        lista.Order().ShouldBe(new[] { Liceu, Centru }.Order());
        (await petra.GetJson("/continut/organizatii")).Lista().Select(o => o.Int("id")).ShouldBe([Liceu]);
    }

    [Fact]
    public async Task LectiaNuSeMutaLaAltaMaterie()
    {
        var id = await Creeaza(admin, Lectie("x", null));
        (await admin.Put($"/parcursuri/{id}", Lectie("x", null, materieId: 2)).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("altă materie");
    }

    [Fact]
    public async Task OrdineaImplicitaEsteLaSfarsit()
    {
        var a = await Creeaza(admin, Lectie("Prima", null));
        var b = await Creeaza(admin, Lectie("A doua", null));
        (await Editor(admin, b)).Int("ordine").ShouldBeGreaterThan((await Editor(admin, a)).Int("ordine"));
    }

    [Fact]
    public async Task CapitolulPrimesteMaterialeSiVideo()
    {
        var l = await Creeaza(admin, Lectie("x", null));
        var c = await admin.Post("/cursuri", new
        {
            parcursId = l, titlu = "Cap", capitol = "Romantismul", videoUrl = "https://www.youtube.com/watch?v=abc",
            materiale = new[] { new { titlu = "S1", text = "T1" }, new { titlu = "S2", text = "T2" } },
        }).Json(HttpStatusCode.Created);
        c.Str("videoUrl").ShouldStartWith("https://");
        c.Lista("materiale").Count.ShouldBe(2);
        c.Str("capitol").ShouldBe("Romantismul");
    }

    [Theory]
    [InlineData("http://exemplu.ro/video")]
    [InlineData("javascript:alert(1)")]
    [InlineData("nu e link")]
    public async Task VideoulTrebuieSaFieHttps(string videoUrl)
    {
        var l = await Creeaza(admin, Lectie("x", null));
        (await admin.Post("/cursuri", new { parcursId = l, titlu = "Cap", videoUrl }).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("https");
    }

    [Fact]
    public async Task SectiuneaFaraTextEsteRefuzata()
    {
        var l = await Creeaza(admin, Lectie("x", null));
        await admin.Post("/cursuri", new { parcursId = l, titlu = "Cap", materiale = new[] { new { titlu = "S1", text = "" } } }).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CapitolulPrimesteMateriaLectiei()
    {
        var l = await Creeaza(admin, Lectie("x", null, materieId: 3));
        var c = await Capitol(admin, l, "Celula");
        (await h.Db(db => db.Curs.Where(x => x.Id == c).Select(x => x.MaterieId).SingleAsync())).ShouldBe(3);
    }

    [Fact]
    public async Task CapitolulNuSeMutaLaAltaMaterie()
    {
        var a = await Creeaza(admin, Lectie("Română", null, materieId: 1));
        var b = await Creeaza(admin, Lectie("Bio", null, materieId: 3));
        var c = await Capitol(admin, a, "Cap");
        (await admin.Put($"/cursuri/{c}", new { parcursId = b, titlu = "Cap" }).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("aceeași materie");
    }

    [Fact]
    public async Task CapitolulSeMutaInAltaLectieAMateriei()
    {
        var a = await Creeaza(admin, Lectie("A", null));
        var b = await Creeaza(admin, Lectie("B", null));
        var c = await Capitol(admin, a, "Cap");
        await admin.Put($"/cursuri/{c}", new { parcursId = b, titlu = "Cap" }).Status(HttpStatusCode.OK);
        (await h.Db(db => db.Curs.Where(x => x.Id == c).Select(x => x.ParcursId).SingleAsync())).ShouldBe(b);
    }

    [Fact]
    public async Task CautareaEsteCompletataLaSalvare()
    {
        var l = await Creeaza(admin, Lectie("Șarpele și Țara", null));
        (await h.Db(db => db.Set<Parcurs>().Where(p => p.Id == l).Select(p => p.Cautare).SingleAsync())).ShouldContain("sarpele si tara");
    }
}

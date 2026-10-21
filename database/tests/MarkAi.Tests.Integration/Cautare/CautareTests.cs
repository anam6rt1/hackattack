using System.Net;

namespace MarkAi.Tests.Integration.Cautare;

/// <summary>Search: titles, descriptions and chapter labels, without diacritics, every word must match, within what the user may see.</summary>
public sealed class CautareTests : TestCautare
{
    private int sarpe, capSarpe;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        sarpe = await Creeaza(admin, Lectie("Șarpele și ţara", null, descriere: "Basme cu zmei"));
        capSarpe = await Capitol(admin, sarpe, "Capitol cu Ştefan", "Harap-Alb");
    }

    [Fact]
    public async Task FaraDiacriticeGasesteVirgulaDedesubt()
    {
        (await Cauta(george, "sarpe")).Lectii.ShouldContain(sarpe);
    }

    [Fact]
    public async Task MajusculeleSiSedilaSePotrivesc()
    {
        (await Cauta(george, "SARPELE TARA")).Lectii.ShouldContain(sarpe);
    }

    [Fact]
    public async Task CuDiacriticeGasesteLaFel()
    {
        (await Cauta(george, "Șarpele")).Lectii.ShouldContain(sarpe);
        (await Cauta(george, "şarpele")).Lectii.ShouldContain(sarpe);
    }

    [Fact]
    public async Task CapitolulSeGasesteDupaTitlu()
    {
        (await Cauta(george, "stefan")).Capitole.ShouldContain(capSarpe);
    }

    [Fact]
    public async Task EticheteleCapitoluluiSeCauta()
    {
        (await Cauta(george, "harap alb")).Capitole.ShouldContain(capSarpe);
        (await Cauta(george, "harap-alb")).Capitole.ShouldContain(capSarpe);
    }

    [Fact]
    public async Task DescriereaSeCauta()
    {
        (await Cauta(george, "zmei")).Lectii.ShouldContain(sarpe);
    }

    [Fact]
    public async Task FiecareCuvantTrebuieSaSePotriveasca()
    {
        (await Cauta(george, "sarpe inexistent")).Lectii.ShouldBeEmpty();
    }

    [Fact]
    public async Task OrdineaCuvintelorNuConteaza()
    {
        (await Cauta(george, "tara sarpele")).Lectii.ShouldContain(sarpe);
    }

    [Fact]
    public async Task OParteDinCuvantAjunge()
    {
        (await Cauta(george, "sarp")).Lectii.ShouldContain(sarpe);
    }

    [Theory]
    [InlineData("a")]
    [InlineData(" ")]
    [InlineData("")]
    public async Task CautareaPreaScurtaEsteRefuzata(string q)
    {
        (await george.Get($"/cautare?q={Uri.EscapeDataString(q)}").Mesaj(HttpStatusCode.BadRequest)).ShouldContain("două litere");
    }

    [Fact]
    public async Task CautareaPreaLungaEsteRefuzata()
    {
        await george.Get($"/cautare?q={new string('a', 101)}").Status(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("%%")]
    [InlineData("__")]
    [InlineData("\\\\")]
    public async Task CaractereleSpecialeSuntCautateLiteral(string q)
    {
        var r = await Cauta(george, q);
        r.Lectii.ShouldBeEmpty();
        r.Capitole.ShouldBeEmpty();
    }

    [Fact]
    public async Task CelDinAfaraNuPrimesteRezultateDinOrganizatie()
    {
        var ascunsa = await Creeaza(irina, Lectie("Șarpe ascuns", Liceu, vizibilitate: "organizatie", clasa: "XI"));
        var capAscuns = await Capitol(irina, ascunsa, "Șarpe secret");
        var r = await Cauta(george, "sarpe");
        r.Lectii.ShouldNotContain(ascunsa);
        r.Capitole.ShouldNotContain(capAscuns);
    }

    [Fact]
    public async Task MembrulOrganizatieiPrimesteRezultatele()
    {
        var ascunsa = await Creeaza(irina, Lectie("Șarpe ascuns", Liceu, vizibilitate: "organizatie", clasa: "XI"));
        var capAscuns = await Capitol(irina, ascunsa, "Șarpe secret");
        var r = await Cauta(elena, "sarpe");
        r.Lectii.ShouldContain(ascunsa);
        r.Capitole.ShouldContain(capAscuns);
    }

    [Fact]
    public async Task AnonimulNuCauta()
    {
        await h.Anonim().Get("/cautare?q=sarpe").Status(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LectiaNepublicataODoarEditorii()
    {
        var ciorna = await Creeaza(irina, Lectie("Șarpe nepublicat", Liceu, publicat: false));
        (await Cauta(elena, "sarpe")).Lectii.ShouldNotContain(ciorna);
        (await Cauta(irina, "sarpe")).Lectii.ShouldContain(ciorna);
    }

    [Fact]
    public async Task CapitolulNepublicatNuApare()
    {
        var c = (await admin.Post("/cursuri", new { parcursId = sarpe, titlu = "Șarpe nevăzut", publicat = false }).Json(HttpStatusCode.Created)).Int("id");
        (await Cauta(george, "nevazut")).Capitole.ShouldNotContain(c);
    }

    [Fact]
    public async Task FiltrulDeClasaSeAplica()
    {
        var ascunsa = await Creeaza(irina, Lectie("Șarpe ascuns", Liceu, vizibilitate: "organizatie", clasa: "XI"));
        (await Cauta(elena, "sarpe", "&clasa=XI")).Lectii.ShouldBe([ascunsa]);
    }

    [Fact]
    public async Task FiltrulDeMaterieSeAplica()
    {
        (await Cauta(elena, "sarpe", "&materieId=2")).Lectii.ShouldBeEmpty();
        (await Cauta(elena, "sarpe", "&materieId=1")).Lectii.ShouldContain(sarpe);
    }

    [Fact]
    public async Task FiltrulPublicLasaDeoparteLectiileOrganizatiei()
    {
        var ascunsa = await Creeaza(irina, Lectie("Șarpe ascuns", Liceu, vizibilitate: "organizatie"));
        (await Cauta(elena, "sarpe", "&filtru=public")).Lectii.ShouldNotContain(ascunsa);
    }

    [Fact]
    public async Task OrganizatiaMeaLasaDeoparteLectiileDePlatforma()
    {
        var ascunsa = await Creeaza(irina, Lectie("Șarpe ascuns", Liceu, vizibilitate: "organizatie"));
        var r = await Cauta(elena, "sarpe", "&filtru=organizatia-mea");
        r.Lectii.ShouldContain(ascunsa);
        r.Lectii.ShouldNotContain(sarpe);
    }

    [Fact]
    public async Task CapitolulGasitArataLectiaSiMateria()
    {
        var c = (await george.GetJson("/cautare?q=stefan")).Lista("capitole").Single();
        (c.Int("parcursId"), c.Str("parcurs"), c.Str("materie")).ShouldBe((sarpe, "Șarpele și ţara", "Limba și literatura română"));
    }

    [Fact]
    public async Task CelMultTreizeciDeRezultate()
    {
        for (var i = 0; i < 32; i++) await Creeaza(admin, Lectie($"Comun {i:D2}", null));
        (await Cauta(george, "comun")).Lectii.Count.ShouldBe(30);
    }

    [Fact]
    public async Task CelMultSaseCuvinteConteaza()
    {
        // Only the first six distinct words are used; the seventh does not have to match.
        (await Cauta(george, "sarpele si tara basme cu zmei cuvantlipsa")).Lectii.ShouldContain(sarpe);
    }
}

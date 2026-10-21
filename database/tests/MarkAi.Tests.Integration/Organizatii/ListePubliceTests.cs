using System.Net;

namespace MarkAi.Tests.Integration.Organizatii;

/// <summary>The public lists of the sign-up forms: counties, locality search (without diacritics), approved organisations.</summary>
public sealed class ListePubliceTests : TestOrganizatii
{
    [Fact]
    public async Task JudeteleSuntListateAlfabetic()
    {
        (await h.Anonim().GetJson("/judete")).Lista().Select(j => j.Str("nume")).ShouldBe(["Alba", "București"]);
    }

    [Fact]
    public async Task LocalitatileSeCautaDupaInceput()
    {
        (await h.Anonim().GetJson("/judete/40/localitati?q=sect")).Lista().Count.ShouldBe(2);
    }

    [Fact]
    public async Task LocalitatileSeCautaFaraDiacritice()
    {
        (await h.Anonim().GetJson("/judete/1/localitati?q=barab")).Lista()[0].Str("nume").ShouldBe("Bărăbanț");
    }

    [Fact]
    public async Task CuDiacriticeSeGasesteLaFel()
    {
        (await h.Anonim().GetJson("/judete/1/localitati?q=Bărăb")).Lista()[0].Str("nume").ShouldBe("Bărăbanț");
    }

    [Fact]
    public async Task FaraTextSuntToateLocalitatileJudetului()
    {
        (await h.Anonim().GetJson("/judete/1/localitati")).Lista().Count.ShouldBe(2);
    }

    [Fact]
    public async Task LocalitateaDupaId()
    {
        var l = await h.Anonim().GetJson("/localitati/1026");
        (l.Str("nume"), l.Int("judetId"), l.Str("uatNume")).ShouldBe(("Alba Iulia", 1, "Municipiul Alba Iulia"));
        await h.Anonim().Get("/localitati/1").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OrganizatiileAprobateSuntListate()
    {
        var o = (await h.Anonim().GetJson("/organizatii")).Lista().Single();
        (o.Str("nume"), o.Str("judet"), o.Str("localitate")).ShouldBe(("Liceul de Test", "București", "Sectorul 1"));
    }

    [Fact]
    public async Task OrganizatiileNeaprobateNuSuntListate()
    {
        await OrganizatieNoua();
        (await h.Anonim().GetJson("/organizatii")).Lista().ShouldNotContain(o => o.Str("nume") == "Centrul Alba");
    }

    [Fact]
    public async Task OrganizatiileSeFiltreazaDupaJudetSiNume()
    {
        await OrganizatieNoua(aprobata: true);
        (await h.Anonim().GetJson("/organizatii?judetId=1")).Lista().Single().Str("nume").ShouldBe("Centrul Alba");
        (await h.Anonim().GetJson("/organizatii?q=liceul")).Lista().Single().Str("nume").ShouldBe("Liceul de Test");
    }
}

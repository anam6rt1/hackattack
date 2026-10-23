using MarkAi.Api.Infrastructura;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;

namespace MarkAi.Tests.Unit;

/// <summary>Search without diacritics, the search terms, SIRUTA names, and the Lecții list filters (on in-memory data).</summary>
public sealed class CautareTests
{
    [Theory]
    [InlineData("Șarpele și Țara", "sarpele si tara")]
    [InlineData("Şarpele şi Ţara", "sarpele si tara")]
    [InlineData("Ăla, Âla, Îla", "ala, ala, ila")]
    [InlineData("Harap-Alb", "harap alb")]
    [InlineData("ÉLÈVE çà", "eleve ca")]
    [InlineData("deja simplu", "deja simplu")]
    public void NormalizareaScoateDiacriticele(string intrare, string asteptat) => CautareText.Normalizeaza(intrare).ShouldBe(asteptat);

    [Fact]
    public void SqlulFolosesteAceeasiTraducere()
    {
        var sql = CautareText.Sql("titlu");
        sql.ShouldStartWith("lower(translate(titlu, '");
        sql.ShouldContain("ȘŞȚŢ");
    }

    [Fact]
    public void TermeniiSuntCuvinteleCuProcent() => TermeniCautare.Din("Șarpele  ţara").ShouldBe(["%sarpele%", "%tara%"]);

    [Fact]
    public void TermeniiRepetatiSuntPastratiOData() => TermeniCautare.Din("ion ion ion").ShouldBe(["%ion%"]);

    [Fact]
    public void CelMultSaseTermeni() => TermeniCautare.Din("a1 b2 c3 d4 e5 f6 g7 h8").Length.ShouldBe(6);

    [Theory]
    [InlineData("%", "%\\%%")]
    [InlineData("_x", "%\\_x%")]
    [InlineData("a\\b", "%a\\\\b%")]
    public void CaractereleSpecialeSuntEscapate(string q, string asteptat) => TermeniCautare.Din(q + "zz").Single().ShouldBe(asteptat[..^1] + "zz%");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a")]
    [InlineData(" b ")]
    public void CautareaPreaScurtaEsteRefuzata(string? q) =>
        Should.Throw<CerereInvalida>(() => TermeniCautare.Din(q)).Message.ShouldContain("două litere");

    [Fact]
    public void CautareaPreaLungaEsteRefuzata() => Should.Throw<CerereInvalida>(() => TermeniCautare.Din(new string('a', 101)));

    [Theory]
    [InlineData("Bărăbanț", "barabant")]
    [InlineData("Sânmartin-Cluj", "sanmartin cluj")]
    public void SirutaPentruCautare(string intrare, string asteptat) => ImportSiruta.PentruCautare(intrare).ShouldBe(asteptat);

    [Theory]
    [InlineData("ALBA IULIA", "Alba Iulia")]
    [InlineData("SÂNMARTIN DE BEIUŞ", "Sânmartin de Beiuș")]
    [InlineData("SECTORUL 1", "Sectorul 1")]
    [InlineData("TÂRGU-JIU", "Târgu-Jiu")]
    public void SirutaTitlu(string intrare, string asteptat) => ImportSiruta.Titlu(intrare).ShouldBe(asteptat);

    private static Parcurs P(int id, string clasa = "Bac", string viz = Parcurs.Public, int? org = null, int? judetOrg = null, params int[] partajari) => new()
    {
        Id = id, Clasa = clasa, Vizibilitate = viz, OrganizatieId = org, Publicat = true,
        Organizatie = org is null ? null : new Organizatie { Id = org.Value, JudetId = judetOrg ?? 40 },
        Partajari = partajari.Select(o => new ParcursOrganizatie { OrganizatieId = o }).ToList(),
    };

    private static readonly Parcurs[] Lectii =
    [
        P(1), P(2, "XI", Parcurs.DoarOrganizatie, org: 1, judetOrg: 40), P(3, "XII", org: 2, judetOrg: 1), P(4, "IX", Parcurs.DoarOrganizatie, org: 2, judetOrg: 1, 1),
    ];

    private static List<int> Filtreaza(string? filtru, string? clasa, int[]? org = null, int? judet = 40) =>
        FiltreLectii.Aplica(Lectii.AsQueryable(), new ContextAcces(5, false, org ?? [1], []), new LocUtilizator(judet, null), filtru, clasa).Select(p => p.Id).ToList();

    [Fact]
    public void FaraFiltruSuntToate() => Filtreaza(null, null).ShouldBe([1, 2, 3, 4]);

    [Fact]
    public void ToateEsteLaFelCaFaraFiltru() => Filtreaza("toate", "").ShouldBe([1, 2, 3, 4]);

    [Fact]
    public void FiltrulDeClasa() => Filtreaza(null, "XI,XII").ShouldBe([2, 3]);

    [Fact]
    public void ClasaNecunoscutaEsteRefuzata() => Should.Throw<CerereInvalida>(() => Filtreaza(null, "XIII"));

    [Fact]
    public void OrganizatiaMeaIncludePartajarile() => Filtreaza("organizatia-mea", null).ShouldBe([2, 4]);

    [Fact]
    public void FiltrulPublic() => Filtreaza("public", null).ShouldBe([1, 3]);

    [Fact]
    public void FiltrulLocalFolosesteJudetulOrganizatiei() => Filtreaza("local", null, judet: 1).ShouldBe([3, 4]);

    [Fact]
    public void FiltrulLocalCereJudetul() =>
        Should.Throw<CerereInvalida>(() => Filtreaza("local", null, judet: null)).Message.ShouldContain("Setări");

    [Fact]
    public void FiltrulNecunoscutEsteRefuzat() => Should.Throw<CerereInvalida>(() => Filtreaza("altceva", null)).Message.ShouldBe(FiltreLectii.Mesaj);
}

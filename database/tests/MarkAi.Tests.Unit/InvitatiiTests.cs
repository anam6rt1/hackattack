using MarkAi.Api.Servicii;
using MarkAi.Api.Infrastructura;

namespace MarkAi.Tests.Unit;
public class InvitatiiTests
{
    [Fact]
    public void CsvAcceptaBOMSiVirgulaInNume()
    {
        var r = Invitatii.Citeste("\uFEFFnume,email,clasa\r\n\"Popescu, Ana\",ANA@example.invalid,VIII").Single();
        r.Nume.ShouldBe("Popescu, Ana"); r.Email.ShouldBe("ana@example.invalid"); r.Eroare.ShouldBeNull();
    }
    [Fact]
    public void CsvAreLimitaDeO100DeElevi() => Should.Throw<CerereInvalida>(() => Invitatii.Citeste("nume,email,clasa\n" + string.Join('\n', Enumerable.Range(1, 101).Select(i => $"Elev{i},e{i}@example.invalid,IX"))));
    [Theory]
    [InlineData("nume,email,clasa\n\"Nume,a@example.invalid,IX")]
    [InlineData("Nume;Email;Clasa\nAna;a@example.invalid;IX")]
    [InlineData("nume,email,clasa\n")]
    public void CsvGresitEsteRespins(string csv) => Should.Throw<CerereInvalida>(() => Invitatii.Citeste(csv));
    [Fact]
    public void TokenurileAuEntropieSiHashDistinct()
    {
        var a = Invitatii.Token(); a.Length.ShouldBe(64); Invitatii.Hash(a).ShouldNotBe(a); Invitatii.Token().ShouldNotBe(a);
        Should.Throw<CerereInvalida>(() => Invitatii.Hash("invalid"));
    }
}

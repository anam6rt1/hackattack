using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MarkAi.Api.Infrastructura;
using MarkAi.Api.Servicii.Mesagerie;
using MarkAi.Api.Setari;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Tokens;

namespace MarkAi.Tests.Unit;

/// <summary>Roles, password hashes, session tokens and the emailed one-time codes for account changes.</summary>
public sealed class ConturiTests
{
    [Theory]
    [InlineData(new[] { "elev" }, "elev")]
    [InlineData(new[] { "profesor" }, "profesor")]
    [InlineData(new[] { "profesor", "organizatie" }, "organizatie")]
    [InlineData(new[] { "elev", "admin" }, "admin")]
    [InlineData(new[] { "organizatie", "admin", "profesor" }, "admin")]
    [InlineData(new string[0], "elev")]
    public void RolulPrincipalEsteCelMaiInalt(string[] roluri, string asteptat) => Roluri.Principal(roluri).ShouldBe(asteptat);

    [Theory]
    [InlineData("profesor", true)]
    [InlineData("organizatie", true)]
    [InlineData("elev", false)]
    [InlineData("admin", false)]
    public void CineEsteProfesor(string rol, bool asteptat) => Roluri.EsteProfesor(rol).ShouldBe(asteptat);

    [Fact]
    public void ParolaSeVerifica()
    {
        var u = new Utilizator();
        u.ParolaHash = Parole.Hash(u, "Parola-buna-2026!");
        Parole.Verifica(u, "Parola-buna-2026!").ShouldBeTrue();
        Parole.Verifica(u, "parola-buna-2026!").ShouldBeFalse();
    }

    [Fact]
    public void HashulEsteDiferitDeFiecareData()
    {
        var u = new Utilizator();
        Parole.Hash(u, "x").ShouldNotBe(Parole.Hash(u, "x"));
    }

    [Fact]
    public void FaraHashNimicNuSeVerifica() => Parole.Verifica(new Utilizator { ParolaHash = "" }, "").ShouldBeFalse();

    private static OptiuniJwt Optiuni(string cheie = "o-cheie-de-test-suficient-de-lunga-123") =>
        ConfigurareAutentificare.Optiuni(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Autentificare:CheieJwt"] = cheie }).Build(),
            new Mediu(Environments.Production));

    [Fact]
    public void TokenulAreIdulRolulSiVersiunea()
    {
        var o = Optiuni();
        var token = new Tokenuri(o).Emite(new Utilizator { Id = 42, Nume = "Ana Pop", VersiuneSesiune = 3 }, "elev");
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.Single(c => c.Type == ClaimTypes.NameIdentifier).Value.ShouldBe("42");
        jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value.ShouldBe("elev");
        jwt.Claims.Single(c => c.Type == OptiuniJwt.ClaimVersiune).Value.ShouldBe("3");
        jwt.Issuer.ShouldBe(OptiuniJwt.Emitent);
    }

    [Fact]
    public void TokenulExpiraDupaDurata()
    {
        var token = new Tokenuri(Optiuni()).Emite(new Utilizator { Id = 1, Nume = "A" }, "elev");
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        (jwt.ValidTo - DateTime.UtcNow).TotalDays.ShouldBeInRange(13.9, 14.1);
    }

    [Fact]
    public void TokenulSemnatCuAltaCheieEsteRefuzat()
    {
        var token = new Tokenuri(Optiuni("alta-cheie-de-test-suficient-de-lunga-9")).Emite(new Utilizator { Id = 1, Nume = "A" }, "elev");
        Should.Throw<SecurityTokenException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(token, Tokenuri.Validare(Optiuni().Cheie), out _));
    }

    [Fact]
    public void CheiaScurtaEsteRefuzata() =>
        Should.Throw<InvalidOperationException>(() => Optiuni("scurta")).Message.ShouldContain("32");

    [Fact]
    public void InProductieCheiaEsteObligatorie() =>
        Should.Throw<InvalidOperationException>(() => ConfigurareAutentificare.Optiuni(new ConfigurationBuilder().Build(), new Mediu(Environments.Production)));

    [Fact]
    public void InDezvoltareSeGenereazaOCheie() =>
        ConfigurareAutentificare.Optiuni(new ConfigurationBuilder().Build(), new Mediu(Environments.Development)).Cheie.Key.Length.ShouldBeGreaterThanOrEqualTo(32);

    [Fact]
    public void CookieulEsteHttpOnlySiLax()
    {
        var ctx = new DefaultHttpContext();
        new Tokenuri(Optiuni()).SeteazaCookie(ctx.Response, "abc");
        var cookie = ctx.Response.Headers.SetCookie.ToString();
        cookie.ShouldContain("markai_sesiune=abc");
        cookie.ShouldContain("httponly");
        cookie.ShouldContain("samesite=lax");
    }

    private static (ConfirmariCont Confirmari, FakeTimeProvider Ceas) Confirmari()
    {
        var ceas = new FakeTimeProvider(DateTimeOffset.UtcNow);
        return (new ConfirmariCont(ceas), ceas);
    }

    private static readonly ModificareConfirmata Date = new("parola", "hash-nou", "hash-vechi", "a@example.invalid");

    [Fact]
    public void CodulBunEsteConsumatOData()
    {
        var (c, _) = Confirmari();
        var (id, cod) = c.Creeaza(1, Date);
        c.Consuma(1, id, cod).ShouldBe(Date);
        Should.Throw<CerereInvalida>(() => c.Consuma(1, id, cod));
    }

    [Fact]
    public void CodulAreSaseCifre()
    {
        var (c, _) = Confirmari();
        c.Creeaza(1, Date).Cod.ShouldMatch(@"^\d{6}$");
    }

    [Fact]
    public void CodulEsteAlContului()
    {
        var (c, _) = Confirmari();
        var (id, cod) = c.Creeaza(1, Date);
        Should.Throw<CerereInvalida>(() => c.Consuma(2, id, cod));
    }

    [Fact]
    public void IdulGresitEsteRefuzat()
    {
        var (c, _) = Confirmari();
        var (_, cod) = c.Creeaza(1, Date);
        Should.Throw<CerereInvalida>(() => c.Consuma(1, "alt-id", cod)).Message.ShouldContain("nu este corect");
    }

    [Fact]
    public void CodulExpiraDupaCinciMinute()
    {
        var (c, ceas) = Confirmari();
        var (id, cod) = c.Creeaza(1, Date);
        ceas.Advance(TimeSpan.FromMinutes(5.1));
        Should.Throw<CerereInvalida>(() => c.Consuma(1, id, cod)).Message.ShouldContain("expirat");
    }

    [Fact]
    public void DupaCinciGreseliCodulEsteAnulat()
    {
        var (c, _) = Confirmari();
        var (id, cod) = c.Creeaza(1, Date);
        for (var i = 0; i < 5; i++) Should.Throw<CerereInvalida>(() => c.Consuma(1, id, "000000x"));
        Should.Throw<CerereInvalida>(() => c.Consuma(1, id, cod)).Message.ShouldContain("Prea multe");
    }

    [Fact]
    public void UnCodNouIlInlocuiestePeCelVechi()
    {
        var (c, _) = Confirmari();
        var (id1, cod1) = c.Creeaza(1, Date);
        c.Creeaza(1, Date);
        Should.Throw<CerereInvalida>(() => c.Consuma(1, id1, cod1));
    }

    [Fact]
    public void CodulAnulatNuMaiMerge()
    {
        var (c, _) = Confirmari();
        var (id, cod) = c.Creeaza(1, Date);
        c.Anuleaza(1);
        Should.Throw<CerereInvalida>(() => c.Consuma(1, id, cod));
    }

    [Fact]
    public void OCerereNouaInUnMinutEsteRefuzata()
    {
        var (c, ceas) = Confirmari();
        c.Limiteaza(1);
        Should.Throw<CerereInvalida>(() => c.Limiteaza(1)).Message.ShouldContain("un minut");
        Should.NotThrow(() => c.Limiteaza(2));
        ceas.Advance(TimeSpan.FromSeconds(61));
        Should.NotThrow(() => c.Limiteaza(1));
    }

    [Fact]
    public void CheiaRecompenseiEsteUnicaPeLectieSiElev()
    {
        ProgresService.CheieRecompensaLectie(5, 9).ShouldBe("lectie:5:utilizator:9");
        ProgresService.CheieRecompensaLectie(5, 9).ShouldNotBe(ProgresService.CheieRecompensaLectie(9, 5));
    }

    private sealed class Mediu(string nume) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = nume;
        public string ApplicationName { get; set; } = "teste";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}

using MarkAi.Api.Endpoints;
using MarkAi.Api.Infrastructura;
using MarkAi.Api.Servicii;
using MarkAi.Api.Servicii.Mesagerie;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace MarkAi.Tests.Unit;

/// <summary>Email and SMS templates, the configuration checks at startup, Twilio limits, and the tutor's voices.</summary>
public sealed class MesagerieTests
{
    [Fact]
    public void ConfirmareaContuluiAreCodulSiPrenumele()
    {
        var m = Sabloane.ConfirmareCont("ana@example.invalid", "Ana Maria Pop", "123456", 24);
        m.Catre.ShouldBe("ana@example.invalid");
        m.Subiect.ShouldStartWith("TeleBac");
        m.Text.ShouldContain("Bun venit, Ana!");
        m.Text.ShouldContain("123456");
        m.Html.ShouldContain("123456");
        m.Text.ShouldContain("24 de ore");
    }

    [Fact]
    public void NumeleSuntEscapateInHtml()
    {
        var m = Sabloane.ConfirmareCont("x@example.invalid", "<script>alert(1)</script>", "123456", 24);
        m.Html.ShouldNotContain("<script>");
        m.Html.ShouldContain("&lt;script&gt;");
    }

    [Fact]
    public void NotificareaFaraCodNuAreCasetaDeCod()
    {
        var m = Sabloane.Notificare("x@example.invalid", "Subiect", "Titlu", "Text");
        m.Subiect.ShouldBe("TeleBac · Subiect");
        m.Html.ShouldNotContain("letter-spacing:8px");
        m.Text.ShouldContain("Detaliile sunt în aplicație");
    }

    [Fact]
    public void ResetareaParoleiSpuneCandExpira()
    {
        var m = Sabloane.ResetareParola("x@example.invalid", "Ion", "654321");
        m.Text.ShouldContain("15 minute");
        m.Text.ShouldContain("654321");
    }

    [Fact]
    public void SmsurileSuntScurteSiAuCodul()
    {
        var t = Sabloane.CodTelefon("+40722111222", "111222");
        t.Text.ShouldContain("111222");
        t.Text.Length.ShouldBeLessThan(160);
        Sabloane.CodParola("+40722111222", "333444").Text.Length.ShouldBeLessThan(160);
    }

    [Theory]
    [InlineData("Ion")]
    [InlineData("  Ion   Popescu ")]
    public void PrenumeleEstePrimulCuvant(string nume) => Sabloane.AvertizareStergere("x@example.invalid", nume, "1").Text.ShouldStartWith("Ion, contul");

    [Fact]
    public void ToateSabloaneleAuTextSimplu()
    {
        Email[] toate =
        [
            Sabloane.ConfirmareCont("a@b.c", "A", "1", 24), Sabloane.AvertizareStergere("a@b.c", "A", "1"), Sabloane.ContSters("a@b.c", "A", "motiv"),
            Sabloane.IncercareInregistrare("a@b.c", "A"), Sabloane.ResetareParola("a@b.c", "A", "1"), Sabloane.ParolaSchimbata("a@b.c", "A"),
        ];
        toate.ShouldAllBe(m => m.Text.Length > 20 && m.Html.Contains("TeleBac") && !m.Text.Contains('<'));
    }

    private static IConfiguration Cfg(params (string, string?)[] valori) =>
        new ConfigurationBuilder().AddInMemoryCollection(valori.ToDictionary(v => v.Item1, v => v.Item2)).Build();

    [Fact]
    public void FaraConexiuneLipsesteSetarea() =>
        ConfigurareObligatorie.Lipsa(Cfg(), new Mediu(Environments.Development), "").ShouldBe(["MARKAI_CONNECTION_STRING"]);

    [Fact]
    public void InProductieCheiaJwtEsteObligatorie() =>
        ConfigurareObligatorie.Lipsa(Cfg(), new Mediu(Environments.Production), "Host=x").ShouldContain("MARKAI_JWT_KEY");

    [Fact]
    public void VerificareaNumesteToateSetarileLipsa() =>
        Should.Throw<InvalidOperationException>(() => ConfigurareObligatorie.Verifica(Cfg(), new Mediu(Environments.Production), ""))
            .Message.ShouldContain("MARKAI_CONNECTION_STRING, MARKAI_JWT_KEY");

    [Fact]
    public void SmtpPartialDaAvertisment() =>
        ConfigurareObligatorie.Avertismente(Cfg(("SetariEmail:Host", "smtp.example"))).ShouldHaveSingleItem().ShouldContain("SetariEmail__Expeditor");

    [Fact]
    public void SmtpCompletNuDaAvertisment() =>
        ConfigurareObligatorie.Avertismente(Cfg(("SetariEmail:Host", "smtp.example"), ("SetariEmail:Expeditor", "a@b.c"))).ShouldBeEmpty();

    [Fact]
    public void PeTrialApelulAreZecePasiSiPatruSecunde()
    {
        var l = LimiteApel.Din(Cfg());
        (l.Pasi, l.Durata, l.RaspunsMaxim).ShouldBe((10, TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(4)));
    }

    [Fact]
    public void PeContulPlatitApelulEsteFaraLimitaDePasi()
    {
        var l = LimiteApel.Din(Cfg(("Twilio:Trial", "false")));
        (l.Pasi, l.Durata, l.RaspunsMaxim).ShouldBe((0, TimeSpan.FromHours(4), TimeSpan.FromSeconds(12)));
    }

    [Fact]
    public void ParametriiApeluluiSuntEscapati() =>
        LiniaAiEndpoints.ParametriApel(7, "a+b/c=").ShouldBe("s=7&k=a%2Bb%2Fc%3D");

    [Fact]
    public void VoceaNecunoscutaDevineImplicita() => VociTutor.Gaseste("robot").Cod.ShouldBe("ioana");

    [Theory]
    [InlineData(50, -40, 70, -20)]
    [InlineData(200, 40, 130, 20)]
    [InlineData(100, 0, 100, 0)]
    public void VitezaSiTonulSuntLimitate(int viteza, int ton, int vitezaAsteptata, int tonAsteptat)
    {
        var v = new SetariVoce("maria", viteza, ton).Normalizat();
        (v.Voce, v.Viteza, v.Ton).ShouldBe(("maria", vitezaAsteptata, tonAsteptat));
    }

    [Fact]
    public void ToateVocileSuntRomanesti() => VociTutor.Toate.ShouldAllBe(v => v.Twilio.Contains("ro-RO") || v.Twilio.StartsWith("Polly."));

    private sealed class Mediu(string nume) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = nume;
        public string ApplicationName { get; set; } = "teste";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}

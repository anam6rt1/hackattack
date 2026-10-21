using System.Net;
using MarkAi.Api.Servicii.Triviador;

namespace MarkAi.Tests.Integration.Triviador;

/// <summary>Rooms are validated and created by the server; the lobby: host, seats, bots, who may start.</summary>
public sealed class CamereTriviadorTests : TestTriviador
{
    [Fact]
    public async Task PachetulCuRaspunsuriNuMaiExista()
    {
        var george = await Conecteaza("george");
        (await george.Http.Get("/trivia/intrebari?materieId=1")).StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }

    [Theory]
    [InlineData(8, 2)]
    [InlineData(31, 2)]
    [InlineData(9, 1)]
    [InlineData(9, 5)]
    public async Task InsuleleSiJucatoriiSuntValidati(int insule, int maxJucatori)
    {
        var george = await Conecteaza("george");
        await george.Http.Post("/trivia/camere", new { materieIds = new[] { 1 }, insule, maxJucatori }).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MateriileSuntValidate()
    {
        var george = await Conecteaza("george");
        await george.Http.Post("/trivia/camere", new { materieIds = Array.Empty<int>(), insule = 9, maxJucatori = 2 }).Status(HttpStatusCode.BadRequest);
        await george.Http.Post("/trivia/camere", new { materieIds = Enumerable.Range(1, 11).ToArray(), insule = 9, maxJucatori = 2 }).Status(HttpStatusCode.BadRequest);
        (await george.Http.Post("/trivia/camere", new { materieIds = new[] { 99 }, insule = 9, maxJucatori = 2 }).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("inexistentă");
    }

    [Fact]
    public async Task CodulCameriiEsteFacutDeServer()
    {
        var cod = await Creeaza(await Conecteaza("george"));
        cod.Length.ShouldBe(6);
        cod.ShouldMatch("^[A-HJ-NP-Z2-9]{6}$");
    }

    [Fact]
    public async Task DoarEleviiCreeazaCamere()
    {
        using var irina = await h.Login("irina");
        await irina.Post("/trivia/camere", new { materieIds = new[] { 1 }, insule = 9, maxJucatori = 2 }).Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CelMultTreiCamereDeschisePeGazda()
    {
        var george = await Conecteaza("george");
        for (var i = 0; i < MotorTriviador.MaxCamerePeGazda; i++) await Creeaza(george);
        (await george.Http.Post("/trivia/camere", new { materieIds = new[] { 1 }, insule = 9, maxJucatori = 2 }).Mesaj(HttpStatusCode.Conflict)).ShouldContain("3 camere");
    }

    [Fact]
    public async Task CreatorulEsteGazda()
    {
        var george = await Conecteaza("george");
        var s = await george.Intra(await Creeaza(george));
        s.Str("faza").ShouldBe("lobby");
        s.Str("gazdaId").ShouldBe(george.Id);
    }

    [Fact]
    public async Task CodulSeAcceptaCuLitereMiciSiSpatii()
    {
        var george = await Conecteaza("george");
        var cod = await Creeaza(george);
        (await george.Intra(" " + cod.ToLowerInvariant() + " ")).Str("cod").ShouldBe(cod);
    }

    [Fact]
    public async Task CameraInexistentaEsteRefuzata()
    {
        var george = await Conecteaza("george");
        await Refuzat(() => george.Intra("ZZZZZZ"));
    }

    [Fact]
    public async Task InsuleleAuNumeleLectiilor()
    {
        var george = await Conecteaza("george");
        var s = await george.Intra(await Creeaza(george));
        s.GetProperty("harta").GetProperty("insule").EnumerateArray().ShouldContain(i => i.Str("nume") == "Romantismul");
    }

    [Fact]
    public async Task HartaAreNumarulCerutDeInsule()
    {
        var george = await Conecteaza("george");
        var s = await george.Intra(await Creeaza(george, insule: 17));
        s.GetProperty("harta").GetProperty("insule").GetArrayLength().ShouldBe(17);
    }

    [Fact]
    public async Task AlDoileaJucatorSeAseaza()
    {
        var george = await Conecteaza("george");
        var maria = await Conecteaza("maria");
        var cod = await Creeaza(george);
        await george.Intra(cod);
        await maria.Intra(cod);
        var s = await george.Asteapta(x => x.GetProperty("jucatori").GetArrayLength() == 2, "second player seated");
        s.GetProperty("jucatori")[1].Str("nume").ShouldBe("maria Test");
    }

    [Fact]
    public async Task DoarGazdaPorneste()
    {
        var george = await Conecteaza("george");
        var maria = await Conecteaza("maria");
        var cod = await Creeaza(george);
        await george.Intra(cod);
        await maria.Intra(cod);
        await Refuzat(() => maria.Hub.InvokeAsync("Porneste"));
    }

    [Fact]
    public async Task PornireaCereDoiJucatori()
    {
        var george = await Conecteaza("george");
        await george.Intra(await Creeaza(george));
        await Refuzat(() => george.Hub.InvokeAsync("Porneste"));
    }

    [Fact]
    public async Task ClientiiNuMaiPotTrimiteStarea()
    {
        var george = await Conecteaza("george");
        await george.Intra(await Creeaza(george));
        await Refuzat(() => george.Hub.InvokeAsync("Trimite", "stare", new { faza = "final" }));
    }

    [Fact]
    public async Task FaraCameraIntentiileSuntRefuzate()
    {
        var george = await Conecteaza("george");
        await Refuzat(() => george.Hub.InvokeAsync("Ataca", "i0"));
        await Refuzat(() => george.Hub.InvokeAsync("Porneste"));
    }

    [Fact]
    public async Task NivelulNecunoscutDeBotEsteRefuzat()
    {
        var george = await Conecteaza("george");
        await george.Intra(await Creeaza(george));
        await Refuzat(() => george.Hub.InvokeAsync("AdaugaBot", "imposibil"));
    }

    [Fact]
    public async Task BotulOcupaUnLoc()
    {
        var george = await Conecteaza("george");
        await george.Intra(await Creeaza(george));
        await george.Hub.InvokeAsync("AdaugaBot", "greu");
        var s = await george.Asteapta(x => x.GetProperty("jucatori").GetArrayLength() == 2, "bot seated");
        s.GetProperty("jucatori")[1].GetProperty("bot").GetBoolean().ShouldBeTrue();
        s.GetProperty("cuBoti").GetBoolean().ShouldBeTrue();
        await Refuzat(() => george.Hub.InvokeAsync("AdaugaBot", "usor"));
    }

    [Fact]
    public async Task BotulSePoateScoate()
    {
        var george = await Conecteaza("george");
        await george.Intra(await Creeaza(george));
        await george.Hub.InvokeAsync("AdaugaBot", "mediu");
        await george.Asteapta(x => x.GetProperty("jucatori").GetArrayLength() == 2, "bot seated");
        await george.Hub.InvokeAsync("ScoateBot", "bot1");
        await george.Asteapta(x => x.GetProperty("jucatori").GetArrayLength() == 1, "bot removed");
    }

    [Fact]
    public async Task DoarGazdaAdaugaBoti()
    {
        var george = await Conecteaza("george");
        var maria = await Conecteaza("maria");
        var cod = await Creeaza(george, maxJucatori: 3);
        await george.Intra(cod);
        await maria.Intra(cod);
        await Refuzat(() => maria.Hub.InvokeAsync("AdaugaBot", "usor"));
    }

    [Fact]
    public async Task LobbyulArataCetatileViitoare()
    {
        var george = await Conecteaza("george");
        await george.Intra(await Creeaza(george));
        await george.Hub.InvokeAsync("AdaugaBot", "usor");
        var s = await george.Asteapta(x => x.GetProperty("jucatori").GetArrayLength() == 2, "bot seated");
        s.GetProperty("proprietari").EnumerateObject().Count().ShouldBe(2);
    }

    [Fact]
    public async Task ClasaFiltreazaInsulele()
    {
        var george = await Conecteaza("george");
        var cod = await Creeaza(george, clasa: "XI");
        Motor.Descriere(cod)!.Value.Insule.ShouldNotContain("Romantismul");
        Motor.Descriere(cod)!.Value.Materie.ShouldEndWith("clasa a XI-a");
    }

    [Fact]
    public async Task ClasaNecunoscutaEsteRefuzata()
    {
        var george = await Conecteaza("george");
        await george.Http.Post("/trivia/camere", new { materieIds = new[] { 1 }, insule = 9, maxJucatori = 2, clasa = "XIII" }).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CameraInCareNuIntraNimeniSeInchide()
    {
        var george = await Conecteaza("george");
        var cod = await Creeaza(george);
        Avanseaza(MotorTriviador.TimpInchidere + TimeSpan.FromSeconds(1));
        for (var i = 0; i < 20 && Motor.Exista(cod); i++) await Task.Delay(50);
        Motor.Exista(cod).ShouldBeFalse();
    }
}

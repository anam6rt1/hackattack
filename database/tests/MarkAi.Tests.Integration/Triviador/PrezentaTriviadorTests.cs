using MarkAi.Api.Servicii.Triviador;

namespace MarkAi.Tests.Integration.Triviador;

/// <summary>Seats and spectators, the grace period after a disconnect, a new host when the host leaves, wiping an empty room.</summary>
public sealed class PrezentaTriviadorTests : TestTriviador
{
    private static readonly TimeSpan DupaGratie = MotorTriviador.GratiePlecare + TimeSpan.FromMilliseconds(100);

    // The server handles a disconnect a moment after StopAsync returns and only then starts the grace timer on the fake
    // clock, so the clock is moved until the condition holds (known race, see the plan's flaky-test rule).
    private async Task AvanseazaPanaCand(Func<bool> conditie)
    {
        for (var i = 0; i < 20 && !conditie(); i++)
        {
            Avanseaza(DupaGratie);
            await Task.Delay(150);
        }
    }

    private async Task<(Jucator Maria, Jucator George, Jucator Ion, string Cod)> CameraPlina()
    {
        var maria = await Conecteaza("maria");
        var george = await Conecteaza("george");
        var ion = await Conecteaza("ion");
        var cod = await Creeaza(maria, maxJucatori: 2);
        await maria.Intra(cod);
        await george.Intra(cod);
        await ion.Intra(cod);
        await maria.Asteapta(x => x.GetProperty("spectatori").GetArrayLength() == 1, "third person watches");
        return (maria, george, ion, cod);
    }

    [Fact]
    public async Task PesteLocuriSuntSpectatori()
    {
        var (maria, _, ion, _) = await CameraPlina();
        maria.Stare.GetProperty("spectatori")[0].Str("id").ShouldBe(ion.Id);
    }

    [Fact]
    public async Task PlecareaSeVedeImediat()
    {
        var (maria, george, _, _) = await CameraPlina();
        await maria.Hub.StopAsync();
        var s = await george.Asteapta(x => x.GetProperty("jucatori").EnumerateArray().All(j => j.Str("id") != maria.Id), "leaving shows at once");
        s.Str("gazdaId").ShouldBe(maria.Id);
    }

    [Fact]
    public async Task DupaGratieGazdaTreceLaUrmatorul()
    {
        var (maria, george, _, _) = await CameraPlina();
        await maria.Hub.StopAsync();
        await george.Asteapta(x => x.GetProperty("jucatori").EnumerateArray().All(j => j.Str("id") != maria.Id), "leaving shows");
        await AvanseazaPanaCand(() => george.Stare.Str("gazdaId") == george.Id);
        var s = await george.Asteapta(x => x.Str("gazdaId") == george.Id, "host handed over");
        s.GetProperty("jurnal").EnumerateArray().ShouldContain(r => r.Str("text").Contains("este acum gazda"));
    }

    [Fact]
    public async Task SpectatorulIaLoculLiber()
    {
        var (maria, george, ion, _) = await CameraPlina();
        await maria.Hub.StopAsync();
        await AvanseazaPanaCand(() => george.Stare.GetProperty("spectatori").GetArrayLength() == 0);
        var s = await george.Asteapta(x => x.GetProperty("jucatori").GetArrayLength() == 2 && x.GetProperty("spectatori").GetArrayLength() == 0, "spectator seated");
        s.GetProperty("jucatori").EnumerateArray().ShouldContain(j => j.Str("id") == ion.Id);
    }

    [Fact]
    public async Task ReincarcareaInGratieNuSchimbaNimic()
    {
        var george = await Conecteaza("george");
        var maria = await Conecteaza("maria");
        var cod = await Creeaza(george);
        await george.Intra(cod);
        await maria.Intra(cod);
        await george.Hub.StopAsync();
        await using var dinNou = await Jucator.Conecteaza(h, "george");
        await dinNou.Intra(cod);
        Avanseaza(DupaGratie);
        var s = await dinNou.Neschimbat();
        s.Str("gazdaId").ShouldBe(george.Id);
        s.GetProperty("jurnal").EnumerateArray().ShouldNotContain(r => r.Str("text").Contains("a ieșit"));
    }

    [Fact]
    public async Task CameraFaraOameniEsteStearsa()
    {
        var ion = await Conecteaza("ion");
        var george = await Conecteaza("george");
        var cod = await Creeaza(ion);
        await ion.Intra(cod);
        await ion.Hub.InvokeAsync("AdaugaBot", "mediu");
        await ion.Asteapta(x => x.GetProperty("jucatori").GetArrayLength() == 2, "host and bot");
        await ion.Hub.StopAsync();
        await AvanseazaPanaCand(() => !Motor.Exista(cod));
        Motor.Exista(cod).ShouldBeFalse();
        await Refuzat(() => george.Intra(cod));
    }

    [Fact]
    public async Task OConexiuneUrmeazaOSinguraCamera()
    {
        var george = await Conecteaza("george");
        var maria = await Conecteaza("maria");
        var prima = await Creeaza(george);
        var aDoua = await Creeaza(maria);
        await george.Intra(prima);
        await george.Intra(aDoua);
        await maria.Intra(aDoua);
        var s = await maria.Asteapta(x => x.GetProperty("jucatori").GetArrayLength() == 2, "both in the second room");
        s.Str("cod").ShouldBe(aDoua);
    }

    [Fact]
    public async Task InTimpulJoculuiJucatorulPlecatIsiPierdeTurele()
    {
        var (george, maria, _, _) = await JocInDoi();
        await maria.Hub.StopAsync();
        await AvanseazaPanaCand(() => george.Stare.GetProperty("jurnal").EnumerateArray().Any(r => r.Str("text").Contains("a ieșit")));
        // George passes; Maria's turn is skipped quickly because she is gone.
        Avanseaza(MotorTriviador.TimpAlegere + TimeSpan.FromSeconds(1));
        await george.Asteapta(x => x.GetProperty("jurnal").EnumerateArray().Any(r => r.Str("text").Contains("nu e în cameră")), "her turn is lost");
        await AvanseazaPanaCand(() => george.Stare.Int("tura") == 0 && george.Stare.Int("runda") == 2);
    }
}

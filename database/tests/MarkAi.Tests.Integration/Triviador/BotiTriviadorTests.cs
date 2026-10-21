using System.Text.Json;
using MarkAi.Api.Servicii.Triviador;

namespace MarkAi.Tests.Integration.Triviador;

/// <summary>Bots play their own turns and answers on the server's clock; games with bots give 30% less XP and bots get none.</summary>
public sealed class BotiTriviadorTests : TestTriviador
{
    private async Task<(Jucator Ion, string Cod, JsonElement Stare)> ContraBot(string nivel = "greu")
    {
        var ion = await Conecteaza("ion");
        var cod = await Creeaza(ion, insule: 12);
        await ion.Intra(cod);
        await ion.Hub.InvokeAsync("AdaugaBot", nivel);
        await ion.Asteapta(x => x.GetProperty("jucatori").GetArrayLength() == 2, "bot seated");
        await ion.Hub.InvokeAsync("Porneste");
        return (ion, cod, await ion.Asteapta(x => x.Str("faza") == "alegere", "game with a bot started"));
    }

    private async Task LaTuraBotului(Jucator ion, JsonElement s)
    {
        await ion.Hub.InvokeAsync("Ataca", Tinte(s)[0]);
        s = await ion.Asteapta(x => x.Str("faza") == "intrebare", "question");
        await ion.Hub.InvokeAsync("Raspunde", IndexBun(s));
        await ion.Asteapta(x => x.Str("faza") == "rezultat", "result");
        Avanseaza(MotorTriviador.PauzaRezultat + TimeSpan.FromMilliseconds(100));
        await ion.Asteapta(x => x.Str("faza") == "alegere" && x.Int("tura") == 1, "bot's turn");
    }

    [Fact]
    public async Task BotulAtacaSingur()
    {
        var (ion, _, s) = await ContraBot();
        await LaTuraBotului(ion, s);
        Avanseaza(TimeSpan.FromSeconds(3.1));
        s = await ion.Asteapta(x => x.Str("faza") == "intrebare", "bot chose an island");
        s.GetProperty("atac").Str("atacator").ShouldBe("bot1");
    }

    [Fact]
    public async Task BotulRaspundeInIntarziereaLui()
    {
        var (ion, _, s) = await ContraBot();
        await LaTuraBotului(ion, s);
        Avanseaza(TimeSpan.FromSeconds(3.1));
        await ion.Asteapta(x => x.Str("faza") == "intrebare", "bot chose an island");
        Avanseaza(TimeSpan.FromSeconds(8.1));
        s = await ion.Asteapta(x => x.Str("faza") == "rezultat", "bot answered");
        s.GetProperty("rezultat").GetProperty("alegeri").TryGetProperty("bot1", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task BotulAparaInDuel()
    {
        var (ion, cod, s) = await ContraBot();
        var vecin = Vecini(s, Cetate(s, 0)).First(v => v != Cetate(s, 1));
        await Motor.PentruVerificariAsync(cod, insula: vecin, proprietar: "bot1");
        await ion.Asteapta(x => x.GetProperty("proprietari").GetProperty(vecin).GetString() == "bot1", "island given to the bot");
        await ion.Hub.InvokeAsync("Ataca", vecin);
        s = await ion.Asteapta(x => x.Str("faza") == "intrebare", "duel with the bot");
        s.GetProperty("atac").Str("aparator").ShouldBe("bot1");
        Avanseaza(TimeSpan.FromSeconds(8.1));
        await ion.Asteapta(x => x.GetProperty("raspunsuri").TryGetProperty("bot1", out _), "bot answered the duel");
    }

    [Fact]
    public async Task XpulCuBotiEsteMaiMicIarBotiiNuPrimesc()
    {
        var (ion, cod, s) = await ContraBot();
        await Motor.PentruVerificariAsync(cod, runda: s.Int("runde") + 1);
        Avanseaza(MotorTriviador.TimpAlegere + TimeSpan.FromSeconds(1));
        s = await ion.Asteapta(x => x.Str("faza") == "final", "bot game over");
        var (insuleIon, insuleBot) = (Insule(s, ion.Id), Insule(s, "bot1"));
        var asteptat = (int)Math.Round((insuleIon * MotorTriviador.XpInsula + (insuleIon >= insuleBot ? MotorTriviador.XpCastig : 0)) * MotorTriviador.FactorBoti);
        s.GetProperty("xp").Int(ion.Id).ShouldBe(asteptat);
        s.GetProperty("xp").TryGetProperty("bot1", out _).ShouldBeFalse();
        s.GetProperty("cuBoti").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task BotiiNuSeAdaugaDupaStart()
    {
        var (ion, _, _) = await ContraBot();
        await Refuzat(() => ion.Hub.InvokeAsync("AdaugaBot", "usor"));
        await Refuzat(() => ion.Hub.InvokeAsync("ScoateBot", "bot1"));
    }

    [Theory]
    [InlineData("usor")]
    [InlineData("mediu")]
    [InlineData("greu")]
    public async Task FiecareNivelDeBotJoaca(string nivel)
    {
        var (ion, _, s) = await ContraBot(nivel);
        s.GetProperty("jucatori")[1].Str("dificultate").ShouldBe(nivel);
        await LaTuraBotului(ion, s);
        Avanseaza(TimeSpan.FromSeconds(3.1));
        await ion.Asteapta(x => x.Str("faza") == "intrebare", "bot plays");
    }
}

using System.Text.Json;
using MarkAi.Api.Servicii.Triviador;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Triviador;

/// <summary>The game, refereed by the server: turns and targets, deadlines, duels and estimates, XP once, no answer before the result.</summary>
public sealed class JocTriviadorTests : TestTriviador
{
    private static readonly TimeSpan DupaRezultat = MotorTriviador.PauzaRezultat + TimeSpan.FromMilliseconds(100);

    private static async Task<JsonElement> AtacaLiber(Jucator j, JsonElement s)
    {
        var tinta = Tinte(s).First(t => s.GetProperty("proprietari").GetProperty(t).ValueKind == JsonValueKind.Null);
        await j.Hub.InvokeAsync("Ataca", tinta);
        return await j.Asteapta(x => x.Str("faza") == "intrebare", "question open");
    }

    [Fact]
    public async Task PrimulLocIncepe()
    {
        var (george, _, _, s) = await JocInDoi();
        s.GetProperty("jucatori")[0].Str("id").ShouldBe(george.Id);
        s.Int("tura").ShouldBe(0);
        s.Int("runda").ShouldBe(1);
    }

    [Fact]
    public async Task NumarulDeRundeUrmeazaHarta()
    {
        var (_, _, _, s) = await JocInDoi(insule: 9);
        // 7 free islands for 2 players: 4 rounds to take them, plus 2.
        s.Int("runde").ShouldBe(6);
    }

    [Fact]
    public async Task CetatileSuntAleJucatorilor()
    {
        var (george, maria, _, s) = await JocInDoi();
        s.GetProperty("proprietari").Str(Cetate(s, 0)).ShouldBe(george.Id);
        s.GetProperty("proprietari").Str(Cetate(s, 1)).ShouldBe(maria.Id);
        Cetate(s, 0).ShouldNotBe(Cetate(s, 1));
    }

    [Fact]
    public async Task TinteleSuntVeciniiCetatii()
    {
        var (_, _, _, s) = await JocInDoi();
        Tinte(s).ShouldNotBeEmpty();
        Tinte(s).ShouldAllBe(t => Vecini(s, Cetate(s, 0)).Contains(t));
        Tinte(s).ShouldNotContain(Cetate(s, 1));
    }

    [Fact]
    public async Task AtaculInAfaraTureiEsteIgnorat()
    {
        var (george, maria, _, s) = await JocInDoi();
        await maria.Hub.InvokeAsync("Ataca", Tinte(s)[0]);
        (await george.Neschimbat()).Str("faza").ShouldBe("alegere");
    }

    [Fact]
    public async Task AtaculPeOCetateEsteIgnorat()
    {
        var (george, _, _, s) = await JocInDoi();
        await george.Hub.InvokeAsync("Ataca", Cetate(s, 1));
        (await george.Neschimbat()).Str("faza").ShouldBe("alegere");
    }

    [Fact]
    public async Task AtaculPeOInsulaNevecinaEsteIgnorat()
    {
        var (george, _, _, s) = await JocInDoi();
        var departe = s.GetProperty("harta").GetProperty("insule").EnumerateArray().Select(i => i.Str("id"))
            .First(i => !Tinte(s).Contains(i) && i != Cetate(s, 0) && i != Cetate(s, 1));
        await george.Hub.InvokeAsync("Ataca", departe);
        (await george.Neschimbat()).Str("faza").ShouldBe("alegere");
    }

    [Fact]
    public async Task IntrebareaDeschisaNuAreRaspunsul()
    {
        var (george, _, _, s) = await JocInDoi();
        s = await AtacaLiber(george, s);
        s.GetProperty("rezultat").ValueKind.ShouldBe(JsonValueKind.Null);
        s.GetProperty("intrebare").TryGetProperty("corect", out _).ShouldBeFalse();
        s.GetProperty("intrebare").GetProperty("v").GetArrayLength().ShouldBe(4);
    }

    [Fact]
    public async Task DoarJucatoriiDuelauluiRaspund()
    {
        var (george, maria, _, s) = await JocInDoi();
        await AtacaLiber(george, s);
        await maria.Hub.InvokeAsync("Raspunde", 0);
        (await george.Neschimbat()).GetProperty("raspunsuri").TryGetProperty(maria.Id, out _).ShouldBeFalse();
    }

    [Fact]
    public async Task VariantaInafaraListeiEsteIgnorata()
    {
        var (george, _, _, s) = await JocInDoi();
        await AtacaLiber(george, s);
        await george.Hub.InvokeAsync("Raspunde", 7);
        (await george.Neschimbat()).Str("faza").ShouldBe("intrebare");
    }

    [Fact]
    public async Task RaspunsulBunCucersteInsulaLibera()
    {
        var (george, _, _, s) = await JocInDoi();
        s = await AtacaLiber(george, s);
        var tinta = s.GetProperty("atac").Str("insula");
        var corect = IndexBun(s);
        await george.Hub.InvokeAsync("Raspunde", corect);
        s = await george.Asteapta(x => x.Str("faza") == "rezultat", "result shown");
        s.GetProperty("proprietari").Str(tinta).ShouldBe(george.Id);
        s.GetProperty("rezultat").Int("corect").ShouldBe(corect);
        s.GetProperty("rezultat").Str("castigator").ShouldBe(george.Id);
    }

    [Fact]
    public async Task RaspunsulGresitLasaInsulaLibera()
    {
        var (george, _, _, s) = await JocInDoi();
        s = await AtacaLiber(george, s);
        var tinta = s.GetProperty("atac").Str("insula");
        await george.Hub.InvokeAsync("Raspunde", (IndexBun(s) + 1) % 4);
        s = await george.Asteapta(x => x.Str("faza") == "rezultat", "result shown");
        s.GetProperty("proprietari").GetProperty(tinta).ValueKind.ShouldBe(JsonValueKind.Null);
        s.GetProperty("rezultat").Str("text").ShouldContain("rămâne liberă");
    }

    [Fact]
    public async Task DupaRezultatTuraTrece()
    {
        var (george, maria, _, s) = await JocInDoi();
        await AtacaLiber(george, s);
        await george.Hub.InvokeAsync("Raspunde", 0);
        await george.Asteapta(x => x.Str("faza") == "rezultat", "result");
        Avanseaza(DupaRezultat);
        await maria.Asteapta(x => x.Str("faza") == "alegere" && x.Int("tura") == 1, "maria's turn");
    }

    [Fact]
    public async Task FaraAlegereLaTimpTuraTrece()
    {
        var (george, _, _, _) = await JocInDoi();
        Avanseaza(MotorTriviador.TimpAlegere + TimeSpan.FromSeconds(1));
        var s = await george.Asteapta(x => x.Int("tura") == 1, "turn passes after the choosing time");
        s.GetProperty("jurnal").EnumerateArray().ShouldContain(r => r.Str("text").Contains("n-a ales nimic la timp"));
    }

    [Fact]
    public async Task IntrebareaSeInchideLaTermenulServerului()
    {
        var (george, _, _, s) = await JocInDoi();
        await AtacaLiber(george, s);
        Avanseaza(MotorTriviador.TimpIntrebare + TimeSpan.FromSeconds(1.1));
        await george.Asteapta(x => x.Str("faza") == "rezultat", "question closed by the server's deadline");
    }

    [Fact]
    public async Task RaspunsulDupaTermenEsteIgnorat()
    {
        var (george, _, _, s) = await JocInDoi();
        s = await AtacaLiber(george, s);
        var corect = IndexBun(s);
        Avanseaza(MotorTriviador.TimpIntrebare + TimeSpan.FromSeconds(1.1));
        await george.Asteapta(x => x.Str("faza") == "rezultat", "closed");
        await george.Hub.InvokeAsync("Raspunde", corect);
        (await george.Neschimbat()).GetProperty("rezultat").GetProperty("alegeri").EnumerateObject().ShouldBeEmpty();
    }

    [Fact]
    public async Task TermenulVineDeLaServer()
    {
        var (george, _, _, s) = await JocInDoi();
        s = await AtacaLiber(george, s);
        var ramas = s.GetProperty("termenLa").GetInt64() - s.GetProperty("acum").GetInt64();
        var asteptat = (long)(MotorTriviador.TimpIntrebare + TimeSpan.FromSeconds(1)).TotalMilliseconds;
        ramas.ShouldBeInRange(asteptat - 50, asteptat);
    }

    private async Task<(Jucator George, Jucator Maria, string Insula, JsonElement Stare)> Duel()
    {
        var (george, maria, cod, s) = await JocInDoi();
        // George passes his turn; Maria attacks an island of George's next to her castle.
        Avanseaza(MotorTriviador.TimpAlegere + TimeSpan.FromSeconds(1));
        s = await maria.Asteapta(x => x.Str("faza") == "alegere" && x.Int("tura") == 1, "maria's turn");
        var vecin = Vecini(s, Cetate(s, 1)).First(v => v != Cetate(s, 0));
        await Motor.PentruVerificariAsync(cod, insula: vecin, proprietar: george.Id);
        await maria.Asteapta(x => x.GetProperty("tinte").EnumerateArray().Any(t => t.GetString() == vecin), "the island is a target");
        await maria.Hub.InvokeAsync("Ataca", vecin);
        s = await maria.Asteapta(x => x.Str("faza") == "intrebare", "duel question");
        return (george, maria, vecin, s);
    }

    [Fact]
    public async Task AtaculPeOInsulaOcupataEsteDuel()
    {
        var (george, _, _, s) = await Duel();
        s.GetProperty("atac").Str("aparator").ShouldBe(george.Id);
    }

    [Fact]
    public async Task InDuelAmbiiRaspund()
    {
        var (george, maria, _, s) = await Duel();
        await george.Hub.InvokeAsync("Raspunde", IndexBun(s));
        (await maria.Asteapta(x => x.GetProperty("raspunsuri").TryGetProperty(george.Id, out _), "defender answered")).Str("faza").ShouldBe("intrebare");
    }

    [Fact]
    public async Task AparatorulCareRaspundeBinePastreazaInsula()
    {
        var (george, maria, insula, s) = await Duel();
        await maria.Hub.InvokeAsync("Raspunde", (IndexBun(s) + 1) % 4);
        await george.Hub.InvokeAsync("Raspunde", IndexBun(s));
        s = await george.Asteapta(x => x.Str("faza") == "rezultat", "duel result");
        s.GetProperty("proprietari").Str(insula).ShouldBe(george.Id);
        s.GetProperty("rezultat").Str("text").ShouldContain("a apărat");
    }

    [Fact]
    public async Task AtacatorulCareRaspundeSinguBineCucereste()
    {
        var (george, maria, insula, s) = await Duel();
        await maria.Hub.InvokeAsync("Raspunde", IndexBun(s));
        await george.Hub.InvokeAsync("Raspunde", (IndexBun(s) + 1) % 4);
        s = await george.Asteapta(x => x.Str("faza") == "rezultat", "duel result");
        s.GetProperty("proprietari").Str(insula).ShouldBe(maria.Id);
    }

    [Fact]
    public async Task AmbiiBineDuceLaEstimare()
    {
        var (george, maria, _, s) = await Duel();
        await maria.Hub.InvokeAsync("Raspunde", IndexBun(s));
        await george.Hub.InvokeAsync("Raspunde", IndexBun(s));
        s = await george.Asteapta(x => x.Str("faza") == "rezultat", "duel result");
        s.GetProperty("rezultat").Str("text").ShouldContain("estimare");
        Avanseaza(DupaRezultat);
        s = await george.Asteapta(x => x.Str("faza") == "estimare", "estimate open");
        s.GetProperty("intrebare").GetProperty("v").ValueKind.ShouldBe(JsonValueKind.Null);
        s.GetProperty("rezultat").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    private async Task<(Jucator George, Jucator Maria, string Insula)> LaEstimare()
    {
        var (george, maria, insula, s) = await Duel();
        await maria.Hub.InvokeAsync("Raspunde", IndexBun(s));
        await george.Hub.InvokeAsync("Raspunde", IndexBun(s));
        await george.Asteapta(x => x.Str("faza") == "rezultat", "duel result");
        Avanseaza(DupaRezultat);
        await george.Asteapta(x => x.Str("faza") == "estimare", "estimate open");
        return (george, maria, insula);
    }

    [Fact]
    public async Task EgalitateaLaEstimareIiLasaInsulaAparatorului()
    {
        var (george, maria, insula) = await LaEstimare();
        await maria.Hub.InvokeAsync("Estimeaza", 1900);
        await george.Hub.InvokeAsync("Estimeaza", 1900);
        var s = await george.Asteapta(x => x.Str("faza") == "rezultat", "estimate result");
        s.GetProperty("rezultat").Str("castigator").ShouldBe(george.Id);
        s.GetProperty("proprietari").Str(insula).ShouldBe(george.Id);
        s.GetProperty("rezultat").Int("corect").ShouldBeOneOf(1883, 1920);
    }

    [Fact]
    public async Task CelMaiAproapeLaEstimareCastiga()
    {
        var (george, maria, insula) = await LaEstimare();
        var corect = await RaspunsEstimare(george);
        await maria.Hub.InvokeAsync("Estimeaza", corect);
        await george.Hub.InvokeAsync("Estimeaza", corect + 500);
        var s = await george.Asteapta(x => x.Str("faza") == "rezultat", "estimate result");
        s.GetProperty("proprietari").Str(insula).ShouldBe(maria.Id);
    }

    [Fact]
    public async Task FaraEstimareAparatorulPastreaza()
    {
        var (george, _, insula) = await LaEstimare();
        Avanseaza(MotorTriviador.TimpIntrebare + TimeSpan.FromSeconds(1.1));
        var s = await george.Asteapta(x => x.Str("faza") == "rezultat", "estimate closed");
        s.GetProperty("proprietari").Str(insula).ShouldBe(george.Id);
    }

    // The estimate's answer, from the question text (the two estimates are known).
    private static Task<int> RaspunsEstimare(Jucator j) =>
        Task.FromResult(j.Stare.GetProperty("intrebare").Str("t").Contains("Luceafărul") ? 1883 : 1920);

    [Fact]
    public async Task LaFinalXpulEsteDatOData()
    {
        var (george, maria, cod, s) = await JocInDoi();
        await Motor.PentruVerificariAsync(cod, runda: s.Int("runde") + 1);
        Avanseaza(MotorTriviador.TimpAlegere + TimeSpan.FromSeconds(1));
        s = await george.Asteapta(x => x.Str("faza") == "final", "game over");
        var (ig, im) = (Insule(s, george.Id), Insule(s, maria.Id));
        var max = Math.Max(ig, im);
        var xp = ig * MotorTriviador.XpInsula + (ig == max ? MotorTriviador.XpCastig : 0);
        s.GetProperty("xp").Int(george.Id).ShouldBe(xp);
        s.GetProperty("cuBoti").GetBoolean().ShouldBeFalse();
        var randuri = await h.Db(db => db.EvenimenteRecompensa.Where(e => e.TipEveniment == "triviador").ToListAsync());
        randuri.Where(r => r.UtilizatorId.ToString() == george.Id).ShouldHaveSingleItem().XpAcordat.ShouldBe(xp);
    }

    [Fact]
    public async Task EgalitateaLaFinalDaCastigCelorDoi()
    {
        var (george, maria, cod, s) = await JocInDoi();
        await Motor.PentruVerificariAsync(cod, runda: s.Int("runde") + 1);
        Avanseaza(MotorTriviador.TimpAlegere + TimeSpan.FromSeconds(1));
        s = await george.Asteapta(x => x.Str("faza") == "final", "game over");
        // Each has only the castle: a tie, both get 10 + 50.
        s.GetProperty("xp").Int(george.Id).ShouldBe(60);
        s.GetProperty("xp").Int(maria.Id).ShouldBe(60);
        s.GetProperty("jurnal")[0].Str("text").ShouldContain("Egalitate");
    }

    [Fact]
    public async Task DoarGazdaIncepeUnJocNou()
    {
        var (george, maria, cod, s) = await JocInDoi();
        await Motor.PentruVerificariAsync(cod, runda: s.Int("runde") + 1);
        Avanseaza(MotorTriviador.TimpAlegere + TimeSpan.FromSeconds(1));
        await george.Asteapta(x => x.Str("faza") == "final", "game over");
        await Refuzat(() => maria.Hub.InvokeAsync("JocNou"));
        await george.Hub.InvokeAsync("JocNou");
        await maria.Asteapta(x => x.Str("faza") == "lobby", "back to the lobby");
    }

    [Fact]
    public async Task JocNouInainteDeFinalEsteRefuzat()
    {
        var (george, _, _, _) = await JocInDoi();
        await Refuzat(() => george.Hub.InvokeAsync("JocNou"));
    }

    [Fact]
    public async Task NiciunMesajNuDezvaluieRaspunsulInainteDeRezultat()
    {
        var (george, maria, _, s) = await Duel();
        await maria.Hub.InvokeAsync("Raspunde", IndexBun(s));
        await george.Hub.InvokeAsync("Raspunde", IndexBun(s));
        await george.Asteapta(x => x.Str("faza") == "rezultat", "duel result");
        Avanseaza(DupaRezultat);
        await george.Asteapta(x => x.Str("faza") == "estimare", "estimate open");
        await maria.Hub.InvokeAsync("Estimeaza", 1900);
        await george.Hub.InvokeAsync("Estimeaza", 1800);
        await george.Asteapta(x => x.Str("faza") == "rezultat", "estimate result");
        var toate = new[] { george, maria }.SelectMany(j => j.Mesaje).Select(m => JsonDocument.Parse(m).RootElement).ToList();
        toate.Count.ShouldBeGreaterThan(8);
        toate.Where(m => m.Str("faza") is "intrebare" or "estimare").ShouldAllBe(m => m.GetProperty("rezultat").ValueKind == JsonValueKind.Null);
        toate.Where(m => m.Str("faza") == "estimare").ShouldAllBe(m => !m.GetRawText().Contains("1883") && !m.GetRawText().Contains("1920"));
    }
}

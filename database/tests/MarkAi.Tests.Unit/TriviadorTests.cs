using MarkAi.Api.Servicii.Triviador;

namespace MarkAi.Tests.Unit;

/// <summary>Triviador maps (every size 9–30), starting castles, and how a room draws its questions.</summary>
public sealed class TriviadorTests
{
    public static TheoryData<int> Marimi => new(Enumerable.Range(GeneratorHarta.MinInsule, GeneratorHarta.MaxInsule - GeneratorHarta.MinInsule + 1));

    private static HartaJoc Harta(int n, int seed = 1) => GeneratorHarta.Genereaza(n, [], new Random(seed));

    [Theory, MemberData(nameof(Marimi))]
    public void HartaAreNumarulCerutDeInsule(int n) => Harta(n, n).Insule.Count.ShouldBe(n);

    [Theory, MemberData(nameof(Marimi))]
    public void HartaEsteConectata(int n)
    {
        var h = Harta(n, n);
        h.Distante(h.Insule[0].Id).Count.ShouldBe(n);
    }

    [Theory, MemberData(nameof(Marimi))]
    public void InsuleleNuSeSuprapun(int n) => Harta(n, n).Insule.Select(i => (i.X, i.Y)).Distinct().Count().ShouldBe(n);

    [Theory, MemberData(nameof(Marimi))]
    public void PatruCetatiSeparate(int n) => Harta(n, n).Cetati(4).Distinct().Count().ShouldBe(4);

    [Theory]
    [InlineData(8)]
    [InlineData(31)]
    [InlineData(0)]
    public void MarimeaInAfaraLimitelorEsteRefuzata(int n) => Should.Throw<ArgumentOutOfRangeException>(() => Harta(n));

    [Fact]
    public void AceeasiSamantaDaAceeasiHarta()
    {
        var a = Harta(17, 42);
        var b = Harta(17, 42);
        a.Insule.Select(i => (i.Id, i.X, i.Y)).ShouldBe(b.Insule.Select(i => (i.Id, i.X, i.Y)));
        a.Poduri.Select(p => p[0] + p[1]).ShouldBe(b.Poduri.Select(p => p[0] + p[1]));
    }

    [Fact]
    public void InsuleleSuntInDesen()
    {
        foreach (var seed in Enumerable.Range(1, 20))
        {
            var h = Harta(30, seed);
            h.Insule.ShouldAllBe(i => i.X > 0 && i.X < h.Latime && i.Y > 0 && i.Y < h.Inaltime);
        }
    }

    [Fact]
    public void PodurileLeagaInsuleExistenteFaraDubluri()
    {
        var h = Harta(20, 3);
        var ids = h.Insule.Select(i => i.Id).ToHashSet();
        h.Poduri.ShouldAllBe(p => ids.Contains(p[0]) && ids.Contains(p[1]) && p[0] != p[1]);
        h.Poduri.Select(p => string.Join("-", p.Order())).Distinct().Count().ShouldBe(h.Poduri.Count);
    }

    [Fact]
    public void PodurileSuntIntreVecini()
    {
        // Islands sit on a grid; a bridge only joins neighbouring cells, so no two bridges cross.
        var h = Harta(25, 5);
        var dupaId = h.Insule.ToDictionary(i => i.Id);
        h.Poduri.ShouldAllBe(p => Math.Abs(dupaId[p[0]].X - dupaId[p[1]].X) < 330 && Math.Abs(dupaId[p[0]].Y - dupaId[p[1]].Y) < 280);
    }

    [Fact]
    public void VeciniiSuntSimetrici()
    {
        var h = Harta(15, 2);
        foreach (var i in h.Insule)
            foreach (var v in h.Vecini(i.Id)) h.Vecini(v).ShouldContain(i.Id);
    }

    [Fact]
    public void DistantaPanaLaSineEsteZero()
    {
        var h = Harta(12);
        h.Distante(h.Insule[3].Id)[h.Insule[3].Id].ShouldBe(0);
    }

    [Fact]
    public void DistanteleCrescCuUnPePod()
    {
        var h = Harta(12);
        var d = h.Distante(h.Insule[0].Id);
        foreach (var p in h.Poduri) Math.Abs(d[p[0]] - d[p[1]]).ShouldBeLessThanOrEqualTo(1);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void CetatileSuntCatMaiDeparte(int jucatori)
    {
        var h = Harta(20, 7);
        var cetati = h.Cetati(jucatori);
        cetati.Count.ShouldBe(jucatori);
        var minim = cetati.SelectMany((a, i) => cetati.Skip(i + 1).Select(b => h.Distante(a)[b])).Min();
        minim.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void CetatileSuntDeterministe() => Harta(16, 9).Cetati(3).ShouldBe(Harta(16, 9).Cetati(3));

    [Fact]
    public void InsuleleIauNumeleLectiilorInOrdine()
    {
        var capitole = new List<(string, string, int[])> { ("Romantismul", "2 capitole", [5]), ("Ion", "1 capitol", [6]) };
        var h = GeneratorHarta.Genereaza(9, capitole, new Random(1));
        (h.Insule[0].Nume, h.Insule[0].Autor, h.Insule[0].ParcursIds.Single()).ShouldBe(("Romantismul", "2 capitole", 5));
        h.Insule[1].Nume.ShouldBe("Ion");
        h.Insule[2].Nume.ShouldBe("Insula 3");
        h.Insule[2].ParcursIds.ShouldBeEmpty();
    }

    [Fact]
    public void FiecareInsulaAreUnSemnCunoscut() =>
        Harta(30).Insule.ShouldAllBe(i => new[] { "castel", "conac", "turn", "casa", "clopot", "far", "moara" }.Contains(i.Semn));

    private static PachetIntrebari Pachet(int grile = 4) => new(
        Enumerable.Range(1, grile).Select(i => new IntrebareGrila(i, i % 2 == 0 ? 100 : 200, $"Q{i}", ["A", "B"], 0)).ToList(),
        [new(-1, null, "E", 1883)]);

    [Fact]
    public void IntrebarileNuSeRepetaPanaSeTermina()
    {
        var p = Pachet(4);
        var rnd = new Random(1);
        var alese = Enumerable.Range(0, 4).Select(_ => p.Alege(p.Grila, [], q => q.Id, q => q.ParcursId, rnd).Id).ToList();
        alese.Distinct().Count().ShouldBe(4);
    }

    [Fact]
    public void DupaCeSeTerminaSeIaDeLaCapat()
    {
        var p = Pachet(2);
        var rnd = new Random(1);
        for (var i = 0; i < 2; i++) p.Alege(p.Grila, [], q => q.Id, q => q.ParcursId, rnd);
        Should.NotThrow(() => p.Alege(p.Grila, [], q => q.Id, q => q.ParcursId, rnd));
    }

    [Fact]
    public void IntrebareaVineDinLectiaInsulei()
    {
        var p = Pachet(6);
        var rnd = new Random(3);
        for (var i = 0; i < 3; i++) p.Alege(p.Grila, [100], q => q.Id, q => q.ParcursId, rnd).ParcursId.ShouldBe(100);
    }

    [Fact]
    public void FaraIntrebariInLectieSeIaDinRest()
    {
        var p = Pachet(4);
        p.Alege(p.Grila, [999], q => q.Id, q => q.ParcursId, new Random(1)).ShouldNotBeNull();
    }

    [Fact]
    public void XpulCuBotiEsteCuTreizeciLaSutaMaiMic()
    {
        MotorTriviador.FactorBoti.ShouldBe(0.7);
        (MotorTriviador.XpInsula, MotorTriviador.XpCastig).ShouldBe((10, 50));
    }

    [Fact]
    public void LimiteleCamerelor()
    {
        (MotorTriviador.MinJucatori, MotorTriviador.MaxJucatoriCamera, MotorTriviador.MaxCamerePeGazda).ShouldBe((2, 4, 3));
        MotorTriviador.GratiePlecare.ShouldBeLessThan(MotorTriviador.TimpIntrebare);
    }

    [Fact]
    public void GrupulCameriiEsteDupaCod() => MotorTriviador.Grup("ABC123").ShouldBe("joc:ABC123");
}

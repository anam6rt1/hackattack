using MarkAi.Api.Servicii.Triviador;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Triviador;

/// <summary>
/// A room's questions are sampled in SQL (<see cref="PachetIntrebari.IncarcaAsync"/>), never a whole subject: at most
/// PeInsula per island's Lecție and Rezerva from the rest, no question twice, only usable ones, the room's grade respected, and
/// the demo set when nothing is left.
/// </summary>
public sealed class PachetIntrebariTests : TestCuHarness
{
    /// <summary>A published Lecție of subject 1 with <paramref name="grile"/> usable questions and <paramref name="estimari"/> estimates.</summary>
    private Task<int> Lectie(string titlu, int grile, int estimari = 0, string clasa = "Bac") => h.Db(async db =>
    {
        var p = new Parcurs { MaterieId = 1, Titlu = titlu, Clasa = clasa, Vizibilitate = Parcurs.Public, CreatLa = DateTime.UtcNow };
        db.Add(p);
        await db.SaveChangesAsync();
        for (var i = 0; i < grile; i++)
            db.Add(new Intrebare { MaterieId = 1, ParcursId = p.Id, Text = $"{titlu} — grilă {i}", CreataLa = DateTime.UtcNow,
                Variante = [new() { Text = "Bun", Corecta = true }, new() { Text = "Rău", Corecta = false }, new() { Text = "Altul", Corecta = false }] });
        for (var i = 0; i < estimari; i++)
            db.Add(new Intrebare { MaterieId = 1, ParcursId = p.Id, Text = $"{titlu} — estimare {i}", Tip = Intrebare.Estimare, RaspunsNumeric = 1800 + i, CreataLa = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return p.Id;
    });

    private Task<PachetIntrebari> Pachet(int[] insule, string? clasa = null) => h.Db(db => PachetIntrebari.IncarcaAsync(db, [1], clasa, insule, Ct));

    [Fact]
    public async Task OInsulaDaCelMultPeInsula()
    {
        var mare = await Lectie("Mare", 40);
        var p = await Pachet([mare]);
        p.Grila.Count(q => q.ParcursId == mare).ShouldBe(PachetIntrebari.PeInsula + Math.Min(PachetIntrebari.Rezerva, 40 - PachetIntrebari.PeInsula));
        p.Grila.Select(q => q.Id).ShouldBeUnique();
    }

    [Fact]
    public async Task RezervaEsteLimitata()
    {
        var insula = await Lectie("Pe hartă", 5);
        var alta = await Lectie("Nu e pe hartă", 150);
        var p = await Pachet([insula]);
        p.Grila.Count(q => q.ParcursId == insula).ShouldBe(5);
        p.Grila.Count(q => q.ParcursId == alta).ShouldBe(PachetIntrebari.Rezerva);
        p.Grila.Count.ShouldBe(5 + PachetIntrebari.Rezerva);
    }

    [Fact]
    public async Task FiecareInsulaAreIntrebarileEi()
    {
        var ids = new[] { await Lectie("A", 20), await Lectie("B", 3), await Lectie("C", 20) };
        var p = await Pachet(ids);
        foreach (var id in ids) p.Grila.Count(q => q.ParcursId == id).ShouldBeGreaterThanOrEqualTo(Math.Min(PachetIntrebari.PeInsula, id == ids[1] ? 3 : 20));
        p.Grila.Select(q => q.Id).ShouldBeUnique();
    }

    [Fact]
    public async Task EsantionulSeSchimbaDeLaOCameraLaAlta()
    {
        var mare = await Lectie("Mare", 60);
        var seturi = new List<string>();
        for (var i = 0; i < 4; i++)
            seturi.Add(string.Join(',', (await h.Db(db => PachetIntrebari.IncarcaAsync(db, [1], null, [mare], Ct))).Grila.Take(PachetIntrebari.PeInsula).Select(q => q.Id).Order()));
        seturi.Distinct().Count().ShouldBeGreaterThan(1);
    }

    [Fact]
    public async Task IntrebarileNefolosibileNuIntra()
    {
        // (Two right options, or an estimate without an answer, cannot exist: the database refuses them.)
        var id = await Lectie("Cu probleme", 2);
        await h.Db(async db =>
        {
            db.AddRange(
                new Intrebare { MaterieId = 1, ParcursId = id, Text = "Inactivă", Activa = false, CreataLa = DateTime.UtcNow,
                    Variante = [new() { Text = "A", Corecta = true }, new() { Text = "B", Corecta = false }] },
                new Intrebare { MaterieId = 1, ParcursId = id, Text = "O singură variantă", CreataLa = DateTime.UtcNow, Variante = [new() { Text = "A", Corecta = true }] });
            await db.SaveChangesAsync();
        });
        var p = await Pachet([id]);
        p.Grila.Select(q => q.Text).ShouldBe(["Cu probleme — grilă 0", "Cu probleme — grilă 1"], ignoreOrder: true);
    }

    [Fact]
    public async Task VariantaCorectaEsteCeaDinBaza()
    {
        var id = await Lectie("Variante", 1);
        var q = (await Pachet([id])).Grila.ShouldHaveSingleItem();
        (q.Variante[q.Corect]).ShouldBe("Bun");
        q.Variante.ShouldBe(["Bun", "Rău", "Altul"]);
    }

    [Fact]
    public async Task ClasaCamereiSeRespectaSiInRezerva()
    {
        var a12 = await Lectie("A XII-a", 3, clasa: "XII");
        var a9 = await Lectie("A IX-a", 30, clasa: "IX");
        var p = await Pachet([a12], "XII");
        p.Grila.ShouldAllBe(q => q.ParcursId == a12);
        p.Grila.ShouldNotContain(q => q.ParcursId == a9);
    }

    [Fact]
    public async Task EstimarileAuSiEleLimite()
    {
        var id = await Lectie("Estimări", 1, estimari: 30);
        var p = await Pachet([id]);
        p.Estimari.Count.ShouldBe(PachetIntrebari.PeInsula / 5 + 1 + Math.Min(PachetIntrebari.Rezerva / 5, 30 - (PachetIntrebari.PeInsula / 5 + 1)));
        p.Estimari.Select(q => q.Id).ShouldBeUnique();
    }

    [Fact]
    public async Task FaraIntrebariSeFolosesteSetulDemo()
    {
        var p = await Pachet([await Lectie("Goală", 0)]);
        p.Grila.ShouldNotBeEmpty();
        p.Grila.ShouldAllBe(q => q.ParcursId == null);
        p.Estimari.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task AltaMaterieNuIntra()
    {
        var id = await Lectie("Română", 2);
        await h.Db(async db =>
        {
            db.Add(new Intrebare { MaterieId = 2, Text = "Matematică", CreataLa = DateTime.UtcNow, Variante = [new() { Text = "A", Corecta = true }, new() { Text = "B", Corecta = false }] });
            await db.SaveChangesAsync();
        });
        (await Pachet([id])).Grila.ShouldNotContain(q => q.Text == "Matematică");
    }
}

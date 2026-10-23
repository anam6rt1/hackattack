using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using MarkAi.Tests.Suport;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace MarkAi.Tests.Integration.Cautare;

/// <summary>
/// The scheduled sessions that <c>date-test</c> and the preview API add (<see cref="SedinteTest"/>): three per professor, every
/// type within 7 days and after, paid and pro bono, one full, booked by the students given, placed like the API places them, and
/// nothing added twice. Professors: irina (Liceul de Test), ovidiu (no place of his own, professor at Școala Bărăbanț) and
/// bianca (her own place in Bărăbanț, in no organisation as a professor).
/// </summary>
public sealed class SedinteTestTests : TestCautare
{
    private static readonly string[] Profesori = ["irina", "ovidiu", "bianca"], Elevi = ["george", "elena", "radu"];

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        await h.Db(async db =>
        {
            db.Add(new TarifFix { Suma = 80, Moneda = "RON", DurataMinute = 60, Activ = true });
            await db.SaveChangesAsync();
        });
    }

    private Task<(int Sedinte, int Rezervari)> Adauga(string[]? profesori = null, string[]? elevi = null) =>
        h.Db(db => SedinteTest.AdaugaAsync(db, profesori ?? Profesori, elevi ?? Elevi, DateTime.UtcNow));

    private Task<List<CameraTutoring>> Sedinte() =>
        h.Db(db => db.CamereTutoring.AsNoTracking().Include(c => c.Rezervari).ThenInclude(r => r.Plati).Include(c => c.Mentor).ThenInclude(m => m.Utilizator)
            .OrderBy(c => c.InceputLa).ToListAsync());

    [Fact]
    public async Task TreiSedintePeProfesorDeToateTipurile()
    {
        var (sedinte, _) = await Adauga();
        sedinte.ShouldBe(9);
        var toate = await Sedinte();
        toate.GroupBy(c => c.MentorId).ShouldAllBe(g => g.Count() == 3 && g.Select(c => c.Titlu).Distinct().Count() == 3);
        var peste7 = DateTime.UtcNow.AddDays(7);
        foreach (var tip in CameraTutoring.Tipuri)
        {
            toate.ShouldContain(c => c.Tip == tip && c.InceputLa < peste7, $"{tip} within 7 days");
            toate.ShouldContain(c => c.Tip == tip && c.InceputLa > peste7, $"{tip} after 7 days");
        }
        toate.ShouldAllBe(c => c.Status == RezervareService.SlotDeschis && c.InceputLa > DateTime.UtcNow && c.FinalizatLa == c.InceputLa!.Value.AddMinutes(60));
    }

    [Fact]
    public async Task OraLocalaDinBucuresti()
    {
        await Adauga();
        var bucuresti = TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest");
        (await Sedinte()).Select(c => TimeZoneInfo.ConvertTimeFromUtc(c.InceputLa!.Value, bucuresti))
            .ShouldAllBe(t => t.Minute == 0 && new[] { 10, 15, 16, 17, 18 }.Contains(t.Hour));
    }

    [Fact]
    public async Task PlatiteSiGratuiteCuPlataSimulata()
    {
        await Adauga();
        var toate = await Sedinte();
        toate.ShouldContain(c => c.ProBono);
        toate.ShouldContain(c => !c.ProBono);
        toate.Where(c => c.ProBono).ShouldAllBe(c => c.TarifId == null && c.Rezervari.All(r => r.TarifAchitat == 0 && r.Plati.Count == 0));
        toate.Where(c => !c.ProBono).ShouldAllBe(c => c.TarifId != null && c.Rezervari.All(r => r.TarifAchitat == 80 && r.Plati.Count == 1));
    }

    [Fact]
    public async Task FaraTarifToateSuntGratuite()
    {
        await h.Db(async db => { await db.TarifeFixe.ExecuteUpdateAsync(t => t.SetProperty(x => x.Activ, false)); });
        await Adauga();
        (await Sedinte()).ShouldAllBe(c => c.ProBono && c.TarifId == null);
    }

    [Fact]
    public async Task OSedintaPlinaSiRestulCuLocuri()
    {
        await Adauga();
        var toate = await Sedinte();
        toate.ShouldContain(c => c.Rezervari.Count == c.Capacitate, "a full session");
        toate.ShouldContain(c => c.Rezervari.Count < c.Capacitate && c.Rezervari.Count > 0, "a booked session with free places");
        toate.ShouldContain(c => c.Rezervari.Count == 0, "a session nobody booked yet");
        toate.SelectMany(c => c.Rezervari).ShouldAllBe(r => r.Status == RezervareService.RezervareConfirmata);
    }

    [Fact]
    public async Task ParticipareaUrmeazaTipul()
    {
        await Adauga();
        var toate = await Sedinte();
        toate.Where(c => c.Tip == CameraTutoring.Online).SelectMany(c => c.Rezervari).ShouldAllBe(r => r.Participare == Rezervare.Online);
        toate.Where(c => c.Tip == CameraTutoring.FataInFata).SelectMany(c => c.Rezervari).ShouldAllBe(r => r.Participare == Rezervare.LaSala);
        toate.Where(c => c.Tip == CameraTutoring.Hibrid).SelectMany(c => c.Rezervari).Select(r => r.Participare).Distinct().Count().ShouldBe(2);
    }

    [Fact]
    public async Task LoculEsteAlOrganizatieiSauAlProfesorului()
    {
        await Adauga();
        var toate = await Sedinte();
        toate.Where(c => c.Tip == CameraTutoring.Online).ShouldAllBe(c => c.LocJudetId == null && c.LocOrganizatieId == null && c.NotaLoc == null
            && c.LinkIntalnire == "https://example.com/sedinta-test");
        toate.Where(c => c.Tip != CameraTutoring.Online).ShouldAllBe(c => c.LocJudetId != null && c.NotaLoc != null);
        toate.Where(c => c.Tip == CameraTutoring.FataInFata).ShouldAllBe(c => c.LinkIntalnire == null);
        // ovidiu has no place of his own: always at Școala Bărăbanț. bianca teaches in no organisation: always at her own place.
        toate.Where(c => c.Mentor.Utilizator.Username == "ovidiu" && c.Tip != CameraTutoring.Online)
            .ShouldAllBe(c => c.LocOrganizatieId == ScoalaBarabant && c.LocLocalitateId == Barabant);
        toate.Where(c => c.Mentor.Utilizator.Username == "bianca" && c.Tip != CameraTutoring.Online)
            .ShouldAllBe(c => c.LocOrganizatieId == null && c.LocJudetId == Alba && c.LocLocalitateId == Barabant);
        toate.Where(c => c.Mentor.Utilizator.Username == "irina" && c.Titlu == "Exerciții rezolvate").ShouldAllBe(c => c.LocOrganizatieId == Liceu);
    }

    [Fact]
    public async Task ADouaRulareNuAdaugaNimic()
    {
        var prima = await Adauga();
        (await Adauga()).ShouldBe((0, 0));
        (await Sedinte()).Count.ShouldBe(prima.Sedinte);
    }

    [Fact]
    public async Task OSedintaTrecutaEsteAdaugataDinNou()
    {
        await Adauga();
        var id = await Id("irina");
        await h.Db(db => db.CamereTutoring.Where(c => c.MentorId == id && c.Titlu == "Recapitulare pentru bacalaureat")
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.InceputLa, DateTime.UtcNow.AddDays(-2)).SetProperty(c => c.FinalizatLa, DateTime.UtcNow.AddDays(-2).AddHours(1))));
        (await Adauga()).Sedinte.ShouldBe(1);
        (await Sedinte()).Count(c => c.MentorId == id && c.Titlu == "Recapitulare pentru bacalaureat").ShouldBe(2);
    }

    [Fact]
    public async Task ProfesoriNeaprobatiSauFaraMaterieSuntSariti()
    {
        await h.Db(async db =>
        {
            var ovidiu = await db.ProfiluriMentor.Include(m => m.Materii).SingleAsync(m => m.Utilizator.Username == "ovidiu");
            ovidiu.StareVerificare = "in_asteptare";
            db.RemoveRange((await db.ProfiluriMentor.Include(m => m.Materii).SingleAsync(m => m.Utilizator.Username == "bianca")).Materii);
            await db.SaveChangesAsync();
        });
        (await Adauga(elevi: [])).ShouldBe((3, 0));
        (await Sedinte()).ShouldAllBe(c => c.Mentor.Utilizator.Username == "irina");
        (await Adauga(["george", "nimeni"])).Sedinte.ShouldBe(0);
    }

    [Fact]
    public async Task ElevulVedeSedinteleSiRezervarile()
    {
        await Adauga();
        var lista = await Profesori(george);
        lista.Where(p => p.Sloturi.Count > 0).Select(p => p.Id).Order().ShouldBe(new[] { await Id("irina"), await Id("ovidiu"), await Id("bianca") }.Order());
        (await george.GetJson("/rezervari")).Lista().Count().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task FaraLocSiFaraOrganizatieDoarSedinteleOnline()
    {
        // A professor with no county and no organisation has nowhere to hold an in-person session (the API refuses it too).
        await h.Db(async db =>
        {
            var u = new Utilizator { Nume = "Nelocat", Username = "nelocat", Email = "nelocat@example.invalid", EmailConfirmatLa = DateTime.UtcNow };
            u.ParolaHash = Parole.Hash(u, Harness.ParolaTest);
            u.ProfilMentor = new ProfilMentor { StareVerificare = "aprobat", ActivatLa = DateTime.UtcNow, Materii = new List<MentorMaterie> { new() { MaterieId = 1 } } };
            db.Add(u);
            await db.SaveChangesAsync();
        });
        (await Adauga(["nelocat"], [])).Sedinte.ShouldBe(1);
        (await Sedinte()).ShouldAllBe(c => c.Tip == CameraTutoring.Online);
    }

    [Fact]
    public async Task UnElevCareEsteSiProfesorulSedinteiNuSiORezerva()
    {
        // A professor listed among the students is skipped for their own sessions (RezervareService refuses it).
        var (sedinte, rezervari) = await Adauga(["irina"], ["irina"]);
        sedinte.ShouldBe(3);
        rezervari.ShouldBe(0);
    }
}

/// <summary><see cref="Seed.ReferintaAsync"/>: the price, the drawing styles and the themes that `seed` and the preview API add.</summary>
public sealed class SeedReferintaTests : TestCuHarness
{
    private Task<(int Interfete, int Teme, int Reguli, int Tarife)> Numara() => h.Db(async db =>
        (await db.Interfata.CountAsync(), await db.Backgrounds.CountAsync(), await db.ReguliBackground.CountAsync(), await db.TarifeFixe.CountAsync(t => t.Activ)));

    [Fact]
    public async Task AdaugaStiluriTemeSiTarif()
    {
        await h.Db(db => Seed.ReferintaAsync(db));
        (await Numara()).ShouldBe((3, 8, 8, 1));
        (await h.Db(db => db.Interfata.Select(i => i.Cod).OrderBy(c => c).ToListAsync())).ShouldBe(["insule", "pixel", "realist"]);
    }

    [Fact]
    public async Task ADouaOaraNuAdaugaNimic()
    {
        await h.Db(db => Seed.ReferintaAsync(db));
        await h.Db(db => Seed.ReferintaAsync(db));
        (await Numara()).ShouldBe((3, 8, 8, 1));
    }

    [Fact]
    public async Task UnTarifExistentRamane()
    {
        await h.Db(async db => { db.Add(new TarifFix { Suma = 95, Moneda = "RON", DurataMinute = 50, Activ = true }); await db.SaveChangesAsync(); });
        await h.Db(db => Seed.ReferintaAsync(db));
        (await h.Db(db => db.TarifeFixe.Where(t => t.Activ).Select(t => t.Suma).ToListAsync())).ShouldBe([95m]);
    }

    [Fact]
    public async Task UnStilSalvatEsteAcceptatDupaSeed()
    {
        await h.Db(db => Seed.ReferintaAsync(db));
        using var george = await h.Login("george");
        (await george.Put("/utilizatori/me/preferinte", new { interfata = "pixel", mod = "static", tema = "iarna-noapte" }).Json()).Str("interfata").ShouldBe("pixel");
    }
}


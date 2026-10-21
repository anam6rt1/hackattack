using System.Net;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using MarkAi.Tests.Suport;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Cautare;

/// <summary>
/// The professors list and profile (subject, free sessions, "within N days"), the fixed price, and the platform admin's list of
/// professors with the fee each one receives. Professors: irina (Română), ovidiu (Română), plus șerban (Matematică and Biologie).
/// </summary>
public sealed class ProfesoriListaTests : TestCautare
{
    private HttpClient serban = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        await h.Db(async db =>
        {
            var u = new Utilizator { Nume = "Șerban Țăranu", Username = "serban", Email = "serban@example.invalid", EmailConfirmatLa = DateTime.UtcNow,
                Telefon = "+40733000111", JudetId = Alba, LocalitateId = Barabant };
            u.ParolaHash = Parole.Hash(u, Harness.ParolaTest);
            u.Roluri.Add(new UtilizatorRol { RolId = await db.Roluri.Where(r => r.Nume == "profesor").Select(r => r.Id).SingleAsync() });
            u.ProfilMentor = new ProfilMentor { StareVerificare = "aprobat", ActivatLa = DateTime.UtcNow, Descriere = "Matematică și biologie, pas cu pas.",
                Materii = new List<MentorMaterie> { new() { MaterieId = 2 }, new() { MaterieId = 3 } } };
            db.Add(u);
            db.Add(new TarifFix { Suma = 80, Moneda = "RON", DurataMinute = 60, Activ = true });
            await db.SaveChangesAsync();
        });
        serban = await h.Login("serban");
    }

    private static object SlotMate(string titlu, DateTime cand, bool proBono = true, int capacitate = 3, int materieId = 2) =>
        new { materieId, titlu, inceputLa = cand, capacitate, proBono, linkIntalnire = "https://meet.example.invalid/mate" };

    [Fact]
    public async Task Tariful()
    {
        var t = await h.Anonim().GetJson("/tarif");
        (t.GetProperty("suma").GetDecimal(), t.Str("moneda"), t.Int("durataMinute")).ShouldBe((80m, "RON", 60));
    }

    [Fact]
    public async Task TarifulInactivNuEsteFolosit()
    {
        await h.Db(db => db.TarifeFixe.ExecuteUpdateAsync(s => s.SetProperty(t => t.Activ, false)));
        var corp = await (await h.Anonim().Get("/tarif").Status(HttpStatusCode.OK)).Content.ReadAsStringAsync(Ct);
        corp.ShouldBeOneOf("", "null");
    }

    [Fact]
    public async Task ListaAreDoarProfesoriiAprobati()
    {
        var (irina, s) = (await Id("irina"), await Id("serban"));
        await h.Db(db => db.ProfiluriMentor.Where(p => p.UtilizatorId == irina).ExecuteUpdateAsync(x => x.SetProperty(p => p.StareVerificare, "in_asteptare")));
        var ids = (await Profesori(h.Anonim())).Select(p => p.Id).ToList();
        ids.ShouldContain(s);
        ids.ShouldNotContain(irina);
        ids.ShouldNotContain(await Id("elena"));
    }

    [Theory]
    [InlineData(1, "irina")]
    [InlineData(2, "serban")]
    [InlineData(3, "serban")]
    public async Task FiltrulDeMaterie(int materieId, string prezent)
    {
        var id = await Id(prezent);
        (await Profesori(george, $"?materieId={materieId}")).ShouldContain(p => p.Id == id);
    }

    [Fact]
    public async Task MateriaFaraProfesoriDaListaGoala()
    {
        int altaMaterie = await h.Db(async db => { var m = new Materie { Denumire = "Filosofie" }; db.Add(m); await db.SaveChangesAsync(); return m.Id; });
        (await Profesori(george, $"?materieId={altaMaterie}")).ShouldBeEmpty();
    }

    [Fact]
    public async Task ProfesorulAreMateriileLui()
    {
        var p = (await h.Anonim().GetJson("/profesori")).Lista().Single(x => x.Str("nume") == "Șerban Țăranu");
        p.Lista("materii").Select(m => m.Str("denumire")).Order().ShouldBe(["Biologie", "Matematică"]);
    }

    [Fact]
    public async Task GratuitLasaDoarProfesoriiCuSedinteProBono()
    {
        await CreeazaSlot(serban, SlotMate("Gratuită", Maine));
        await CreeazaSlot(irina, Slot("Plătită", Maine, proBono: false));
        var s = await Id("serban");
        var gratuit = await Profesori(george, "?gratuit=true");
        gratuit.Select(p => p.Id).ShouldContain(s);
        gratuit.ShouldAllBe(p => p.Sloturi.Count > 0);
    }

    [Fact]
    public async Task ZileLasaDoarSedinteleApropiateCuLocuri()
    {
        await CreeazaSlot(serban, SlotMate("Peste o lună", DateTime.UtcNow.AddDays(30)));
        var s = await Id("serban");
        (await Profesori(george, "?zile=7")).ShouldNotContain(p => p.Id == s);
        (await Profesori(george, "?zile=40")).ShouldContain(p => p.Id == s);
    }

    [Fact]
    public async Task SedintaPlinaNuConteazaLaZile()
    {
        var plina = await CreeazaSlot(serban, SlotMate("Un loc", Maine, capacitate: 1));
        await elena.Post("/rezervari", new { slotId = plina }).Status(HttpStatusCode.Created);
        var s = await Id("serban");
        (await Profesori(radu, "?zile=7")).ShouldNotContain(p => p.Id == s);
        (await h.Anonim().GetJson("/profesori")).Lista().Single(p => p.Int("id") == s).Lista("sloturi").Single().Int("locuriLibere").ShouldBe(0);
    }

    [Fact]
    public async Task SedinteleTrecuteSiAnulateNuApar()
    {
        var anulata = await CreeazaSlot(serban, SlotMate("Anulată", Maine));
        await serban.Delete($"/profesori/me/sloturi/{anulata}").Status(HttpStatusCode.NoContent);
        var s = await Id("serban");
        await h.Db(async db =>
        {
            db.Add(new CameraTutoring { MentorId = s, MaterieId = 2, Titlu = "Ieri", Capacitate = 3, ProBono = true, Status = RezervareService.SlotDeschis,
                InceputLa = DateTime.UtcNow.AddDays(-1), FinalizatLa = DateTime.UtcNow.AddDays(-1).AddHours(1) });
            await db.SaveChangesAsync();
        });
        (await george.GetJson($"/profesori/{s}")).Lista("sloturi").ShouldBeEmpty();
    }

    [Fact]
    public async Task SedinteleSuntInOrdineCronologica()
    {
        await CreeazaSlot(serban, SlotMate("A treia", Maine.AddDays(3)));
        await CreeazaSlot(serban, SlotMate("Prima", Maine));
        await CreeazaSlot(serban, SlotMate("A doua", Maine.AddDays(1)));
        var s = await Id("serban");
        (await george.GetJson($"/profesori/{s}")).Lista("sloturi").Select(x => x.Str("titlu")).ShouldBe(["Prima", "A doua", "A treia"]);
    }

    [Fact]
    public async Task LinkulSeVedeDoarDupaRezervare()
    {
        var slot = await CreeazaSlot(serban, SlotMate("Cu link", Maine));
        var s = await Id("serban");
        (await george.GetJson($"/profesori/{s}")).Lista("sloturi").Single().EsteNull("linkIntalnire").ShouldBeTrue();
        await george.Post("/rezervari", new { slotId = slot }).Status(HttpStatusCode.Created);
        var dupa = (await george.GetJson($"/profesori/{s}")).Lista("sloturi").Single();
        dupa.Str("linkIntalnire").ShouldBe("https://meet.example.invalid/mate");
        dupa.Bool("rezervatDeMine").ShouldBeTrue();
        (await radu.GetJson($"/profesori/{s}")).Lista("sloturi").Single().EsteNull("linkIntalnire").ShouldBeTrue();
    }

    [Fact]
    public async Task ProfilulNeaprobatEste404()
    {
        var s = await Id("serban");
        await h.Db(db => db.ProfiluriMentor.Where(p => p.UtilizatorId == s).ExecuteUpdateAsync(x => x.SetProperty(p => p.StareVerificare, "respins")));
        await george.Get($"/profesori/{s}").Status(HttpStatusCode.NotFound);
        await george.Get("/profesori/99999").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ProfilulElevuluiNuEsteProfesor()
    {
        await george.Get($"/profesori/{await Id("elena")}").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SedintaPlatitaArePretulPlatformei()
    {
        var platita = await CreeazaSlot(serban, SlotMate("Plătită", Maine, proBono: false));
        var r = await elena.Post("/rezervari", new { slotId = platita }).Json(HttpStatusCode.Created);
        (r.GetProperty("tarifAchitat").GetDecimal(), r.Str("moneda")).ShouldBe((80m, "RON"));
    }

    [Fact]
    public async Task AdminulVedeProfesoriiCuMateriiSiOnorariu()
    {
        var lista = (await admin.GetJson("/admin/profesori")).Lista();
        var s = lista.Single(p => p.Str("username") == "serban");
        (s.Str("nume"), s.Str("email"), s.Str("telefon"), s.Str("stare")).ShouldBe(("Șerban Țăranu", "serban@example.invalid", "+40733000111", "aprobat"));
        s.Lista("materii").Count.ShouldBe(2);
        s.GetProperty("onorariuNegociat").GetDecimal().ShouldBe(0m);
    }

    [Theory]
    [InlineData("aprobat", true)]
    [InlineData("in_asteptare", false)]
    [InlineData("respins", false)]
    public async Task AdminulFiltreazaDupaStare(string stare, bool contineSerban)
    {
        (await admin.GetJson($"/admin/profesori?stare={stare}")).Lista().Any(p => p.Str("username") == "serban").ShouldBe(contineSerban);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(40.555)]
    [InlineData(80)]
    public async Task OnorariulInLimitaSeSalveaza(decimal onorariu)
    {
        var s = await Id("serban");
        await admin.Put($"/admin/profesori/{s}/onorariu", new { onorariuNegociat = onorariu }).Status(HttpStatusCode.NoContent);
        (await h.Db(db => db.ProfiluriMentor.Where(p => p.UtilizatorId == s).Select(p => p.OnorariuNegociat).SingleAsync())).ShouldBe(Math.Round(onorariu, 2));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(80.01)]
    [InlineData(1000)]
    public async Task OnorariulInAfaraLimiteiEsteRefuzat(decimal onorariu)
    {
        (await admin.Put($"/admin/profesori/{await Id("serban")}/onorariu", new { onorariuNegociat = onorariu }).Mesaj(HttpStatusCode.BadRequest))
            .ShouldContain("între 0 și tariful elevului (80");
    }

    [Fact]
    public async Task OnorariulPentruUnProfesorInexistentEste404()
    {
        await admin.Put("/admin/profesori/99999/onorariu", new { onorariuNegociat = 10 }).Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DoarAdminulSchimbaOnorariul()
    {
        await serban.Put($"/admin/profesori/{await Id("serban")}/onorariu", new { onorariuNegociat = 80 }).Status(HttpStatusCode.Forbidden);
        await george.Get("/admin/profesori").Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OnorariulApareInPanouSiLaCastiguri()
    {
        var s = await Id("serban");
        await admin.Put($"/admin/profesori/{s}/onorariu", new { onorariuNegociat = 50 });
        var platita = await CreeazaSlot(serban, SlotMate("Plătită", Maine, proBono: false));
        await elena.Post("/rezervari", new { slotId = platita }).Status(HttpStatusCode.Created);
        await radu.Post("/rezervari", new { slotId = platita }).Status(HttpStatusCode.Created);
        var panou = await serban.GetJson("/profesori/me");
        panou.GetProperty("onorariuNegociat").GetDecimal().ShouldBe(50m);
        panou.GetProperty("castiguri").GetProperty("total").GetDecimal().ShouldBe(100m);
        panou.GetProperty("castiguri").GetProperty("inAsteptare").GetDecimal().ShouldBe(100m);
        panou.GetProperty("pret").GetProperty("suma").GetDecimal().ShouldBe(80m);
    }

    [Fact]
    public async Task AnulareaScadeCastigurile()
    {
        await admin.Put($"/admin/profesori/{await Id("serban")}/onorariu", new { onorariuNegociat = 30 });
        var platita = await CreeazaSlot(serban, SlotMate("Plătită", Maine, proBono: false));
        var id = (await elena.Post("/rezervari", new { slotId = platita }).Json(HttpStatusCode.Created)).Int("id");
        await elena.Delete($"/rezervari/{id}").Status(HttpStatusCode.NoContent);
        (await serban.GetJson("/profesori/me")).GetProperty("castiguri").GetProperty("total").GetDecimal().ShouldBe(0m);
    }

    [Fact]
    public async Task ProfesorulNeaprobatNuArePanou()
    {
        var s = await Id("serban");
        await h.Db(db => db.ProfiluriMentor.Where(p => p.UtilizatorId == s).ExecuteUpdateAsync(x => x.SetProperty(p => p.StareVerificare, "in_asteptare")));
        await serban.Get("/profesori/me").Status(HttpStatusCode.Forbidden);
        await serban.Post("/profesori/me/sloturi", SlotMate("x", Maine)).Status(HttpStatusCode.Forbidden);
    }
}

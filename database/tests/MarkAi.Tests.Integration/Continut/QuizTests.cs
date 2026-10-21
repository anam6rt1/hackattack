using System.Net;
using System.Text.Json;
using MarkAi.Data.Entities;
using MarkAi.Tests.Suport;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Continut;

/// <summary>
/// The Lecție quiz: answers never leak before the end, passing needs 80% (4 of 5 with the 5-question setting used here), a pause
/// between attempts, XP once per Lecție; the Lecție counts as completed only after its quiz is passed.
/// </summary>
public sealed class QuizTests : TestCuHarness
{
    protected override IReadOnlyDictionary<string, string?> Setari => new Dictionary<string, string?>
    {
        ["Limite:IntrebariPeTest"] = "5", ["Limite:AutentificariPeMinut"] = "100",
    };

    private HttpClient admin = null!, elev = null!;
    private int lectie, capitol;
    private HashSet<int> corecte = [];

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        admin = await h.Login("admin");
        elev = await h.Login("george");
        lectie = await Harness.LectiePlatforma(admin);
        capitol = await Harness.Capitol(admin, lectie);
    }

    /// <summary>Hand-written multiple-choice questions; the first option is the right one.</summary>
    private async Task Intrebari(int numar, int? parcurs = null)
    {
        for (var i = 1; i <= numar; i++)
            await admin.Post($"/parcursuri/{parcurs ?? lectie}/intrebari", new
            {
                text = $"Întrebarea {i}", explicatie = $"Explicația {i}", activa = true,
                variante = new[] { new { text = "Răspunsul bun", corecta = true }, new { text = "Greșit 1", corecta = false }, new { text = "Greșit 2", corecta = false } },
            }).Status(HttpStatusCode.Created);
        corecte = (await h.Db(db => db.Set<VariantaIntrebare>().Where(o => o.Corecta).Select(o => o.Id).ToListAsync())).ToHashSet();
    }

    private Task<JsonElement> Incepe() => elev.Post($"/parcursuri/{lectie}/quiz").Json();

    private Task<HttpResponseMessage> Raspunde(int incercare, JsonElement intrebare, bool bine) =>
        elev.Post($"/parcursuri/{lectie}/quiz/{incercare}/raspuns", new { intrebareId = intrebare.Int("id"), variantaId = Varianta(intrebare, bine) });

    private int Varianta(JsonElement intrebare, bool bine) =>
        intrebare.Lista("variante").Select(v => v.Int("id")).First(v => corecte.Contains(v) == bine);

    /// <summary>Answers every question: the first <paramref name="bune"/> right, the rest wrong. Returns the final result.</summary>
    private async Task<JsonElement> Termina(JsonElement incercare, int bune)
    {
        JsonElement ultim = default;
        foreach (var (q, i) in incercare.Lista("intrebari").Select((q, i) => (q, i)))
            ultim = await Raspunde(incercare.Int("id"), q, i < bune).Json();
        return ultim.GetProperty("final");
    }

    [Fact]
    public async Task LectiaFaraIntrebariNuAreTest()
    {
        (await elev.Post($"/parcursuri/{lectie}/quiz").Mesaj(HttpStatusCode.Conflict)).ShouldContain("nu are încă întrebări");
    }

    [Fact]
    public async Task TestulVechiPeCapitolNuMaiExista()
    {
        (await elev.Post($"/cursuri/{capitol}/quiz")).StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task LectiaArataMarimeaTestuluiSiNiciunProgres()
    {
        await Intrebari(6);
        var d = await elev.GetJson($"/parcursuri/{lectie}");
        d.Int("intrebariTest").ShouldBe(6);
        d.EsteNull("progres").ShouldBeTrue();
    }

    [Fact]
    public async Task IncercareaAreCinciIntrebariSiCerePatru()
    {
        await Intrebari(6);
        var t = await Incepe();
        (t.Int("total"), t.Int("necesare"), t.Int("raspunse")).ShouldBe((5, 4, 0));
        t.Lista("intrebari").Count.ShouldBe(5);
    }

    [Fact]
    public async Task CuMaiPutineIntrebariTestulLeFolosestePeToate()
    {
        await Intrebari(3);
        var t = await Incepe();
        (t.Int("total"), t.Int("necesare")).ShouldBe((3, 3));
    }

    [Fact]
    public async Task IncercareaNuDezvaluieRaspunsurile()
    {
        await Intrebari(5);
        var text = await (await elev.Post($"/parcursuri/{lectie}/quiz")).Content.ReadAsStringAsync(Ct);
        text.ShouldNotContain("corect", Case.Insensitive);
        text.ShouldNotContain("explicati", Case.Insensitive);
    }

    [Fact]
    public async Task IntrebarileSuntDiferiteInAceeasiIncercare()
    {
        await Intrebari(6);
        var ids = (await Incepe()).Lista("intrebari").Select(q => q.Int("id")).ToList();
        ids.Distinct().Count().ShouldBe(ids.Count);
    }

    [Fact]
    public async Task RaspunsulGresitSpuneDoarCaEGresit()
    {
        await Intrebari(5);
        var t = await Incepe();
        var r = await Raspunde(t.Int("id"), t.Lista("intrebari")[0], bine: false).Status(HttpStatusCode.OK);
        var text = await r.Content.ReadAsStringAsync(Ct);
        var j = JsonDocument.Parse(text).RootElement;
        j.Bool("corect").ShouldBeFalse();
        j.Int("ramase").ShouldBe(4);
        j.EsteNull("final").ShouldBeTrue();
        text.ShouldNotContain("variantaCorecta");
    }

    [Fact]
    public async Task RaspunsulBunSpuneCaEBun()
    {
        await Intrebari(5);
        var t = await Incepe();
        (await Raspunde(t.Int("id"), t.Lista("intrebari")[0], bine: true).Json()).Bool("corect").ShouldBeTrue();
    }

    [Fact]
    public async Task OIntrebareNuPrimesteDouaRaspunsuri()
    {
        await Intrebari(5);
        var t = await Incepe();
        var q = t.Lista("intrebari")[0];
        await Raspunde(t.Int("id"), q, false);
        (await Raspunde(t.Int("id"), q, true).Mesaj(HttpStatusCode.Conflict)).ShouldContain("deja");
    }

    [Fact]
    public async Task IncercareaNeterminataSeReiaFaraIntrebarileRaspunse()
    {
        await Intrebari(5);
        var t = await Incepe();
        await Raspunde(t.Int("id"), t.Lista("intrebari")[0], false);
        var reluat = await Incepe();
        reluat.Int("id").ShouldBe(t.Int("id"));
        reluat.Lista("intrebari").Count.ShouldBe(4);
        reluat.Int("raspunse").ShouldBe(1);
    }

    [Fact]
    public async Task DupaTreizeciDeMinuteIncercareaNeterminataNuMaiSeReia()
    {
        await Intrebari(5);
        var t = await Incepe();
        h.Ceas.Advance(TimeSpan.FromMinutes(31));
        (await Incepe()).Int("id").ShouldNotBe(t.Int("id"));
    }

    [Fact]
    public async Task DouaDinCinciNuPromoveaza()
    {
        await Intrebari(5);
        var final = await Termina(await Incepe(), 2);
        final.Bool("promovat").ShouldBeFalse();
        (final.Int("corecte"), final.Int("total"), final.Int("necesare"), final.Int("xpCastigat")).ShouldBe((2, 5, 4, 0));
    }

    [Fact]
    public async Task TreiDinCinciNuPromoveaza()
    {
        await Intrebari(5);
        (await Termina(await Incepe(), 3)).Bool("promovat").ShouldBeFalse();
    }

    [Fact]
    public async Task LaFinalSeVadCorectiileSiExplicatiile()
    {
        await Intrebari(5);
        var final = await Termina(await Incepe(), 2);
        var corectii = final.Lista("corectii");
        corectii.Count.ShouldBe(5);
        corectii.ShouldAllBe(c => corecte.Contains(c.Int("variantaCorectaId")) && c.Str("explicatie").StartsWith("Explicația"));
    }

    [Fact]
    public async Task UnTestNouImediatEsteRefuzat()
    {
        await Intrebari(5);
        await Termina(await Incepe(), 2);
        (await elev.Post($"/parcursuri/{lectie}/quiz").Mesaj(HttpStatusCode.Conflict)).ShouldContain("secunde");
    }

    [Fact]
    public async Task DupaPauzaSePoateIncepeDinNou()
    {
        await Intrebari(5);
        var prima = await Incepe();
        await Termina(prima, 2);
        h.Ceas.Advance(TimeSpan.FromSeconds(61));
        (await Incepe()).Int("id").ShouldNotBe(prima.Int("id"));
    }

    [Fact]
    public async Task PauzaEsteSetabila()
    {
        await using var scurt = await Harness.Porneste(setari: new Dictionary<string, string?> { ["Limite:PauzaQuizSecunde"] = "5", ["Limite:IntrebariPeTest"] = "1" });
        using var a = await scurt.Login("admin");
        using var e = await scurt.Login("george");
        var l = await Harness.LectiePlatforma(a);
        await a.Post($"/parcursuri/{l}/intrebari", new { text = "Q", activa = true, variante = new[] { new { text = "A", corecta = true }, new { text = "B", corecta = false } } });
        var t = await e.Post($"/parcursuri/{l}/quiz").Json();
        var q = t.Lista("intrebari")[0];
        await e.Post($"/parcursuri/{l}/quiz/{t.Int("id")}/raspuns", new { intrebareId = q.Int("id"), variantaId = q.Lista("variante")[1].Int("id") });
        scurt.Ceas.Advance(TimeSpan.FromSeconds(6));
        await e.Post($"/parcursuri/{l}/quiz").Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatruDinCinciPromoveazaSiDa100Xp()
    {
        await Intrebari(5);
        var final = await Termina(await Incepe(), 4);
        final.Bool("promovat").ShouldBeTrue();
        final.Int("xpCastigat").ShouldBe(100);
        final.GetProperty("promovatLa").ValueKind.ShouldBe(JsonValueKind.String);
    }

    [Fact]
    public async Task PromovareaTermineLectia()
    {
        await Intrebari(5);
        await elev.Post($"/cursuri/{capitol}/acces").Status(HttpStatusCode.NoContent);
        await Termina(await Incepe(), 5);
        var d = await elev.GetJson($"/parcursuri/{lectie}");
        d.GetProperty("progres").GetProperty("promovatLa").ValueKind.ShouldBe(JsonValueKind.String);
        var progres = await elev.GetJson("/progres");
        progres.Lista("lectiiCompletate").ShouldContain(l => l.Int("parcursId") == lectie);
        progres.GetProperty("ultimaLectie").Int("cursId").ShouldBe(capitol);
    }

    [Fact]
    public async Task DeschidereaUnuiCapitolNuTermineLectia()
    {
        await Intrebari(5);
        await elev.Post($"/cursuri/{capitol}/acces").Status(HttpStatusCode.NoContent);
        var p = (await elev.GetJson($"/parcursuri/{lectie}")).GetProperty("progres");
        p.Int("ultimulCursId").ShouldBe(capitol);
        p.EsteNull("promovatLa").ShouldBeTrue();
    }

    [Fact]
    public async Task XpSeDaODataPeLectie()
    {
        await Intrebari(5);
        await Termina(await Incepe(), 5);
        h.Ceas.Advance(TimeSpan.FromSeconds(61));
        var final = await Termina(await Incepe(), 5);
        final.Bool("promovat").ShouldBeTrue();
        final.Int("xpCastigat").ShouldBe(0);
        (await h.Db(db => db.EvenimenteRecompensa.CountAsync(e => e.Utilizator.Username == "george"))).ShouldBe(1);
    }

    [Fact]
    public async Task DataPromovariiRamaneCeaDintai()
    {
        await Intrebari(5);
        var prima = (await Termina(await Incepe(), 5)).Str("promovatLa");
        h.Ceas.Advance(TimeSpan.FromSeconds(61));
        var a2 = await Termina(await Incepe(), 5);
        a2.Str("promovatLa").ShouldBe(prima);
    }

    [Fact]
    public async Task UnPicajDupaPromovareNuStergeProgresul()
    {
        await Intrebari(5);
        await Termina(await Incepe(), 5);
        h.Ceas.Advance(TimeSpan.FromSeconds(61));
        await Termina(await Incepe(), 0);
        (await elev.GetJson($"/parcursuri/{lectie}")).GetProperty("progres").GetProperty("promovatLa").ValueKind.ShouldBe(JsonValueKind.String);
    }

    [Fact]
    public async Task AltContNuPoateRaspundeLaIncercare()
    {
        await Intrebari(5);
        var t = await Incepe();
        using var altElev = await h.InregistreazaElev("ana");
        var q = t.Lista("intrebari")[0];
        await altElev.Post($"/parcursuri/{lectie}/quiz/{t.Int("id")}/raspuns", new { intrebareId = q.Int("id"), variantaId = Varianta(q, true) })
            .Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DoarEleviiDauTestul()
    {
        await Intrebari(5);
        await admin.Post($"/parcursuri/{lectie}/quiz").Status(HttpStatusCode.Forbidden);
        using var irina = await h.Login("irina");
        await irina.Post($"/parcursuri/{lectie}/quiz").Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task IntrebareaDinAltTestEsteRefuzata()
    {
        await Intrebari(6);
        var t = await Incepe();
        var inTest = t.Lista("intrebari").Select(q => q.Int("id")).ToHashSet();
        var alta = await h.Db(db => db.Set<Intrebare>().Where(q => !inTest.Contains(q.Id)).Select(q => q.Id).FirstAsync());
        var varianta = await h.Db(db => db.Set<VariantaIntrebare>().Where(v => v.IntrebareId == alta).Select(v => v.Id).FirstAsync());
        (await elev.Post($"/parcursuri/{lectie}/quiz/{t.Int("id")}/raspuns", new { intrebareId = alta, variantaId = varianta })
            .Mesaj(HttpStatusCode.BadRequest)).ShouldContain("nu face parte");
    }

    [Fact]
    public async Task VariantaAlteiIntrebariEsteRefuzata()
    {
        await Intrebari(5);
        var t = await Incepe();
        var (q1, q2) = (t.Lista("intrebari")[0], t.Lista("intrebari")[1]);
        (await elev.Post($"/parcursuri/{lectie}/quiz/{t.Int("id")}/raspuns", new { intrebareId = q1.Int("id"), variantaId = q2.Lista("variante")[0].Int("id") })
            .Mesaj(HttpStatusCode.BadRequest)).ShouldContain("nu aparține");
    }

    [Fact]
    public async Task TestulTerminatNuMaiPrimesteRaspunsuri()
    {
        await Intrebari(5);
        var t = await Incepe();
        await Termina(t, 5);
        (await Raspunde(t.Int("id"), t.Lista("intrebari")[0], true).Mesaj(HttpStatusCode.Conflict)).ShouldContain("s-a încheiat");
    }

    [Fact]
    public async Task IntrebarileInactiveNuIntraInTest()
    {
        await Intrebari(5);
        var prima = await h.Db(db => db.Set<Intrebare>().OrderBy(q => q.Id).FirstAsync());
        await admin.Put($"/intrebari/{prima.Id}", new { text = prima.Text, activa = false, variante = (object?)null }).Status(HttpStatusCode.OK);
        var t = await Incepe();
        t.Int("total").ShouldBe(4);
        t.Lista("intrebari").ShouldNotContain(q => q.Int("id") == prima.Id);
    }

    [Fact]
    public async Task EstimarileNuIntraInTest()
    {
        await Intrebari(2);
        await admin.Post($"/parcursuri/{lectie}/intrebari", new { text = "În ce an?", tip = "estimare", raspunsNumeric = 1883, activa = true }).Status(HttpStatusCode.Created);
        (await Incepe()).Int("total").ShouldBe(2);
    }

    [Fact]
    public async Task LectiaNepublicataNuAreTest()
    {
        await Intrebari(5);
        await h.Db(db => db.Set<Parcurs>().Where(p => p.Id == lectie).ExecuteUpdateAsync(s => s.SetProperty(p => p.Publicat, false)));
        await elev.Post($"/parcursuri/{lectie}/quiz").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LectiaDoarPentruOrganizatieNuAreTestPentruCeiDinAfara()
    {
        using var irina = await h.Login("irina");
        var r = await irina.Post("/parcursuri", new { materieId = 1, titlu = "Doar liceu", clasa = "Bac", vizibilitate = "organizatie", organizatieId = Harness.IdLiceu })
            .Json(HttpStatusCode.Created);
        var privata = r.Int("id");
        await irina.Post($"/parcursuri/{privata}/intrebari", new { text = "Q", activa = true, variante = new[] { new { text = "A", corecta = true }, new { text = "B", corecta = false } } })
            .Status(HttpStatusCode.Created);
        await elev.Post($"/parcursuri/{privata}/quiz").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task IesireaDinOrganizatieOpresteIncercareaInCurs()
    {
        using var director = await h.Login("director");
        using var ana = await h.InregistreazaElev("ana");
        await ana.Post("/utilizatori/me/organizatii", new { organizatieId = Harness.IdLiceu });
        var anaId = (await ana.GetJson("/utilizatori/me")).Int("id");
        await director.Post($"/organizatii/{Harness.IdLiceu}/elevi/{anaId}/aprobare").Status(HttpStatusCode.NoContent);
        using var irina = await h.Login("irina");
        var privata = (await irina.Post("/parcursuri", new { materieId = 1, titlu = "Doar liceu", clasa = "Bac", vizibilitate = "organizatie", organizatieId = Harness.IdLiceu })
            .Json(HttpStatusCode.Created)).Int("id");
        await irina.Post($"/parcursuri/{privata}/intrebari", new { text = "Q", activa = true, variante = new[] { new { text = "A", corecta = true }, new { text = "B", corecta = false } } });
        var t = await ana.Post($"/parcursuri/{privata}/quiz").Json();
        await ana.Delete($"/utilizatori/me/organizatii/{Harness.IdLiceu}").Status(HttpStatusCode.OK);
        var q = t.Lista("intrebari")[0];
        await ana.Post($"/parcursuri/{privata}/quiz/{t.Int("id")}/raspuns", new { intrebareId = q.Int("id"), variantaId = q.Lista("variante")[0].Int("id") })
            .Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ScorulVineDinRaspunsurileSalvate()
    {
        await Intrebari(5);
        var t = await Incepe();
        await Termina(t, 4);
        var incercare = await h.Db(db => db.Set<IncercareQuiz>().SingleAsync(i => i.Id == t.Int("id")));
        incercare.Corecte.ShouldBe(4);
        incercare.FinalizataLa.ShouldNotBeNull();
        incercare.Raspunsuri.ShouldAllBe(r => r != 0);
    }
}

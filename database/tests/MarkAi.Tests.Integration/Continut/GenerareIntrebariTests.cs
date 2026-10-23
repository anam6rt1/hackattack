using System.Net;
using System.Text.Json;
using MarkAi.Api.Servicii;
using MarkAi.Tests.Suport;

namespace MarkAi.Tests.Integration.Continut;

/// <summary>
/// AI questions only on request: N questions are added to the Lecție's quiz from its chapters and files, the model sees the existing
/// ones, a bad reply gives a clear message, and spending is capped per account and for the platform (questions asked in 24 h).
/// </summary>
public sealed class GenerareIntrebariTests : TestCuHarness
{
    protected override IReadOnlyDictionary<string, string?> Setari => new Dictionary<string, string?>
    {
        ["Limite:IntrebariAiPeZi"] = "30", ["Limite:IntrebariAiPeZiTotal"] = "50", ["Limite:CereriAiPeMinut"] = "100",
        ["Limite:AutentificariPeMinut"] = "100",
    };

    private HttpClient admin = null!;
    private int lectie;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        admin = await h.Login("admin");
        lectie = await Harness.LectiePlatforma(admin);
        await Harness.Capitol(admin, lectie);
    }

    // Every question the fake model writes is about another fact (a counter), so only deliberate repeats look alike.
    private static int fapt;
    private static string Fapt() => $"faptul{Interlocked.Increment(ref fapt)}";

    private static string Raspuns(int grile, int estimari = 0, string prefix = "Întrebarea") => JsonSerializer.Serialize(new
    {
        grila = Enumerable.Range(1, grile).Select(i => new { text = $"{prefix} {i}: {Fapt()}", variante = new[] { "A", "B", "C", "D" }, corect = i % 4, explicatie = $"Explicația {i}" }),
        estimari = Enumerable.Range(1, estimari).Select(i => new { text = $"Estimare {i}: {Fapt()}", raspuns = 1880 + i }),
    });

    private Task<HttpResponseMessage> Cere(int numar, HttpClient? c = null, int? parcurs = null) =>
        (c ?? admin).Post($"/parcursuri/{parcurs ?? lectie}/genereaza-intrebari", new { numar });

    private Task Ruleaza() => h.Serviciu<GeneratorIntrebari>().RuleazaOdataAsync(Ct);
    private Task<JsonElement> Stare() => admin.GetJson($"/parcursuri/{lectie}/generare");
    private async Task<List<JsonElement>> Intrebari() => (await admin.GetJson($"/continut/parcursuri/{lectie}/intrebari")).Lista();

    [Fact]
    public async Task CapitolulNouNuGenereazaNimic()
    {
        (await Stare()).Str("stare").ShouldBe("necunoscuta");
        h.Ai.Apeluri.ShouldBeEmpty();
    }

    [Fact]
    public async Task CerereaEsteAcceptataSiAsteapta()
    {
        await Cere(3).Status(HttpStatusCode.Accepted);
        (await Stare()).Str("stare").ShouldBe("in_asteptare");
        h.Ai.Apeluri.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerareaAdaugaIntrebarile()
    {
        h.Ai.Raspunde = (_, _, _) => Raspuns(3);
        await Cere(3);
        await Ruleaza();
        var s = await Stare();
        (s.Str("stare"), s.Int("intrebariGenerate")).ShouldBe(("gata", 3));
        (await Intrebari()).Count(q => q.Str("sursa") == "ai").ShouldBe(3);
    }

    [Fact]
    public async Task VariantaCorectaEsteCeaIndicataDeModel()
    {
        h.Ai.Raspunde = (_, _, _) => Raspuns(1);
        await Cere(1);
        await Ruleaza();
        var q = (await Intrebari()).Single();
        q.Lista("variante").Select(v => v.Bool("corecta")).ShouldBe([false, true, false, false]);
        q.Str("explicatie").ShouldBe("Explicația 1");
    }

    [Fact]
    public async Task ModelulPrimesteMaterialulLectiei()
    {
        h.Ai.Raspunde = (_, _, _) => Raspuns(1);
        await Cere(1);
        await Ruleaza();
        h.Ai.Apeluri.TryPeek(out var apel).ShouldBeTrue();
        apel.Scop.ShouldBe(ScopAi.Intrebari);
        apel.Mesaje[0].Text.ShouldContain("1883");
        apel.Mesaje[0].Text.ShouldContain("Lecție de test");
        // A few more than asked, so a near-duplicate can be dropped; only the asked number is added.
        apel.Mesaje[0].Text.ShouldContain("Scrie 2 întrebări grilă");
        (await Intrebari()).Count(q => q.Str("tip") == "grila").ShouldBe(1);
    }

    [Fact]
    public async Task ModelulPrimesteToateCapitolele()
    {
        await Harness.Capitol(admin, lectie, "Scrisoarea I", "Scrisoarea I a apărut în 1881.");
        h.Ai.Raspunde = (_, _, _) => Raspuns(1);
        await Cere(1);
        await Ruleaza();
        h.Ai.Apeluri.TryPeek(out var apel);
        apel.Mesaje[0].Text.ShouldContain("1881");
        apel.Mesaje[0].Text.ShouldContain("Capitol: Scrisoarea I");
    }

    [Fact]
    public async Task ModelulVedeIntrebarileExistente()
    {
        h.Ai.Raspunde = (_, _, _) => Raspuns(2, prefix: "Prima tură");
        await Cere(2);
        await Ruleaza();
        h.Ai.Raspunde = (_, _, _) => Raspuns(2, prefix: "A doua tură");
        await Cere(2);
        await Ruleaza();
        h.Ai.Apeluri.Last().Mesaje[0].Text.ShouldContain("- Prima tură 1");
        (await Intrebari()).Count.ShouldBe(4);
    }

    [Fact]
    public async Task IntrebarileSeAdaugaNuSeInlocuiesc()
    {
        await admin.Post($"/parcursuri/{lectie}/intrebari", new { text = "Scrisă de mână", activa = true, variante = new[] { new { text = "A", corecta = true }, new { text = "B", corecta = false } } });
        h.Ai.Raspunde = (_, _, _) => Raspuns(2);
        await Cere(2);
        await Ruleaza();
        var toate = await Intrebari();
        toate.Count.ShouldBe(3);
        toate.ShouldContain(q => q.Str("text") == "Scrisă de mână" && q.Str("sursa") == "manual");
    }

    [Fact]
    public async Task ModelulCareDaMaiMulteEsteTaiatLaNumarulCerut()
    {
        h.Ai.Raspunde = (_, _, _) => Raspuns(8);
        await Cere(3);
        await Ruleaza();
        (await Intrebari()).Count(q => q.Str("tip") == "grila").ShouldBe(3);
    }

    [Fact]
    public async Task EstimarileSeCompleteazaPanaLaDoua()
    {
        h.Ai.Raspunde = (_, _, _) => Raspuns(1, estimari: 5);
        await Cere(1);
        await Ruleaza();
        (await Intrebari()).Count(q => q.Str("tip") == "estimare").ShouldBe(2);
        await Cere(1);
        await Ruleaza();
        (await Intrebari()).Count(q => q.Str("tip") == "estimare").ShouldBe(2);
        h.Ai.Apeluri.Last().Mesaje[0].Text.ShouldContain("cel mult 0 întrebări de estimare");
    }

    [Fact]
    public async Task RaspunsulGolDaUnMesajClar()
    {
        h.Ai.Raspunde = (_, _, _) => "{}";
        await Cere(2);
        await Ruleaza();
        var s = await Stare();
        s.Str("stare").ShouldBe("eroare");
        s.Str("mesaj").ShouldContain("nicio întrebare validă");
        h.Jurnal.Linii.ShouldNotContain(l => l.Contains("ArgumentNullException"));
    }

    [Fact]
    public async Task IntrebarileInvalideSuntIgnorate()
    {
        h.Ai.Raspunde = (_, _, _) => JsonSerializer.Serialize(new
        {
            grila = new object[]
            {
                new { text = "Trei variante", variante = new[] { "A", "B", "C" }, corect = 0, explicatie = "x" },
                new { text = "Index greșit", variante = new[] { "A", "B", "C", "D" }, corect = 4, explicatie = "x" },
                new { text = "  ", variante = new[] { "A", "B", "C", "D" }, corect = 0, explicatie = "x" },
                new { text = "Variantă goală", variante = new[] { "A", "", "C", "D" }, corect = 0, explicatie = "x" },
                new { text = "Bună", variante = new[] { "A", "B", "C", "D" }, corect = 2, explicatie = "x" },
            },
            estimari = Array.Empty<object>(),
        });
        await Cere(5);
        await Ruleaza();
        (await Intrebari()).Single().Str("text").ShouldBe("Bună");
    }

    [Fact]
    public async Task FaraMaterialGenerareaSpuneDeCe()
    {
        var goala = await Harness.LectiePlatforma(admin, titlu: "Goală");
        await Cere(1, parcurs: goala);
        await Ruleaza();
        (await admin.GetJson($"/parcursuri/{goala}/generare")).Str("mesaj").ShouldContain("nu are materiale");
        h.Ai.Apeluri.ShouldBeEmpty();
    }

    [Fact]
    public async Task FaraModelAiGenerareaSpuneCaNuEConfigurata()
    {
        await using var faraAi = await Harness.Porneste(faraAi: true);
        using var a = await faraAi.Login("admin");
        var l = await Harness.LectiePlatforma(a);
        await Harness.Capitol(a, l);
        await a.Post($"/parcursuri/{l}/genereaza-intrebari", new { numar = 1 });
        await faraAi.Serviciu<GeneratorIntrebari>().RuleazaOdataAsync(Ct);
        (await a.GetJson($"/parcursuri/{l}/generare")).Str("mesaj").ShouldContain("nu este configurată");
        faraAi.Jurnal.Linii.ShouldContain(l => l.Contains("No AI key"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    [InlineData(-3)]
    public async Task NumarulTrebuieSaFieIntre1Si20(int numar)
    {
        (await Cere(numar).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("între 1 și 20");
    }

    [Fact]
    public async Task OADouaCerereInTimpCeAPrimaAsteaptaEsteRefuzata()
    {
        await Cere(1).Status(HttpStatusCode.Accepted);
        (await Cere(1).Mesaj(HttpStatusCode.Conflict)).ShouldContain("se generează deja");
    }

    [Fact]
    public async Task BugetulArataCateMaiRaman()
    {
        (await admin.GetJson("/continut/buget-ai")).Int("ramase").ShouldBe(30);
        await Cere(12);
        var b = await admin.GetJson("/continut/buget-ai");
        (b.Int("ramase"), b.Int("peZi")).ShouldBe((18, 30));
    }

    [Fact]
    public async Task PesteBugetCerereaSpuneCateMaiRaman()
    {
        await Cere(20);
        await Ruleaza();
        (await Cere(11).Mesaj(HttpStatusCode.Conflict)).ShouldContain("Îți mai rămân 10");
    }

    [Fact]
    public async Task LaZeroCerereaEsteRefuzata()
    {
        await Cere(20); await Ruleaza();
        await Cere(10); await Ruleaza();
        (await admin.GetJson("/continut/buget-ai")).Int("ramase").ShouldBe(0);
        (await Cere(1).Mesaj(HttpStatusCode.Conflict)).ShouldContain("Ai folosit întrebările AI");
    }

    [Fact]
    public async Task CerereaRefuzataNuConsumaBugetul()
    {
        await Cere(25);
        (await admin.GetJson("/continut/buget-ai")).Int("ramase").ShouldBe(30);
    }

    [Fact]
    public async Task GenerareaEsuataTotConsumaBugetul()
    {
        // The model was called (and may have been paid for) even when its reply is unusable.
        h.Ai.Raspunde = (_, _, _) => "{}";
        await Cere(5);
        await Ruleaza();
        (await admin.GetJson("/continut/buget-ai")).Int("ramase").ShouldBe(25);
    }

    [Fact]
    public async Task BugetulEstePeCont()
    {
        await Cere(20); await Ruleaza();
        using var irina = await h.Login("irina");
        (await irina.GetJson("/continut/buget-ai")).Int("ramase").ShouldBe(30);
    }

    [Fact]
    public async Task PlafonulPlatformeiLimiteazaToateConturile()
    {
        using var irina = await h.Login("irina");
        await Cere(20); await Ruleaza();
        await Cere(10, parcurs: await Harness.LectiePlatforma(admin, titlu: "A doua")); await Ruleaza();
        // 30 of the platform's 50 are spent: irina still has 30 of her own, but only 20 are left overall.
        (await irina.GetJson("/continut/buget-ai")).Int("ramase").ShouldBe(20);
    }

    [Fact]
    public async Task ElevulNuPoateGenera()
    {
        using var george = await h.Login("george");
        await Cere(1, george).Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ProfesorulNuPoateGeneraPentruLectiaAltcuiva()
    {
        using var irina = await h.Login("irina");
        (await Cere(1, irina)).StatusCode.ShouldBeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        (await irina.GetJson("/continut/buget-ai")).Int("ramase").ShouldBe(30);
    }

    [Fact]
    public async Task LimitaDeCereriAiPeMinut()
    {
        await using var strict = await Harness.Porneste(setari: new Dictionary<string, string?> { ["Limite:CereriAiPeMinut"] = "2" });
        using var a = await strict.Login("admin");
        var ids = new List<int>();
        for (var i = 0; i < 3; i++) ids.Add(await Harness.LectiePlatforma(a, titlu: $"L{i}"));
        await a.Post($"/parcursuri/{ids[0]}/genereaza-intrebari", new { numar = 1 });
        await a.Post($"/parcursuri/{ids[1]}/genereaza-intrebari", new { numar = 1 });
        (await a.Post($"/parcursuri/{ids[2]}/genereaza-intrebari", new { numar = 1 })).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    // ---- plan 2, phase D: what the model proposes is checked before it is added

    private static string RaspunsCu(params string[] texte) => JsonSerializer.Serialize(new
    {
        grila = texte.Select((t, i) => new { text = t, variante = new[] { "A", "B", "C", "D" }, corect = i % 4, explicatie = "De ce." }),
        estimari = Array.Empty<object>(),
    });

    private async Task<List<string>> TexteGrila() =>
        (await Intrebari()).Where(q => q.Str("tip") == "grila").Select(q => q.Str("text")).ToList();

    [Fact]
    public async Task ModelulPrimesteRegulileDeCalitate()
    {
        h.Ai.Raspunde = (_, _, _) => Raspuns(1);
        await Cere(1);
        await Ruleaza();
        h.Ai.Apeluri.TryPeek(out var apel);
        apel.Sistem.ShouldContain("despre materie, nu despre material");
        apel.Sistem.ShouldContain("Fiecare întrebare verifică o altă idee");
        apel.Sistem.ShouldContain("Nu repeta și nu reformula");
    }

    [Fact]
    public async Task CerereaMareAreCatevaInPlus()
    {
        h.Ai.Raspunde = (_, _, _) => Raspuns(30);
        await Cere(20);
        await Ruleaza();
        h.Ai.Apeluri.Last().Mesaje[0].Text.ShouldContain("Scrie 30 întrebări grilă");
        (await TexteGrila()).Count.ShouldBe(20);
        (await Stare()).EsteNull("mesaj").ShouldBeTrue();
    }

    [Fact]
    public async Task OIntrebareCaCeaExistentaEsteInlocuitaDeUnaInPlus()
    {
        await admin.Post($"/parcursuri/{lectie}/intrebari", new { text = "În ce an a apărut poemul „Luceafărul”?", activa = true,
            variante = new[] { new { text = "1883", corecta = true }, new { text = "1870", corecta = false } } });
        h.Ai.Raspunde = (_, _, _) => RaspunsCu("Poemul Luceafărul a apărut în ce an?", "Cine este Hyperion?", "Cine este Cătălina?");
        await Cere(2);
        await Ruleaza();
        (await TexteGrila()).ShouldBe(new[] { "În ce an a apărut poemul „Luceafărul”?", "Cine este Hyperion?", "Cine este Cătălina?" }, ignoreOrder: true);
        var s = await Stare();
        (s.Str("stare"), s.Int("intrebariGenerate")).ShouldBe(("gata", 2));
        s.EsteNull("mesaj").ShouldBeTrue();
    }

    [Fact]
    public async Task RepetareaDinAcelasiLotEsteScoasa()
    {
        h.Ai.Raspunde = (_, _, _) => RaspunsCu("Cine este Hyperion în „Luceafărul”?", "În „Luceafărul”, cine este Hyperion?", "Cine este Cătălina?");
        await Cere(2);
        await Ruleaza();
        (await TexteGrila()).ShouldBe(["Cine este Hyperion în „Luceafărul”?", "Cine este Cătălina?"], ignoreOrder: true);
    }

    [Fact]
    public async Task IntrebarileDespreMaterialSuntScoase()
    {
        h.Ai.Raspunde = (_, _, _) => RaspunsCu("Cum se numește capitolul despre Luceafărul?", "Conform materialului, cine este Hyperion?", "Cine este Hyperion?");
        await Cere(1);
        await Ruleaza();
        (await TexteGrila()).ShouldBe(["Cine este Hyperion?"]);
    }

    [Fact]
    public async Task CandRamanMaiPutineMesajulSpuneDeCe()
    {
        await admin.Post($"/parcursuri/{lectie}/intrebari", new { text = "Cine este Cătălina?", activa = true,
            variante = new[] { new { text = "A", corecta = true }, new { text = "B", corecta = false } } });
        h.Ai.Raspunde = (_, _, _) => RaspunsCu("Cine este Hyperion?", "CINE ESTE CATALINA", "Din material: ce este romantismul?", "Cum se numește capitolul 1?");
        await Cere(3);
        await Ruleaza();
        var s = await Stare();
        (s.Str("stare"), s.Int("intrebariGenerate")).ShouldBe(("gata", 1));
        s.Str("mesaj").ShouldBe("Au fost adăugate 1 din 3 cerute: dintre cele propuse, una semăna prea mult cu o întrebare existentă, iar 2 erau despre "
            + "material, nu despre materie. Poți genera din nou sau scrie restul manual.");
    }

    [Fact]
    public async Task CandModelulDaMaiPutineFaraRepetariMesajulSpuneAsta()
    {
        h.Ai.Raspunde = (_, _, _) => RaspunsCu("Cine este Hyperion?");
        await Cere(3);
        await Ruleaza();
        (await Stare()).Str("mesaj").ShouldBe("Au fost adăugate 1 din 3 cerute: modelul AI n-a propus mai multe valide. Poți genera din nou sau scrie restul manual.");
    }

    [Fact]
    public async Task CandToateSuntRepetariEsteOEroareClara()
    {
        h.Ai.Raspunde = (_, _, _) => RaspunsCu("Cine este Hyperion?");
        await Cere(1);
        await Ruleaza();
        await Cere(1);
        await Ruleaza();
        var s = await Stare();
        s.Str("stare").ShouldBe("eroare");
        s.Str("mesaj").ShouldContain("semănau prea mult cu cele existente");
        (await TexteGrila()).ShouldBe(["Cine este Hyperion?"]);
    }

    [Fact]
    public async Task EstimarileRepetateSuntScoase()
    {
        string Estimari(params string[] texte) => JsonSerializer.Serialize(new
        {
            grila = new[] { new { text = $"Cine a scris {Fapt()}?", variante = new[] { "A", "B", "C", "D" }, corect = 0, explicatie = "x" } },
            estimari = texte.Select(t => new { text = t, raspuns = 1883 }),
        });
        h.Ai.Raspunde = (_, _, _) => Estimari("În ce an a apărut „Luceafărul”?", "Luceafărul a apărut în ce an?");
        await Cere(1);
        await Ruleaza();
        (await Intrebari()).Count(q => q.Str("tip") == "estimare").ShouldBe(1);
    }

    [Fact]
    public async Task BugetulFolosesteCeasulAplicatiei()
    {
        // At most 20 a request: the day's 30 as 20 + 10.
        await Cere(20).Status(HttpStatusCode.Accepted);
        await Ruleaza();
        await Cere(10).Status(HttpStatusCode.Accepted);
        (await admin.GetJson("/continut/buget-ai")).Int("ramase").ShouldBe(0);
        h.Ceas.Advance(TimeSpan.FromHours(23));
        (await admin.GetJson("/continut/buget-ai")).Int("ramase").ShouldBe(0);
        h.Ceas.Advance(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(1));
        (await admin.GetJson("/continut/buget-ai")).Int("ramase").ShouldBe(30);
    }
}

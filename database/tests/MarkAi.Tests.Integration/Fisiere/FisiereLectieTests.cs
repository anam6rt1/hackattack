using System.Net;
using System.Security.Cryptography;
using MarkAi.Api.Servicii;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using static MarkAi.Tests.Suport.DocumenteTest;

namespace MarkAi.Tests.Integration.Fisiere;

/// <summary>
/// One file per type with a confirmed replacement, background text extraction, who may download (outsiders get 404),
/// inline PDF vs downloaded Office files, and cleanup on disk (replacement, deletion, Lecție deletion, orphan sweep).
/// </summary>
public sealed class FisiereLectieTests : TestFisiere
{
    [Fact]
    public async Task AutorulIncarcaUnPdf()
    {
        var f = await Incarca(irina, Pdf(), "curs.pdf").Json(HttpStatusCode.Created);
        (f.Str("nume"), f.Str("tip"), f.Str("stareText"), f.Str("incarcatDe")).ShouldBe(("curs.pdf", "pdf", "in_asteptare", "Irina Marin"));
    }

    [Fact]
    public async Task NumeleFisieruluiEsteCuratat()
    {
        (await Incarca(irina, Pdf(), "../../etc/Curs: Eminescu*?.pdf").Json(HttpStatusCode.Created)).Str("nume").ShouldBe("Curs Eminescu.pdf");
    }

    [Fact]
    public async Task AdministratorulOrganizatieiIncarca()
    {
        await IncarcaId(director, Docx(), "Ștefan.docx");
    }

    [Fact]
    public async Task ShaUlContinutuluiEsteSalvat()
    {
        var octeti = Pdf();
        var id = await IncarcaId(irina, octeti, "a.pdf");
        (await Rand(id)).Sha256.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(octeti)));
    }

    [Fact]
    public async Task UnFisierDinFiecareTip()
    {
        await IncarcaId(irina, Pdf(), "a.pdf");
        await IncarcaId(irina, Docx(), "a.docx");
        await IncarcaId(irina, Pptx(), "a.pptx");
        (await elena.GetJson($"/parcursuri/{lectie}/fisiere")).Int("total").ShouldBe(3);
    }

    [Fact]
    public async Task AlDoileaPdfFaraConfirmareEsteRefuzat()
    {
        await IncarcaId(irina, Pdf(), "a.pdf");
        (await Incarca(irina, Pdf("Alt PDF"), "alt.pdf").Mesaj(HttpStatusCode.Conflict)).ShouldContain("deja un fișier PDF");
        Stocare.Chei(Stocare.DirectorLectii).Count().ShouldBe(1);
    }

    [Fact]
    public async Task CuConfirmareNoulPdfIlInlocuiestePeCelVechi()
    {
        var vechi = await IncarcaId(irina, Pdf(), "a.pdf");
        var cheie = await Cheie(vechi);
        var nou = (await Incarca(irina, Pdf("Nou"), "nou.pdf", inlocuieste: true).Json(HttpStatusCode.Created)).Int("id");
        File.Exists(Stocare.CaleFisier(cheie)).ShouldBeTrue(); // Shared blobs are collected after the grace period.
        (await h.Db(db => db.Set<FisierLectie>().Where(f => f.ParcursId == lectie && f.Tip == "pdf").Select(f => f.Id).ToListAsync())).ShouldBe([nou]);
        await elena.Get($"/fisiere/{vechi}").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InlocuireaRefuzataPastreazaFisierulVechi()
    {
        var vechi = await IncarcaId(irina, Pdf(), "a.pdf");
        await Incarca(irina, Exe, "rau.pdf", inlocuieste: true).Status(HttpStatusCode.BadRequest);
        File.Exists(Stocare.CaleFisier(await Cheie(vechi))).ShouldBeTrue();
        await elena.Get($"/fisiere/{vechi}").Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TextulPdfEsteExtras()
    {
        var id = await IncarcaId(irina, Pdf(), "a.pdf");
        await Procesor.RuleazaOdataAsync(Ct);
        var f = await Rand(id);
        f.StareText.ShouldBe("gata");
        f.TextExtras!.ShouldContain(FrazaPdf);
    }

    [Fact]
    public async Task TextulDocxEsteExtrasCuDiacritice()
    {
        var id = await IncarcaId(irina, Docx(), "a.docx");
        await Procesor.RuleazaOdataAsync(Ct);
        (await Rand(id)).TextExtras!.ShouldContain(FrazaDocx);
    }

    [Fact]
    public async Task TextulPptxEsteExtrasInOrdineaDiapozitivelor()
    {
        var id = await IncarcaId(irina, Pptx(), "a.pptx");
        await Procesor.RuleazaOdataAsync(Ct);
        var text = (await Rand(id)).TextExtras!;
        text.ShouldContain(FrazaPptx);
        text.IndexOf("doi", StringComparison.Ordinal).ShouldBeLessThan(text.IndexOf("zece", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PdfulFaraTextRamaneVizibilElevilor()
    {
        var id = await IncarcaId(irina, Pdf("x", umplutura: 100_000), "scanat.pdf");
        await Procesor.RuleazaOdataAsync(Ct);
        (await Rand(id)).StareText.ShouldBeOneOf("eroare", "fara_text");
        await elena.Get($"/fisiere/{id}").Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EditorulVedeMarimeaTextuluiSiLimita()
    {
        await IncarcaId(irina, Pdf(), "a.pdf");
        await Procesor.RuleazaOdataAsync(Ct);
        var e = await irina.GetJson($"/continut/parcursuri/{lectie}/fisiere");
        e.Int("maxMb").ShouldBe(3);
        e.Lista().ShouldAllBe(f => f.Int("caractereText") > 0);
    }

    [Fact]
    public async Task FisiereleNuGenereazaIntrebariSingure()
    {
        await IncarcaId(irina, Pdf(), "a.pdf");
        await Procesor.RuleazaOdataAsync(Ct);
        h.Ceas.Advance(TimeSpan.FromMinutes(5));
        await Procesor.RuleazaOdataAsync(Ct);
        await h.Serviciu<GeneratorIntrebari>().RuleazaOdataAsync(Ct);
        (await h.Db(db => db.Set<GenerareAi>().CountAsync(g => g.ParcursId == lectie))).ShouldBe(0);
        h.Ai.Apeluri.ShouldBeEmpty();
    }

    [Fact]
    public async Task MaterialulAiAreToateFisiereleSiCapitolele()
    {
        await IncarcaId(irina, Pdf(), "a.pdf");
        await IncarcaId(irina, Docx(), "a.docx");
        await IncarcaId(irina, Pptx(), "prezentare.pptx");
        await Procesor.RuleazaOdataAsync(Ct);
        h.Ai.Raspunde = (_, _, _) => """{"grila":[{"text":"Cine?","variante":["A","B","C","D"],"corect":0,"explicatie":"."}],"estimari":[]}""";
        await irina.Post($"/parcursuri/{lectie}/genereaza-intrebari", new { numar = 3 }).Status(HttpStatusCode.Accepted);
        await h.Serviciu<GeneratorIntrebari>().RuleazaOdataAsync(Ct);
        var prompt = string.Join("\n", h.Ai.Apeluri.SelectMany(a => a.Mesaje).Select(m => m.Text));
        prompt.ShouldContain(FrazaPdf);
        prompt.ShouldContain(FrazaDocx);
        prompt.ShouldContain(FrazaPptx);
        prompt.ShouldContain("# Fișier: prezentare.pptx");
        prompt.ShouldContain("Capitol cu fișiere");
    }

    [Fact]
    public async Task ElevulOrganizatieiVedeListaFaraChei()
    {
        await IncarcaId(irina, Pdf(), "a.pdf");
        var r = await elena.Get($"/parcursuri/{lectie}/fisiere").Status(HttpStatusCode.OK);
        var text = await r.Content.ReadAsStringAsync(Ct);
        text.ShouldNotContain("cheie");
        text.ShouldNotContain("lectii/");
    }

    [Fact]
    public async Task PdfulSeDeschideInBrowserCuCspStrict()
    {
        var octeti = Pdf();
        var id = await IncarcaId(irina, octeti, "a.pdf");
        var r = await elena.Get($"/fisiere/{id}").Status(HttpStatusCode.OK);
        r.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        r.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("inline");
        var csp = r.Headers.GetValues("Content-Security-Policy").Single();
        csp.ShouldContain("default-src 'none'");
        csp.ShouldContain("frame-ancestors 'none'");
        csp.ShouldNotContain("sandbox");
        r.Headers.GetValues("X-Content-Type-Options").Single().ShouldBe("nosniff");
        (await r.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(octeti);
    }

    [Fact]
    public async Task PdfulSePoateDescarca()
    {
        var id = await IncarcaId(irina, Pdf(), "a.pdf");
        (await elena.Get($"/fisiere/{id}?descarca=true")).Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
    }

    [Fact]
    public async Task DocxulSeDescarcaMereuSubNumeleLui()
    {
        var id = await IncarcaId(director, Docx(), "Ștefan.docx");
        var r = await elena.Get($"/fisiere/{id}");
        r.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
        r.Content.Headers.ContentDisposition.FileNameStar.ShouldBe("Ștefan.docx");
        r.Headers.GetValues("Content-Security-Policy").Single().ShouldContain("sandbox");
    }

    [Fact]
    public async Task DescarcareaSuportaIntervale()
    {
        var id = await IncarcaId(irina, Pdf(), "a.pdf");
        var cerere = new HttpRequestMessage(HttpMethod.Get, $"/fisiere/{id}");
        cerere.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 9);
        var r = await elena.SendAsync(cerere, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.PartialContent);
        (await r.Content.ReadAsByteArrayAsync(Ct)).Length.ShouldBe(10);
    }

    [Fact]
    public async Task CelDinAfaraPrimeste404()
    {
        var id = await IncarcaId(irina, Pdf(), "a.pdf");
        await george.Get($"/fisiere/{id}").Status(HttpStatusCode.NotFound);
        await george.Get($"/parcursuri/{lectie}/fisiere").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AnonimulNuDescarca()
    {
        var id = await IncarcaId(irina, Pdf(), "a.pdf");
        await h.Anonim().Get($"/fisiere/{id}").Status(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FisierulNecunoscutEste404()
    {
        await elena.Get("/fisiere/999999").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ElevulNuSterge()
    {
        var id = await IncarcaId(irina, Pdf(), "a.pdf");
        await elena.Delete($"/fisiere/{id}").Status(HttpStatusCode.Forbidden);
        await george.Get($"/continut/parcursuri/{lectie}/fisiere").Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StergereaUnuiFisierEliminaAccesulIarCopiaSeCurataUlterior()
    {
        var id = await IncarcaId(irina, Pdf(), "a.pdf");
        var cheie = await Cheie(id);
        await irina.Delete($"/fisiere/{id}").Status(HttpStatusCode.NoContent);
        File.Exists(Stocare.CaleFisier(cheie)).ShouldBeTrue();
        File.SetLastWriteTimeUtc(Stocare.CaleFisier(cheie)!, h.Ceas.GetUtcNow().UtcDateTime.AddHours(-3));
        h.Ceas.Advance(TimeSpan.FromHours(2));
        await Procesor.RuleazaOdataAsync(Ct);
        File.Exists(Stocare.CaleFisier(cheie)).ShouldBeFalse();
        await elena.Get($"/fisiere/{id}").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task StergereaCapitoluluiPastreazaFisiereleLectiei()
    {
        await IncarcaId(irina, Pdf(), "a.pdf");
        var capitol = (await irina.GetJson($"/continut/cursuri?parcursId={lectie}")).Lista()[0].Int("id");
        await irina.Delete($"/cursuri/{capitol}").Status(HttpStatusCode.NoContent);
        (await h.Db(db => db.Set<FisierLectie>().CountAsync(f => f.ParcursId == lectie))).ShouldBe(1);
    }

    [Fact]
    public async Task StergereaLectieiStergeFisierele()
    {
        await IncarcaId(irina, Pdf(), "a.pdf");
        await IncarcaId(irina, Docx(), "a.docx");
        await irina.Delete($"/parcursuri/{lectie}").Status(HttpStatusCode.NoContent);
        foreach (var f in Stocare.Chei(Stocare.DirectorLectii)) File.SetLastWriteTimeUtc(Stocare.CaleFisier(f.Cheie)!, h.Ceas.GetUtcNow().UtcDateTime.AddHours(-3));
        h.Ceas.Advance(TimeSpan.FromHours(2));
        await Procesor.RuleazaOdataAsync(Ct);
        Stocare.Chei(Stocare.DirectorLectii).ShouldBeEmpty();
        (await h.Db(db => db.Set<FisierLectie>().CountAsync())).ShouldBe(0);
    }

    [Fact]
    public async Task CuratareaStergeDoarOrfaneleVechi()
    {
        var vechi = Stocare.CaleFisier($"{Stocare.DirectorLectii}/orfan-vechi.pdf")!;
        var nou = Stocare.CaleFisier($"{Stocare.DirectorLectii}/orfan-nou.pdf")!;
        Directory.CreateDirectory(Path.GetDirectoryName(vechi)!);
        await File.WriteAllBytesAsync(vechi, Pdf("orfan"), Ct); File.SetLastWriteTimeUtc(vechi, h.Ceas.GetUtcNow().UtcDateTime.AddHours(-3));
        await File.WriteAllBytesAsync(nou, Pdf("orfan"), Ct); File.SetLastWriteTimeUtc(nou, h.Ceas.GetUtcNow().UtcDateTime.AddHours(1));
        h.Ceas.Advance(TimeSpan.FromHours(1));
        await Procesor.RuleazaOdataAsync(Ct);
        File.Exists(vechi).ShouldBeFalse();
        File.Exists(nou).ShouldBeTrue();
    }

    [Fact]
    public async Task CuratareaNuAtingeFisiereleDinBaza()
    {
        var id = await IncarcaId(irina, Pdf(), "a.pdf");
        var cale = Stocare.CaleFisier(await Cheie(id))!;
        File.SetLastWriteTimeUtc(cale, h.Ceas.GetUtcNow().UtcDateTime.AddHours(-5));
        h.Ceas.Advance(TimeSpan.FromHours(1));
        await Procesor.RuleazaOdataAsync(Ct);
        File.Exists(cale).ShouldBeTrue();
    }

    [Fact]
    public async Task FisierulStersInainteDeExtragereEsteSarit()
    {
        var id = await IncarcaId(irina, Pdf(), "a.pdf");
        await irina.Delete($"/fisiere/{id}");
        await Procesor.RuleazaOdataAsync(Ct);
        h.Jurnal.Linii.ShouldNotContain(l => l.Contains("Lecție file processing failed"));
    }

    [Fact]
    public async Task ElevulVedeFisierelePeLectie()
    {
        await IncarcaId(irina, Pdf(), "a.pdf");
        var f = (await elena.GetJson($"/parcursuri/{lectie}/fisiere")).Lista().Single();
        (f.Str("tip"), f.Str("nume")).ShouldBe(("pdf", "a.pdf"));
        f.GetProperty("marimeOcteti").GetInt64().ShouldBeGreaterThan(0);
    }
}

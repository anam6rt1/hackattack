using System.Net;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using static MarkAi.Tests.Suport.DocumenteTest;

namespace MarkAi.Tests.Integration.Fisiere;

/// <summary>Lecție files are checked from their content: renamed executables, macros, zip bombs and non-Office ZIPs are refused; the size limit is configurable.</summary>
public sealed class ValidareFisiereTests : TestFisiere
{
    [Theory]
    [InlineData("tema.pdf")]
    [InlineData("tema.docx")]
    [InlineData("tema.pptx")]
    public async Task ExecutabilRedenumitEsteRefuzat(string nume)
    {
        await Incarca(irina, Exe, nume).Status(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("tema.exe")]
    [InlineData("tema.docm")]
    [InlineData("tema.pptm")]
    [InlineData("tema.doc")]
    [InlineData("tema.txt")]
    [InlineData("tema")]
    public async Task ExtensiaNepermisaEsteRefuzata(string nume)
    {
        (await Incarca(irina, Docx(), nume).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("Tip de fișier nepermis");
    }

    [Fact]
    public async Task PptxCuProiectVbaEsteRefuzat()
    {
        (await Incarca(irina, Pptx(macro: true), "prezentare.pptx").Mesaj(HttpStatusCode.BadRequest)).ShouldContain("macrocomenzi");
    }

    [Fact]
    public async Task PptxCuActiveXEsteRefuzat()
    {
        (await Incarca(irina, Pptx(activeX: true), "prezentare.pptx").Mesaj(HttpStatusCode.BadRequest)).ShouldContain("macrocomenzi");
    }

    [Fact]
    public async Task DocxCuMacrocomenziRedenumitEsteRefuzat()
    {
        (await Incarca(irina, Docx(macroEnabled: true), "tema.docx").Mesaj(HttpStatusCode.BadRequest)).ShouldContain("macrocomenzi");
    }

    [Fact]
    public async Task BombaZipEsteRefuzata()
    {
        (await Incarca(irina, ZipBomba(), "bomba.docx").Mesaj(HttpStatusCode.BadRequest)).ShouldContain("neobișnuit de mare");
    }

    [Fact]
    public async Task ZipulCareNuEPrezentareEsteRefuzat()
    {
        (await Incarca(irina, ZipSimplu(), "arhiva.pptx").Mesaj(HttpStatusCode.BadRequest)).ShouldContain("nu este un PPTX valid");
    }

    [Fact]
    public async Task DocxFaraDocumentEsteRefuzat()
    {
        (await Incarca(irina, Docx(faraDocument: true), "gol.docx").Mesaj(HttpStatusCode.BadRequest)).ShouldContain("nu este un DOCX valid");
    }

    [Fact]
    public async Task UnPptxRedenumitDocxEsteRefuzat()
    {
        await Incarca(irina, Pptx(), "prezentare.docx").Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ZipulStricatEsteRefuzatCuMesaj()
    {
        (await Incarca(irina, ZipStricat(), "stricat.docx").Mesaj(HttpStatusCode.BadRequest)).ShouldContain("nu este un DOCX valid");
    }

    [Fact]
    public async Task PesteLimitaSePrimeste413CuMesaj()
    {
        (await Incarca(irina, Pdf("x", umplutura: 5 * 1024 * 1024), "mare.pdf").Mesaj(HttpStatusCode.RequestEntityTooLarge)).ShouldContain("cel mult 3 MB");
    }

    [Fact]
    public async Task ChiarPesteLimitaEsteRefuzatDupaMarime()
    {
        (await Incarca(irina, Pdf("x", umplutura: 3 * 1024 * 1024 + 100_000), "limita.pdf").Mesaj(HttpStatusCode.BadRequest)).ShouldContain("cel mult 3 MB");
    }

    [Fact]
    public async Task SubLimitaEsteAcceptat()
    {
        await Incarca(irina, Pdf("x", umplutura: 3 * 1024 * 1024 - 2000), "mare.pdf").Status(HttpStatusCode.Created);
    }

    [Fact]
    public async Task FisierulGolEsteRefuzat()
    {
        await Incarca(irina, [], "gol.pdf").Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FaraCampulFisierEsteRefuzat()
    {
        var form = new MultipartFormDataContent { { new StringContent("x"), "altceva" } };
        (await irina.PostAsync($"/parcursuri/{lectie}/fisiere", form, Ct).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("Alege un fișier");
    }

    [Fact]
    public async Task RefuzurileNuLasaNimicInBazaSauPeDisc()
    {
        await Incarca(irina, Exe, "tema.pdf");
        await Incarca(irina, ZipBomba(), "bomba.docx");
        await Incarca(irina, Pptx(macro: true), "p.pptx");
        await Incarca(irina, Pdf("x", umplutura: 3 * 1024 * 1024 + 100_000), "limita.pdf");
        (await h.Db(db => db.Set<FisierLectie>().CountAsync())).ShouldBe(0);
        Stocare.Chei(MarkAi.Api.Servicii.Stocare.DirectorLectii).ShouldBeEmpty();
    }

    [Fact]
    public async Task ElevulNuIncarca()
    {
        await Incarca(george, Pdf(), "a.pdf").Status(HttpStatusCode.Forbidden);
        await Incarca(elena, Pdf(), "a.pdf").Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AnonimulNuIncarca()
    {
        await Incarca(h.Anonim(), Pdf(), "a.pdf").Status(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AltProfesorNuIncarcaInLectiaAltcuiva()
    {
        using var admin = await h.Login("admin");
        var aAdminului = await MarkAi.Tests.Suport.Harness.LectiePlatforma(admin);
        (await Incarca(irina, Pdf(), "a.pdf", parcurs: aAdminului)).IsSuccessStatusCode.ShouldBeFalse();
    }

    [Fact]
    public async Task LimitaEsteImplicit20Mb()
    {
        await using var implicit20 = await MarkAi.Tests.Suport.Harness.Porneste();
        implicit20.Serviciu<MarkAi.Api.Servicii.Stocare>().MaxFisierLectieMb.ShouldBe(20);
    }
}

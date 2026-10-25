using System.Net;
using static MarkAi.Tests.Suport.DocumenteTest;

namespace MarkAi.Tests.Integration.Fisiere;

public sealed class PrevizualizareOfficeTests : TestFisiere
{
    protected override IReadOnlyDictionary<string, string?> Setari => new Dictionary<string, string?>(base.Setari)
    {
        ["Previzualizare:LibreOffice"] = Path.Combine(Path.GetTempPath(), "markai-converter-inexistent", "soffice"),
    };

    private Task<HttpResponseMessage> Preview(HttpClient client, byte[] date, string nume)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(date), "fisier", nume);
        return client.PostAsync("/fisiere/previzualizare", form, Ct);
    }

    [Fact]
    public async Task CopiaTemporaraEsteStearsaInclusivCandConverterulLipseste()
    {
        (await Preview(irina, Docx(), "tema.docx").Mesaj(HttpStatusCode.BadRequest)).ShouldContain("nu este disponibilă");
        Directory.GetFiles(Path.Combine(h.DirStocare, "lectii")).ShouldBeEmpty();
    }

    [Fact]
    public async Task ContinutulInvalidEsteRefuzatInainteDeConversie()
    {
        await Preview(irina, Exe, "tema.docx").Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task VizitatorulSiElevulNuPotTrimiteDocumentePentruConversie()
    {
        await Preview(h.Anonim(), Docx(), "tema.docx").Status(HttpStatusCode.Unauthorized);
        await Preview(george, Docx(), "tema.docx").Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PrevizualizareaSalvataRespectaAccesulLaLectieSiPastreazaOriginalul()
    {
        var original = Docx();
        var id = await IncarcaId(irina, original, "tema.docx");
        var cheie = await Cheie(id);
        await george.GetAsync($"/fisiere/{id}/previzualizare", Ct).Status(HttpStatusCode.NotFound);
        await h.Anonim().GetAsync($"/fisiere/{id}/previzualizare", Ct).Status(HttpStatusCode.Unauthorized);
        await irina.GetAsync($"/fisiere/{id}/previzualizare", Ct).Status(HttpStatusCode.BadRequest);
        File.ReadAllBytes(Stocare.CaleFisier(cheie)!).ShouldBe(original);
    }
}

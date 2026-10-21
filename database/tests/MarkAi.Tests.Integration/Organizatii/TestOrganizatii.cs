using System.Net;
using System.Net.Http.Headers;
using MarkAi.Api.Servicii.Mesagerie;
using MarkAi.Tests.Suport;

namespace MarkAi.Tests.Integration.Organizatii;

/// <summary>Helpers for the organisation tests: applying as an organisation admin, a professor or a student, and confirming the email.</summary>
public abstract class TestOrganizatii : TestCuHarness
{
    protected override IReadOnlyDictionary<string, string?> Setari => new Dictionary<string, string?>
    {
        ["Limite:AutentificariPeMinut"] = "200", ["Limite:InregistrariPeOra"] = "100", ["Limite:EmailuriPeOra"] = "100",
    };

    protected const int Liceu = Harness.IdLiceu;
    private static int telefon = 20000000;

    protected Task Proceseaza() => h.Serviciu<ProcesorMesaje>().RuleazaOdataAsync(Ct);

    protected async Task Confirma(HttpClient c, string username) =>
        await c.Post("/autentificare/confirma-email", new { email = username + "@example.invalid", cod = h.Emailuri.UltimulCod(username + "@example.invalid") })
            .Status(HttpStatusCode.OK);

    /// <summary>An organisation admin who applied and confirmed the email; returns the client and the organisation's id.</summary>
    protected async Task<(HttpClient Client, int Organizatie)> OrganizatieNoua(string username = "ovidiu", string nume = "Centrul Alba", bool aprobata = false)
    {
        var c = h.Anonim();
        await c.Post("/autentificare/inregistrare-organizatie", new
        {
            nume = "Ovidiu Organizator", username, email = username + "@example.invalid", parola = Harness.ParolaTest, telefon = "07" + Interlocked.Increment(ref telefon),
            numeOrganizatie = nume, adresa = "Str. Unirii 2", judetId = 1, localitateId = 1026,
        }).Status(HttpStatusCode.Accepted);
        await Confirma(c, username);
        var id = (await c.GetJson("/utilizatori/me")).GetProperty("organizatieAdministrata").Int("id");
        if (aprobata)
        {
            using var admin = await h.Login("admin");
            await admin.Post($"/admin/organizatii/{id}/aprobare").Status(HttpStatusCode.NoContent);
        }
        return (c, id);
    }

    protected static MultipartFormDataContent FormularProfesor(string username, params int[] organizatii)
    {
        var f = new MultipartFormDataContent
        {
            { new StringContent("Profesor " + username), "Nume" }, { new StringContent(username), "Username" },
            { new StringContent(username + "@example.invalid"), "Email" }, { new StringContent(Harness.ParolaTest), "Parola" },
            { new StringContent("07" + Interlocked.Increment(ref telefon)), "Telefon" },
            { new StringContent("Predau de mulți ani."), "Descriere" }, { new StringContent("1"), "MaterieIds" }, { new StringContent("40"), "JudetId" },
        };
        foreach (var o in organizatii) f.Add(new StringContent(o.ToString()), "OrganizatieIds");
        var poza = new ByteArrayContent(Raspunsuri.Png);
        poza.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var doc = new ByteArrayContent(Raspunsuri.Pdf);
        doc.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        f.Add(poza, "Poza", "poza.png");
        f.Add(doc, "Dovada", "dovada.pdf");
        return f;
    }

    /// <summary>A professor who applied to these organisations and confirmed the email; returns the client and the account id.</summary>
    protected async Task<(HttpClient Client, int Id)> ProfesorNou(string username = "paul", params int[] organizatii)
    {
        var c = h.Anonim();
        await c.PostAsync("/autentificare/inregistrare-profesor", FormularProfesor(username, organizatii.Length == 0 ? [Liceu] : organizatii), Ct).Status(HttpStatusCode.Accepted);
        await Confirma(c, username);
        return (c, (await c.GetJson("/utilizatori/me")).Int("id"));
    }

    /// <summary>A student who registered asking to join <paramref name="organizatie"/> and confirmed the email.</summary>
    protected async Task<(HttpClient Client, int Id)> ElevNou(string username = "sara", int? organizatie = Liceu)
    {
        var c = h.Anonim();
        await c.Post("/autentificare/inregistrare", new { nume = "Sara Eleva", username, email = username + "@example.invalid", parola = Harness.ParolaTest,
            judetId = 40, localitateId = 179141, clasa = "xi", organizatieId = organizatie }).Status(HttpStatusCode.Accepted);
        await Confirma(c, username);
        return (c, (await c.GetJson("/utilizatori/me")).Int("id"));
    }
}

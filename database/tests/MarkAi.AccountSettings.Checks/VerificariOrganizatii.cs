using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MarkAi.Api.Servicii;
using MarkAi.Api.Servicii.Mesagerie;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Organisations: platform admins approve organisation admins; organisation admins approve their professors; professors and
/// admins validate students; requests expire in 7 days; unapproved applications are reminded on day 6 and deleted on day 7.
/// </summary>
static class VerificariOrganizatii
{
    public static readonly Dictionary<string, string?> Setari = new()
    {
        ["Limite:AutentificariPeMinut"] = "200", ["Limite:InregistrariPeOra"] = "100", ["Limite:EmailuriPeOra"] = "100",
    };

    public static async Task Ruleaza(Harness h)
    {
        using var anonim = h.Anonim();

        // Public lists for the sign-up forms.
        var judete = await anonim.GetFromJsonAsync<JsonElement>("/judete");
        var sectoare = await anonim.GetFromJsonAsync<JsonElement>("/judete/40/localitati?q=sect");
        var diacritice = await anonim.GetFromJsonAsync<JsonElement>("/judete/1/localitati?q=barab");
        h.Check(judete.GetProperty("elemente").GetArrayLength() == 2 && sectoare.GetProperty("elemente").GetArrayLength() == 2
            && diacritice.GetProperty("elemente")[0].GetProperty("nume").GetString() == "Bărăbanț", "counties and locality search (without diacritics)");
        h.Check((await anonim.GetFromJsonAsync<JsonElement>("/organizatii")).GetProperty("elemente").EnumerateArray().Any(o => o.GetProperty("nume").GetString() == "Liceul de Test"),
            "approved organisations are listed");

        // 1. A new organisation: its admin applies, confirms the email, the platform admin approves.
        var r = await anonim.PostAsJsonAsync("/autentificare/inregistrare-organizatie", new { nume = "Ovidiu Organizator", username = "ovidiu",
            email = "ovidiu@example.invalid", parola = Harness.ParolaTest, telefon = "0744000111", numeOrganizatie = "Centrul Alba", adresa = "Str. Unirii 2",
            judetId = 1, localitateId = 1026 });
        h.Check(r.StatusCode == HttpStatusCode.Accepted, "organisation admin applies");
        h.Check((await anonim.PostAsJsonAsync("/autentificare/inregistrare-organizatie", new { nume = "X", username = "x1", email = "x1@example.invalid",
            parola = Harness.ParolaTest, telefon = "0744000222", numeOrganizatie = "Y", adresa = "Z", judetId = 40, localitateId = 1026 })).StatusCode == HttpStatusCode.BadRequest,
            "a locality must be in the chosen county");
        using var ovidiu = h.Anonim();
        (await ovidiu.PostAsJsonAsync("/autentificare/confirma-email", new { email = "ovidiu@example.invalid", cod = h.Emailuri.UltimulCod("ovidiu@example.invalid") })).EnsureSuccessStatusCode();
        await h.Serviciu<ProcesorMesaje>().RuleazaOdataAsync(CancellationToken.None);
        h.Check(h.Emailuri.Trimise.Any(m => m.Catre == "admin@example.invalid" && m.Subiect.Contains("Centrul Alba")), "platform admins are told about the new organisation");
        var eu = await ovidiu.GetFromJsonAsync<JsonElement>("/utilizatori/me");
        h.Check(eu.GetProperty("rol").GetString() == "organizatie" && eu.GetProperty("stareProfesor").GetString() == "in_asteptare"
            && eu.GetProperty("organizatieAdministrata").GetProperty("stare").GetString() == "in_asteptare", "the applicant waits for approval");
        h.Check((await ovidiu.GetAsync("/organizatia-mea")).StatusCode == HttpStatusCode.Forbidden, "an unapproved organisation has no dashboard");

        using var admin = await h.Login("admin");
        var cereri = await admin.GetFromJsonAsync<JsonElement>("/admin/organizatii?stare=in_asteptare");
        var orgOvidiu = cereri.GetProperty("elemente").EnumerateArray().Single(o => o.GetProperty("nume").GetString() == "Centrul Alba").GetProperty("id").GetInt32();
        using var director = await h.Login("director");
        h.Check((await director.PostAsync($"/admin/organizatii/{orgOvidiu}/aprobare", null)).StatusCode == HttpStatusCode.Forbidden, "only platform admins approve organisations");
        h.Check((await admin.PostAsync($"/admin/organizatii/{orgOvidiu}/aprobare", null)).StatusCode == HttpStatusCode.NoContent, "platform admin approves the organisation");
        eu = await ovidiu.GetFromJsonAsync<JsonElement>("/utilizatori/me");
        h.Check(eu.GetProperty("stareProfesor").GetString() == "aprobat" && (await ovidiu.GetAsync("/organizatia-mea")).IsSuccessStatusCode,
            "its admin is now approved (and teaches in it) and has the dashboard");
        h.Check((await ovidiu.GetAsync("/profesori/me")).IsSuccessStatusCode, "the organisation admin can do what an approved professor does");

        // 2. Professors: approved by the admin of the organisation they asked to join, and only by them.
        using var prof = h.Anonim();
        r = await prof.PostAsync("/autentificare/inregistrare-profesor", Profesor("paul", Harness.IdLiceu));
        h.Check(r.StatusCode == HttpStatusCode.Accepted, "professor applies to an organisation");
        h.Check((await prof.PostAsync("/autentificare/inregistrare-profesor", Profesor("paul2", null))).StatusCode == HttpStatusCode.BadRequest,
            "a professor must choose an organisation");
        (await prof.PostAsJsonAsync("/autentificare/confirma-email", new { email = "paul@example.invalid", cod = h.Emailuri.UltimulCod("paul@example.invalid") })).EnsureSuccessStatusCode();
        var paulId = (await prof.GetFromJsonAsync<JsonElement>("/utilizatori/me")).GetProperty("id").GetInt32();
        await h.Serviciu<ProcesorMesaje>().RuleazaOdataAsync(CancellationToken.None);
        h.Check(h.Emailuri.Trimise.Any(m => m.Catre == "director@example.invalid" && m.Subiect.Contains("profesor")), "the organisation's admin is told about the request");
        h.Check((await prof.GetAsync("/profesori/me")).StatusCode == HttpStatusCode.Forbidden, "a professor waiting for approval cannot teach yet");
        var membri = await director.GetFromJsonAsync<JsonElement>("/organizatia-mea/membri?stare=in_asteptare");
        h.Check(membri.GetProperty("elemente").EnumerateArray().Any(m => m.GetProperty("utilizatorId").GetInt32() == paulId && m.GetProperty("areDovada").GetBoolean()),
            "the organisation admin sees the request and the qualification document");
        var dovada = await director.GetAsync($"/organizatia-mea/membri/{paulId}/dovada");
        h.Check(dovada.IsSuccessStatusCode && dovada.Content.Headers.ContentDisposition?.DispositionType == "attachment", "and can download the document");
        h.Check((await ovidiu.PostAsync($"/organizatia-mea/membri/{paulId}/aprobare", null)).StatusCode == HttpStatusCode.NotFound,
            "another organisation's admin cannot decide");
        h.Check((await director.PostAsync($"/organizatia-mea/membri/{paulId}/aprobare", null)).StatusCode == HttpStatusCode.NoContent, "organisation admin approves the professor");
        h.Check((await prof.GetAsync("/profesori/me")).IsSuccessStatusCode, "the approved professor can teach");
        h.Check((await admin.PostAsJsonAsync($"/admin/profesori/{paulId}/aprobare", new { onorariuNegociat = 50 })).StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            "platform admins no longer approve professors");

        // A professor may join more organisations.
        (await prof.PostAsJsonAsync("/utilizatori/me/organizatii", new { organizatieId = orgOvidiu })).EnsureSuccessStatusCode();
        h.Check((await prof.PostAsJsonAsync("/utilizatori/me/organizatii", new { organizatieId = orgOvidiu })).StatusCode == HttpStatusCode.Conflict, "no duplicate request");
        await ovidiu.PostAsJsonAsync($"/organizatia-mea/membri/{paulId}/respingere", new { motiv = "Nu predăm această materie." });
        var aleMele = await prof.GetFromJsonAsync<JsonElement>("/utilizatori/me/organizatii");
        h.Check(aleMele.GetProperty("elemente").GetArrayLength() == 2 && (await prof.GetFromJsonAsync<JsonElement>("/utilizatori/me")).GetProperty("stareProfesor").GetString() == "aprobat",
            "a refusal in one organisation keeps the approval from another");

        // Publishing switch (used by the course pages).
        h.Check((await director.PutAsJsonAsync($"/organizatia-mea/membri/{paulId}/publicare", new { poatePublicaPublic = false })).StatusCode == HttpStatusCode.NoContent
            && !(await h.Db(db => db.Set<MembruOrganizatie>().Where(m => m.UtilizatorId == paulId && m.OrganizatieId == Harness.IdLiceu).Select(m => m.PoatePublicaPublic).SingleAsync())),
            "the organisation admin can stop a professor publishing publicly");

        // 3. Students: one organisation, validated by its professors or admin.
        r = await anonim.PostAsJsonAsync("/autentificare/inregistrare", new { nume = "Sara Eleva", username = "sara", email = "sara@example.invalid",
            parola = Harness.ParolaTest, judetId = 40, localitateId = 179141, clasa = "xi", organizatieId = Harness.IdLiceu });
        using var sara = h.Anonim();
        (await sara.PostAsJsonAsync("/autentificare/confirma-email", new { email = "sara@example.invalid", cod = h.Emailuri.UltimulCod("sara@example.invalid") })).EnsureSuccessStatusCode();
        eu = await sara.GetFromJsonAsync<JsonElement>("/utilizatori/me");
        var saraId = eu.GetProperty("id").GetInt32();
        h.Check(eu.GetProperty("clasa").GetString() == "XI" && eu.GetProperty("judetId").GetInt32() == 40
            && eu.GetProperty("organizatii")[0].GetProperty("stare").GetString() == "in_asteptare", "student registers with grade, place and an organisation request");
        h.Check((await sara.PostAsJsonAsync("/utilizatori/me/organizatii", new { organizatieId = orgOvidiu })).StatusCode == HttpStatusCode.Conflict,
            "a student can be in only one organisation");
        h.Check((await ovidiu.PostAsync($"/organizatii/{Harness.IdLiceu}/elevi/{saraId}/aprobare", null)).StatusCode == HttpStatusCode.Forbidden,
            "someone outside the organisation cannot validate its students");
        using var irina = await h.Login("irina");
        h.Check((await irina.GetFromJsonAsync<JsonElement>($"/organizatii/{Harness.IdLiceu}/elevi?stare=in_asteptare")).GetProperty("elemente").GetArrayLength() == 1
            && (await irina.PostAsync($"/organizatii/{Harness.IdLiceu}/elevi/{saraId}/aprobare", null)).StatusCode == HttpStatusCode.NoContent,
            "an approved professor of the organisation validates the student");
        h.Check((await sara.GetFromJsonAsync<JsonElement>("/utilizatori/me")).GetProperty("organizatii")[0].GetProperty("stare").GetString() == "aprobat", "student is now a member");
        (await sara.DeleteAsync($"/utilizatori/me/organizatii/{Harness.IdLiceu}")).EnsureSuccessStatusCode();
        h.Check((await sara.PostAsJsonAsync("/utilizatori/me/organizatii", new { organizatieId = orgOvidiu })).IsSuccessStatusCode, "after leaving, the student may ask another organisation");

        // Place and grade in settings.
        h.Check((await sara.PutAsJsonAsync("/utilizatori/me/setari/loc", new { judetId = 1, localitateId = 1035, clasa = "XII" })).IsSuccessStatusCode
            && (await sara.GetFromJsonAsync<JsonElement>("/utilizatori/me")).GetProperty("clasa").GetString() == "XII", "place and grade updated in settings");
        h.Check((await sara.PutAsJsonAsync("/utilizatori/me/setari/loc", new { judetId = 1, localitateId = 1035, clasa = "XIII" })).StatusCode == HttpStatusCode.BadRequest,
            "an unknown grade is refused");

        // 4. Time: requests expire, applications are reminded and then deleted.
        using var neaprobat = h.Anonim();
        await neaprobat.PostAsync("/autentificare/inregistrare-profesor", Profesor("uitat", Harness.IdLiceu));
        (await neaprobat.PostAsJsonAsync("/autentificare/confirma-email", new { email = "uitat@example.invalid", cod = h.Emailuri.UltimulCod("uitat@example.invalid") })).EnsureSuccessStatusCode();
        await admin.PostAsJsonAsync("/autentificare/iesire", new { });
        using var respins = h.Anonim();
        await respins.PostAsJsonAsync("/autentificare/inregistrare-organizatie", new { nume = "Rares Respins", username = "rares", email = "rares@example.invalid",
            parola = Harness.ParolaTest, telefon = "0744000333", numeOrganizatie = "Școala Respinsă", adresa = "Str. 3", judetId = 40 });
        (await respins.PostAsJsonAsync("/autentificare/confirma-email", new { email = "rares@example.invalid", cod = h.Emailuri.UltimulCod("rares@example.invalid") })).EnsureSuccessStatusCode();
        using var admin2 = await h.Login("admin");
        var orgRespinsa = (await admin2.GetFromJsonAsync<JsonElement>("/admin/organizatii?stare=in_asteptare")).GetProperty("elemente").EnumerateArray()
            .Single(o => o.GetProperty("nume").GetString() == "Școala Respinsă").GetProperty("id").GetInt32();
        await admin2.PostAsJsonAsync($"/admin/organizatii/{orgRespinsa}/respingere", new { motiv = "Date incomplete." });

        h.Ceas.Advance(TimeSpan.FromDays(6.1));
        await h.Serviciu<CuratareConturi>().RuleazaOdataAsync(CancellationToken.None);
        await h.Serviciu<ProcesorMesaje>().RuleazaOdataAsync(CancellationToken.None);
        h.Check(h.Emailuri.Trimise.Any(m => m.Catre == "uitat@example.invalid" && m.Subiect.Contains("nu a fost încă aprobată"))
            && h.Emailuri.Trimise.Any(m => m.Catre == "rares@example.invalid" && m.Subiect.Contains("nu a fost încă aprobată")), "applicants are reminded on day 6");
        h.Check(h.Emailuri.Trimise.Any(m => m.Catre == "ovidiu@example.invalid" && m.Subiect.Contains("expiră mâine")), "the organisation admin is reminded of a request about to expire");

        h.Ceas.Advance(TimeSpan.FromDays(1));
        await h.Serviciu<CuratareConturi>().RuleazaOdataAsync(CancellationToken.None);
        await h.Serviciu<ProcesorMesaje>().RuleazaOdataAsync(CancellationToken.None);
        var saraOrg = await h.Db(db => db.Set<MembruOrganizatie>().Where(m => m.UtilizatorId == saraId).Select(m => m.Stare).SingleAsync());
        h.Check(saraOrg == MembruOrganizatie.Expirat && await h.Db(db => db.Utilizatori.AnyAsync(u => u.Id == saraId)), "a student's request expires after 7 days; the account stays");
        h.Check(h.Emailuri.Trimise.Any(m => m.Catre == "sara@example.invalid" && m.Subiect.Contains("a expirat")), "the student is told");
        h.Check(!await h.Db(db => db.Utilizatori.AnyAsync(u => u.Username == "uitat")), "a professor never approved is deleted after 7 days");
        h.Check(!await h.Db(db => db.Utilizatori.AnyAsync(u => u.Username == "rares")) && !await h.Db(db => db.Set<Organizatie>().AnyAsync(o => o.Id == orgRespinsa)),
            "a rejected organisation and its admin are deleted after 7 days");
        h.Check(await h.Db(db => db.Utilizatori.AnyAsync(u => u.Username == "paul")) && await h.Db(db => db.Utilizatori.AnyAsync(u => u.Username == "ovidiu")),
            "approved professors and organisation admins stay");
    }

    static MultipartFormDataContent Profesor(string username, int? organizatieId)
    {
        var f = new MultipartFormDataContent
        {
            { new StringContent("Profesor " + username), "Nume" }, { new StringContent(username), "Username" },
            { new StringContent(username + "@example.invalid"), "Email" }, { new StringContent(Harness.ParolaTest), "Parola" },
            { new StringContent("07" + Random.Shared.Next(10000000, 99999999)), "Telefon" },
            { new StringContent("Predau de mulți ani."), "Descriere" }, { new StringContent("1"), "MaterieIds" }, { new StringContent("40"), "JudetId" },
        };
        if (organizatieId is { } id) f.Add(new StringContent(id.ToString()), "OrganizatieIds");
        var poza = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13]);
        poza.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var doc = new ByteArrayContent("%PDF-1.4\n"u8.ToArray());
        doc.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        f.Add(poza, "Poza", "poza.png");
        f.Add(doc, "Dovada", "dovada.pdf");
        return f;
    }
}

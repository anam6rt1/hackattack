using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>Phase 1 request-level security: errors, headers, CSRF, limits, session revocation, uploads.</summary>
static class VerificariSecuritate
{
    static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52];
    static readonly byte[] Pdf = "%PDF-1.4\n%fals\n"u8.ToArray();
    static readonly byte[] Exe = [(byte)'M', (byte)'Z', 0x90, 0, 3, 0, 0, 0, 4, 0, 0, 0, 0xFF, 0xFF, 0, 0];

    public static async Task Ruleaza(Harness h)
    {
        using var anonim = h.Anonim();

        // Errors: internal messages stay on the server.
        var eroare = await anonim.GetAsync(Harness.RutaEroareInterna);
        var text = await eroare.Content.ReadAsStringAsync();
        h.Check(eroare.StatusCode == HttpStatusCode.InternalServerError && !text.Contains("Sequence") && !text.Contains("intern"),
            "unexpected errors return a generic message, not the internal one");
        h.Check(h.Jurnal.Linii.Any(l => l.Contains(Harness.MesajIntern)), "the internal error is logged on the server");

        // Security headers on every response.
        var materii = await anonim.GetAsync("/materii");
        h.Check(materii.Headers.TryGetValues("X-Content-Type-Options", out var nosniff) && nosniff.Single() == "nosniff"
            && materii.Headers.Contains("Content-Security-Policy") && materii.Headers.Contains("X-Frame-Options"), "security headers set");

        // CSRF: browser mutations must carry X-MarkAi.
        using var faraAntet = new HttpClient { BaseAddress = new Uri(Harness.Adresa) };
        var csrf = await faraAntet.PostAsJsonAsync("/autentificare", new { identificator = "george", parola = Harness.ParolaTest });
        h.Check(csrf.StatusCode == HttpStatusCode.Forbidden, "POST without the X-MarkAi header rejected (even login)");
        h.Check((await faraAntet.GetAsync("/materii")).IsSuccessStatusCode, "GET needs no CSRF header");

        // Request size: JSON bodies are capped at 2 MB.
        using var george = await h.Login("george");
        var mare = new StringContent("{\"nume\":\"" + new string('a', 3 * 1024 * 1024) + "\"}", System.Text.Encoding.UTF8, "application/json");
        var r = await george.PatchAsync("/utilizatori/me", mare);
        h.Check(r.StatusCode == HttpStatusCode.RequestEntityTooLarge, "request body over 2 MB rejected (413)");

        // Phone number: no longer changeable through PATCH /utilizatori/me; the settings endpoint checks duplicates.
        await george.PatchAsJsonAsync("/utilizatori/me", new { telefon = "0722123456" });
        var telefon = await h.Db(db => db.Utilizatori.Where(u => u.Username == "george").Select(u => u.Telefon).SingleAsync());
        h.Check(telefon is null, "PATCH /utilizatori/me ignores the phone number");
        r = await george.PostAsJsonAsync("/utilizatori/me/setari/telefon/cod", new { telefon = "0722123456" });
        h.Check(r.StatusCode == HttpStatusCode.BadRequest, "phone already used by another account rejected");
        await h.VerificaTelefon(george, "0722999888");
        r = await george.GetAsync("/utilizatori/me");
        h.Check(r.IsSuccessStatusCode, "own phone number saved through settings");

        // Session revocation: "sign out everywhere" ends every session of the account.
        using var georgeAltDispozitiv = await h.Login("george");
        h.Check((await georgeAltDispozitiv.GetAsync("/utilizatori/me")).IsSuccessStatusCode, "second device signed in");
        h.Check((await george.PostAsync("/autentificare/iesire-peste-tot", null)).StatusCode == HttpStatusCode.NoContent, "sign out everywhere accepted");
        h.Check((await georgeAltDispozitiv.GetAsync("/utilizatori/me")).StatusCode == HttpStatusCode.Unauthorized, "other device's session revoked");
        h.Check((await george.GetAsync("/utilizatori/me")).StatusCode == HttpStatusCode.Unauthorized, "this device's session ended too");
        using var georgeDinNou = await h.Login("george");
        h.Check((await georgeDinNou.GetAsync("/utilizatori/me")).IsSuccessStatusCode, "signing in again works");

        // Password change: other devices are signed out, this one gets a new session.
        using var celalalt = await h.Login("george");
        var cerere = await georgeDinNou.PostAsJsonAsync("/utilizatori/me/setari/confirmari", new { scop = "parola", parolaActuala = Harness.ParolaTest, valoareNoua = "Alta-parola-2026!" });
        var idCod = (await cerere.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        r = await georgeDinNou.PostAsJsonAsync("/utilizatori/me/setari/confirmari/finalizeaza", new { id = idCod, cod = h.Mail.Code });
        h.Check(r.IsSuccessStatusCode, "password changed with the emailed code");
        h.Check((await celalalt.GetAsync("/utilizatori/me")).StatusCode == HttpStatusCode.Unauthorized, "password change signs out the other devices");
        h.Check((await georgeDinNou.GetAsync("/utilizatori/me")).IsSuccessStatusCode, "the device that changed the password stays signed in");

        // Uploads: the content must match the extension.
        var redenumit = await InregistrareProfesor(anonim, "prof1", ("poza.png", Png), ("dovada.pdf", Png));
        h.Check(redenumit.StatusCode == HttpStatusCode.BadRequest, "PNG renamed to .pdf rejected");
        var exe = await InregistrareProfesor(anonim, "prof2", ("poza.jpg", Exe), ("dovada.pdf", Pdf));
        h.Check(exe.StatusCode == HttpStatusCode.BadRequest, "executable renamed to .jpg rejected");
        using var aplicant = h.Anonim();
        var bun = await InregistrareProfesor(aplicant, "prof3", ("poza.png", Png), ("dovada.pdf", Pdf));
        h.Check(bun.StatusCode == HttpStatusCode.Accepted, "valid photo and PDF accepted");
        (await aplicant.PostAsJsonAsync("/autentificare/confirma-email", new { email = "prof3@example.invalid", cod = h.Emailuri.UltimulCod("prof3@example.invalid") })).EnsureSuccessStatusCode();
        var aplicantId = (await aplicant.GetFromJsonAsync<JsonElement>("/utilizatori/me")).GetProperty("id").GetInt32();
        h.Check(Directory.EnumerateFiles(h.Serviciu<Microsoft.Extensions.Configuration.IConfiguration>()["Stocare:Director"]!, "*.partial", SearchOption.AllDirectories).Any() == false,
            "no half-written files left in storage");

        // Photos of applicants who are not approved are not public.
        h.Check((await anonim.GetAsync($"/profesori/{aplicantId}/poza")).StatusCode == HttpStatusCode.NotFound, "pending applicant's photo hidden from the public");
        h.Check((await aplicant.GetAsync($"/profesori/{aplicantId}/poza")).IsSuccessStatusCode, "applicant sees their own photo");
        using var admin = await h.Login("admin");
        h.Check((await admin.GetAsync($"/profesori/{aplicantId}/poza")).IsSuccessStatusCode, "admin sees the applicant's photo");
        var dovada = await admin.GetAsync($"/admin/profesori/{aplicantId}/dovada");
        h.Check(dovada.IsSuccessStatusCode && dovada.Content.Headers.ContentDisposition?.DispositionType == "attachment",
            "qualification document is downloaded as an attachment, never shown inline");

        // Replacing a document deletes the old file.
        var veche = await h.Db(db => db.ProfiluriMentor.Where(p => p.Utilizator.Username == "prof3").Select(p => p.DovadaCalificareUrl).SingleAsync());
        using var formular = new MultipartFormDataContent { Fisier("dovada.pdf", Pdf, "Dovada") };
        r = await aplicant.PostAsync("/utilizatori/me/setari/documente", formular);
        var dir = h.Serviciu<Microsoft.Extensions.Configuration.IConfiguration>()["Stocare:Director"]!;
        h.Check(r.IsSuccessStatusCode && !File.Exists(Path.Combine(dir, veche!)), "replaced document deleted from storage");

        // Rate limit: the 11th login attempt within a minute from one address is refused.
        HttpStatusCode ultim = 0;
        for (var i = 0; i < 11; i++)
            ultim = (await anonim.PostAsJsonAsync("/autentificare", new { identificator = "nimeni", parola = "gresita" })).StatusCode;
        h.Check(ultim == HttpStatusCode.TooManyRequests, "too many login attempts get 429");
    }

    static async Task<HttpResponseMessage> InregistrareProfesor(HttpClient client, string username, (string Nume, byte[] Date) poza, (string Nume, byte[] Date) dovada)
    {
        using var f = new MultipartFormDataContent
        {
            { new StringContent("Profesor " + username), "Nume" }, { new StringContent(username), "Username" },
            { new StringContent(username + "@example.invalid"), "Email" }, { new StringContent(Harness.ParolaTest), "Parola" },
            { new StringContent("07" + Random.Shared.Next(10000000, 99999999)), "Telefon" },
            { new StringContent("Predau limba română de mulți ani."), "Descriere" }, { new StringContent("1"), "MaterieIds" },
            { new StringContent(Harness.IdLiceu.ToString()), "OrganizatieIds" }, { new StringContent("40"), "JudetId" },
            Fisier(poza.Nume, poza.Date, "Poza"), Fisier(dovada.Nume, dovada.Date, "Dovada"),
        };
        return await client.PostAsync("/autentificare/inregistrare-profesor", f);
    }

    static (HttpContent, string, string) Fisier(string nume, byte[] date, string camp)
    {
        var c = new ByteArrayContent(date);
        c.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return (c, camp, nume);
    }

    static void Add(this MultipartFormDataContent f, (HttpContent Continut, string Camp, string Nume) fisier) => f.Add(fisier.Continut, fisier.Camp, fisier.Nume);
}

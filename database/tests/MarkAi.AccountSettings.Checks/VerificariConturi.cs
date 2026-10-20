using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MarkAi.Api.Servicii;
using MarkAi.Api.Servicii.Mesagerie;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>Verified accounts: email confirmation, expiry warnings and deletion, SMS phone verification, the outbox.</summary>
static class VerificariConturi
{
    public static readonly Dictionary<string, string?> Setari = new() { ["Sms:MaxPeZi"] = "3", ["Limite:SmsPeOra"] = "100", ["Limite:EmailuriPeOra"] = "100", ["Limite:AutentificariPeMinut"] = "100" };

    public static async Task Ruleaza(Harness h)
    {
        using var anonim = h.Anonim();

        // Registration: no session until the emailed code is confirmed.
        var r = await anonim.PostAsJsonAsync("/autentificare/inregistrare", new { nume = "Ana Pop", username = "ana", email = "ana@example.invalid", parola = Harness.ParolaTest });
        var raspunsNou = await r.Content.ReadFromJsonAsync<JsonElement>();
        h.Check(r.StatusCode == HttpStatusCode.Accepted && raspunsNou.GetProperty("mesaj").GetString()!.Contains("cod de confirmare"), "registration asks for the emailed code");
        h.Check((await anonim.GetAsync("/utilizatori/me")).StatusCode == HttpStatusCode.Unauthorized, "registering does not sign in");
        var login = await anonim.PostAsJsonAsync("/autentificare", new { identificator = "ana", parola = Harness.ParolaTest });
        var corp = await login.Content.ReadFromJsonAsync<JsonElement>();
        h.Check(login.StatusCode == HttpStatusCode.Forbidden && corp.GetProperty("cod").GetString() == "email_neconfirmat", "an unconfirmed account cannot sign in");
        h.Check((await anonim.PostAsJsonAsync("/autentificare", new { identificator = "ana", parola = "gresita-123" })).StatusCode == HttpStatusCode.Unauthorized,
            "with a wrong password nothing reveals the account is unconfirmed");

        // Same answer for an email that already has an account; the owner is told.
        var dublu = await anonim.PostAsJsonAsync("/autentificare/inregistrare", new { nume = "Altcineva", username = "altcineva", email = "GEORGE@example.invalid", parola = Harness.ParolaTest });
        var raspunsDublu = await dublu.Content.ReadFromJsonAsync<JsonElement>();
        h.Check(dublu.StatusCode == r.StatusCode && raspunsDublu.GetProperty("mesaj").GetString()!.Replace("GEORGE@example.invalid", "x") == raspunsNou.GetProperty("mesaj").GetString()!.Replace("ana@example.invalid", "x"),
            "an email that already has an account gets the same answer");
        h.Check(await h.Db(db => db.Utilizatori.CountAsync(u => u.Username == "altcineva")) == 0, "no second account for that email");
        await h.Serviciu<ProcesorMesaje>().RuleazaOdataAsync(CancellationToken.None);
        h.Check(h.Emailuri.Trimise.Any(m => m.Catre == "george@example.invalid" && m.Subiect.Contains("Cineva a încercat")), "the owner is told someone tried to register");

        // Codes: wrong code refused; resend replaces the code; five wrong attempts lock it.
        var primul = h.Emailuri.UltimulCod("ana@example.invalid");
        h.Check((await anonim.PostAsJsonAsync("/autentificare/confirma-email", new { email = "ana@example.invalid", cod = "000000" })).StatusCode == HttpStatusCode.BadRequest,
            "wrong code refused");
        await anonim.PostAsJsonAsync("/autentificare/retrimite-confirmare", new { email = "ana@example.invalid" });
        var alDoilea = h.Emailuri.UltimulCod("ana@example.invalid");
        h.Check(alDoilea != primul && (await anonim.PostAsJsonAsync("/autentificare/confirma-email", new { email = "ana@example.invalid", cod = primul })).StatusCode == HttpStatusCode.BadRequest,
            "a resent code replaces the previous one");
        for (var i = 0; i < 5; i++) await anonim.PostAsJsonAsync("/autentificare/confirma-email", new { email = "ana@example.invalid", cod = "111111" });
        h.Check((await anonim.PostAsJsonAsync("/autentificare/confirma-email", new { email = "ana@example.invalid", cod = alDoilea })).StatusCode == HttpStatusCode.BadRequest,
            "after five wrong attempts the code stops working");
        await anonim.PostAsJsonAsync("/autentificare/retrimite-confirmare", new { email = "ana@example.invalid" });
        var ok = await anonim.PostAsJsonAsync("/autentificare/confirma-email", new { email = "ana@example.invalid", cod = h.Emailuri.UltimulCod("ana@example.invalid") });
        h.Check(ok.IsSuccessStatusCode && (await anonim.GetAsync("/utilizatori/me")).IsSuccessStatusCode, "the right code confirms the account and signs in");
        h.Check((await anonim.PostAsJsonAsync("/autentificare/retrimite-confirmare", new { email = "nimeni@example.invalid" })).IsSuccessStatusCode,
            "resending to an unknown email answers the same");

        // Expiry: a warning (with a new code) in the last hour, then deletion with everything that belongs to the account.
        using var prof = h.Anonim();
        using var f = new MultipartFormDataContent
        {
            { new StringContent("Profesor Uitat"), "Nume" }, { new StringContent("uitat"), "Username" }, { new StringContent("uitat@example.invalid"), "Email" },
            { new StringContent(Harness.ParolaTest), "Parola" }, { new StringContent("0755111222"), "Telefon" },
            { new StringContent("Predau de mulți ani."), "Descriere" }, { new StringContent("1"), "MaterieIds" },
            { new StringContent(Harness.IdLiceu.ToString()), "OrganizatieIds" }, { new StringContent("40"), "JudetId" },
        };
        f.Add(Fisier([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13]), "Poza", "poza.png");
        f.Add(Fisier("%PDF-1.4\n"u8.ToArray()), "Dovada", "dovada.pdf");
        h.Check((await prof.PostAsync("/autentificare/inregistrare-profesor", f)).StatusCode == HttpStatusCode.Accepted, "professor applies (unconfirmed)");
        var uitat = await h.Db(db => db.Utilizatori.Where(u => u.Username == "uitat").Select(u => new { u.Id, u.ProfilMentor!.PozaUrl, u.ProfilMentor.DovadaCalificareUrl }).SingleAsync());
        var director = h.Serviciu<Microsoft.Extensions.Configuration.IConfiguration>()["Stocare:Director"]!;

        h.Ceas.Advance(TimeSpan.FromHours(22));
        await h.Serviciu<CuratareConturi>().RuleazaOdataAsync(CancellationToken.None);
        h.Check(await h.Db(db => db.Utilizatori.Where(u => u.Id == uitat.Id).Select(u => u.AvertizatLa).SingleAsync()) is null, "no warning before the last hour");
        h.Ceas.Advance(TimeSpan.FromHours(1.1));
        await h.Serviciu<CuratareConturi>().RuleazaOdataAsync(CancellationToken.None);
        await h.Serviciu<ProcesorMesaje>().RuleazaOdataAsync(CancellationToken.None);
        h.Check(h.Emailuri.Trimise.Any(m => m.Catre == "uitat@example.invalid" && m.Subiect.Contains("se șterge")), "warning email in the last hour");
        await h.Serviciu<CuratareConturi>().RuleazaOdataAsync(CancellationToken.None);
        h.Check(h.Emailuri.Trimise.Count(m => m.Catre == "uitat@example.invalid" && m.Subiect.Contains("se șterge")) == 1, "the warning is sent only once");

        h.Ceas.Advance(TimeSpan.FromHours(1));
        await h.Serviciu<CuratareConturi>().RuleazaOdataAsync(CancellationToken.None);
        await h.Serviciu<ProcesorMesaje>().RuleazaOdataAsync(CancellationToken.None);
        h.Check(await h.Db(db => db.Utilizatori.AnyAsync(u => u.Id == uitat.Id)) == false, "unconfirmed account deleted after 24 hours");
        h.Check(!File.Exists(Path.Combine(director, uitat.PozaUrl!)) && !File.Exists(Path.Combine(director, uitat.DovadaCalificareUrl!)), "its uploaded files deleted");
        h.Check(h.Emailuri.Trimise.Any(m => m.Catre == "uitat@example.invalid" && m.Subiect.Contains("a fost șters")), "the person is told the account was deleted");
        var orfane = await h.Db(async db =>
        {
            var c = db.Database.GetDbConnection();
            await c.OpenAsync();
            await using var cmd = c.CreateCommand();
            // Every column in the schema named like a user reference, checked for the deleted id.
            cmd.CommandText = $"""
                SELECT string_agg(table_name || '.' || column_name, ', ') FROM information_schema.columns
                WHERE table_schema = 'mark_ai' AND column_name IN ('utilizator_id', 'elev_id', 'mentor_id', 'owner_id', 'creat_de_id', 'verificat_de_id')
                """;
            var coloane = ((string)(await cmd.ExecuteScalarAsync())!).Split(", ");
            var gasite = new List<string>();
            foreach (var col in coloane)
            {
                var (tabel, coloana) = (col.Split('.')[0], col.Split('.')[1]);
                cmd.CommandText = $"SELECT count(*) FROM mark_ai.\"{tabel}\" WHERE \"{coloana}\" = {uitat.Id}";
                if ((long)(await cmd.ExecuteScalarAsync())! > 0) gasite.Add(col);
            }
            return gasite;
        });
        h.Check(orfane.Count == 0, "no row anywhere still points at the deleted account" + (orfane.Count == 0 ? "" : ": " + string.Join(", ", orfane)));
        h.Check(await h.Db(db => db.Utilizatori.AnyAsync(u => u.Username == "ana")), "confirmed accounts are never deleted");

        // Phone verification by SMS.
        using var george = await h.Login("george");
        h.Check((await george.PostAsync("/linia-ai/suna-ma", null)).StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict, "no call without a verified phone");
        r = await george.PostAsJsonAsync("/utilizatori/me/setari/telefon/cod", new { telefon = "0766111222" });
        h.Check(r.IsSuccessStatusCode && h.Sms.Trimise.Last().Catre == "+40766111222", "SMS code sent to the new number");
        h.Check((await george.PostAsJsonAsync("/utilizatori/me/setari/telefon/confirma", new { telefon = "0766111222", cod = "000000" })).StatusCode == HttpStatusCode.BadRequest,
            "wrong SMS code refused");
        h.Check((await george.PostAsJsonAsync("/utilizatori/me/setari/telefon/confirma", new { telefon = "0766999999", cod = h.Sms.UltimulCod() })).StatusCode == HttpStatusCode.BadRequest,
            "a code works only for the number it was sent to");
        r = await george.PostAsJsonAsync("/utilizatori/me/setari/telefon/confirma", new { telefon = "0766111222", cod = h.Sms.UltimulCod() });
        var eu = await george.GetFromJsonAsync<JsonElement>("/utilizatori/me");
        h.Check(r.IsSuccessStatusCode && eu.GetProperty("telefon").GetString() == "+40766111222" && eu.GetProperty("telefonVerificat").GetBoolean(), "number verified and saved");
        await george.PatchAsJsonAsync("/utilizatori/me/setari", new { nume = "George Pavel", username = "george", telefon = "0766222333" });
        h.Check(!(await george.GetFromJsonAsync<JsonElement>("/utilizatori/me")).GetProperty("telefonVerificat").GetBoolean(), "changing the number in the profile removes the verification");
        h.Check((await george.PostAsJsonAsync("/utilizatori/me/setari/telefon/cod", new { telefon = "0766333444" })).IsSuccessStatusCode
            && (await george.PostAsJsonAsync("/utilizatori/me/setari/telefon/cod", new { telefon = "0766333445" })).IsSuccessStatusCode, "second and third SMS today");
        h.Check((await george.PostAsJsonAsync("/utilizatori/me/setari/telefon/cod", new { telefon = "0766444555" })).StatusCode == HttpStatusCode.Conflict,
            "daily SMS cap stops further codes (they cost money)");

        // Outbox: a failed notification is retried later.
        h.Emailuri.Esueaza = true;
        await anonim.PostAsJsonAsync("/autentificare/inregistrare", new { nume = "X", username = "x2", email = "ana@example.invalid", parola = Harness.ParolaTest });
        await h.Serviciu<ProcesorMesaje>().RuleazaOdataAsync(CancellationToken.None);
        var esuat = await h.Db(db => db.Set<MesajIesire>().OrderByDescending(m => m.Id).FirstAsync(m => m.Tip == "incercare_inregistrare" && m.Catre == "ana@example.invalid"));
        h.Check(esuat.TrimisLa is null && esuat.Incercari == 1 && esuat.Eroare is not null, "a failed email is kept for a retry");
        h.Emailuri.Esueaza = false;
        h.Ceas.Advance(TimeSpan.FromMinutes(1.5));
        await h.Serviciu<ProcesorMesaje>().RuleazaOdataAsync(CancellationToken.None);
        h.Check(await h.Db(db => db.Set<MesajIesire>().Where(m => m.Id == esuat.Id).Select(m => m.TrimisLa).SingleAsync()) is not null, "and sent on the retry");
        h.Check(await h.Db(db => db.Set<MesajIesire>().Where(m => m.Id == esuat.Id).Select(m => m.Html).SingleAsync()) is null,
            "once delivered by a real provider, the content is not kept");

        // Admin list of sent messages.
        using var admin = await h.Login("admin");
        var lista = await admin.GetFromJsonAsync<JsonElement>("/admin/mesaje");
        h.Check(lista.GetProperty("elemente").GetArrayLength() > 5 && lista.GetProperty("elemente").EnumerateArray().All(m => m.GetProperty("continut").ValueKind == JsonValueKind.Null),
            "admin sees the sent messages, without content for real providers");
        h.Check((await george.GetAsync("/admin/mesaje")).StatusCode == HttpStatusCode.Forbidden, "only admins see the messages");
    }

    static ByteArrayContent Fisier(byte[] date)
    {
        var c = new ByteArrayContent(date);
        c.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return c;
    }
}

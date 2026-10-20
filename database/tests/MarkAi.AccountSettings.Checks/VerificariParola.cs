using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MarkAi.Api.Servicii.Mesagerie;

/// <summary>Forgotten password: a code by email or SMS, the same answer for unknown accounts, sessions closed after the change.</summary>
static class VerificariParola
{
    public static readonly Dictionary<string, string?> Setari = new() { ["Limite:AutentificariPeMinut"] = "100", ["Limite:EmailuriPeOra"] = "100" };
    const string Noua = "Parola-noua-2026!";

    public static async Task Ruleaza(Harness h)
    {
        using var anonim = h.Anonim();
        using var altDispozitiv = await h.Login("george");

        var necunoscut = await (await anonim.PostAsJsonAsync("/autentificare/parola-uitata", new { identificator = "nimeni", canal = "email" })).Content.ReadFromJsonAsync<JsonElement>();
        var r = await anonim.PostAsJsonAsync("/autentificare/parola-uitata", new { identificator = "george", canal = "email" });
        var cunoscut = await r.Content.ReadFromJsonAsync<JsonElement>();
        h.Check(r.IsSuccessStatusCode && cunoscut.GetProperty("mesaj").GetString() == necunoscut.GetProperty("mesaj").GetString(),
            "the answer is the same whether or not the account exists");
        h.Check(h.Emailuri.Trimise.Last().Catre == "george@example.invalid" && h.Emailuri.Trimise.Last().Subiect.Contains("parolă nouă"), "reset code sent by email");
        var cod = h.Emailuri.UltimulCod("george@example.invalid");

        h.Check((await anonim.PostAsJsonAsync("/autentificare/parola-noua", new { identificator = "george", cod, parola = "scurta" })).StatusCode == HttpStatusCode.BadRequest,
            "too short a password refused (the code stays usable)");
        h.Check((await anonim.PostAsJsonAsync("/autentificare/parola-noua", new { identificator = "george", cod = "000000", parola = Noua })).StatusCode == HttpStatusCode.BadRequest,
            "wrong code refused");
        h.Check((await anonim.PostAsJsonAsync("/autentificare/parola-noua", new { identificator = "george", cod, parola = Noua })).IsSuccessStatusCode, "password changed with the code");
        h.Check((await anonim.PostAsJsonAsync("/autentificare/parola-noua", new { identificator = "george", cod, parola = "Inca-una-2026!" })).StatusCode == HttpStatusCode.BadRequest,
            "the code works only once");
        h.Check((await anonim.PostAsJsonAsync("/autentificare", new { identificator = "george", parola = Harness.ParolaTest })).StatusCode == HttpStatusCode.Unauthorized
            && (await anonim.PostAsJsonAsync("/autentificare", new { identificator = "george", parola = Noua })).IsSuccessStatusCode, "old password no longer works, the new one does");
        h.Check((await altDispozitiv.GetAsync("/utilizatori/me")).StatusCode == HttpStatusCode.Unauthorized, "every other session is closed");
        await h.Serviciu<ProcesorMesaje>().RuleazaOdataAsync(CancellationToken.None);
        h.Check(h.Emailuri.Trimise.Any(m => m.Catre == "george@example.invalid" && m.Subiect.Contains("a fost schimbată")), "the owner is told the password changed");

        // By SMS, to the account's verified phone.
        using var george = await h.Login("george", Noua);
        await h.VerificaTelefon(george, "0777111222");
        var sms = h.Sms.Trimise.Count;
        await anonim.PostAsJsonAsync("/autentificare/parola-uitata", new { identificator = "george@example.invalid", canal = "sms" });
        h.Check(h.Sms.Trimise.Count == sms + 1 && h.Sms.Trimise.Last().Catre == "+40777111222", "reset code sent by SMS to the verified phone");
        h.Check((await anonim.PostAsJsonAsync("/autentificare/parola-noua", new { identificator = "george@example.invalid", cod = h.Sms.UltimulCod(), parola = "Prin-sms-2026!" })).IsSuccessStatusCode,
            "SMS code resets the password");

        // SMS asked for, but no verified phone: the code goes by email.
        sms = h.Sms.Trimise.Count;
        await anonim.PostAsJsonAsync("/autentificare/parola-uitata", new { identificator = "admin", canal = "sms" });
        h.Check(h.Sms.Trimise.Count == sms && h.Emailuri.Trimise.Last().Catre == "admin@example.invalid", "without a verified phone the code goes by email");

        // An unconfirmed account that resets by email is confirmed (the code proved the address).
        await anonim.PostAsJsonAsync("/autentificare/inregistrare", new { nume = "Uitucul", username = "uituc", email = "uituc@example.invalid", parola = Harness.ParolaTest });
        await anonim.PostAsJsonAsync("/autentificare/parola-uitata", new { identificator = "uituc", canal = "email" });
        await anonim.PostAsJsonAsync("/autentificare/parola-noua", new { identificator = "uituc", cod = h.Emailuri.UltimulCod("uituc@example.invalid"), parola = Noua });
        h.Check((await anonim.PostAsJsonAsync("/autentificare", new { identificator = "uituc", parola = Noua })).IsSuccessStatusCode, "a reset by email also confirms the account");
    }
}

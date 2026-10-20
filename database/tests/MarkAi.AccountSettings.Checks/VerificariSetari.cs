using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

/// <summary>Account settings: profile fields, CSRF header, emailed one-time codes for password and email changes.</summary>
static class VerificariSetari
{
    public static async Task Ruleaza(Harness h)
    {
        using var anon = h.Anonim();
        h.Check((await anon.GetAsync("/utilizatori/me/setari")).StatusCode == HttpStatusCode.Unauthorized, "anonymous account access rejected");
        using var student = await h.Login("george"); using var teacher = await h.Login("irina"); using var adminUser = await h.Login("admin");
        var profile = await student.GetFromJsonAsync<JsonElement>("/utilizatori/me/setari");
        h.Check(profile.GetProperty("utilizator").GetProperty("rol").GetString() == "elev" && profile.GetProperty("profesor").ValueKind == JsonValueKind.Null, "student profile omits teacher fields");
        h.Check((await student.PatchAsJsonAsync("/utilizatori/me/setari", new { nume="George Pavel", username="george", telefon="", descriere="pretend teacher", materieIds=new[] {1} })).StatusCode == HttpStatusCode.BadRequest, "student cannot submit teacher profile");
        var save = await student.PatchAsJsonAsync("/utilizatori/me/setari", new { nume="George Actualizat", username="george_nou", telefon="0722111222" });
        h.Check(save.IsSuccessStatusCode, "student personal fields saved");
        profile = await student.GetFromJsonAsync<JsonElement>("/utilizatori/me/setari");
        h.Check(profile.GetProperty("utilizator").GetProperty("telefon").GetString() == "+40722111222", "phone normalized and persisted");
        h.Check((await student.PatchAsJsonAsync("/utilizatori/me/setari", new { nume="George", username="irina", telefon="" })).StatusCode == HttpStatusCode.BadRequest, "duplicate username rejected");
        student.DefaultRequestHeaders.Remove("X-MarkAi");
        h.Check((await student.PatchAsJsonAsync("/utilizatori/me/setari", new { nume="George", username="george_nou", telefon="" })).StatusCode == HttpStatusCode.Forbidden, "cross-site simple mutation rejected");
        student.DefaultRequestHeaders.Add("X-MarkAi", "1");
        h.Check((await adminUser.GetAsync("/utilizatori/me/setari")).IsSuccessStatusCode, "administrator can edit own account");
        var mentor = await teacher.GetFromJsonAsync<JsonElement>("/utilizatori/me/setari");
        h.Check(mentor.GetProperty("profesor").GetProperty("areDovada").GetBoolean(), "teacher current qualification state returned");
        var teacherSaved = await teacher.PatchAsJsonAsync("/utilizatori/me/setari", new { nume="Irina Marin", username="irina", telefon="0722123456", descriere="Profil actualizat", materieIds=new[] {1,2} });
        h.Check(teacherSaved.IsSuccessStatusCode, "teacher biography and subjects saved");
        mentor = await teacher.GetFromJsonAsync<JsonElement>("/utilizatori/me/setari");
        h.Check(mentor.GetProperty("profesor").GetProperty("stareVerificare").GetString() == "in_asteptare", "changed subjects require new approval");
        async Task<string> Request(string purpose, string value, string password=Harness.ParolaTest)
        {
            var r = await student.PostAsJsonAsync("/utilizatori/me/setari/confirmari", new { scop=purpose, parolaActuala=password, valoareNoua=value });
            r.EnsureSuccessStatusCode(); return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        }
        var id = await Request("parola", "New-test-password-2026!"); var code = h.Mail.Code;
        h.Check(h.Mail.Email == "george@example.invalid", "OTP sent to existing account email, not supplied destination");
        h.Check((await teacher.PostAsJsonAsync("/utilizatori/me/setari/confirmari/finalizeaza", new {id,cod=code})).StatusCode == HttpStatusCode.BadRequest, "OTP bound to account");
        h.Check((await student.PostAsJsonAsync("/utilizatori/me/setari/confirmari/finalizeaza", new {id,cod="bad"})).StatusCode == HttpStatusCode.BadRequest, "incorrect OTP rejected");
        h.Check((await student.PostAsJsonAsync("/utilizatori/me/setari/confirmari/finalizeaza", new {id,cod=code})).IsSuccessStatusCode, "password changed only after correct OTP");
        h.Check((await student.PostAsJsonAsync("/utilizatori/me/setari/confirmari/finalizeaza", new {id,cod=code})).StatusCode == HttpStatusCode.BadRequest, "used OTP cannot be replayed");
        using var verifyLogin = h.Anonim();
        h.Check((await verifyLogin.PostAsJsonAsync("/autentificare",new {identificator="george_nou",parola="New-test-password-2026!"})).IsSuccessStatusCode, "new password works for login");
        h.Check((await verifyLogin.PostAsJsonAsync("/autentificare",new {identificator="george_nou",parola=Harness.ParolaTest})).StatusCode == HttpStatusCode.Unauthorized, "old password no longer works");
        h.Check((await student.PostAsJsonAsync("/utilizatori/me/setari/confirmari",new {scop="email",parolaActuala="New-test-password-2026!",valoreNoua="new@example.invalid"})).StatusCode == HttpStatusCode.BadRequest, "challenge resend throttled");
        h.Ceas.Advance(TimeSpan.FromMinutes(2));
        id = await Request("email","new@example.invalid","New-test-password-2026!"); code=h.Mail.Code;
        h.Ceas.Advance(TimeSpan.FromMinutes(6));
        h.Check((await student.PostAsJsonAsync("/utilizatori/me/setari/confirmari/finalizeaza",new {id,cod=code})).StatusCode == HttpStatusCode.BadRequest,"expired OTP rejected");
        id=await Request("email","new@example.invalid","New-test-password-2026!"); code=h.Mail.Code;
        h.Check((await student.PostAsJsonAsync("/utilizatori/me/setari/confirmari/finalizeaza",new {id,cod=code})).IsSuccessStatusCode,"email change confirmed with second factor");
        profile=await student.GetFromJsonAsync<JsonElement>("/utilizatori/me/setari");
        h.Check(profile.GetProperty("utilizator").GetProperty("email").GetString()=="new@example.invalid","new email persisted");
        h.Ceas.Advance(TimeSpan.FromMinutes(2)); h.Mail.Fail=true;
        h.Check((await student.PostAsJsonAsync("/utilizatori/me/setari/confirmari",new {scop="parola",parolaActuala="New-test-password-2026!",valoareNoua="Another-password-2026!"})).StatusCode==HttpStatusCode.ServiceUnavailable,"mail failure never claims code was sent");
    }
}

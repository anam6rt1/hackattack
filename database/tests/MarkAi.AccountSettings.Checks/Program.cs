using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MarkAi.Api.Endpoints;
using MarkAi.Api.Infrastructura;
using MarkAi.Api.Servicii;
using MarkAi.Api.Setari;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Npgsql;

var adminConnection = Environment.GetEnvironmentVariable("MARKAI_TEST_CONNECTION")
    ?? throw new Exception("Set MARKAI_TEST_CONNECTION to a test PostgreSQL server with CREATEDB permission.");
var name = "markai_settings_" + Guid.NewGuid().ToString("N");
await using (var admin = new NpgsqlConnection(adminConnection))
{
    await admin.OpenAsync();
    await new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin).ExecuteNonQueryAsync();
}
var connection = new NpgsqlConnectionStringBuilder(adminConnection) { Database = name }.ConnectionString;
var options = new DbContextOptionsBuilder<MarkAiDbContext>().UseNpgsql(connection, x => x.MigrationsHistoryTable("__EFMigrationsHistory", "mark_ai")).Options;
WebApplication? app = null;
try
{
    await using (var db = new MarkAiDbContext(options))
    {
        await db.Database.MigrateAsync();
        var roles = new[] { new Rol { Nume = "elev" }, new Rol { Nume = "profesor" }, new Rol { Nume = "admin" } };
        var subjects = new[] { new Materie { Denumire = "Limba și literatura română" }, new Materie { Denumire = "Matematică" }, new Materie { Denumire = "Biologie" } };
        db.AddRange(roles); db.AddRange(subjects); await db.SaveChangesAsync();
        foreach (var (username, fullName, role) in new[] { ("george", "George Pavel", 0), ("irina", "Irina Marin", 1), ("admin", "Administrator", 2) })
        {
            var u = new Utilizator { Nume = fullName, Username = username, Email = username + "@example.invalid", Telefon = role == 1 ? "+40722123456" : null };
            u.ParolaHash = Parole.Hash(u, "Test-demo-2026!");
            u.Roluri.Add(new UtilizatorRol { RolId = roles[role].Id });
            if (role == 1) u.ProfilMentor = new ProfilMentor { StareVerificare = "aprobat", Descriere = "Profesor de limba română, cu 8 ani de experiență. Învățăm cu răbdare și exemple clare.", PozaUrl = "fixture-photo.png", DovadaCalificareUrl = "fixture-diploma.pdf", Materii = new List<MentorMaterie> { new() { MaterieId = subjects[0].Id } } };
            db.Add(u);
        }
        await db.SaveChangesAsync();
    }
    var builder = WebApplication.CreateBuilder();
    builder.Environment.EnvironmentName = "Development";
    builder.WebHost.UseUrls("http://127.0.0.1:5081");
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Logging:LogLevel:Default"] = "Warning" });
    builder.Services.AddDbContext<MarkAiDbContext>(x => x.UseNpgsql(connection));
    builder.Services.AddSingleton<Stocare>(); builder.Services.AdaugaSetariCont();
    var mail = new FakeMail(); var clock = new FakeClock();
    builder.Services.AddSingleton<IEmailConfirmare>(mail); builder.Services.AddSingleton<TimeProvider>(args.Contains("--preview") ? TimeProvider.System : clock);
    ConfigurareAutentificare.Adauga(builder.Services, ConfigurareAutentificare.Optiuni(builder.Configuration, builder.Environment));
    app = builder.Build(); app.UseMiddleware<TratareErori>(); app.UseAuthentication(); app.UseAuthorization();
    AutentificareEndpoints.Mapeaza(app); app.MapeazaSetariCont();
    app.MapGet("/materii", async (MarkAiDbContext db) => new { elemente = await db.Materii.Select(x => new { x.Id, x.Denumire }).ToListAsync() });
    app.MapGet("/progres", () => new { nivel = new { nivel = 1, xpTotal = 200 } });
    app.MapGet("/utilizatori/me/preferinte", () => new { mod = "auto" });
    await app.StartAsync();
    if (args.Contains("--preview"))
    {
        Console.WriteLine("Preview fixtures only: George/Irina/Admin; usernames george, irina, admin; password Test-demo-2026!; API 127.0.0.1:5081.");
        await app.WaitForShutdownAsync(); return;
    }
    var count = 0;
    void Check(bool condition, string label) { if(!condition) throw new Exception("FAIL: " + label); Console.WriteLine("PASS: " + label); count++; }
    async Task<HttpClient> Login(string username)
    {
        var client = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer() }) { BaseAddress = new Uri("http://127.0.0.1:5081") };
        var r = await client.PostAsJsonAsync("/autentificare", new { identificator = username, parola = "Test-demo-2026!" });
        r.EnsureSuccessStatusCode(); client.DefaultRequestHeaders.Add("X-MarkAi-Settings", "1"); return client;
    }
    using var anon = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5081") };
    Check((await anon.GetAsync("/utilizatori/me/setari")).StatusCode == HttpStatusCode.Unauthorized, "anonymous account access rejected");
    using var student = await Login("george"); using var teacher = await Login("irina"); using var adminUser = await Login("admin");
    var profile = await student.GetFromJsonAsync<JsonElement>("/utilizatori/me/setari");
    Check(profile.GetProperty("utilizator").GetProperty("rol").GetString() == "elev" && profile.GetProperty("profesor").ValueKind == JsonValueKind.Null, "student profile omits teacher fields");
    Check((await student.PatchAsJsonAsync("/utilizatori/me/setari", new { nume="George Pavel", username="george", telefon="", descriere="pretend teacher", materieIds=new[] {1} })).StatusCode == HttpStatusCode.BadRequest, "student cannot submit teacher profile");
    var save = await student.PatchAsJsonAsync("/utilizatori/me/setari", new { nume="George Actualizat", username="george_nou", telefon="0722111222" });
    Check(save.IsSuccessStatusCode, "student personal fields saved");
    profile = await student.GetFromJsonAsync<JsonElement>("/utilizatori/me/setari");
    Check(profile.GetProperty("utilizator").GetProperty("telefon").GetString() == "+40722111222", "phone normalized and persisted");
    Check((await student.PatchAsJsonAsync("/utilizatori/me/setari", new { nume="George", username="irina", telefon="" })).StatusCode == HttpStatusCode.BadRequest, "duplicate username rejected");
    student.DefaultRequestHeaders.Remove("X-MarkAi-Settings");
    Check((await student.PatchAsJsonAsync("/utilizatori/me/setari", new { nume="George", username="george_nou", telefon="" })).StatusCode == HttpStatusCode.Forbidden, "cross-site simple mutation rejected");
    student.DefaultRequestHeaders.Add("X-MarkAi-Settings", "1");
    Check((await adminUser.GetAsync("/utilizatori/me/setari")).IsSuccessStatusCode, "administrator can edit own account");
    var mentor = await teacher.GetFromJsonAsync<JsonElement>("/utilizatori/me/setari");
    Check(mentor.GetProperty("profesor").GetProperty("areDovada").GetBoolean(), "teacher current qualification state returned");
    var teacherSaved = await teacher.PatchAsJsonAsync("/utilizatori/me/setari", new { nume="Irina Marin", username="irina", telefon="0722123456", descriere="Profil actualizat", materieIds=new[] {1,2} });
    Check(teacherSaved.IsSuccessStatusCode, "teacher biography and subjects saved");
    mentor = await teacher.GetFromJsonAsync<JsonElement>("/utilizatori/me/setari");
    Check(mentor.GetProperty("profesor").GetProperty("stareVerificare").GetString() == "in_asteptare", "changed subjects require new approval");
    async Task<string> Request(string purpose, string value, string password="Test-demo-2026!")
    {
        var r = await student.PostAsJsonAsync("/utilizatori/me/setari/confirmari", new { scop=purpose, parolaActuala=password, valoareNoua=value });
        r.EnsureSuccessStatusCode(); return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }
    var id = await Request("parola", "New-test-password-2026!"); var code = mail.Code;
    Check(mail.Email == "george@example.invalid", "OTP sent to existing account email, not supplied destination");
    Check((await teacher.PostAsJsonAsync("/utilizatori/me/setari/confirmari/finalizeaza", new {id,cod=code})).StatusCode == HttpStatusCode.BadRequest, "OTP bound to account");
    Check((await student.PostAsJsonAsync("/utilizatori/me/setari/confirmari/finalizeaza", new {id,cod="bad"})).StatusCode == HttpStatusCode.BadRequest, "incorrect OTP rejected");
    Check((await student.PostAsJsonAsync("/utilizatori/me/setari/confirmari/finalizeaza", new {id,cod=code})).IsSuccessStatusCode, "password changed only after correct OTP");
    Check((await student.PostAsJsonAsync("/utilizatori/me/setari/confirmari/finalizeaza", new {id,cod=code})).StatusCode == HttpStatusCode.BadRequest, "used OTP cannot be replayed");
    using var verifyLogin = new HttpClient { BaseAddress=student.BaseAddress };
    Check((await verifyLogin.PostAsJsonAsync("/autentificare",new {identificator="george_nou",parola="New-test-password-2026!"})).IsSuccessStatusCode, "new password works for login");
    Check((await verifyLogin.PostAsJsonAsync("/autentificare",new {identificator="george_nou",parola="Test-demo-2026!"})).StatusCode == HttpStatusCode.Unauthorized, "old password no longer works");
    Check((await student.PostAsJsonAsync("/utilizatori/me/setari/confirmari",new {scop="email",parolaActuala="New-test-password-2026!",valoreNoua="new@example.invalid"})).StatusCode == HttpStatusCode.BadRequest, "challenge resend throttled");
    clock.Advance(TimeSpan.FromMinutes(2));
    id = await Request("email","new@example.invalid","New-test-password-2026!"); code=mail.Code;
    clock.Advance(TimeSpan.FromMinutes(6));
    Check((await student.PostAsJsonAsync("/utilizatori/me/setari/confirmari/finalizeaza",new {id,cod=code})).StatusCode == HttpStatusCode.BadRequest,"expired OTP rejected");
    id=await Request("email","new@example.invalid","New-test-password-2026!"); code=mail.Code;
    Check((await student.PostAsJsonAsync("/utilizatori/me/setari/confirmari/finalizeaza",new {id,cod=code})).IsSuccessStatusCode,"email change confirmed with second factor");
    profile=await student.GetFromJsonAsync<JsonElement>("/utilizatori/me/setari");
    Check(profile.GetProperty("utilizator").GetProperty("email").GetString()=="new@example.invalid","new email persisted");
    clock.Advance(TimeSpan.FromMinutes(2)); mail.Fail=true;
    Check((await student.PostAsJsonAsync("/utilizatori/me/setari/confirmari",new {scop="parola",parolaActuala="New-test-password-2026!",valoareNoua="Another-password-2026!"})).StatusCode==HttpStatusCode.ServiceUnavailable,"mail failure never claims code was sent");
    Console.WriteLine($"All {count} checks passed on isolated PostgreSQL. No real emails sent.");
}
finally
{
    if(app is not null) { await app.StopAsync(); await app.DisposeAsync(); }
    NpgsqlConnection.ClearAllPools();
    await using var admin=new NpgsqlConnection(adminConnection); await admin.OpenAsync();
    await new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)",admin).ExecuteNonQueryAsync();
}

sealed class FakeMail : IEmailConfirmare
{
    public string Code="", Email=""; public bool Fail;
    public Task Trimite(string email,string cod,string actiune,CancellationToken ct)
    { if(Fail) throw new EmailNeconfiguratException(); Email=email; Code=cod; return Task.CompletedTask; }
}
sealed class FakeClock : TimeProvider
{
    private DateTimeOffset now=DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow()=>now;
    public void Advance(TimeSpan delta)=>now+=delta;
}

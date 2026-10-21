using System.Net;
using MarkAi.Api.Servicii;
using MarkAi.Api.Servicii.Mesagerie;
using MarkAi.Data.Entities;
using MarkAi.Tests.Suport;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Conturi;

/// <summary>Accounts that never become active: a warning in the last hour, then deletion with everything that belongs to them.</summary>
public sealed class ExpirareContTests : TestCuHarness
{
    protected override IReadOnlyDictionary<string, string?> Setari => new Dictionary<string, string?>
    {
        ["Limite:EmailuriPeOra"] = "100", ["Limite:InregistrariPeOra"] = "100",
    };

    private async Task Curata()
    {
        await h.Serviciu<CuratareConturi>().RuleazaOdataAsync(Ct);
        await h.Serviciu<ProcesorMesaje>().RuleazaOdataAsync(Ct);
    }

    private async Task<int> ProfesorNeconfirmat()
    {
        using var f = new MultipartFormDataContent
        {
            { new StringContent("Profesor Uitat"), "Nume" }, { new StringContent("uitat"), "Username" }, { new StringContent("uitat@example.invalid"), "Email" },
            { new StringContent(Harness.ParolaTest), "Parola" }, { new StringContent("0755111222"), "Telefon" },
            { new StringContent("Predau de mulți ani."), "Descriere" }, { new StringContent("1"), "MaterieIds" },
            { new StringContent(Harness.IdLiceu.ToString()), "OrganizatieIds" }, { new StringContent("40"), "JudetId" },
        };
        f.Add(Raspunsuri.Fisier(Raspunsuri.Png), "Poza", "poza.png");
        f.Add(Raspunsuri.Fisier(Raspunsuri.Pdf), "Dovada", "dovada.pdf");
        await h.Anonim().PostAsync("/autentificare/inregistrare-profesor", f, Ct).Status(HttpStatusCode.Accepted);
        return await h.Db(db => db.Utilizatori.Where(u => u.Username == "uitat").Select(u => u.Id).SingleAsync());
    }

    private async Task<int> ElevNeconfirmat(string username = "ana")
    {
        await h.Anonim().Post("/autentificare/inregistrare", new { nume = "Ana Pop", username, email = username + "@example.invalid", parola = Harness.ParolaTest })
            .Status(HttpStatusCode.Accepted);
        return await h.Db(db => db.Utilizatori.Where(u => u.Username == username).Select(u => u.Id).SingleAsync());
    }

    private Task<bool> Exista(int id) => h.Db(db => db.Utilizatori.AnyAsync(u => u.Id == id));

    [Fact]
    public async Task NiciunAvertismentInainteDeUltimaOra()
    {
        var id = await ElevNeconfirmat();
        h.Ceas.Advance(TimeSpan.FromHours(22));
        await Curata();
        (await h.Db(db => db.Utilizatori.Where(u => u.Id == id).Select(u => u.AvertizatLa).SingleAsync())).ShouldBeNull();
    }

    [Fact]
    public async Task AvertismentInUltimaOraCuUnCodNou()
    {
        await ElevNeconfirmat();
        var vechi = h.Emailuri.UltimulCod("ana@example.invalid");
        h.Ceas.Advance(TimeSpan.FromHours(23.1));
        await Curata();
        var avertisment = h.Emailuri.Trimise.Last(m => m.Catre == "ana@example.invalid");
        avertisment.Subiect.ShouldContain("se șterge");
        var nou = h.Emailuri.UltimulCod("ana@example.invalid");
        if (nou != vechi) await h.Anonim().Post("/autentificare/confirma-email", new { email = "ana@example.invalid", cod = vechi }).Status(HttpStatusCode.BadRequest);
        await h.Anonim().Post("/autentificare/confirma-email", new { email = "ana@example.invalid", cod = nou }).Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AvertismentulSeTrimiteODataSingura()
    {
        await ElevNeconfirmat();
        h.Ceas.Advance(TimeSpan.FromHours(23.1));
        await Curata();
        await Curata();
        h.Emailuri.Trimise.Count(m => m.Catre == "ana@example.invalid" && m.Subiect.Contains("se șterge")).ShouldBe(1);
    }

    [Fact]
    public async Task ContulNeconfirmatSeStergeDupa24DeOre()
    {
        var id = await ElevNeconfirmat();
        h.Ceas.Advance(TimeSpan.FromHours(24.1));
        await Curata();
        (await Exista(id)).ShouldBeFalse();
        h.Emailuri.Trimise.ShouldContain(m => m.Catre == "ana@example.invalid" && m.Subiect.Contains("a fost șters"));
    }

    [Fact]
    public async Task OreleDeConfirmareSuntSetabile()
    {
        await using var scurt = await Harness.Porneste(setari: new Dictionary<string, string?> { ["Conturi:OreConfirmare"] = "2" });
        await scurt.Anonim().Post("/autentificare/inregistrare", new { nume = "Ana", username = "ana", email = "ana@example.invalid", parola = Harness.ParolaTest });
        scurt.Ceas.Advance(TimeSpan.FromHours(2.1));
        await scurt.Serviciu<CuratareConturi>().RuleazaOdataAsync(Ct);
        (await scurt.Db(db => db.Utilizatori.AnyAsync(u => u.Username == "ana"))).ShouldBeFalse();
    }

    [Fact]
    public async Task ContulConfirmatNuSeStergeNiciodata()
    {
        await h.InregistreazaElev("ana");
        h.Ceas.Advance(TimeSpan.FromDays(30));
        await Curata();
        (await h.Db(db => db.Utilizatori.AnyAsync(u => u.Username == "ana"))).ShouldBeTrue();
    }

    [Fact]
    public async Task FixturileVechiNuSuntAtinse()
    {
        h.Ceas.Advance(TimeSpan.FromDays(30));
        await Curata();
        (await h.Db(db => db.Utilizatori.CountAsync(u => new[] { "george", "irina", "admin", "director" }.Contains(u.Username)))).ShouldBe(4);
    }

    [Fact]
    public async Task FisiereleProfesoruluiSuntSterse()
    {
        var id = await ProfesorNeconfirmat();
        var fisiere = await h.Db(db => db.ProfiluriMentor.Where(p => p.UtilizatorId == id).Select(p => new[] { p.PozaUrl!, p.DovadaCalificareUrl! }).SingleAsync());
        fisiere.ShouldAllBe(f => File.Exists(Path.Combine(h.DirStocare, f)));
        h.Ceas.Advance(TimeSpan.FromHours(24.1));
        await Curata();
        fisiere.ShouldAllBe(f => !File.Exists(Path.Combine(h.DirStocare, f)));
    }

    [Fact]
    public async Task NiciunRandNuMaiArataSpreContulSters()
    {
        var id = await ProfesorNeconfirmat();
        h.Ceas.Advance(TimeSpan.FromHours(24.1));
        await Curata();
        var orfane = await h.Db(async db =>
        {
            var c = db.Database.GetDbConnection();
            await c.OpenAsync(Ct);
            await using var cmd = c.CreateCommand();
            // Every column in the schema named like a user reference, checked for the deleted id.
            cmd.CommandText = """
                SELECT string_agg(table_name || '.' || column_name, ', ') FROM information_schema.columns
                WHERE table_schema = 'mark_ai' AND column_name IN ('utilizator_id', 'elev_id', 'mentor_id', 'owner_id', 'creat_de_id', 'verificat_de_id', 'admin_id', 'autor_id', 'decis_de_id')
                """;
            var gasite = new List<string>();
            foreach (var col in ((string)(await cmd.ExecuteScalarAsync(Ct))!).Split(", "))
            {
                var (tabel, coloana) = (col.Split('.')[0], col.Split('.')[1]);
                cmd.CommandText = $"SELECT count(*) FROM mark_ai.\"{tabel}\" WHERE \"{coloana}\" = {id}";
                if ((long)(await cmd.ExecuteScalarAsync(Ct))! > 0) gasite.Add(col);
            }
            return gasite;
        });
        orfane.ShouldBeEmpty();
    }

    [Fact]
    public async Task CerereaDeMembruAProfesoruluiDispareCuContul()
    {
        var id = await ProfesorNeconfirmat();
        (await h.Db(db => db.Set<MembruOrganizatie>().AnyAsync(m => m.UtilizatorId == id))).ShouldBeTrue();
        h.Ceas.Advance(TimeSpan.FromHours(24.1));
        await Curata();
        (await h.Db(db => db.Set<MembruOrganizatie>().AnyAsync(m => m.UtilizatorId == id))).ShouldBeFalse();
    }

    [Fact]
    public async Task OrganizatiaNeaprobataDispareCuAdministratorul()
    {
        await h.Anonim().Post("/autentificare/inregistrare-organizatie", new
        {
            nume = "Olga Org", username = "olga", email = "olga@example.invalid", parola = Harness.ParolaTest, telefon = "0744000999",
            numeOrganizatie = "Școala Nouă", adresa = "Str. Nouă 1", judetId = 40, localitateId = (int?)null,
        }).Status(HttpStatusCode.Accepted);
        h.Ceas.Advance(TimeSpan.FromHours(24.1));
        await Curata();
        (await h.Db(db => db.Set<Organizatie>().AnyAsync(o => o.Nume == "Școala Nouă"))).ShouldBeFalse();
    }

    [Fact]
    public async Task ProfesorulNeaprobatPrimesteAmintireInZiuaASasea()
    {
        await h.InregistreazaElev("x"); // a confirmed account that is not a professor is never reminded
        var id = await ProfesorNeconfirmat();
        await h.Anonim().Post("/autentificare/confirma-email", new { email = "uitat@example.invalid", cod = h.Emailuri.UltimulCod("uitat@example.invalid") });
        h.Ceas.Advance(TimeSpan.FromDays(5.9));
        await Curata();
        h.Emailuri.Trimise.ShouldNotContain(m => m.Catre == "uitat@example.invalid" && m.Subiect.Contains("nu a fost încă aprobată"));
        h.Ceas.Advance(TimeSpan.FromDays(0.2));
        await Curata();
        h.Emailuri.Trimise.ShouldContain(m => m.Catre == "uitat@example.invalid" && m.Subiect.Contains("nu a fost încă aprobată"));
        h.Emailuri.Trimise.ShouldNotContain(m => m.Catre == "x@example.invalid" && m.Subiect.Contains("aprobată"));
        (await Exista(id)).ShouldBeTrue();
    }

    [Fact]
    public async Task ProfesorulNeaprobatInSapteZileEsteSters()
    {
        var id = await ProfesorNeconfirmat();
        await h.Anonim().Post("/autentificare/confirma-email", new { email = "uitat@example.invalid", cod = h.Emailuri.UltimulCod("uitat@example.invalid") });
        h.Ceas.Advance(TimeSpan.FromDays(7.1));
        await Curata();
        (await Exista(id)).ShouldBeFalse();
        h.Emailuri.Trimise.ShouldContain(m => m.Catre == "uitat@example.invalid" && m.Subiect.Contains("a fost șters"));
    }

    [Fact]
    public async Task ProfesorulAprobatRamane()
    {
        var id = await ProfesorNeconfirmat();
        await h.Anonim().Post("/autentificare/confirma-email", new { email = "uitat@example.invalid", cod = h.Emailuri.UltimulCod("uitat@example.invalid") });
        using var director = await h.Login("director");
        await director.Post($"/organizatia-mea/membri/{id}/aprobare").Status(HttpStatusCode.NoContent);
        h.Ceas.Advance(TimeSpan.FromDays(8));
        await Curata();
        (await Exista(id)).ShouldBeTrue();
    }

    [Fact]
    public async Task ContulCareConduceOOrganizatieAprobataNuEsteSters()
    {
        var id = await ElevNeconfirmat();
        await h.Db(async db =>
        {
            db.Add(new Organizatie { Nume = "Aprobată de test", Adresa = "x", JudetId = 40, Stare = Organizatie.Aprobata, AdminId = id, CreatLa = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });
        h.Ceas.Advance(TimeSpan.FromHours(24.1));
        await Curata();
        (await Exista(id)).ShouldBeTrue();
        h.Jurnal.Linii.ShouldContain(l => l.Contains($"Account {id} not deleted"));
    }

    [Fact]
    public async Task CerereaDeMembruNedecisaExpiraInSapteZile()
    {
        using var elev = await h.InregistreazaElev("ana");
        await elev.Post("/utilizatori/me/organizatii", new { organizatieId = Harness.IdLiceu }).Status(HttpStatusCode.OK);
        h.Ceas.Advance(TimeSpan.FromDays(6.1));
        await Curata();
        h.Emailuri.Trimise.ShouldContain(m => m.Catre == "director@example.invalid" && m.Subiect.Contains("expiră mâine"));
        h.Ceas.Advance(TimeSpan.FromDays(1));
        await Curata();
        (await h.Db(db => db.Set<MembruOrganizatie>().Where(m => m.Utilizator.Username == "ana").Select(m => m.Stare).SingleAsync()))
            .ShouldBe(MembruOrganizatie.Expirat);
        h.Emailuri.Trimise.ShouldContain(m => m.Catre == "ana@example.invalid" && m.Subiect.Contains("a expirat"));
        (await h.Db(db => db.Utilizatori.AnyAsync(u => u.Username == "ana"))).ShouldBeTrue();
    }
}

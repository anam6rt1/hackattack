using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MarkAi.Api.Servicii;
using MarkAi.Data.Entities;
using MarkAi.Tests.Suport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.SignalR.Client;

namespace MarkAi.Tests.Integration.Conturi;

public class DatePersonaleTests : TestCuHarness
{
    [Fact]
    public async Task StergereaBlocheazaSiConexiunileDeJocDejaDeschise()
    {
        await using var jucator = await Triviador.Jucator.Conecteaza(h, "george");
        (await jucator.Hub.InvokeAsync<string>("Instanta", Ct)).ShouldNotBeNullOrEmpty();
        (await Sterge(jucator.Http)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await Should.ThrowAsync<Exception>(async () => await jucator.Hub.InvokeAsync<string>("Instanta", Ct));
    }

    protected override IReadOnlyDictionary<string, string?> Setari => new Dictionary<string, string?> { ["Limite:AutentificariPeMinut"] = "200" };
    private Task<HttpResponseMessage> Sterge(HttpClient client, string parola = Harness.ParolaTest, string confirmare = "STERGE") =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/utilizatori/me/date") { Content = JsonContent.Create(new { parolaActuala = parola, confirmare }) }, Ct);

    [Fact]
    public async Task ConfiguratiaCompresieiEstePublicaSiNuSolicitaDocumentul()
    {
        var r = await h.Anonim().GetJson("/fisiere/optimizare/config");
        r.Str("procesare").ShouldBe("browser");
        r.GetProperty("documente").GetProperty("faraPierderi").GetBoolean().ShouldBeTrue();
        r.GetProperty("incarcare").Int("bucataOcteti").ShouldBe(IncarcariReluabile.Bucata);
        (await h.Db(db => db.Set<FisierLectie>().CountAsync())).ShouldBe(0);
    }

    [Theory]
    [InlineData("george")]
    [InlineData("irina")]
    [InlineData("director")]
    public async Task ExportulEsteDoarAlTitularuluiSiNuContineSecrete(string username)
    {
        var client = await h.Login(username);
        var r = await client.Post("/utilizatori/me/date/export", new { parolaActuala = Harness.ParolaTest });
        r.StatusCode.ShouldBe(HttpStatusCode.OK);
        r.Content.Headers.ContentDisposition!.FileNameStar.ShouldBe("datele-mele.json");
        r.Headers.CacheControl!.NoStore.ShouldBeTrue();
        var text = await r.Content.ReadAsStringAsync(Ct);
        var data = JsonDocument.Parse(text).RootElement;
        data.GetProperty("cont").Str("username").ShouldBe(username);
        text.ShouldNotContain("parolaHash"); text.ShouldNotContain("cheieHash"); text.ShouldNotContain("hashCod");
        text.ShouldNotContain(Harness.ParolaTest);
        data.GetProperty("conversatii").ValueKind.ShouldBe(JsonValueKind.Array);
    }

    [Fact]
    public async Task ExportulSiStergereaCerAutentificareParolaSiConfirmare()
    {
        await h.Anonim().Post("/utilizatori/me/date/export", new { parolaActuala = Harness.ParolaTest }).Status(HttpStatusCode.Unauthorized);
        var client = await h.Login("george");
        await client.Post("/utilizatori/me/date/export", new { parolaActuala = "gresita" }).Status(HttpStatusCode.BadRequest);
        await Sterge(client, confirmare: "nu").Status(HttpStatusCode.BadRequest);
        await Sterge(client, parola: "gresita").Status(HttpStatusCode.BadRequest);
        (await h.Db(db => db.Utilizatori.SingleAsync(x => x.Username == "george"))).StergereCerutaLa.ShouldBeNull();
    }

    [Fact]
    public async Task InchidereaRevocaToateSesiunileSiNuStergeMaterialeleOrganizatiei()
    {
        var profesor = await h.Login("irina");
        var alDoileaDispozitiv = await h.Login("irina");
        var uid = (await profesor.GetJson("/utilizatori/me")).Int("id");
        var lectie = (await profesor.Post("/parcursuri", new { materieId = 1, titlu = "Material păstrat", clasa = "XII", vizibilitate = "organizatie", organizatieId = Harness.IdLiceu }).Json(HttpStatusCode.Created)).Int("id");
        var original = await h.Db(db => db.Utilizatori.AsNoTracking().SingleAsync(x => x.Id == uid));
        var poza = "poze/" + Guid.NewGuid().ToString("N") + ".png";
        var cale = h.Serviciu<Stocare>().CaleFisier(poza)!;
        Directory.CreateDirectory(Path.GetDirectoryName(cale)!);
        await File.WriteAllTextAsync(cale, "personal-file-content", Ct);
        await h.Db(async db =>
        {
            var profil = await db.ProfiluriMentor.SingleAsync(x => x.UtilizatorId == uid);
            profil.PozaUrl = poza;
            await db.SaveChangesAsync();
        });
        // Warm the other device's session-version cache before revocation.
        await alDoileaDispozitiv.Get("/utilizatori/me").Status(HttpStatusCode.OK);
        var token = (await profesor.Post($"/parcursuri/{lectie}/incarcari", new { nume = "neterminat.pdf", marime = 8, sha256 = new string('0', 64) }).Json(HttpStatusCode.OK)).Str("token");
        await profesor.PutAsync($"/incarcari/{token}?offset=0", new ByteArrayContent("personal"u8.ToArray()), Ct).Status(HttpStatusCode.OK);
        await Sterge(profesor).Status(HttpStatusCode.Accepted);
        await alDoileaDispozitiv.Get("/utilizatori/me").Status(HttpStatusCode.Unauthorized);
        await profesor.Get("/utilizatori/me").Status(HttpStatusCode.Unauthorized);
        var sters = await h.Db(db => db.Utilizatori.SingleAsync(x => x.Id == uid));
        sters.Email.ShouldNotBe(original.Email); sters.Nume.ShouldBe("Cont șters"); sters.ParolaHash.ShouldBe(""); sters.Telefon.ShouldBeNull();
        (await h.Db(db => db.Set<Parcurs>().SingleAsync(x => x.Id == lectie))).AutorId.ShouldBeNull();
        (await h.Db(db => db.Set<Parcurs>().SingleAsync(x => x.Id == lectie))).OrganizatieId.ShouldBe(Harness.IdLiceu);
        var procesorNou = ActivatorUtilities.CreateInstance<CuratareDatePersonale>(h.App.Services);
        await procesorNou.RuleazaOdataAsync(Ct);
        File.Exists(cale).ShouldBeFalse();
        Directory.GetFiles(Path.Combine(h.DirStocare, "incarcari")).ShouldBeEmpty();
        (await h.Db(db => db.Set<StergereDateCont>().SingleAsync(x => x.UtilizatorId == uid))).FinalizatLa.ShouldNotBeNull();
        await h.Anonim().Post("/autentificare", new { identificator = original.Username, parola = Harness.ParolaTest }).Status(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdministratorulOrganizatieiTrebuieSaTransfereResponsabilitatea()
    {
        await Sterge(await h.Login("director")).Status(HttpStatusCode.Conflict);
        (await h.Db(db => db.Utilizatori.SingleAsync(x => x.Username == "director"))).StergereCerutaLa.ShouldBeNull();
    }

    [Fact]
    public async Task DocumentulPersonalNuPoateFiCerutPentruAltUtilizator()
    {
        var irina = await h.Login("irina");
        var uid = (await irina.GetJson("/utilizatori/me")).Int("id");
        var key = "dovezi/" + Guid.NewGuid().ToString("N") + ".pdf";
        var path = h.Serviciu<Stocare>().CaleFisier(key)!;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bytes = DocumenteTest.Pdf();
        await File.WriteAllBytesAsync(path, bytes, Ct);
        await h.Db(async db => { (await db.ProfiluriMentor.SingleAsync(x => x.UtilizatorId == uid)).DovadaCalificareUrl = key; await db.SaveChangesAsync(); });
        (await irina.GetByteArrayAsync("/utilizatori/me/date/documente/dovada", Ct)).ShouldBe(bytes);
        await (await h.Login("george")).Get($"/utilizatori/me/date/documente/dovada?uid={uid}").Status(HttpStatusCode.NotFound);
        await h.Anonim().Get("/utilizatori/me/date/documente/dovada").Status(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RetentiaRamaneDezactivataImplicit()
    {
        var vechi = h.Ceas.GetUtcNow().UtcDateTime.AddYears(-1);
        await h.Db(async db =>
        {
            db.Add(new MesajIesire { Canal = "email", Catre = "retentie@example.invalid", Tip = "notificare", CreatLa = vechi, TrimisLa = vechi, UrmatoareaIncercareLa = vechi });
            await db.SaveChangesAsync();
        });
        await h.Serviciu<CuratareDatePersonale>().RuleazaOdataAsync(Ct);
        (await h.Db(db => db.Set<MesajIesire>().CountAsync(x => x.Catre == "retentie@example.invalid"))).ShouldBe(1);
        var politica = await h.Anonim().GetJson("/confidentialitate");
        politica.GetProperty("retentie").Int("conversatiiZile").ShouldBe(0);
        politica.GetProperty("retentie").Int("mesajeTrimiseZile").ShouldBe(0);
    }
}

public sealed class DeclaratieMinorTests : TestCuHarness
{
    protected override IReadOnlyDictionary<string, string?> Setari => new Dictionary<string, string?> { ["Confidentialitate:SolicitaDeclaratieVarsta"] = "true", ["Limite:InregistrariPeOra"] = "100" };
    [Fact]
    public async Task DeclaratiaEsteCerutaSiAcordulParinteluiEsteDoarInregistratCaDeclaratie()
    {
        var client = h.Anonim();
        var date = new { nume = "Elev test", username = "minor", email = "minor@example.invalid", parola = Harness.ParolaTest };
        await client.Post("/autentificare/inregistrare", date).Status(HttpStatusCode.BadRequest);
        await client.Post("/autentificare/inregistrare", new { date.nume, date.username, date.email, date.parola, sub16Ani = true }).Status(HttpStatusCode.BadRequest);
        await client.Post("/autentificare/inregistrare", new { date.nume, date.username, date.email, date.parola, sub16Ani = true, acordParinteDeclarat = true }).Status(HttpStatusCode.Accepted);
        var u = await h.Db(db => db.Utilizatori.SingleAsync(x => x.Username == "minor"));
        u.Sub16AniDeclarat.ShouldBe(true); u.AcordParinteDeclaratLa.ShouldNotBeNull(); u.VersiuneInformare.ShouldBe(DatePersonale.VersiuneInformare);
    }
}

public sealed class RetentieDateTests : TestCuHarness
{
    protected override IReadOnlyDictionary<string, string?> Setari => new Dictionary<string, string?>
    { ["Confidentialitate:ConversatiiZile"] = "30", ["Confidentialitate:MesajeTrimiseZile"] = "90" };

    [Fact]
    public async Task OptiuneaExplicitaStergeDoarConversatiiIncheiateSiMesajeTrimiseVechi()
    {
        var acum = h.Ceas.GetUtcNow().UtcDateTime;
        await h.Db(async db =>
        {
            var uid = await db.Utilizatori.Where(u => u.Username == "george").Select(u => u.Id).SingleAsync();
            var inchis = new Sesiune { UtilizatorId = uid, Canal = "telefon", Status = "incheiat", InceputLa = acum.AddDays(-100), FinalizatLa = acum.AddDays(-99) };
            var activ = new Sesiune { UtilizatorId = uid, Canal = "telefon", Status = "in_curs", InceputLa = acum.AddDays(-100) };
            db.AddRange(inchis, activ);
            db.AddRange(new Mesaj { Sesiune = inchis, Expeditor = "elev", Tip = "voce", Continut = "de eliminat", CreatLa = acum.AddDays(-100) },
                new Mesaj { Sesiune = activ, Expeditor = "elev", Tip = "voce", Continut = "activ păstrat", CreatLa = acum.AddDays(-100) });
            db.AddRange(new MesajIesire { Canal = "email", Catre = "trimis@example.invalid", Tip = "notificare", CreatLa = acum.AddDays(-100), TrimisLa = acum.AddDays(-99), UrmatoareaIncercareLa = acum.AddDays(-100) },
                new MesajIesire { Canal = "email", Catre = "asteapta@example.invalid", Tip = "notificare", Text = "încă netrimis", CreatLa = acum.AddDays(-100), UrmatoareaIncercareLa = acum });
            await db.SaveChangesAsync();
        });
        await h.Serviciu<CuratareDatePersonale>().RuleazaOdataAsync(Ct);
        (await h.Db(db => db.Mesaje.Where(m => m.Continut == "de eliminat").CountAsync())).ShouldBe(0);
        (await h.Db(db => db.Mesaje.Where(m => m.Continut == "activ păstrat").CountAsync())).ShouldBe(1);
        (await h.Db(db => db.Set<MesajIesire>().CountAsync(m => m.Catre == "trimis@example.invalid"))).ShouldBe(0);
        (await h.Db(db => db.Set<MesajIesire>().CountAsync(m => m.Catre == "asteapta@example.invalid"))).ShouldBe(1);
    }
}

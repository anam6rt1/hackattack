using System.Net;
using System.Text.Json;
using System.Net.Http.Json;
using MarkAi.Data.Entities;
using MarkAi.Tests.Suport;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Continut;

public class TemeTests : TestCuHarness
{
    private HttpClient profesor = null!, elev = null!, director = null!;
    private int org, uid, lectie;
    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        profesor = await h.Login("irina"); elev = await h.Login("george"); director = await h.Login("director");
        uid = (await elev.GetJson("/utilizatori/me")).Int("id");
        org = await h.Db(db => db.Set<Organizatie>().Select(o => o.Id).SingleAsync());
        await h.Db(async db => { db.Add(new MembruOrganizatie { OrganizatieId = org, UtilizatorId = uid, Rol = "elev", Stare = "aprobat", CerutLa = h.Ceas.GetUtcNow().UtcDateTime }); await db.SaveChangesAsync(); });
        using var admin = await h.Login("admin"); lectie = await Harness.LectiePlatforma(admin);
        await h.Db(async db => { db.Add(new Intrebare { ParcursId = lectie, MaterieId = 1, Text = "Întrebarea temei", Variante = [new() { Text = "Da", Corecta = true }, new() { Text = "Nu" }] }); await db.SaveChangesAsync(); });
    }
    private string Baza => $"/organizatii/{org}/teme";
    private object Corp(int[]? grupe = null) => new { titlu = "Tema de luni", descriere = "Reia lecția.", termenLa = h.Ceas.GetUtcNow().AddDays(2), parcursuriIds = new[] { lectie }, toataOrganizatia = grupe is null, grupeIds = grupe ?? [] };
    private async Task<int> Creeaza(int[]? grupe = null) => (await profesor.Post(Baza, Corp(grupe)).Json(HttpStatusCode.Created)).Int("id");

    [Fact]
    public async Task TemaEsteVizibilaDoarMembrilorAprobatiIarPlecareaRevocaAccesul()
    {
        await Creeaza();
        (await elev.GetJson("/teme")).Int("total").ShouldBe(1);
        await h.Db(async db => { await db.Set<MembruOrganizatie>().Where(m => m.UtilizatorId == uid).ExecuteDeleteAsync(); });
        (await elev.GetJson("/teme")).Int("total").ShouldBe(0);
        (await h.Anonim().GetAsync("/teme", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await elev.Post(Baza, Corp())).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
    [Fact]
    public async Task RaportulEstePaginatFiltratSiAcceptaPredareaTarzie()
    {
        var tema = await Creeaza();
        var nedeschis = await profesor.GetJson($"{Baza}/{tema}/rezultate?stare=nedeschis&ordine=progres&pagina=1");
        nedeschis.Int("total").ShouldBe(1);
        nedeschis.GetProperty("elemente")[0].Str("stare").ShouldBe("nedeschis");
        h.Ceas.Advance(TimeSpan.FromDays(3));
        await h.Db(async db => { db.Add(new ProgresLectie { UtilizatorId = uid, ParcursId = lectie, UltimaAccesare = h.Ceas.GetUtcNow().UtcDateTime, PromovatLa = h.Ceas.GetUtcNow().UtcDateTime }); await db.SaveChangesAsync(); });
        var r = await profesor.GetJson($"{Baza}/{tema}/rezultate?stare=predat&ordre=nume");
        r.Int("total").ShouldBe(1); r.GetProperty("elemente")[0].Str("stare").ShouldBe("predat");
        r.GetProperty("elemente")[0].GetProperty("predatLa").GetDateTime().ShouldBeGreaterThan(r.GetProperty("termenLa").GetDateTime());
        (await director.GetJson(Baza)).Int("total").ShouldBe(1);
        (await elev.GetJson("/teme")).Int("total").ShouldBe(1);
    }
    [Fact]
    public async Task GrupeleDeterminaDestinatariiFaraDubluriSiPotFiActualizate()
    {
        var g = (await profesor.Post(Baza + "/grupe", new { nume = "XI B", eleviIds = new[] { uid } }).Json(HttpStatusCode.Created)).Int("id");
        var tema = await Creeaza([g]);
        (await elev.GetJson("/teme")).Int("total").ShouldBe(1);
        (await profesor.GetJson(Baza + "/grupe")).Int("total").ShouldBe(1);
        (await profesor.GetJson(Baza + $"/grupe/{g}")).GetProperty("eleviIds").GetArrayLength().ShouldBe(1);
        (await profesor.DeleteAsync(Baza + $"/grupe/{g}", Ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await profesor.Put(Baza + $"/grupe/{g}", new { nume = "XI B", eleviIds = Array.Empty<int>() }).Status(HttpStatusCode.NoContent);
        (await elev.GetJson("/teme")).Int("total").ShouldBe(0);
        await director.DeleteAsync(Baza + $"/{tema}", Ct).Status(HttpStatusCode.NoContent);
        await profesor.DeleteAsync(Baza + $"/grupe/{g}", Ct).Status(HttpStatusCode.NoContent);
    }
    [Fact]
    public async Task NuPotFiAlesiEleviSauLectiiDinAltaOrganizatie()
    {
        await profesor.Post(Baza + "/grupe", new { nume = "Grupa", eleviIds = new[] { 999999 } }).Status(HttpStatusCode.BadRequest);
        await h.Db(async db => { var l = await db.Set<Parcurs>().FindAsync(lectie); l!.Publicat = false; await db.SaveChangesAsync(); });
        await profesor.Post(Baza, Corp()).Status(HttpStatusCode.BadRequest);
        await profesor.GetAsync(Baza + "?pagina=0", Ct).Status(HttpStatusCode.BadRequest);
    }
    [Fact]
    public async Task ProfesorulRevocatNuMaiPoateVedeaSauPublicaTeme()
    {
        await Creeaza();
        await h.Db(async db => { await db.Set<MembruOrganizatie>().Where(m => m.Utilizator.Username == "irina").ExecuteDeleteAsync(); });
        await profesor.GetAsync(Baza, Ct).Status(HttpStatusCode.Forbidden);
        await profesor.Post(Baza, Corp()).Status(HttpStatusCode.Forbidden);
    }
    [Fact]
    public async Task ListelePentruFormularSiAnulareaFunctioneaza()
    {
        (await profesor.GetJson("/teme/organizatii")).GetArrayLength().ShouldBe(1);
        (await profesor.GetJson(Baza + "/lectii?q=Lectie")).Int("total").ShouldBe(1);
        (await profesor.GetJson(Baza + "/elevi")).Int("total").ShouldBe(1);
        var id = await Creeaza();
        await profesor.DeleteAsync(Baza + $"/{id}", Ct).Status(HttpStatusCode.NoContent);
        (await elev.GetJson("/teme")).Int("total").ShouldBe(0);
    }
    [Fact]
    public async Task ProfesoriiNuPotCitiTemeleSauGrupeleAltuiaDarAdministratorulLeVede()
    {
        var t = (await director.Post(Baza, Corp()).Json(HttpStatusCode.Created)).Int("id");
        var g = (await director.Post(Baza + "/grupe", new { nume = "Grupa directorului", eleviIds = new[] { uid } }).Json(HttpStatusCode.Created)).Int("id");
        (await profesor.GetJson(Baza)).Int("total").ShouldBe(0);
        await profesor.GetAsync(Baza + $"/{t}/rezultate", Ct).Status(HttpStatusCode.NotFound);
        await profesor.GetAsync(Baza + $"/grupe/{g}", Ct).Status(HttpStatusCode.NotFound);
        await profesor.Post(Baza, Corp([g])).Status(HttpStatusCode.BadRequest);
        await director.GetAsync(Baza + $"/{t}/rezultate", Ct).Status(HttpStatusCode.OK);
    }
    [Fact]
    public async Task RaportulDistingeElevulCareADeschisLectiaFaraSaOTermine()
    {
        var t = await Creeaza();
        await h.Db(async db => { db.Add(new ProgresLectie { UtilizatorId = uid, ParcursId = lectie, UltimaAccesare = h.Ceas.GetUtcNow().UtcDateTime }); await db.SaveChangesAsync(); });
        var r = await profesor.GetJson(Baza + $"/{t}/rezultate?stare=inceput");
        r.Int("total").ShouldBe(1); r.GetProperty("elemente")[0].Str("stare").ShouldBe("inceput");
        (await profesor.GetJson(Baza + $"/{t}/rezultate?stare=nedeschis")).Int("total").ShouldBe(0);
    }
    [Fact]
    public async Task StergereaLectieiAnuleazaTemaInLocSaOConsiderePredata()
    {
        var id = await Creeaza();
        using var admin = await h.Login("admin");
        await admin.DeleteAsync($"/parcursuri/{lectie}", Ct).Status(HttpStatusCode.NoContent);
        (await elev.GetJson("/teme")).Int("total").ShouldBe(0);
        (await profesor.GetJson(Baza + $"/{id}/rezultate")).Bool("anulata").ShouldBeTrue();
    }
    [Fact]
    public async Task StergereaProfesoruluiPastreazaTemeleOrganizatieiFaraAtribuireaContului()
    {
        var id = await Creeaza();
        await profesor.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/utilizatori/me/date") { Content = JsonContent.Create(new { parolaActuala = Harness.ParolaTest, confirmare = "STERGE" }) }, Ct).Status(HttpStatusCode.Accepted);
        (await h.Db(db => db.Set<Tema>().Where(t => t.Id == id).Select(t => t.ProfesorId).SingleAsync())).ShouldBeNull();
        (await director.GetJson(Baza)).Int("total").ShouldBe(1);
        (await elev.GetJson("/teme")).Int("total").ShouldBe(1);
    }
}

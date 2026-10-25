using System.Net;
using System.Text.Json;
using System.Net.Http.Json;
using MarkAi.Data.Entities;
using MarkAi.Tests.Suport;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Continut;

public class GreseliTests : TestCuHarness
{
    private HttpClient elev = null!;
    private int lectie, intrebare, bun, gresit, uid;
    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync(); elev = await h.Login("george");
        uid = (await elev.GetJson("/utilizatori/me")).Int("id");
        using var admin = await h.Login("admin"); lectie = await Harness.LectiePlatforma(admin);
        await h.Db(async db => {
            var q = new Intrebare { ParcursId = lectie, MaterieId = 1, Text = "Capitala?", Explicatie = "Explicația corectă.", Variante = [new() { Text = "București", Corecta = true }, new() { Text = "Alt oraș" }] };
            db.Add(q); await db.SaveChangesAsync(); intrebare = q.Id; bun = q.Variante.Single(v => v.Corecta).Id; gresit = q.Variante.Single(v => !v.Corecta).Id;
        });
        var quiz = await elev.Post($"/parcursuri/{lectie}/quiz").Json();
        await elev.Post($"/parcursuri/{lectie}/quiz/{quiz.Int("id")}/raspuns", new { intrebareId = intrebare, variantaId = gresit }).Status(HttpStatusCode.OK);
    }
    private Task<JsonElement> Incepe() => elev.Post($"/greseli/{intrebare}/incepe").Json();
    private Task<HttpResponseMessage> Raspunde(JsonElement x, int varianta) => elev.Post($"/greseli/{intrebare}/raspuns", new { token = x.GetProperty("token").GetGuid(), variantaId = varianta });
    [Fact]
    public async Task GreselileSeProgrameazaSiNuExpunRaspunsurile()
    {
        var r = await elev.GetJson("/greseli"); r.Int("total").ShouldBe(1); r.Int("scadente").ShouldBe(0);
        await elev.Post($"/greseli/{intrebare}/incepe").Status(HttpStatusCode.Conflict);
        h.Ceas.Advance(TimeSpan.FromDays(1));
        var x = await Incepe(); x.GetRawText().ShouldNotContain("corecta"); x.GetRawText().ShouldNotContain("explicatie");
        var raspuns = await Raspunde(x, gresit).Json(); raspuns.Bool("corect").ShouldBeFalse();
        raspuns.GetProperty("scadentaLa").GetDateTime().ShouldBe(h.Ceas.GetUtcNow().UtcDateTime.AddDays(1));
        await Raspunde(x, bun).Status(HttpStatusCode.Conflict);
        (await h.Db(db => db.EvenimenteRecompensa.CountAsync(e => e.UtilizatorId == uid))).ShouldBe(0);
    }
    [Fact]
    public async Task DouaCorecteConsecutiveLaIntervalScotIntrebareaDinLista()
    {
        h.Ceas.Advance(TimeSpan.FromDays(1));
        var x = await Incepe(); var r = await Raspunde(x, bun).Json(); r.Bool("rezolvata").ShouldBeFalse();
        r.GetProperty("scadentaLa").GetDateTime().ShouldBe(h.Ceas.GetUtcNow().UtcDateTime.AddDays(3));
        await elev.Post($"/greseli/{intrebare}/incepe").Status(HttpStatusCode.Conflict);
        h.Ceas.Advance(TimeSpan.FromDays(3));
        r = await Raspunde(await Incepe(), bun).Json(); r.Bool("rezolvata").ShouldBeTrue();
        (await elev.GetJson("/greseli")).Int("total").ShouldBe(0);
        (await h.Db(db => db.Set<ProgresLectie>().CountAsync(p => p.UtilizatorId == uid && p.PromovatLa != null))).ShouldBe(0);
    }
    [Fact]
    public async Task SesiuneaExpiraIarAltiEleviNuPotRaspundeInLoculTitularului()
    {
        h.Ceas.Advance(TimeSpan.FromDays(1)); var x = await Incepe();
        h.Ceas.Advance(TimeSpan.FromMinutes(11)); await Raspunde(x, bun).Status(HttpStatusCode.Conflict);
        using var altul = await h.InregistreazaElev("altul");
        await altul.Post($"/greseli/{intrebare}/incepe").Status(HttpStatusCode.NotFound);
        await altul.Post($"/greseli/{intrebare}/raspuns", new { token = x.GetProperty("token").GetGuid(), variantaId = bun }).Status(HttpStatusCode.NotFound);
    }
    [Fact]
    public async Task AccesulLaLectieEsteReverificatLaFiecareRaspuns()
    {
        h.Ceas.Advance(TimeSpan.FromDays(1)); var x = await Incepe();
        await h.Db(async db => { var p = await db.Set<Parcurs>().FindAsync(lectie); p!.Publicat = false; await db.SaveChangesAsync(); });
        await Raspunde(x, bun).Status(HttpStatusCode.NotFound);
        (await elev.GetJson("/greseli")).Int("total").ShouldBe(0);
    }
    [Fact]
    public async Task OGresealaNouaLaQuizRedeschideIntrebareaRezolvata()
    {
        h.Ceas.Advance(TimeSpan.FromDays(1)); await Raspunde(await Incepe(), bun).Status(HttpStatusCode.OK);
        h.Ceas.Advance(TimeSpan.FromDays(3)); await Raspunde(await Incepe(), bun).Status(HttpStatusCode.OK);
        var quiz = await elev.Post($"/parcursuri/{lectie}/quiz").Json();
        await elev.Post($"/parcursuri/{lectie}/quiz/{quiz.Int("id")}/raspuns", new { intrebareId = intrebare, variantaId = gresit }).Status(HttpStatusCode.OK);
        var r = await elev.GetJson("/greseli"); r.Int("total").ShouldBe(1); r.GetProperty("elemente")[0].Int("corecteConsecutive").ShouldBe(0);
    }
    [Fact]
    public async Task ExportulIncludeGreselileFaraTokenIarStergereaLeElimina()
    {
        h.Ceas.Advance(TimeSpan.FromDays(1)); var x = await Incepe();
        var export = await elev.Post("/utilizatori/me/date/export", new { parolaActuala = Harness.ParolaTest }).Json();
        export.GetProperty("greseli").GetArrayLength().ShouldBe(1);
        export.GetRawText().ShouldNotContain(x.GetProperty("token").GetString()!);
        await elev.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/utilizatori/me/date") { Content = JsonContent.Create(new { parolaActuala = Harness.ParolaTest, confirmare = "STERGE" }) }, Ct).Status(HttpStatusCode.Accepted);
        (await h.Db(db => db.Set<Greseala>().CountAsync(g => g.UtilizatorId == uid))).ShouldBe(0);
    }
}

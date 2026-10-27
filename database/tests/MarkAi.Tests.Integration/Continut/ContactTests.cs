using System.Net;
using MarkAi.Data.Entities;
using MarkAi.Tests.Suport;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Continut;
public class FeedbackPilotTests : TestCuHarness
{
    [Fact]
    public async Task CacheOrganizatiiSeInvalideazaLaEditareFaraPartajareaDatelorPrivate()
    {
        var anonim = h.Anonim(); using var director = await h.Login("director");
        (await anonim.GetJson("/organizatii")).GetRawText().ShouldContain("Liceul de Test");
        var o = await director.GetJson("/organizatia-mea");
        await director.Put("/organizatia-mea", new { nume = "Liceul Actualizat", adresa = o.Str("adresa"), judetId = o.Int("judetId"), localitateId = (int?)null }).Status(HttpStatusCode.OK);
        var updated = await anonim.GetJson("/organizatii"); updated.GetRawText().ShouldContain("Liceul Actualizat"); updated.GetRawText().ShouldNotContain("adminEmail");
        (await anonim.GetJson("/judete")).GetRawText().ShouldBe((await anonim.GetJson("/judete")).GetRawText());
    }
    [Fact]
    public async Task FeedbackulEsteLimitatPrivatSiPoateFiRezolvatDeAdmin()
    {
        using var elev = await h.Login("george"); using var prof = await h.Login("irina"); using var admin = await h.Login("admin");
        await elev.Post("/feedback", new { text = "Nu se vede lecția", pagina = "/materiale?token=secret" }).Status(HttpStatusCode.NoContent);
        await prof.GetAsync("/admin/feedback", Ct).Status(HttpStatusCode.Forbidden);
        var r = await admin.GetJson("/admin/feedback"); var f = r.GetProperty("elemente")[0]; f.Str("pagina").ShouldBe("/materiale");
        await admin.Post($"/admin/feedback/{f.Int("id")}/rezolvat").Status(HttpStatusCode.NoContent);
        (await admin.GetJson("/admin/feedback")).Int("total").ShouldBe(0);
        for (var i = 0; i < 4; i++) await elev.Post("/feedback", new { text = "Mesaj", pagina = "/" }).Status(HttpStatusCode.NoContent);
        await elev.Post("/feedback", new { text = "În plus", pagina = "/" }).Status(HttpStatusCode.Conflict);
        (await h.Db(db => db.Set<FeedbackPilot>().CountAsync())).ShouldBe(5);
    }
}

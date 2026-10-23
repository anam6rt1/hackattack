using System.Net;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Cautare;

/// <summary>
/// A subject's Lecții (GET /materii/{id}/parcursuri, the Materiale page) a page at a time: the total, the finished ones hidden by
/// the server (ascundeTerminate), and under "near me" the user's own locality first across pages (ordered in SQL).
/// radu lives in Alba Iulia; Centrul Alba is there, Școala Bărăbanț in another locality of the same county.
/// </summary>
public sealed class LectiiMaterieTests : TestCautare
{
    private int dinAlbaIulia, dinBarabant;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        // Titles chosen so the plain order (by title) would put the Bărăbanț one first.
        dinBarabant = await Creeaza(ovidiu, Lectie("A — din Bărăbanț", ScoalaBarabant));
        dinAlbaIulia = await Creeaza(ovidiu, Lectie("Z — din Alba Iulia", Centru));
    }

    private Task<int> Lectia(string titlu) => h.Db(db => db.Set<Parcurs>().Where(p => p.Titlu == titlu).Select(p => p.Id).SingleAsync());

    [Fact]
    public async Task ListaArePagini()
    {
        var toate = await george.GetJson("/materii/1/parcursuri?marime=100");
        var total = toate.Int("total");
        total.ShouldBe(toate.Lista().Count);
        var p1 = await george.GetJson("/materii/1/parcursuri?marime=1");
        (p1.Int("total"), p1.Lista().Count).ShouldBe((total, 1));
        var ids = new List<int>();
        for (var p = 1; p <= total; p++) ids.Add((await george.GetJson($"/materii/1/parcursuri?marime=1&pagina={p}")).Lista().Single().Int("id"));
        ids.ShouldBe(toate.Lista().Select(l => l.Int("id")).ToList());
    }

    [Fact]
    public async Task AproapeDeMineLocalitateaElevuluiPrimaPePagini()
    {
        var p1 = await radu.GetJson("/materii/1/parcursuri?filtru=local&marime=1");
        p1.Int("total").ShouldBe(2);
        p1.Lista().Single().Int("id").ShouldBe(dinAlbaIulia);
        (await radu.GetJson("/materii/1/parcursuri?filtru=local&marime=1&pagina=2")).Lista().Single().Int("id").ShouldBe(dinBarabant);
    }

    [Fact]
    public async Task LectiileTerminateSuntAscunseDeServer()
    {
        var georgeId = await Id("george");
        await h.Db(async db =>
        {
            db.Add(new ProgresLectie { UtilizatorId = georgeId, ParcursId = dinAlbaIulia, PromovatLa = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });
        var toate = await george.GetJson("/materii/1/parcursuri?marime=100");
        var fara = await george.GetJson("/materii/1/parcursuri?marime=100&ascundeTerminate=true");
        fara.Int("total").ShouldBe(toate.Int("total") - 1);
        fara.Lista().ShouldNotContain(l => l.Int("id") == dinAlbaIulia);
        // Someone else's progress hides nothing for george's classmates.
        (await elena.GetJson("/materii/1/parcursuri?marime=100&ascundeTerminate=true")).Int("total").ShouldBe((await elena.GetJson("/materii/1/parcursuri?marime=100")).Int("total"));
    }

    [Fact]
    public async Task PaginaGresitaEsteRefuzata()
    {
        await george.Get("/materii/1/parcursuri?pagina=0").Status(HttpStatusCode.BadRequest);
    }
}

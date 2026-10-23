using MarkAi.Api.Endpoints;
using MarkAi.Api.Servicii;
using MarkAi.Data;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MarkAi.Tests.Integration.Lectii;

/// <summary>The phone tutor follows the same visibility: its prompt and speech hints never contain chapters the student may not see.</summary>
public sealed class TutorVizibilitateTests : TestLectii
{
    private async Task<string> PromptPentru(string username)
    {
        var elevId = await Id(username);
        await using var scope = h.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MarkAiDbContext>();
        var sesiune = new Sesiune { UtilizatorId = elevId, Canal = LiniaAiEndpoints.CanalTelefon, Status = LiniaAiEndpoints.StareInCurs, InceputLa = DateTime.UtcNow };
        db.Add(sesiune);
        await db.SaveChangesAsync(Ct);
        db.Add(new Mesaj { SesiuneId = sesiune.Id, Expeditor = TutorTelefon.ExpeditorElev, Tip = TutorTelefon.TipVoce, Continut = "Vreau să vorbim despre Ion, glasul pământului.", CreatLa = DateTime.UtcNow });
        await db.SaveChangesAsync(Ct);
        h.Ai.Raspunde = (_, _, _) => """{"raspuns":"Bine.","final":false,"voce":{"voce":"ioana","viteza":100,"ton":0}}""";
        while (h.Ai.Apeluri.TryDequeue(out _)) { }
        await scope.ServiceProvider.GetRequiredService<TutorTelefon>().RaspundeAsync(sesiune, VociTutor.Implicit, ultimaReplica: false, Ct);
        return h.Ai.Apeluri.Last().Sistem;
    }

    private async Task<string> Indicii(string username)
    {
        await using var scope = h.App.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TutorTelefon>().IndiciiVorbire(await Id(username), Ct);
    }

    [Fact]
    public async Task CelDinAfaraNuPrimesteCapitoleleOrganizatiei()
    {
        await LectiePrivata();
        (await PromptPentru("george")).ShouldNotContain("Ion: capitolul 1");
    }

    [Fact]
    public async Task ElevulOrganizatieiPrimesteCapitolele()
    {
        await LectiePrivata();
        (await PromptPentru("elena")).ShouldContain("Ion: capitolul 1");
    }

    [Fact]
    public async Task IndiciileDeVorbireUrmeazaVizibilitatea()
    {
        await LectiePrivata();
        (await Indicii("george")).ShouldNotContain("Ion — organizație");
        (await Indicii("elena")).ShouldContain("Ion — organizație");
    }

    [Fact]
    public async Task LectiaNepublicataNuAjungeLaTutor()
    {
        var (l, _, _) = await LectiePrivata();
        await h.Db(db => db.Set<Parcurs>().Where(p => p.Id == l).ExecuteUpdateAsync(s => s.SetProperty(p => p.Publicat, false)));
        (await Indicii("elena")).ShouldNotContain("Ion — organizație");
    }

    [Fact]
    public async Task LectiaPublicaAjungeLaToti()
    {
        await LectiePlatforma();
        (await Indicii("george")).ShouldContain("Platformă Bac");
    }
}

using System.Net;
using MarkAi.Api.Endpoints;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MarkAi.Tests.Integration.Lectii;

/// <summary>Hard deletion: a chapter or a whole Lecție with everything under it; XP stays, tutor calls lose only the link.</summary>
public sealed class StergereLectiiTests : TestLectii
{
    private async Task<(int Lectie, int Cap1, int Cap2, int Apel, int XpInainte)> CuActivitate()
    {
        var (l, c1, c2) = await LectiePrivata();
        var elenaId = await Id("elena");
        var apel = await h.Db(async db =>
        {
            var s = new Sesiune { UtilizatorId = elenaId, CursId = c2, Canal = LiniaAiEndpoints.CanalTelefon, Status = "finalizat", InceputLa = DateTime.UtcNow };
            db.Add(s);
            await db.SaveChangesAsync();
            return s.Id;
        });
        await using (var scope = h.App.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ProgresService>().AcceseazaAsync(elenaId, c2, Ct);
            await scope.ServiceProvider.GetRequiredService<ProgresService>().PromoveazaAsync(elenaId, l, Ct);
        }
        await elena.Post($"/parcursuri/{l}/quiz").Status(HttpStatusCode.OK);
        var xp = await h.Db(db => db.EvenimenteRecompensa.Where(e => e.UtilizatorId == elenaId).SumAsync(e => e.XpAcordat));
        return (l, c1, c2, apel, xp);
    }

    [Fact]
    public async Task AutorulStergeUnCapitolCuActivitate()
    {
        var (_, _, c2, _, _) = await CuActivitate();
        await irina.Delete($"/cursuri/{c2}").Status(HttpStatusCode.NoContent);
        (await h.Db(db => db.Curs.AnyAsync(c => c.Id == c2))).ShouldBeFalse();
    }

    [Fact]
    public async Task TestulSiPromovareaRamanDupaStergereaCapitolului()
    {
        var (l, _, c2, _, _) = await CuActivitate();
        await irina.Delete($"/cursuri/{c2}");
        var elenaId = await Id("elena");
        var p = await h.Db(db => db.Set<ProgresLectie>().SingleAsync(x => x.ParcursId == l && x.UtilizatorId == elenaId));
        p.UltimulCursId.ShouldBeNull();
        p.PromovatLa.ShouldNotBeNull();
        (await h.Db(db => db.Set<Intrebare>().CountAsync(q => q.ParcursId == l))).ShouldBe(2);
    }

    [Fact]
    public async Task ApelulTutoruluiRamaneFaraLegatura()
    {
        var (_, _, c2, apel, _) = await CuActivitate();
        await irina.Delete($"/cursuri/{c2}");
        (await h.Db(db => db.Sesiuni.Where(s => s.Id == apel).Select(s => s.CursId).SingleAsync())).ShouldBeNull();
    }

    [Fact]
    public async Task StergereaLectieiIaTotCeEDedesubt()
    {
        var (l, c1, c2, _, _) = await CuActivitate();
        await irina.Put($"/parcursuri/{l}", Lectie("Ion", Liceu, clasa: "XI", vizibilitate: "organizatie", partajari: [Centru]));
        await irina.Delete($"/parcursuri/{l}").Status(HttpStatusCode.NoContent);
        (await h.Db(async db => new[]
        {
            await db.Curs.AnyAsync(c => c.Id == c1 || c.Id == c2),
            await db.Set<ParcursOrganizatie>().AnyAsync(s => s.ParcursId == l),
            await db.Set<Parcurs>().AnyAsync(p => p.Id == l),
            await db.Set<Intrebare>().AnyAsync(q => q.ParcursId == l),
            await db.Set<ProgresLectie>().AnyAsync(p => p.ParcursId == l),
            await db.Set<IncercareQuiz>().AnyAsync(i => i.ParcursId == l),
        })).ShouldAllBe(x => !x);
    }

    [Fact]
    public async Task XpulRamaneDupaStergereaLectiei()
    {
        var (l, _, _, _, xp) = await CuActivitate();
        await irina.Delete($"/parcursuri/{l}");
        xp.ShouldBeGreaterThanOrEqualTo(ProgresService.XpLectie);
        (await h.Db(async db => await db.EvenimenteRecompensa.Where(e => e.Utilizator.Username == "elena").SumAsync(e => e.XpAcordat))).ShouldBe(xp);
    }

    [Fact]
    public async Task LectiaStearsaEste404()
    {
        var (l, c1, _) = await LectiePrivata();
        await irina.Delete($"/parcursuri/{l}");
        await elena.Get($"/parcursuri/{l}").Status(HttpStatusCode.NotFound);
        await elena.Get($"/cursuri/{c1}").Status(HttpStatusCode.NotFound);
        await irina.Delete($"/parcursuri/{l}").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DoarEditoriiSterg()
    {
        var (l, _, _) = await LectiePrivata();
        await petra.Delete($"/parcursuri/{l}").Status(HttpStatusCode.Forbidden);
        await elena.Delete($"/parcursuri/{l}").Status(HttpStatusCode.Forbidden);
        await director.Delete($"/parcursuri/{l}").Status(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task StergereaEsteInJurnal()
    {
        var (l, _, _) = await LectiePrivata();
        await irina.Delete($"/parcursuri/{l}");
        h.Jurnal.Linii.ShouldContain(x => x.Contains($"Lecție {l} deleted with 2 chapters"));
    }

    [Fact]
    public async Task LegaturaCameriiDeTutoringSeScoate()
    {
        var (_, c1, _) = await LectiePrivata();
        var camera = await h.Db(async db =>
        {
            var c = new CameraTutoring { MentorId = await Id("irina"), MaterieId = 1, CursId = c1, Titlu = "Ședință", Status = RezervareService.SlotDeschis,
                InceputLa = DateTime.UtcNow.AddDays(1), FinalizatLa = DateTime.UtcNow.AddDays(1).AddHours(1), Capacitate = 5, ProBono = true };
            db.Add(c); await db.SaveChangesAsync(); return c.Id;
        });
        await irina.Delete($"/cursuri/{c1}").Status(HttpStatusCode.NoContent);
        (await h.Db(db => db.CamereTutoring.Where(c => c.Id == camera).Select(c => c.CursId).SingleAsync())).ShouldBeNull();
    }
}

using System.Net;
using System.Net.Http.Headers;
using MarkAi.Api.Servicii;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using MarkAi.Tests.Suport;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Fisiere;

/// <summary>
/// An organisation-only Lecție of irina's (Liceul de Test) with one chapter; elena is a student of the organisation, george is not.
/// Files are limited to 3 MB here (Fisiere:MaxMb).
/// </summary>
public abstract class TestFisiere : TestCuHarness
{
    protected override IReadOnlyDictionary<string, string?> Setari => new Dictionary<string, string?>
    {
        ["Limite:AutentificariPeMinut"] = "200", ["Limite:CereriPeMinut"] = "2000", ["Fisiere:MaxMb"] = "3",
    };

    protected HttpClient irina = null!, director = null!, elena = null!, george = null!;
    protected int lectie;
    protected ProcesorFisiere Procesor => h.Serviciu<ProcesorFisiere>();
    protected Stocare Stocare => h.Serviciu<Stocare>();

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        await h.Db(async db =>
        {
            var e = new Utilizator { Nume = "Elena Elev", Username = "elena", Email = "elena@example.invalid", EmailConfirmatLa = DateTime.UtcNow };
            e.ParolaHash = Parole.Hash(e, Harness.ParolaTest);
            e.Roluri.Add(new UtilizatorRol { RolId = await db.Roluri.Where(r => r.Nume == "elev").Select(r => r.Id).SingleAsync() });
            db.Add(e);
            await db.SaveChangesAsync();
            db.Add(new MembruOrganizatie { OrganizatieId = Harness.IdLiceu, UtilizatorId = e.Id, Rol = MembruOrganizatie.RolElev, Stare = MembruOrganizatie.Aprobat, CerutLa = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });
        (irina, director, elena, george) = (await h.Login("irina"), await h.Login("director"), await h.Login("elena"), await h.Login("george"));
        // The first run also sweeps orphans; nothing is stored yet.
        await Procesor.RuleazaOdataAsync(Ct);
        lectie = (await irina.Post("/parcursuri", new { materieId = 1, titlu = "Lecție cu fișiere", clasa = "XII", vizibilitate = "organizatie", organizatieId = Harness.IdLiceu })
            .Json(HttpStatusCode.Created)).Int("id");
        await irina.Post("/cursuri", new { parcursId = lectie, titlu = "Capitol cu fișiere", materiale = new[] { new { titlu = "Notițe", text = "Material de test." } } })
            .Status(HttpStatusCode.Created);
    }

    protected Task<HttpResponseMessage> Incarca(HttpClient c, byte[] continut, string nume, bool inlocuieste = false, int? parcurs = null)
    {
        var form = new MultipartFormDataContent();
        var fisier = new ByteArrayContent(continut);
        fisier.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fisier, "fisier", nume);
        return c.PostAsync($"/parcursuri/{parcurs ?? lectie}/fisiere{(inlocuieste ? "?inlocuieste=true" : "")}", form, Ct);
    }

    protected async Task<int> IncarcaId(HttpClient c, byte[] continut, string nume) => (await Incarca(c, continut, nume).Json(HttpStatusCode.Created)).Int("id");

    protected Task<string> Cheie(int fisierId) => h.Db(db => db.Set<FisierLectie>().Where(f => f.Id == fisierId).Select(f => f.Cheie).SingleAsync());

    protected Task<FisierLectie> Rand(int fisierId) => h.Db(db => db.Set<FisierLectie>().AsNoTracking().SingleAsync(f => f.Id == fisierId));
}

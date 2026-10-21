using System.Net;
using System.Text.Json;
using MarkAi.Api.Servicii.Mesagerie;
using MarkAi.Data.Entities;
using MarkAi.Tests.Suport;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Conturi;

/// <summary>The outbox: notifications are queued with the change that caused them and retried; admins see what was sent.</summary>
public sealed class MesajeTests : TestCuHarness
{
    protected override IReadOnlyDictionary<string, string?> Setari => new Dictionary<string, string?>
    {
        ["Limite:EmailuriPeOra"] = "100", ["Limite:InregistrariPeOra"] = "100",
    };

    private async Task<MesajIesire> NotificareEsuata()
    {
        h.Emailuri.Esueaza = true;
        await h.Anonim().Post("/autentificare/inregistrare", new { nume = "X", username = "x2", email = "george@example.invalid", parola = Harness.ParolaTest });
        await Proceseaza();
        return await h.Db(db => db.Set<MesajIesire>().SingleAsync(m => m.Tip == "incercare_inregistrare"));
    }

    private Task Proceseaza() => h.Serviciu<ProcesorMesaje>().RuleazaOdataAsync(Ct);
    private Task<MesajIesire> Rand(int id) => h.Db(db => db.Set<MesajIesire>().SingleAsync(m => m.Id == id));

    [Fact]
    public async Task NotificareaAsteaptaProcesorul()
    {
        await h.Anonim().Post("/autentificare/inregistrare", new { nume = "X", username = "x2", email = "george@example.invalid", parola = Harness.ParolaTest });
        h.Emailuri.Trimise.ShouldBeEmpty();
        await Proceseaza();
        h.Emailuri.Trimise.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task EmailulEsuatEstePastratPentruReincercare()
    {
        var m = await NotificareEsuata();
        m.TrimisLa.ShouldBeNull();
        m.Incercari.ShouldBe(1);
        m.Eroare.ShouldNotBeNull();
    }

    [Fact]
    public async Task ReincercareaNuVineInainteDeUnMinut()
    {
        var m = await NotificareEsuata();
        h.Emailuri.Esueaza = false;
        h.Ceas.Advance(TimeSpan.FromSeconds(50));
        await Proceseaza();
        (await Rand(m.Id)).TrimisLa.ShouldBeNull();
    }

    [Fact]
    public async Task ReincercareaTrimiteEmailul()
    {
        var m = await NotificareEsuata();
        h.Emailuri.Esueaza = false;
        h.Ceas.Advance(TimeSpan.FromMinutes(1.5));
        await Proceseaza();
        var dupa = await Rand(m.Id);
        dupa.TrimisLa.ShouldNotBeNull();
        dupa.Eroare.ShouldBeNull();
    }

    [Fact]
    public async Task ContinutulNuEstePastratDupaLivrare()
    {
        var m = await NotificareEsuata();
        h.Emailuri.Esueaza = false;
        h.Ceas.Advance(TimeSpan.FromMinutes(1.5));
        await Proceseaza();
        var dupa = await Rand(m.Id);
        dupa.Html.ShouldBeNull();
        dupa.Text.ShouldBeNull();
    }

    [Fact]
    public async Task AsteptarileCrescIarApoiSeRenunta()
    {
        var m = await NotificareEsuata();
        foreach (var minute in new[] { 5, 15, 60, 240, 240 })
        {
            h.Ceas.Advance(TimeSpan.FromMinutes(minute + 0.5));
            await Proceseaza();
        }
        var dupa = await Rand(m.Id);
        dupa.Incercari.ShouldBe(6);
        h.Ceas.Advance(TimeSpan.FromDays(1));
        h.Emailuri.Esueaza = false;
        await Proceseaza();
        (await Rand(m.Id)).TrimisLa.ShouldBeNull();
    }

    [Fact]
    public async Task CodulCareNuPleacaEsteInregistratCuEroarea()
    {
        h.Emailuri.Esueaza = true;
        await h.Anonim().Post("/autentificare/inregistrare", new { nume = "Ana", username = "ana", email = "ana@example.invalid", parola = Harness.ParolaTest });
        var m = await h.Db(db => db.Set<MesajIesire>().SingleAsync(x => x.Tip == "confirmare_email"));
        m.Eroare.ShouldNotBeNull();
        m.TrimisLa.ShouldBeNull();
    }

    [Fact]
    public async Task AdminulVedeMesajeleTrimiseFaraContinut()
    {
        await h.InregistreazaElev("ana");
        await h.InregistreazaElev("bogdan");
        using var admin = await h.Login("admin");
        var lista = (await admin.GetJson("/admin/mesaje")).Lista();
        lista.Count.ShouldBeGreaterThanOrEqualTo(2);
        lista.ShouldAllBe(m => m.GetProperty("continut").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task DoarAdminiiVadMesajele()
    {
        using var george = await h.Login("george");
        using var irina = await h.Login("irina");
        await george.Get("/admin/mesaje").Status(HttpStatusCode.Forbidden);
        await irina.Get("/admin/mesaje").Status(HttpStatusCode.Forbidden);
        await h.Anonim().Get("/admin/mesaje").Status(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CuFurnizorulJurnalAdminulVedeCodul()
    {
        await using var jurnal = await Harness.Porneste(ceasReal: true);
        await jurnal.Anonim().Post("/autentificare/inregistrare", new { nume = "Ana", username = "ana", email = "ana@example.invalid", parola = Harness.ParolaTest });
        using var admin = await jurnal.Login("admin");
        var m = (await admin.GetJson("/admin/mesaje")).Lista().First(x => x.Str("catre") == "ana@example.invalid");
        m.GetProperty("continut").ToString().ShouldMatch(@"\b\d{6}\b");
    }
}

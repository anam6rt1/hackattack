using System.Net;
using MarkAi.Api.Servicii;
using MarkAi.Api.Servicii.Mesagerie;
using MarkAi.Data.Entities;
using MarkAi.Tests.Suport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.WebUtilities;

namespace MarkAi.Tests.Integration.Continut;
public class NotificariTests : TestCuHarness
{
    private sealed class PushFals : IExpeditorPush
    {
        public bool Configurat => true;
        public bool Expirat { get; set; }
        public int Trimise { get; set; }
        public Task TrimiteAsync(AbonamentPush a, string url, CancellationToken ct) { if (Expirat) throw new AbonamentExpirat(); Trimise++; return Task.CompletedTask; }
    }
    private readonly PushFals push = new();
    private HttpClient elev = null!, profesor = null!;
    private int uid, org, tema;
    public override async ValueTask InitializeAsync()
    {
        h = await Harness.Porneste(configureazaServicii: s => s.AddSingleton<IExpeditorPush>(push));
        elev = await h.Login("george"); profesor = await h.Login("irina"); uid = (await elev.GetJson("/utilizatori/me")).Int("id");
        await h.Db(async db => {
            org = await db.Set<Organizatie>().Select(o => o.Id).SingleAsync();
            db.Add(new MembruOrganizatie { OrganizatieId = org, UtilizatorId = uid, Rol = "elev", Stare = "aprobat", CerutLa = h.Ceas.GetUtcNow().UtcDateTime });
            var p = new Parcurs { MaterieId = 1, Titlu = "Tema de test", Publicat = true, Clasa = "Bac" }; db.Add(p);
            var t = new Tema { OrganizatieId = org, Titlu = "Tema privată", CreatLa = h.Ceas.GetUtcNow().UtcDateTime, TermenLa = h.Ceas.GetUtcNow().UtcDateTime.AddHours(20), ToataOrganizatia = true, Lectii = [new() { Parcurs = p }] };
            db.Add(t); await db.SaveChangesAsync(); tema = t.Id;
        });
    }
    private Task Activeaza(bool toate = true) => elev.Put("/utilizatori/me/notificari", new { sedinte = toate, decizii = toate, temeNoi = toate, termeneTeme = toate }).Status(HttpStatusCode.NoContent);
    private Task Proceseaza() => h.Serviciu<ProcesorNotificari>().RuleazaOdataAsync(Ct);
    private static object Abonament(string endpoint = "https://fcm.googleapis.com/test") => new { endpoint, keys = new { p256dh = WebPush.VapidHelper.GenerateVapidKeys().PublicKey, auth = WebEncoders.Base64UrlEncode(new byte[16]) } };
    [Fact]
    public async Task TotulEsteDezactivatImplicitSiNuSeTrimiteNimic()
    {
        var p = await elev.GetJson("/utilizatori/me/notificari"); p.Bool("sedinte").ShouldBeFalse(); p.Bool("decizii").ShouldBeFalse(); p.Bool("temeNoi").ShouldBeFalse(); p.Bool("termeneTeme").ShouldBeFalse();
        await Proceseaza(); (await h.Db(db => db.Set<Notificare>().CountAsync())).ShouldBe(0);
    }
    [Fact]
    public async Task EmailulEsteFallbackIarRemindereleNuSeDublezaza()
    {
        await Activeaza(); await Proceseaza(); await Proceseaza();
        (await h.Db(db => db.Set<Notificare>().CountAsync(n => n.UtilizatorId == uid))).ShouldBe(2);
        await h.Serviciu<ProcesorMesaje>().RuleazaOdataAsync(Ct);
        (await h.Db(db => db.Set<MesajIesire>().CountAsync(m => m.Tip.StartsWith("notificare_") && m.TrimisLa != null))).ShouldBe(2);
        push.Trimise.ShouldBe(0);
    }
    [Fact]
    public async Task AbonamentulPushEsteFolositSiEliminatDacaExpira()
    {
        await Activeaza(); await elev.Post("/utilizatori/me/notificari/abonamente", Abonament()).Status(HttpStatusCode.NoContent);
        await Proceseaza(); push.Trimise.ShouldBe(2);
        (await h.Db(db => db.Set<MesajIesire>().CountAsync(m => m.Tip.StartsWith("notificare_")))).ShouldBe(0);
    }
    [Fact]
    public async Task EndpointurileArbitrareSuntRefuzateIarExpirareaRevineLaEmail()
    {
        await elev.Post("/utilizatori/me/notificari/abonamente", Abonament("https://127.0.0.1/private")).Status(HttpStatusCode.BadRequest);
        await Activeaza(); await elev.Post("/utilizatori/me/notificari/abonamente", Abonament()).Status(HttpStatusCode.NoContent);
        push.Expirat = true; await Proceseaza();
        (await h.Db(db => db.Set<AbonamentPush>().CountAsync())).ShouldBe(0);
        (await h.Db(db => db.Set<MesajIesire>().CountAsync(m => m.Tip.StartsWith("notificare_")))).ShouldBe(2);
    }
    [Fact]
    public async Task EmailulInAsteptareEsteAnulatDupaPlecareaDinOrganizatie()
    {
        await Activeaza(); await Proceseaza();
        await h.Db(async db => { await db.Set<MembruOrganizatie>().Where(m => m.UtilizatorId == uid).ExecuteDeleteAsync(); });
        await h.Serviciu<ProcesorMesaje>().RuleazaOdataAsync(Ct);
        (await h.Db(db => db.Set<Notificare>().CountAsync(n => n.Canal == "anulata"))).ShouldBe(2);
    }
    [Fact]
    public async Task DezactivareaInainteDeLivrareOpresteMesajele()
    {
        await Activeaza(); await Proceseaza(); await Activeaza(false);
        await h.Serviciu<ProcesorMesaje>().RuleazaOdataAsync(Ct);
        (await h.Db(db => db.Set<MesajIesire>().CountAsync(m => m.Furnizor == "anulat"))).ShouldBe(2);
    }
    [Fact]
    public async Task SedinteleAuReminderePentruElevSiProfesorFaraDublare()
    {
        await Activeaza(); await profesor.Put("/utilizatori/me/notificari", new { sedinte = true, decizii = false, temeNoi = false, termeneTeme = false }).Status(HttpStatusCode.NoContent);
        await h.Db(async db => {
            var profesorId = await db.Utilizatori.Where(u => u.Username == "irina").Select(u => u.Id).SingleAsync();
            var s = new CameraTutoring { MentorId = profesorId, MaterieId = 1, Titlu = "Pregătire", Status = "deschisa", Capacitate = 5, ProBono = true,
                InceputLa = h.Ceas.GetUtcNow().UtcDateTime.AddMinutes(30), FinalizatLa = h.Ceas.GetUtcNow().UtcDateTime.AddMinutes(90), Tip = "online", LinkIntalnire = "https://meet.example.invalid/test" };
            db.Add(s); db.Add(new Rezervare { Camera = s, ElevId = uid, Status = "confirmata", Moneda = "RON", RezervataLa = h.Ceas.GetUtcNow().UtcDateTime }); await db.SaveChangesAsync();
        });
        await Proceseaza(); await Proceseaza();
        (await h.Db(db => db.Set<Notificare>().CountAsync(n => n.Tip == "sedinta"))).ShouldBe(1);
        (await h.Db(db => db.Set<Notificare>().CountAsync(n => n.Tip == "sedinta_profesor"))).ShouldBe(1);
    }
    [Fact]
    public async Task DeciziaSeTrimiteDoarCandUtilizatorulAActivatTipul()
    {
        await Activeaza();
        await h.Db(async db => { var m = await db.Set<MembruOrganizatie>().FindAsync(org, uid); m!.Stare = "in_asteptare"; await db.SaveChangesAsync(); });
        await profesor.Post($"/organizatii/{org}/elevi/{uid}/aprobare").Status(HttpStatusCode.NoContent);
        (await h.Db(db => db.Set<Notificare>().CountAsync(n => n.Tip == "decizie"))).ShouldBe(1);
    }
}

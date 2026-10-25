using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Api.Servicii.Mesagerie;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Servicii;

public sealed class Notificari(MarkAiDbContext db, Posta posta, IExpeditorPush push, TimeProvider ceas)
{
    public PreferinteNotificari Implicite(int uid) => new() { UtilizatorId = uid };
    public static bool Permite(PreferinteNotificari p, string tip) => tip switch { "sedinta" or "sedinta_profesor" => p.Sedinte, "decizie" => p.Decizii, "tema_noua" => p.TemeNoi, "tema_termen" => p.TermeneTeme, _ => false };
    public async Task PuneAsync(int uid, string cheie, string tip, int resursa, string titlu, string text, string url, CancellationToken ct)
    {
        var preferinte = await db.Set<PreferinteNotificari>().FindAsync([uid], ct) ?? Implicite(uid);
        if (!Permite(preferinte, tip) || await db.Set<Notificare>().AnyAsync(n => n.UtilizatorId == uid && n.Cheie == cheie, ct)
            || db.Set<Notificare>().Local.Any(n => n.UtilizatorId == uid && n.Cheie == cheie)) return;
        var u = await db.Utilizatori.SingleOrDefaultAsync(u => u.Id == uid && u.StergereCerutaLa == null && u.EmailConfirmatLa != null, ct);
        if (u == null) return;
        var acum = ceas.GetUtcNow().UtcDateTime;
        var n = new Notificare { UtilizatorId = uid, Cheie = cheie, Tip = tip, ResursaId = resursa, Titlu = titlu, Text = text, Url = url, CreatLa = acum, UrmatoareaIncercareLa = acum };
        db.Add(n);
        if (!push.Configurat || !await db.Set<AbonamentPush>().AnyAsync(a => a.UtilizatorId == uid, ct)) Email(n, u.Email);
    }
    public void Email(Notificare n, string catre)
    {
        n.MesajEmail = posta.Pune(Sabloane.Notificare(catre, n.Titlu, n.Titlu, n.Text, "Deschide TeleBac pentru detalii."), n.Tip == "decizie" ? n.Cheie.StartsWith("aprobat:") ? "membru_aprobat" : "membru_respins" : "notificare_" + n.Tip);
        n.Canal = "email"; n.FinalizatLa = ceas.GetUtcNow().UtcDateTime;
    }
}

public sealed class ProcesorNotificari(IServiceScopeFactory scopes, TimeProvider ceas, ILogger<ProcesorNotificari> log) : BackgroundService
{
    private readonly SemaphoreSlim mutex = new(1, 1);
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await RuleazaOdataAsync(ct); } catch (Exception e) when (e is not OperationCanceledException) { log.LogError("Notification processing failed ({Tip}).", e.GetType().Name); }
            await Task.Delay(TimeSpan.FromMinutes(1), ct);
        }
    }
    public async Task RuleazaOdataAsync(CancellationToken ct)
    {
        await mutex.WaitAsync(ct);
        try
        {
            using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MarkAiDbContext>();
            var coada = scope.ServiceProvider.GetRequiredService<Notificari>(); var push = scope.ServiceProvider.GetRequiredService<IExpeditorPush>();
            var acum = ceas.GetUtcNow().UtcDateTime;
            // Bounded SQL batches with durable receipts; a restart never recreates delivered reminders.
            var sedinte = await db.Rezervari.Where(r => r.Status == "confirmata" && r.Camera.Status == "deschisa" && r.Camera.InceputLa > acum && r.Camera.InceputLa <= acum.AddHours(1)
                && !db.Set<Notificare>().Any(n => n.UtilizatorId == r.ElevId && n.Tip == "sedinta" && n.ResursaId == r.Id)
                && db.Set<PreferinteNotificari>().Any(p => p.UtilizatorId == r.ElevId && p.Sedinte))
                .OrderBy(r => r.Camera.InceputLa).ThenBy(r => r.Id).Take(100).Select(r => new { r.Id, r.ElevId, r.Camera.Titlu }).ToListAsync(ct);
            foreach (var s in sedinte) await coada.PuneAsync(s.ElevId, $"sedinta:{s.Id}", "sedinta", s.Id, "Ședința ta începe în curând", $"„{s.Titlu}” începe în mai puțin de o oră.", "/", ct);
            var profesori = await db.CamereTutoring.Where(s => s.Status == "deschisa" && s.InceputLa > acum && s.InceputLa <= acum.AddHours(1)
                && db.Set<PreferinteNotificari>().Any(p => p.UtilizatorId == s.MentorId && p.Sedinte)
                && !db.Set<Notificare>().Any(n => n.UtilizatorId == s.MentorId && n.Tip == "sedinta_profesor" && n.ResursaId == s.Id))
                .OrderBy(s => s.InceputLa).ThenBy(s => s.Id).Take(100).Select(s => new { s.Id, s.MentorId, s.Titlu }).ToListAsync(ct);
            foreach (var s in profesori) await coada.PuneAsync(s.MentorId, $"sedinta_profesor:{s.Id}", "sedinta_profesor", s.Id, "Ședința ta începe în curând", $"„{s.Titlu}” începe în mai puțin de o oră.", "/", ct);
            await Teme(db, coada, acum, false, ct); await Teme(db, coada, acum, true, ct); await db.SaveChangesAsync(ct);
            foreach (var n in await db.Set<Notificare>().Where(n => n.FinalizatLa == null && n.UrmatoareaIncercareLa <= acum).OrderBy(n => n.Id).Take(50).ToListAsync(ct))
            {
                var p = await db.Set<PreferinteNotificari>().FindAsync([n.UtilizatorId], ct) ?? coada.Implicite(n.UtilizatorId);
                var u = await db.Utilizatori.SingleOrDefaultAsync(u => u.Id == n.UtilizatorId && u.StergereCerutaLa == null, ct);
                if (u == null || !Notificari.Permite(p, n.Tip) || !await Actuala(db, n, acum, ct)) { n.FinalizatLa = acum; n.Canal = "anulata"; continue; }
                var abonamente = await db.Set<AbonamentPush>().Where(a => a.UtilizatorId == n.UtilizatorId).ToListAsync(ct);
                var trimis = false; var eroare = false;
                if (push.Configurat) foreach (var a in abonamente)
                {
                    try { await push.TrimiteAsync(a, n.Url, ct); trimis = true; }
                    catch (AbonamentExpirat) { db.Remove(a); }
                    catch (Exception e) when (e is not OperationCanceledException) { eroare = true; log.LogWarning("Push delivery deferred for notification {Id} ({Tip}).", n.Id, e.GetType().Name); }
                }
                if (trimis) { n.FinalizatLa = acum; n.Canal = "push"; }
                else if (eroare && ++n.Incercari < 3) n.UrmatoareaIncercareLa = acum.AddMinutes(n.Incercari * 2);
                else coada.Email(n, u.Email);
            }
            await db.SaveChangesAsync(ct);
        }
        finally { mutex.Release(); }
    }
    private static IQueryable<Tema> TemeElev(MarkAiDbContext db, int uid) => db.Set<Tema>().Where(t => !t.Anulata && t.Organizatie.Stare == Organizatie.Aprobata && t.Lectii.Any()
        && db.Set<MembruOrganizatie>().Any(m => m.OrganizatieId == t.OrganizatieId && m.UtilizatorId == uid && m.Rol == "elev" && m.Stare == "aprobat")
        && (t.ToataOrganizatia || t.Grupe.Any(g => g.Grupa.Membri.Any(m => m.ElevId == uid)))
        && t.Lectii.All(l => l.Parcurs.Publicat && (l.Parcurs.Vizibilitate == Parcurs.Public || l.Parcurs.OrganizatieId == t.OrganizatieId || l.Parcurs.Partajari.Any(s => s.OrganizatieId == t.OrganizatieId))));
    internal static async Task<bool> Actuala(MarkAiDbContext db, Notificare n, DateTime acum, CancellationToken ct) => n.Tip switch
    {
        "sedinta" => await db.Rezervari.AnyAsync(r => r.Id == n.ResursaId && r.ElevId == n.UtilizatorId && r.Status == "confirmata" && r.Camera.Status == "deschisa" && r.Camera.InceputLa > acum, ct),
        "sedinta_profesor" => await db.CamereTutoring.AnyAsync(s => s.Id == n.ResursaId && s.MentorId == n.UtilizatorId && s.Status == "deschisa" && s.InceputLa > acum, ct),
        "tema_noua" or "tema_termen" => await TemeElev(db, n.UtilizatorId).AnyAsync(t => t.Id == n.ResursaId && t.TermenLa > acum && t.Lectii.Any(l => !l.Parcurs.Progrese.Any(p => p.UtilizatorId == n.UtilizatorId && p.PromovatLa != null)), ct),
        _ => true,
    };
    private static async Task Teme(MarkAiDbContext db, Notificari coada, DateTime acum, bool termen, CancellationToken ct)
    {
        var implicitActiv = termen ? coada.Implicite(0).TermeneTeme : coada.Implicite(0).TemeNoi;
        var tip = termen ? "tema_termen" : "tema_noua";
        var candidati = from m in db.Set<MembruOrganizatie>() where m.Rol == "elev" && m.Stare == "aprobat" && m.Organizatie.Stare == Organizatie.Aprobata && m.Utilizator.StergereCerutaLa == null && m.Utilizator.EmailConfirmatLa != null
                        from t in db.Set<Tema>() where t.OrganizatieId == m.OrganizatieId && !t.Anulata && t.TermenLa > acum
                            && (termen ? t.TermenLa <= acum.AddDays(1) : t.CreatLa >= acum.AddDays(-1))
                            && (db.Set<PreferinteNotificari>().Any(p => p.UtilizatorId == m.UtilizatorId && (termen ? p.TermeneTeme : p.TemeNoi))
                                || (implicitActiv && !db.Set<PreferinteNotificari>().Any(p => p.UtilizatorId == m.UtilizatorId)))
                            && !db.Set<Notificare>().Any(n => n.UtilizatorId == m.UtilizatorId && n.Tip == tip && n.ResursaId == t.Id)
                            && (t.ToataOrganizatia || t.Grupe.Any(g => g.Grupa.Membri.Any(e => e.ElevId == m.UtilizatorId)))
                            && t.Lectii.Any() && t.Lectii.Any(l => !l.Parcurs.Progrese.Any(p => p.UtilizatorId == m.UtilizatorId && p.PromovatLa != null))
                            && t.Lectii.All(l => l.Parcurs.Publicat && (l.Parcurs.Vizibilitate == Parcurs.Public || l.Parcurs.OrganizatieId == m.OrganizatieId || l.Parcurs.Partajari.Any(s => s.OrganizatieId == m.OrganizatieId)))
                        orderby t.TermenLa, m.UtilizatorId select new { t.Id, t.Titlu, m.UtilizatorId };
        foreach (var x in await candidati.Take(100).ToListAsync(ct))
            await coada.PuneAsync(x.UtilizatorId, $"{tip}:{x.Id}", tip, x.Id, termen ? "O temă se apropie de termen" : "Ai o temă nouă", $"„{x.Titlu}” — vezi lecțiile și termenul în aplicație.", "/teme", ct);
    }
}

using MarkAi.Api.Servicii.Mesagerie;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Servicii;
public sealed class SumareParinti(IServiceScopeFactory scopes, TimeProvider ceas, ILogger<SumareParinti> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await RuleazaOdataAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { log.LogError(ex, "Parent summary failed"); }
            await Task.Delay(TimeSpan.FromMinutes(10), ceas, ct);
        }
    }
    public static DateTime Luni(DateTime acum)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(acum, TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest"));
        var luni = local.Date.AddDays(-((int)local.DayOfWeek + 6) % 7);
        return TimeZoneInfo.ConvertTimeToUtc(luni, TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest"));
    }
    public static async Task<bool> Permis(MarkAiDbContext db, EmailSumar e, DateTime now, CancellationToken ct)
    {
        var p = await db.Set<SumarParinte>().FindAsync([e.UtilizatorId], ct);
        return p != null && p.TokenHash == e.Versiune && (e.Confirmare ? p.ConfirmatLa == null && p.ExpiraLa > now : p.ConfirmatLa != null)
            && await db.Utilizatori.AnyAsync(u => u.Id == e.UtilizatorId && u.StergereCerutaLa == null, ct);
    }
    public async Task RuleazaOdataAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope(); var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<MarkAiDbContext>(); var cfg = sp.GetRequiredService<IConfiguration>();
        var posta = sp.GetRequiredService<Posta>(); var protector = sp.GetRequiredService<IDataProtectionProvider>().CreateProtector("sumar-parinte");
        var now = ceas.GetUtcNow().UtcDateTime; var end = Luni(now);
        var start = TimeZoneInfo.ConvertTimeToUtc(TimeZoneInfo.ConvertTimeFromUtc(end, TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest")).AddDays(-7), TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest"));
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7731026)", ct);
        var lista = await db.Set<SumarParinte>().Where(p => p.ConfirmatLa != null && p.ConfirmatLa < end && (p.UltimaSaptamana == null || p.UltimaSaptamana < end)
            && db.Utilizatori.Any(u => u.Id == p.UtilizatorId && u.StergereCerutaLa == null)).OrderBy(p => p.UtilizatorId).Take(50).ToListAsync(ct);
        await Invitatii.Buget(db, cfg, now, lista.Count, ct);
        foreach (var p in lista)
        {
            var lectii = await db.Set<ProgresLectie>().CountAsync(l => l.UtilizatorId == p.UtilizatorId && l.PromovatLa >= start && l.PromovatLa < end, ct);
            var teme = await Rapoarte.Teme(db, 0, p.UtilizatorId, start, end, ct);
            var sedinte = await db.Rezervari.CountAsync(r => r.ElevId == p.UtilizatorId && r.Status != "anulata" && r.Camera.FinalizatLa >= start && r.Camera.FinalizatLa < end, ct);
            var text = $"Săptămâna {TimeZoneInfo.ConvertTimeFromUtc(start, TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest")):dd.MM.yyyy}: {lectii} lecții terminate, {teme.Predate} din {teme.Total} teme cu termen în această săptămână predate până la încheierea ei, {sedinte} ședințe încheiate în aplicație. Rezervările nu dovedesc prezența fizică. Pentru oprirea sumarelor, deschide linkul de mai jos.";
            var url = Invitatii.Url(cfg, "/sumar-parinte/oprire", protector.Unprotect(p.OprireProtejat));
            var m = posta.Pune(Invitatii.Mesaj(p.Email, "TeleBac · Sumar săptămânal", text, url), "sumar_parinte");
            db.Add(new EmailSumar { Mesaj = m, UtilizatorId = p.UtilizatorId, Versiune = p.TokenHash }); p.UltimaSaptamana = end;
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}

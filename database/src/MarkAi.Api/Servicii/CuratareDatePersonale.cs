using MarkAi.Data;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Servicii;

public sealed class CuratareDatePersonale(IServiceScopeFactory scopes, Stocare stocare, IncarcariReluabile incarcari,
    IConfiguration cfg, TimeProvider ceas, ILogger<CuratareDatePersonale> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await RuleazaOdataAsync(ct); }
            catch (Exception e) when (e is not OperationCanceledException) { log.LogError("Personal-data cleanup failed ({Tip}).", e.GetType().Name); }
            await Task.Delay(TimeSpan.FromMinutes(1), ct);
        }
    }

    public async Task RuleazaOdataAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarkAiDbContext>();
        foreach (var cerere in await db.Set<StergereDateCont>().Where(x => x.FinalizatLa == null).OrderBy(x => x.Id).Take(20).ToListAsync(ct))
        {
            try
            {
                await incarcari.StergePentruAsync(cerere.UtilizatorId, ct);
                foreach (var cheie in cerere.CheiFisiere) stocare.StergeDatePersonale(cheie);
                cerere.CheiFisiere = [];
                cerere.FinalizatLa = ceas.GetUtcNow().UtcDateTime;
                await db.SaveChangesAsync(ct);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
            { log.LogWarning("Personal-file cleanup deferred for request {Id} ({Tip}).", cerere.Id, e.GetType().Name); }
        }
        // Explicit opt-in only: zero/missing means retention has not been decided and no automatic deletion occurs.
        var acum = ceas.GetUtcNow().UtcDateTime;
        var conversatii = cfg.GetValue<int>("Confidentialitate:ConversatiiZile");
        if (conversatii > 0)
        {
            var limita = acum.AddDays(-conversatii);
            await db.Mesaje.Where(m => m.Sesiune.Canal == "telefon" && m.Sesiune.FinalizatLa != null && m.Sesiune.FinalizatLa < limita).ExecuteDeleteAsync(ct);
        }
        var mesaje = cfg.GetValue<int>("Confidentialitate:MesajeTrimiseZile");
        if (mesaje > 0)
        {
            var limita = acum.AddDays(-mesaje);
            await db.Set<MesajIesire>().Where(m => m.TrimisLa != null && m.TrimisLa < limita).ExecuteDeleteAsync(ct);
        }
        var contact = cfg.GetValue<int>("Confidentialitate:MesajeContactZile");
        if (contact > 0)
        {
            var limita = acum.AddDays(-contact);
            await db.Set<MesajContact>().Where(m => m.RezolvatLa != null && m.RezolvatLa < limita).ExecuteDeleteAsync(ct);
        }
    }
}

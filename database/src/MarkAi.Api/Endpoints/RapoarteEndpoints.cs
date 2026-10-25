using System.Globalization;
using MarkAi.Api.Infrastructura;
using MarkAi.Api.Servicii;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace MarkAi.Api.Endpoints;
public static class RapoarteEndpoints
{
    public static async Task Admin(MarkAiDbContext db, int org, int uid, CancellationToken ct)
    {
        if (!await db.Set<Organizatie>().AnyAsync(o => o.Id == org && o.AdminId == uid && o.Stare == Organizatie.Aprobata, ct)) throw new UnauthorizedAccessException();
    }
    public static void Mapeaza(WebApplication app)
    {
        app.MapGet("/organizatii/{org:int}/raport", async (int org, string luna, HttpContext h, MarkAiDbContext db, IMemoryCache cache, TimeProvider ceas, CancellationToken ct) =>
        {
            await Admin(db, org, h.User.Id(), ct); h.Response.Headers.CacheControl = "no-store";
            if (!DateTime.TryParseExact(luna + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local) || local.Year < 2020 || local.Year > ceas.GetUtcNow().Year + 1)
                throw new CerereInvalida("Alege o lună validă.");
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest");
            var start = TimeZoneInfo.ConvertTimeToUtc(local, tz); var end = TimeZoneInfo.ConvertTimeToUtc(local.AddMonths(1), tz);
            var key = $"raport-lunar:{org}:{luna}";
            if (!cache.TryGetValue<RaportLunar>(key, out var rezultat))
            {
                rezultat = await Rapoarte.Lunar(db, org, start, end, ceas.GetUtcNow().UtcDateTime, ct);
                cache.Set(key, rezultat, TimeSpan.FromHours(1));
            }
            return rezultat;
        }).RequireAuthorization(Politici.ProfesorAprobat);
        app.MapGet("/parcursuri/{id:int}/statistici-intrebari", async (int id, int? pagina, HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) =>
        {
            await acces.PentruEditareAsync(h.User, id, ct); h.Response.Headers.CacheControl = "no-store";
            var q = db.Set<Intrebare>().Where(q => q.ParcursId == id).Select(q => new StatisticaIntrebare {
                Id = q.Id, Text = q.Text, Raspunsuri = db.Set<RaspunsStatistica>().Count(r => r.IntrebareId == q.Id),
                Corecte = db.Set<RaspunsStatistica>().Count(r => r.IntrebareId == q.Id && r.Corect) });
            return (object)new { Total = await q.CountAsync(ct), Elemente = await q.OrderBy(x => x.Raspunsuri == 0 ? 2.0 : (double)x.Corecte / x.Raspunsuri).ThenBy(x => x.Id)
                .Skip((Math.Clamp(pagina ?? 1, 1, 10000) - 1) * 20).Take(20).ToListAsync(ct) };
        }).RequireAuthorization(Politici.Continut);
    }
}
public class StatisticaIntrebare { public int Id { get; set; } public string Text { get; set; } = ""; public int Raspunsuri { get; set; } public int Corecte { get; set; } }

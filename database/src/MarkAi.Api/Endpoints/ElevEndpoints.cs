using MarkAi.Api.Infrastructura;
using MarkAi.Api.Servicii.Triviador;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Endpoints;

public record ProgresMaterie(int MaterieId, string Denumire, int Puncte, int LectiiCompletate, int TotalLectii);
public record LectieCompletata(int ParcursId, string Titlu, string Clasa, int MaterieId, string Materie, DateTime CompletatLa);
/// <param name="CursId">The chapter opened last in that Lecție (null if it was deleted).</param>
public record UltimaLectie(int ParcursId, string Titlu, int MaterieId, string Materie, bool Completata, DateTime Accesat, int? CursId, string? Capitol);
public record ProgresGeneral(NivelCurent Nivel, IReadOnlyList<ProgresMaterie> Materii, UltimaLectie? UltimaLectie,
    IReadOnlyList<LectieCompletata> LectiiCompletate);

public record CameraNoua(List<int>? MaterieIds, int Insule = 9, int MaxJucatori = 3, string? Clasa = null);

public record TemaDto(string Cod, string Nume, string? Anotimp, string? PerioadaZilei);
// Mod/Tema = fundalul (anotimp × zi/noapte), Interfata = stilul desenelor (insule/realist/pixel), Avatar = personajul din profil.
// La PUT, câmpurile lipsă (null) rămân cum erau, deci clienții vechi care trimit doar { mod, tema } merg la fel.
/// <param name="AscundeLectiiTerminate">Materiale hides the finished Lecții; null in a PUT = unchanged.</param>
public record Preferinte(string? Mod, string? Tema, string? Interfata = null, string? Avatar = null, bool? AscundeLectiiTerminate = null);

public static class ElevEndpoints
{
    public static void Mapeaza(WebApplication app)
    {
        app.MapGet("/progres", async (HttpContext h, MarkAiDbContext db, AccesContinut acces, ProgresService progres, CancellationToken ct) =>
        {
            var uid = h.User.Id();
            var a = await acces.PentruAsync(h.User, ct);
            var puncte = await db.EvenimenteRecompensa.Where(e => e.UtilizatorId == uid && e.MaterieId != null)
                .GroupBy(e => e.MaterieId!.Value).Select(g => new { MaterieId = g.Key, Puncte = g.Sum(e => e.PuncteMaterie) })
                .ToDictionaryAsync(x => x.MaterieId, x => x.Puncte, ct);
            // Only Lecții the student may still see (for example, not those of an organisation they left).
            var vizibile = acces.Parcursuri(a).Where(p => p.Publicat);
            var lectii = await vizibile.Select(p => new { p.MaterieId, Gata = p.Progrese.Any(x => x.UtilizatorId == uid && x.PromovatLa != null) }).ToListAsync(ct);
            var materii = await db.Materii.AsNoTracking().OrderBy(m => m.Ordine).Select(m => new { m.Id, m.Denumire }).ToListAsync(ct);
            var progrese = db.Set<ProgresLectie>().AsNoTracking().Where(x => x.UtilizatorId == uid && vizibile.Any(p => p.Id == x.ParcursId));
            var ultima = await progrese.OrderByDescending(x => x.UltimaAccesare)
                .Select(x => new UltimaLectie(x.ParcursId, x.Parcurs.Titlu, x.Parcurs.MaterieId, x.Parcurs.Materie.Denumire, x.PromovatLa != null, x.UltimaAccesare,
                    x.UltimulCursId, x.UltimulCurs == null ? null : x.UltimulCurs.Titlu))
                .FirstOrDefaultAsync(ct);
            var completate = await progrese.Where(x => x.PromovatLa != null).OrderByDescending(x => x.PromovatLa)
                .Select(x => new LectieCompletata(x.ParcursId, x.Parcurs.Titlu, x.Parcurs.Clasa, x.Parcurs.MaterieId, x.Parcurs.Materie.Denumire, x.PromovatLa!.Value))
                .ToListAsync(ct);
            return new ProgresGeneral(await progres.NivelAsync(uid, ct),
                materii.Select(m => new ProgresMaterie(m.Id, m.Denumire, puncte.GetValueOrDefault(m.Id),
                    lectii.Count(l => l.MaterieId == m.Id && l.Gata), lectii.Count(l => l.MaterieId == m.Id))).ToList(),
                ultima, completate);
        }).WithTags("Progres").RequireAuthorization(Politici.Elev);

        // Triviador: a room is created here and then played over the /hub/joc SignalR hub; the server referees the game.
        app.MapPost("/trivia/camere", async (CameraNoua c, HttpContext h, MotorTriviador motor, CancellationToken ct) =>
        {
            var cod = await motor.CreeazaAsync(h.User.Id(), new SetariCamera(c.MaterieIds ?? [], c.Insule, c.MaxJucatori, c.Clasa), ct);
            return Results.Created($"/joc/{cod}", new { cod });
        }).WithTags("Triviador").RequireAuthorization(Politici.Elev);

        var aspect = app.MapGroup("").WithTags("Aspect");
        aspect.MapGet("/aspect/teme", async (MarkAiDbContext db, CancellationToken ct) =>
        {
            var teme = await db.Backgrounds.AsNoTracking().Where(b => b.Activ && b.SelectabilStatic).OrderBy(b => b.Id)
                .Select(b => new TemaDto(b.Cod, b.Nume,
                    b.ReguliBackground.Where(r => r.Activa).OrderByDescending(r => r.Prioritate).Select(r => r.Anotimp).FirstOrDefault(),
                    b.ReguliBackground.Where(r => r.Activa).OrderByDescending(r => r.Prioritate).Select(r => r.PerioadaZilei).FirstOrDefault()))
                .ToListAsync(ct);
            return Lista<TemaDto>.Din(teme);
        });

        var pref = app.MapGroup("/utilizatori/me/preferinte").WithTags("Aspect").RequireAuthorization();
        pref.MapGet("", (HttpContext h, MarkAiDbContext db, CancellationToken ct) => CitestePreferinte(db, h.User.Id(), ct));
        pref.MapPut("", async (Preferinte c, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var uid = h.User.Id();
            var p = await db.PreferinteUtilizatori.SingleOrDefaultAsync(x => x.UtilizatorId == uid, ct);
            if (p is null) db.PreferinteUtilizatori.Add(p = new PreferinteUtilizator { UtilizatorId = uid, ModBackground = "auto", Timezone = "Europe/Bucharest" });
            if (c.Mod is not null)
            {
                int? fundal = null;
                if (c.Mod == "static")
                    fundal = await db.Backgrounds.Where(b => b.Cod == c.Tema && b.Activ && b.SelectabilStatic).Select(b => (int?)b.Id).SingleOrDefaultAsync(ct)
                        ?? throw new CerereInvalida("Tema aleasă nu există.");
                else if (c.Mod != "auto") throw new CerereInvalida("Modul trebuie să fie „auto” sau „static”.");
                p.ModBackground = c.Mod;
                p.BackgroundStaticId = fundal;
            }
            if (c.Interfata is not null)
                p.InterfataId = await db.Interfata.Where(i => i.Cod == c.Interfata && i.Activa).Select(i => (int?)i.Id).SingleOrDefaultAsync(ct)
                    ?? throw new CerereInvalida("Stilul ales nu există.");
            if (c.Avatar is not null)
            {
                // Desenele avatarelor sunt în frontend; aici păstrăm doar un cod scurt.
                if (c.Avatar.Length > 40 || !c.Avatar.All(ch => ch is >= 'a' and <= 'z' or '-'))
                    throw new CerereInvalida("Avatarul ales nu există.");
                p.Avatar = c.Avatar.Length == 0 ? null : c.Avatar;
            }
            if (c.AscundeLectiiTerminate is { } ascunde) p.AscundeLectiiTerminate = ascunde;
            p.ActualizatLa = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return await CitestePreferinte(db, uid, ct);
        });
    }

    static async Task<Preferinte> CitestePreferinte(MarkAiDbContext db, int uid, CancellationToken ct) =>
        await db.PreferinteUtilizatori.AsNoTracking().Where(x => x.UtilizatorId == uid)
            .Select(x => new Preferinte(x.ModBackground, x.ModBackground == "static" && x.BackgroundStatic != null ? x.BackgroundStatic.Cod : null,
                x.Interfata == null ? null : x.Interfata.Cod, x.Avatar, x.AscundeLectiiTerminate))
            .SingleOrDefaultAsync(ct)
        ?? new Preferinte("auto", null, AscundeLectiiTerminate: false);
}

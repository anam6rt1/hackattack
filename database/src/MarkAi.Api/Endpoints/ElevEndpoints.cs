using MarkAi.Api.Infrastructura;
using MarkAi.Api.Servicii.Triviador;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using MarkAi.Data.Trivia;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Endpoints;

public record ProgresMaterie(int MaterieId, string Denumire, int Puncte, int CursuriCompletate, int TotalCursuri);
public record CursCompletat(int CursId, string Titlu, string? Capitol, int MaterieId, string Materie, DateTime CompletatLa, int ParcursId, string Parcurs);
public record UltimulCurs(int CursId, string Titlu, string? Capitol, int MaterieId, string Materie, bool Completat, DateTime Accesat, int ParcursId, string Parcurs);
public record ProgresGeneral(NivelCurent Nivel, IReadOnlyList<ProgresMaterie> Materii, UltimulCurs? UltimulCurs,
    IReadOnlyList<CursCompletat> CursuriCompletate);

public record CameraNoua(List<int>? MaterieIds, int Insule = 9, int MaxJucatori = 3, string? Clasa = null);

public record TemaDto(string Cod, string Nume, string? Anotimp, string? PerioadaZilei);
// Mod/Tema = fundalul (anotimp × zi/noapte), Interfata = stilul desenelor (insule/realist/pixel), Avatar = personajul din profil.
// La PUT, câmpurile lipsă (null) rămân cum erau, deci clienții vechi care trimit doar { mod, tema } merg la fel.
public record Preferinte(string? Mod, string? Tema, string? Interfata = null, string? Avatar = null);

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
            // Only chapters the student may still see (for example, not those of an organisation they left).
            var vizibile = acces.Capitole(a).Where(c => c.Publicat && c.Parcurs.Publicat);
            var capitole = await vizibile.Select(c => new { c.MaterieId, Gata = c.Progrese.Any(p => p.UtilizatorId == uid && p.QuizPromovat) }).ToListAsync(ct);
            var materii = await db.Materii.AsNoTracking().OrderBy(m => m.Ordine).Select(m => new { m.Id, m.Denumire }).ToListAsync(ct);
            var progrese = db.Set<ProgresCurs>().AsNoTracking().Where(p => p.UtilizatorId == uid && vizibile.Any(c => c.Id == p.CursId));
            var ultimul = await progrese.OrderByDescending(p => p.UltimaAccesare)
                .Select(p => new UltimulCurs(p.CursId, p.Curs.Titlu, p.Curs.Capitol, p.Curs.MaterieId, p.Curs.Materie.Denumire, p.QuizPromovat, p.UltimaAccesare,
                    p.Curs.ParcursId, p.Curs.Parcurs.Titlu))
                .FirstOrDefaultAsync(ct);
            var completate = await progrese.Where(p => p.CompletatLa != null).OrderByDescending(p => p.CompletatLa)
                .Select(p => new CursCompletat(p.CursId, p.Curs.Titlu, p.Curs.Capitol, p.Curs.MaterieId, p.Curs.Materie.Denumire, p.CompletatLa!.Value,
                    p.Curs.ParcursId, p.Curs.Parcurs.Titlu))
                .ToListAsync(ct);
            return new ProgresGeneral(await progres.NivelAsync(uid, ct),
                materii.Select(m => new ProgresMaterie(m.Id, m.Denumire, puncte.GetValueOrDefault(m.Id),
                    capitole.Count(c => c.MaterieId == m.Id && c.Gata), capitole.Count(c => c.MaterieId == m.Id))).ToList(),
                ultimul, completate);
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
            p.ActualizatLa = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return await CitestePreferinte(db, uid, ct);
        });
    }

    static async Task<Preferinte> CitestePreferinte(MarkAiDbContext db, int uid, CancellationToken ct) =>
        await db.PreferinteUtilizatori.AsNoTracking().Where(x => x.UtilizatorId == uid)
            .Select(x => new Preferinte(x.ModBackground, x.ModBackground == "static" && x.BackgroundStatic != null ? x.BackgroundStatic.Cod : null,
                x.Interfata == null ? null : x.Interfata.Cod, x.Avatar))
            .SingleOrDefaultAsync(ct)
        ?? new Preferinte("auto", null);
}

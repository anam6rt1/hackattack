using MarkAi.Api.Infrastructura;
using MarkAi.Api.Servicii;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Endpoints;

public record PretDto(decimal Suma, string Moneda, int DurataMinute);
public record MaterieScurta(int Id, string Denumire);
/// <param name="LinkIntalnire">Only filled in for the session's professor and students with a confirmed booking.</param>
public record SlotDto(int Id, string Titlu, string? Descriere, int MaterieId, string Materie, DateTime? InceputLa, DateTime? FinalizatLa,
    int Capacitate, int LocuriLibere, bool ProBono, string Status, bool RezervatDeMine, string? LinkIntalnire);
public record ProfesorDto(int Id, string Nume, string? Descriere, string? PozaUrl, IReadOnlyList<MaterieScurta> Materii, bool OferaProBono,
    IReadOnlyList<SlotDto> Sloturi);
public record RezervareDto(int Id, string Status, decimal TarifAchitat, string Moneda, DateTime RezervataLa, SlotDto Slot, int ProfesorId, string Profesor);
public record SlotNou(int MaterieId, string Titlu, string? Descriere, DateTime InceputLa, int Capacitate, bool ProBono, string? LinkIntalnire);
public record LinkSlot(string? LinkIntalnire);
public record ElevRezervare(int RezervareId, string Nume, string Status);
public record SlotProfesor(SlotDto Slot, IReadOnlyList<ElevRezervare> Elevi);
public record CastiguriDto(decimal Total, decimal InAsteptare, string Moneda);
public record PanouProfesor(ProfesorDto Profil, decimal OnorariuNegociat, string Moneda, PretDto? Pret, IReadOnlyList<SlotProfesor> Sloturi, CastiguriDto Castiguri);

public record CerereProfesor(int Id, string Nume, string Username, string Email, string? Telefon, string? Descriere, string? PozaUrl,
    bool AreDovada, string Stare, string? MotivRespingere, decimal OnorariuNegociat, IReadOnlyList<MaterieScurta> Materii, DateTime CreatLa);
public record Aprobare(decimal OnorariuNegociat);
public record Respingere(string Motiv);

public static class ProfesoriEndpoints
{
    public static void Mapeaza(WebApplication app)
    {
        var g = app.MapGroup("").WithTags("Profesori");

        g.MapGet("/tarif", async (MarkAiDbContext db, CancellationToken ct) => await Pret(db, ct));

        g.MapGet("/profesori", async (int? materieId, bool? gratuit, int? zile, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var q = db.ProfiluriMentor.AsNoTracking().Where(m => m.StareVerificare == RezervareService.MentorAprobat);
            if (materieId is not null) q = q.Where(m => m.Materii.Any(x => x.MaterieId == materieId));
            var profesori = await Profesori(db, q, Utilizator(h), ct);
            if (gratuit == true) profesori = profesori.Where(p => p.OferaProBono).ToList();
            if (zile is > 0)
            {
                var pana = DateTime.UtcNow.AddDays(zile.Value);
                profesori = profesori.Where(p => p.Sloturi.Any(s => s.LocuriLibere > 0 && s.InceputLa <= pana)).ToList();
            }
            return Lista<ProfesorDto>.Din(profesori);
        });

        g.MapGet("/profesori/{id:int}", async (int id, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
            (await Profesori(db, db.ProfiluriMentor.AsNoTracking().Where(m => m.UtilizatorId == id && m.StareVerificare == RezervareService.MentorAprobat),
                Utilizator(h), ct)).SingleOrDefault() ?? throw new KeyNotFoundException("Profesorul nu există."));

        g.MapGet("/profesori/{id:int}/poza", async (int id, MarkAiDbContext db, Stocare stocare, CancellationToken ct) =>
            stocare.Serveste(await db.ProfiluriMentor.Where(m => m.UtilizatorId == id).Select(m => m.PozaUrl).SingleOrDefaultAsync(ct)));

        var elev = app.MapGroup("/rezervari").WithTags("Profesori").RequireAuthorization(Politici.Elev);
        elev.MapPost("", async (RezervareNoua c, HttpContext h, MarkAiDbContext db, RezervareService rezervari, CancellationToken ct) =>
        {
            var r = await rezervari.RezervaAsync(h.User.Id(), c.SlotId, ct);
            return Results.Created($"/rezervari/{r.Id}", (await Rezervari(db, db.Rezervari.Where(x => x.Id == r.Id), h.User.Id(), ct)).Single());
        });
        elev.MapGet("", async (HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
            Lista<RezervareDto>.Din(await Rezervari(db, db.Rezervari.Where(x => x.ElevId == h.User.Id()), h.User.Id(), ct)));
        elev.MapDelete("/{id:int}", async (int id, HttpContext h, RezervareService rezervari, CancellationToken ct) =>
        {
            await rezervari.AnuleazaAsync(h.User.Id(), id, ct);
            return Results.NoContent();
        });

        var prof = app.MapGroup("/profesori/me").WithTags("Profesori").RequireAuthorization(Politici.ProfesorAprobat);
        prof.MapGet("", async (HttpContext h, MarkAiDbContext db, CancellationToken ct) => await Panou(db, h.User.Id(), ct));
        prof.MapPost("/sloturi", async (SlotNou c, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var uid = h.User.Id();
            if (!await db.Set<MentorMaterie>().AnyAsync(m => m.MentorId == uid && m.MaterieId == c.MaterieId, ct))
                throw new CerereInvalida("Poți crea ședințe doar la materiile pe care le predai.");
            if (c.InceputLa <= DateTime.UtcNow.AddMinutes(30)) throw new CerereInvalida("Ședința trebuie programată cu cel puțin 30 de minute înainte.");
            if (c.Capacitate is < 1 or > 30) throw new CerereInvalida("Numărul de locuri trebuie să fie între 1 și 30.");
            var pret = await db.TarifeFixe.Where(t => t.Activ).OrderBy(t => t.Id).FirstOrDefaultAsync(ct);
            if (!c.ProBono && pret is null) throw new InvalidOperationException("Platforma nu are încă un tarif configurat.");
            var inceput = DateTime.SpecifyKind(c.InceputLa, DateTimeKind.Utc);
            var slot = new CameraTutoring
            {
                MentorId = uid, MaterieId = c.MaterieId, Titlu = Validare.Obligatoriu(c.Titlu, "Titlu", 200), Descriere = Validare.Optional(c.Descriere, 1000),
                Capacitate = c.Capacitate, ProBono = c.ProBono, TarifId = c.ProBono ? null : pret!.Id, Status = RezervareService.SlotDeschis,
                InceputLa = inceput, FinalizatLa = inceput.AddMinutes(pret?.DurataMinute ?? 60), LinkIntalnire = LinkValid(c.LinkIntalnire),
            };
            db.CamereTutoring.Add(slot);
            await db.SaveChangesAsync(ct);
            return Results.Created("/profesori/me", await Panou(db, uid, ct));
        });
        // The link can be added or changed later, e.g. once the professor creates the meeting.
        prof.MapPut("/sloturi/{id:int}/link", async (int id, LinkSlot c, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var uid = h.User.Id();
            var slot = await db.CamereTutoring.SingleOrDefaultAsync(s => s.Id == id && s.MentorId == uid, ct) ?? throw new KeyNotFoundException("Ședința nu există.");
            slot.LinkIntalnire = LinkValid(c.LinkIntalnire);
            await db.SaveChangesAsync(ct);
            return await Panou(db, uid, ct);
        });
        prof.MapDelete("/sloturi/{id:int}", async (int id, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var uid = h.User.Id();
            var slot = await db.CamereTutoring.Include(s => s.Rezervari).ThenInclude(r => r.ComisionMentor)
                .SingleOrDefaultAsync(s => s.Id == id && s.MentorId == uid, ct) ?? throw new KeyNotFoundException("Ședința nu există.");
            slot.Status = "anulata";
            foreach (var r in slot.Rezervari.Where(r => r.Status == RezervareService.RezervareConfirmata))
            {
                r.Status = RezervareService.RezervareAnulata;
                if (r.ComisionMentor is not null) r.ComisionMentor.Status = "anulat";
            }
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        var admin = app.MapGroup("/admin").WithTags("Administrare").RequireAuthorization(Politici.Admin);
        admin.MapGet("/profesori", async (string? stare, MarkAiDbContext db, CancellationToken ct) =>
        {
            var q = db.ProfiluriMentor.AsNoTracking();
            if (!string.IsNullOrEmpty(stare)) q = q.Where(m => m.StareVerificare == stare);
            var cereri = await q.OrderBy(m => m.Utilizator.CreatLa)
                .Select(m => new CerereProfesor(m.UtilizatorId, m.Utilizator.Nume, m.Utilizator.Username, m.Utilizator.Email, m.Utilizator.Telefon,
                    m.Descriere, m.PozaUrl == null ? null : $"/profesori/{m.UtilizatorId}/poza", m.DovadaCalificareUrl != null, m.StareVerificare,
                    m.MotivRespingere, m.OnorariuNegociat, m.Materii.Select(x => new MaterieScurta(x.MaterieId, x.Materie.Denumire)).ToList(), m.Utilizator.CreatLa))
                .ToListAsync(ct);
            return Lista<CerereProfesor>.Din(cereri);
        });
        admin.MapGet("/profesori/{id:int}/dovada", async (int id, MarkAiDbContext db, Stocare stocare, CancellationToken ct) =>
            stocare.Serveste(await db.ProfiluriMentor.Where(m => m.UtilizatorId == id).Select(m => m.DovadaCalificareUrl).SingleOrDefaultAsync(ct)));
        admin.MapPost("/profesori/{id:int}/aprobare", async (int id, Aprobare c, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var m = await Profil(db, id, ct);
            m.OnorariuNegociat = await OnorariuValid(db, c.OnorariuNegociat, ct);
            m.StareVerificare = RezervareService.MentorAprobat;
            m.MotivRespingere = null;
            m.VerificatDeId = h.User.Id();
            m.ActivatLa ??= DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
        admin.MapPost("/profesori/{id:int}/respingere", async (int id, Respingere c, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var m = await Profil(db, id, ct);
            m.StareVerificare = AutentificareEndpoints.StareRespins;
            m.MotivRespingere = Validare.Obligatoriu(c.Motiv, "Motiv", 1000);
            m.VerificatDeId = h.User.Id();
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
        admin.MapPut("/profesori/{id:int}/onorariu", async (int id, Aprobare c, MarkAiDbContext db, CancellationToken ct) =>
        {
            var m = await Profil(db, id, ct);
            m.OnorariuNegociat = await OnorariuValid(db, c.OnorariuNegociat, ct);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    public record RezervareNoua(int SlotId);

    private static string? LinkValid(string? link)
    {
        var l = Validare.Optional(link, 500);
        if (l is not null && !(Uri.TryCreate(l, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps))
            throw new CerereInvalida("Linkul ședinței trebuie să fie o adresă https (de ex. Google Meet, Zoom sau Teams).");
        return l;
    }

    private static int? Utilizator(HttpContext h) => h.User.Identity?.IsAuthenticated == true ? h.User.Id() : null;

    private static async Task<ProfilMentor> Profil(MarkAiDbContext db, int id, CancellationToken ct) =>
        await db.ProfiluriMentor.SingleOrDefaultAsync(m => m.UtilizatorId == id, ct) ?? throw new KeyNotFoundException("Profesorul nu există.");

    // The mentor's share comes out of the student's fixed price, so it cannot exceed it.
    private static async Task<decimal> OnorariuValid(MarkAiDbContext db, decimal onorariu, CancellationToken ct)
    {
        var pret = await Pret(db, ct);
        if (onorariu < 0 || (pret is not null && onorariu > pret.Suma))
            throw new CerereInvalida($"Onorariul trebuie să fie între 0 și tariful elevului ({pret?.Suma} {pret?.Moneda}).");
        return Math.Round(onorariu, 2);
    }

    private static Task<PretDto?> Pret(MarkAiDbContext db, CancellationToken ct) =>
        db.TarifeFixe.AsNoTracking().Where(t => t.Activ).OrderBy(t => t.Id).Select(t => new PretDto(t.Suma, t.Moneda, t.DurataMinute)).FirstOrDefaultAsync(ct);

    private sealed record SlotCuMentor(int MentorId, SlotDto Slot);

    private static IQueryable<SlotCuMentor> Sloturi(IQueryable<CameraTutoring> q, int? uid) => q.Select(s => new SlotCuMentor(s.MentorId, new SlotDto(
        s.Id, s.Titlu, s.Descriere, s.MaterieId, s.Materie.Denumire, s.InceputLa, s.FinalizatLa, s.Capacitate,
        s.Capacitate - s.Rezervari.Count(r => r.Status == RezervareService.RezervareConfirmata), s.ProBono, s.Status,
        uid != null && s.Rezervari.Any(r => r.ElevId == uid && r.Status == RezervareService.RezervareConfirmata),
        uid != null && (s.MentorId == uid || s.Rezervari.Any(r => r.ElevId == uid && r.Status == RezervareService.RezervareConfirmata))
            ? s.LinkIntalnire : null)));

    private static async Task<List<ProfesorDto>> Profesori(MarkAiDbContext db, IQueryable<ProfilMentor> q, int? uid, CancellationToken ct)
    {
        var acum = DateTime.UtcNow;
        var profesori = await q.OrderBy(m => m.Utilizator.Nume)
            .Select(m => new { m.UtilizatorId, m.Utilizator.Nume, m.Descriere, ArePoza = m.PozaUrl != null,
                Materii = m.Materii.Select(x => new MaterieScurta(x.MaterieId, x.Materie.Denumire)).ToList() })
            .ToListAsync(ct);
        var ids = profesori.Select(p => p.UtilizatorId).ToList();
        var sloturi = (await Sloturi(db.CamereTutoring.AsNoTracking()
                .Where(s => ids.Contains(s.MentorId) && s.Status == RezervareService.SlotDeschis && s.InceputLa > acum)
                .OrderBy(s => s.InceputLa), uid)
            .ToListAsync(ct)).ToLookup(x => x.MentorId, x => x.Slot);
        return profesori.Select(p => new ProfesorDto(p.UtilizatorId, p.Nume, p.Descriere, p.ArePoza ? $"/profesori/{p.UtilizatorId}/poza" : null,
            p.Materii, sloturi[p.UtilizatorId].Any(s => s.ProBono), sloturi[p.UtilizatorId].ToList())).ToList();
    }

    private static async Task<List<RezervareDto>> Rezervari(MarkAiDbContext db, IQueryable<Rezervare> q, int uid, CancellationToken ct)
    {
        var rezervari = await q.AsNoTracking().OrderByDescending(r => r.RezervataLa)
            .Select(r => new { r.Id, r.Status, r.TarifAchitat, r.Moneda, r.RezervataLa, r.CameraId, r.Camera.MentorId, Profesor = r.Camera.Mentor.Utilizator.Nume })
            .ToListAsync(ct);
        var slotIds = rezervari.Select(r => r.CameraId).Distinct().ToList();
        var sloturi = await Sloturi(db.CamereTutoring.AsNoTracking().Where(s => slotIds.Contains(s.Id)), uid).ToDictionaryAsync(s => s.Slot.Id, s => s.Slot, ct);
        return rezervari.Select(r => new RezervareDto(r.Id, r.Status, r.TarifAchitat, r.Moneda, r.RezervataLa, sloturi[r.CameraId], r.MentorId, r.Profesor)).ToList();
    }

    private static async Task<PanouProfesor> Panou(MarkAiDbContext db, int uid, CancellationToken ct)
    {
        var profil = (await Profesori(db, db.ProfiluriMentor.AsNoTracking().Where(m => m.UtilizatorId == uid), uid, ct)).Single();
        var mentor = await db.ProfiluriMentor.AsNoTracking().SingleAsync(m => m.UtilizatorId == uid, ct);
        var sloturi = await Sloturi(db.CamereTutoring.AsNoTracking().Where(s => s.MentorId == uid).OrderByDescending(s => s.InceputLa), uid)
            .Select(s => s.Slot).ToListAsync(ct);
        var slotIds = sloturi.Select(s => s.Id).ToList();
        var elevi = (await db.Rezervari.AsNoTracking().Where(r => slotIds.Contains(r.CameraId))
            .Select(r => new { r.CameraId, e = new ElevRezervare(r.Id, r.Elev.Nume, r.Status) }).ToListAsync(ct)).ToLookup(x => x.CameraId, x => x.e);
        var comisioane = await db.ComisioaneMentor.AsNoTracking().Where(c => c.MentorId == uid && c.Status != "anulat")
            .Select(c => new { c.SumaComision, c.Status }).ToListAsync(ct);
        return new PanouProfesor(profil, mentor.OnorariuNegociat, mentor.Moneda, await Pret(db, ct),
            sloturi.Select(s => new SlotProfesor(s, elevi[s.Id].ToList())).ToList(),
            new CastiguriDto(comisioane.Sum(c => c.SumaComision), comisioane.Where(c => c.Status == "in_asteptare").Sum(c => c.SumaComision), mentor.Moneda));
    }
}

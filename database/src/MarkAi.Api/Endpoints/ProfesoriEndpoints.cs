using MarkAi.Api.Infrastructura;
using MarkAi.Api.Servicii;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Endpoints;

public record PretDto(decimal Suma, string Moneda, int DurataMinute);
public record MaterieScurta(int Id, string Denumire);
/// <summary>A place: the organisation (if any), the locality and the county.</summary>
public record LocDto(string? Organizatie, string? Localitate, string? Judet);
/// <param name="LinkIntalnire">Only filled in for the session's professor and students with a confirmed booking (never for in-person sessions).</param>
/// <param name="Tip">online, fata_in_fata or hibrid.</param>
/// <param name="NotaLoc">The note about the room; like the link, only for the professor and booked students.</param>
public record SlotDto(int Id, string Titlu, string? Descriere, int MaterieId, string Materie, DateTime? InceputLa, DateTime? FinalizatLa,
    int Capacitate, int LocuriLibere, bool ProBono, string Status, bool RezervatDeMine, string? LinkIntalnire, string Tip, LocDto? Loc, string? NotaLoc);
/// <param name="Loc">Where the professor teaches (their own place or the organisation they chose); null when not set.</param>
/// <param name="InLocalitateaTa">The professor's place is in the signed-in user's locality.</param>
public record ProfesorDto(int Id, string Nume, string? Descriere, string? PozaUrl, IReadOnlyList<MaterieScurta> Materii, bool OferaProBono,
    IReadOnlyList<SlotDto> Sloturi, LocDto? Loc, bool InLocalitateaTa);
/// <param name="Participare">online or la_sala (in the room).</param>
public record RezervareDto(int Id, string Status, decimal TarifAchitat, string Moneda, DateTime RezervataLa, SlotDto Slot, int ProfesorId, string Profesor,
    string Participare);
/// <param name="Tip">online (default), fata_in_fata or hibrid.</param>
/// <param name="LocOrganizatieId">In-person/hybrid: one of the professor's organisations, or null for their own place.</param>
public record SlotNou(int MaterieId, string Titlu, string? Descriere, DateTime InceputLa, int Capacitate, bool ProBono, string? LinkIntalnire,
    string? Tip = null, int? LocOrganizatieId = null, string? NotaLoc = null);
public record LinkSlot(string? LinkIntalnire);
public record NotaSlot(string? NotaLoc);
public record ElevRezervare(int RezervareId, string Nume, string Status, string Participare);
/// <param name="OrganizatieId">null = the professor's own place (county and locality from Settings).</param>
public record LocPredare(int? OrganizatieId);
public record OptiuneLoc(int? OrganizatieId, string Eticheta, LocDto? Loc);
public record LocPredareDto(int? OrganizatieId, LocDto? Loc, IReadOnlyList<OptiuneLoc> Optiuni);
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

        // filtru: toate | organizatia-mea (teaches in one of the user's organisations) | local (teaches in the user's county; their
        // own locality first). tip: online | fata_in_fata (hybrid sessions count for both): only those sessions are listed, and
        // professors without one are left out.
        g.MapGet("/profesori", async (int? materieId, bool? gratuit, int? zile, string? filtru, string? tip, HttpContext h, MarkAiDbContext db,
            AccesContinut acces, CancellationToken ct) =>
        {
            if (tip is not (null or "" or CameraTutoring.Online or CameraTutoring.FataInFata))
                throw new CerereInvalida("Tipul ședinței trebuie să fie „online” sau „fata_in_fata”.");
            var a = await acces.PentruAsync(h.User, ct);
            var loc = await LocUtilizator.PentruAsync(db, a.UtilizatorId, ct);
            var q = db.ProfiluriMentor.AsNoTracking().Where(m => m.StareVerificare == RezervareService.MentorAprobat);
            if (materieId is not null) q = q.Where(m => m.Materii.Any(x => x.MaterieId == materieId));
            var profesori = await ProfesoriCuLoc(db, q, Utilizator(h), loc, ct);
            profesori = filtru switch
            {
                null or "" or "toate" => profesori,
                "organizatia-mea" => profesori.Where(p => p.Organizatii.Any(a.Organizatii.Contains)).ToList(),
                "local" => profesori.Where(p => p.JudetId == loc.JudetObligatoriu).OrderByDescending(p => p.Dto.InLocalitateaTa).ToList(),
                _ => throw new CerereInvalida("Filtrul trebuie să fie „toate”, „organizatia-mea” sau „local”."),
            };
            var lista = profesori.Select(p => p.Dto).ToList();
            if (!string.IsNullOrEmpty(tip))
                lista = lista.Select(p => p with { Sloturi = p.Sloturi.Where(s => s.Tip == tip || s.Tip == CameraTutoring.Hibrid).ToList() })
                    .Where(p => p.Sloturi.Count > 0).ToList();
            if (gratuit == true) lista = lista.Where(p => p.OferaProBono).ToList();
            if (zile is > 0)
            {
                var pana = DateTime.UtcNow.AddDays(zile.Value);
                lista = lista.Where(p => p.Sloturi.Any(s => s.LocuriLibere > 0 && s.InceputLa <= pana)).ToList();
            }
            return Lista<ProfesorDto>.Din(lista);
        });

        g.MapGet("/profesori/{id:int}", async (int id, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
            (await Profesori(db, db.ProfiluriMentor.AsNoTracking().Where(m => m.UtilizatorId == id && m.StareVerificare == RezervareService.MentorAprobat),
                Utilizator(h), ct)).SingleOrDefault() ?? throw new KeyNotFoundException("Profesorul nu există."));

        // Public only for approved professors; applicants see their own photo and admins review it.
        g.MapGet("/profesori/{id:int}/poza", async (int id, HttpContext h, MarkAiDbContext db, Stocare stocare, CancellationToken ct) =>
        {
            var poateVedea = h.User.Identity?.IsAuthenticated == true && (h.User.IsInRole(Roluri.Admin) || h.User.Id() == id);
            return stocare.Serveste(await db.ProfiluriMentor
                .Where(m => m.UtilizatorId == id && (poateVedea || m.StareVerificare == RezervareService.MentorAprobat))
                .Select(m => m.PozaUrl).SingleOrDefaultAsync(ct));
        });

        var elev = app.MapGroup("/rezervari").WithTags("Profesori").RequireAuthorization(Politici.Elev);
        elev.MapPost("", async (RezervareNoua c, HttpContext h, MarkAiDbContext db, RezervareService rezervari, CancellationToken ct) =>
        {
            var r = await rezervari.RezervaAsync(h.User.Id(), c.SlotId, c.Participare, ct);
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
        // Where the professor teaches, for students' "near me": their own place or one of their organisations.
        prof.MapGet("/loc", async (HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) => await LocPredareAsync(db, acces, h.User.Id(), ct));
        prof.MapPut("/loc", async (LocPredare c, HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) =>
        {
            var uid = h.User.Id();
            if (c.OrganizatieId is { } org && (await acces.OrganizatiiAutorAsync(uid, ct)).All(o => o.Id != org))
                throw new CerereInvalida("Poți alege doar o organizație aprobată din care faci parte.");
            var m = await db.ProfiluriMentor.SingleAsync(x => x.UtilizatorId == uid, ct);
            m.LocOrganizatieId = c.OrganizatieId;
            await db.SaveChangesAsync(ct);
            return await LocPredareAsync(db, acces, uid, ct);
        });

        prof.MapPost("/sloturi", async (SlotNou c, HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) =>
        {
            var uid = h.User.Id();
            if (!await db.Set<MentorMaterie>().AnyAsync(m => m.MentorId == uid && m.MaterieId == c.MaterieId, ct))
                throw new CerereInvalida("Poți crea ședințe doar la materiile pe care le predai.");
            if (c.InceputLa <= DateTime.UtcNow.AddMinutes(30)) throw new CerereInvalida("Ședința trebuie programată cu cel puțin 30 de minute înainte.");
            if (c.Capacitate is < 1 or > 30) throw new CerereInvalida("Numărul de locuri trebuie să fie între 1 și 30.");
            var pret = await db.TarifeFixe.Where(t => t.Activ).OrderBy(t => t.Id).FirstOrDefaultAsync(ct);
            if (!c.ProBono && pret is null) throw new Conflict("Platforma nu are încă un tarif configurat.");
            var inceput = DateTime.SpecifyKind(c.InceputLa, DateTimeKind.Utc);
            var tip = string.IsNullOrEmpty(c.Tip) ? CameraTutoring.Online
                : CameraTutoring.Tipuri.Contains(c.Tip) ? c.Tip : throw new CerereInvalida("Tipul ședinței trebuie să fie online, față în față sau hibrid.");
            var slot = new CameraTutoring
            {
                MentorId = uid, MaterieId = c.MaterieId, Titlu = Validare.Obligatoriu(c.Titlu, "Titlu", 200), Descriere = Validare.Optional(c.Descriere, 1000),
                Capacitate = c.Capacitate, ProBono = c.ProBono, TarifId = c.ProBono ? null : pret!.Id, Status = RezervareService.SlotDeschis,
                InceputLa = inceput, FinalizatLa = inceput.AddMinutes(pret?.DurataMinute ?? 60), Tip = tip,
                LinkIntalnire = tip == CameraTutoring.FataInFata ? FaraLink(c.LinkIntalnire) : LinkValid(c.LinkIntalnire),
            };
            if (tip != CameraTutoring.Online)
            {
                // The place is copied, so the session stays where it was announced even if the professor or organisation moves.
                if (c.LocOrganizatieId is { } org)
                {
                    if ((await acces.OrganizatiiAutorAsync(uid, ct)).All(o => o.Id != org))
                        throw new CerereInvalida("Poți ține ședințe doar la o organizație aprobată din care faci parte.");
                    var o = await db.Set<Organizatie>().AsNoTracking().SingleAsync(x => x.Id == org, ct);
                    (slot.LocOrganizatieId, slot.LocJudetId, slot.LocLocalitateId) = (o.Id, o.JudetId, o.LocalitateId);
                }
                else
                {
                    var eu = await db.Utilizatori.AsNoTracking().Where(u => u.Id == uid).Select(u => new { u.JudetId, u.LocalitateId }).SingleAsync(ct);
                    if (eu.JudetId is null)
                        throw new CerereInvalida("Alege-ți județul (și localitatea) în Setări sau alege o organizație ca loc al ședinței.");
                    (slot.LocJudetId, slot.LocLocalitateId) = (eu.JudetId, eu.LocalitateId);
                }
                slot.NotaLoc = Validare.Optional(c.NotaLoc, 200);
            }
            else if (!string.IsNullOrWhiteSpace(c.NotaLoc)) throw new CerereInvalida("O ședință online nu are loc; scrie detaliile în descriere.");
            db.CamereTutoring.Add(slot);
            await db.SaveChangesAsync(ct);
            return Results.Created("/profesori/me", await Panou(db, uid, ct));
        });
        // The link can be added or changed later, e.g. once the professor creates the meeting.
        prof.MapPut("/sloturi/{id:int}/link", async (int id, LinkSlot c, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var uid = h.User.Id();
            var slot = await db.CamereTutoring.SingleOrDefaultAsync(s => s.Id == id && s.MentorId == uid, ct) ?? throw new KeyNotFoundException("Ședința nu există.");
            slot.LinkIntalnire = slot.Tip == CameraTutoring.FataInFata ? FaraLink(c.LinkIntalnire) : LinkValid(c.LinkIntalnire);
            await db.SaveChangesAsync(ct);
            return await Panou(db, uid, ct);
        });
        prof.MapPut("/sloturi/{id:int}/nota", async (int id, NotaSlot c, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var uid = h.User.Id();
            var slot = await db.CamereTutoring.SingleOrDefaultAsync(s => s.Id == id && s.MentorId == uid, ct) ?? throw new KeyNotFoundException("Ședința nu există.");
            if (slot.Tip == CameraTutoring.Online) throw new CerereInvalida("O ședință online nu are loc; scrie detaliile în descriere.");
            slot.NotaLoc = Validare.Optional(c.NotaLoc, 200);
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
            stocare.Serveste(await db.ProfiluriMentor.Where(m => m.UtilizatorId == id).Select(m => m.DovadaCalificareUrl).SingleOrDefaultAsync(ct),
                $"dovada-calificare-{id}"));
        // Professors are approved by the admins of their organisations (OrganizatiiEndpoints); the platform admin sees the
        // list and sets the fee each professor receives for paid tutoring.
        admin.MapPut("/profesori/{id:int}/onorariu", async (int id, Aprobare c, MarkAiDbContext db, CancellationToken ct) =>
        {
            var m = await Profil(db, id, ct);
            m.OnorariuNegociat = await OnorariuValid(db, c.OnorariuNegociat, ct);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    /// <param name="Participare">Required for a hybrid session: online or la_sala.</param>
    public record RezervareNoua(int SlotId, string? Participare = null);

    private static string? FaraLink(string? link) => string.IsNullOrWhiteSpace(link) ? null
        : throw new CerereInvalida("O ședință față în față nu are link; alege „hibrid” dacă vrei ca unii elevi să participe online.");

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
            ? s.LinkIntalnire : null,
        s.Tip,
        s.LocJudetId == null ? null : new LocDto(s.LocOrganizatie == null ? null : s.LocOrganizatie.Nume,
            s.LocLocalitate == null ? null : s.LocLocalitate.Nume, s.LocJudet!.Nume),
        uid != null && (s.MentorId == uid || s.Rezervari.Any(r => r.ElevId == uid && r.Status == RezervareService.RezervareConfirmata))
            ? s.NotaLoc : null)));

    private static async Task<List<ProfesorDto>> Profesori(MarkAiDbContext db, IQueryable<ProfilMentor> q, int? uid, CancellationToken ct) =>
        (await ProfesoriCuLoc(db, q, uid, LocUtilizator.Necunoscut, ct)).Select(p => p.Dto).ToList();

    /// <summary>A professor with what the filters need: the organisations they teach in and their place's county.</summary>
    private sealed record ProfesorIntern(ProfesorDto Dto, int[] Organizatii, int? JudetId);

    private static async Task<List<ProfesorIntern>> ProfesoriCuLoc(MarkAiDbContext db, IQueryable<ProfilMentor> q, int? uid, LocUtilizator loc, CancellationToken ct)
    {
        var acum = DateTime.UtcNow;
        var profesori = (await q.OrderBy(m => m.Utilizator.Nume)
            .Select(m => new { m.UtilizatorId, m.Utilizator.Nume, m.Descriere, ArePoza = m.PozaUrl != null,
                Materii = m.Materii.Select(x => new MaterieScurta(x.MaterieId, x.Materie.Denumire)).ToList(),
                CaProfesor = db.Set<MembruOrganizatie>().Where(x => x.UtilizatorId == m.UtilizatorId && x.Rol == MembruOrganizatie.RolProfesor
                    && x.Stare == MembruOrganizatie.Aprobat && x.Organizatie.Stare == Organizatie.Aprobata).Select(x => x.OrganizatieId).ToList(),
                CaAdmin = db.Set<Organizatie>().Where(o => o.AdminId == m.UtilizatorId && o.Stare == Organizatie.Aprobata).Select(o => o.Id).ToList(),
                m.LocOrganizatieId, PropriuJudet = m.Utilizator.JudetId, PropriuLocalitate = m.Utilizator.LocalitateId })
            .ToListAsync(ct))
            .Select(p => new { p.UtilizatorId, p.Nume, p.Descriere, p.ArePoza, p.Materii, Organizatii = p.CaProfesor.Union(p.CaAdmin).ToList(),
                p.LocOrganizatieId, p.PropriuJudet, p.PropriuLocalitate })
            .ToList();
        // The chosen organisation counts only while the professor still teaches there; otherwise their own place.
        var orgIds = profesori.SelectMany(p => p.Organizatii).Distinct().ToList();
        var org = await db.Set<Organizatie>().AsNoTracking().Where(o => orgIds.Contains(o.Id))
            .Select(o => new { o.Id, o.Nume, o.JudetId, o.LocalitateId }).ToDictionaryAsync(o => o.Id, ct);
        var locuri = profesori.ToDictionary(p => p.UtilizatorId, p => p.LocOrganizatieId is { } o && p.Organizatii.Contains(o)
            ? (Organizatie: org[o].Nume, Judet: (int?)org[o].JudetId, Localitate: org[o].LocalitateId)
            : (Organizatie: (string?)null, Judet: p.PropriuJudet, Localitate: p.PropriuLocalitate));
        var judete = locuri.Values.Select(l => l.Judet).OfType<int>().Distinct().ToList();
        var localitati = locuri.Values.Select(l => l.Localitate).OfType<int>().Distinct().ToList();
        var numeJudete = await db.Set<Judet>().Where(j => judete.Contains(j.Id)).ToDictionaryAsync(j => j.Id, j => j.Nume, ct);
        var numeLocalitati = await db.Set<Localitate>().Where(l => localitati.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Nume, ct);
        var ids = profesori.Select(p => p.UtilizatorId).ToList();
        var sloturi = (await Sloturi(db.CamereTutoring.AsNoTracking()
                .Where(s => ids.Contains(s.MentorId) && s.Status == RezervareService.SlotDeschis && s.InceputLa > acum)
                .OrderBy(s => s.InceputLa), uid)
            .ToListAsync(ct)).ToLookup(x => x.MentorId, x => x.Slot);
        return profesori.Select(p =>
        {
            var l = locuri[p.UtilizatorId];
            var locDto = l.Judet is null ? null : new LocDto(l.Organizatie, l.Localitate is { } li ? numeLocalitati.GetValueOrDefault(li) : null, numeJudete.GetValueOrDefault(l.Judet.Value));
            return new ProfesorIntern(new ProfesorDto(p.UtilizatorId, p.Nume, p.Descriere, p.ArePoza ? $"/profesori/{p.UtilizatorId}/poza" : null,
                p.Materii, sloturi[p.UtilizatorId].Any(s => s.ProBono), sloturi[p.UtilizatorId].ToList(), locDto,
                loc.LocalitateId is not null && l.Localitate == loc.LocalitateId), p.Organizatii.ToArray(), l.Judet);
        }).ToList();
    }

    private static async Task<List<RezervareDto>> Rezervari(MarkAiDbContext db, IQueryable<Rezervare> q, int uid, CancellationToken ct)
    {
        var rezervari = await q.AsNoTracking().OrderByDescending(r => r.RezervataLa)
            .Select(r => new { r.Id, r.Status, r.TarifAchitat, r.Moneda, r.RezervataLa, r.CameraId, r.Camera.MentorId, Profesor = r.Camera.Mentor.Utilizator.Nume, r.Participare })
            .ToListAsync(ct);
        var slotIds = rezervari.Select(r => r.CameraId).Distinct().ToList();
        var sloturi = await Sloturi(db.CamereTutoring.AsNoTracking().Where(s => slotIds.Contains(s.Id)), uid).ToDictionaryAsync(s => s.Slot.Id, s => s.Slot, ct);
        return rezervari.Select(r => new RezervareDto(r.Id, r.Status, r.TarifAchitat, r.Moneda, r.RezervataLa, sloturi[r.CameraId], r.MentorId, r.Profesor, r.Participare)).ToList();
    }

    private static async Task<LocPredareDto> LocPredareAsync(MarkAiDbContext db, AccesContinut acces, int uid, CancellationToken ct)
    {
        var ale = await acces.OrganizatiiAutorAsync(uid, ct);
        var ids = ale.Select(o => o.Id).ToList();
        var org = await db.Set<Organizatie>().AsNoTracking().Where(o => ids.Contains(o.Id))
            .Select(o => new { o.Id, o.Nume, Judet = o.Judet.Nume, Localitate = o.Localitate == null ? null : o.Localitate.Nume }).ToDictionaryAsync(o => o.Id, ct);
        var eu = await db.Utilizatori.AsNoTracking().Where(u => u.Id == uid).Select(u => new
        {
            Judet = db.Set<Judet>().Where(j => j.Id == u.JudetId).Select(j => j.Nume).FirstOrDefault(),
            Localitate = db.Set<Localitate>().Where(l => l.Id == u.LocalitateId).Select(l => l.Nume).FirstOrDefault(),
        }).SingleAsync(ct);
        var propriu = eu.Judet is null ? null : new LocDto(null, eu.Localitate, eu.Judet);
        var optiuni = new List<OptiuneLoc> { new(null, propriu is null ? "Locul meu (alege județul în „Organizație și loc”)" : "Locul meu", propriu) };
        optiuni.AddRange(ale.Select(o => new OptiuneLoc(o.Id, o.Nume, new LocDto(o.Nume, org[o.Id].Localitate, org[o.Id].Judet))));
        var ales = await db.ProfiluriMentor.Where(m => m.UtilizatorId == uid).Select(m => m.LocOrganizatieId).SingleAsync(ct);
        if (ales is not null && !ids.Contains(ales.Value)) ales = null; // no longer one of theirs: their own place applies
        return new LocPredareDto(ales, optiuni.Single(o => o.OrganizatieId == ales).Loc, optiuni);
    }

    private static async Task<PanouProfesor> Panou(MarkAiDbContext db, int uid, CancellationToken ct)
    {
        var profil = (await Profesori(db, db.ProfiluriMentor.AsNoTracking().Where(m => m.UtilizatorId == uid), uid, ct)).Single();
        var mentor = await db.ProfiluriMentor.AsNoTracking().SingleAsync(m => m.UtilizatorId == uid, ct);
        var sloturi = await Sloturi(db.CamereTutoring.AsNoTracking().Where(s => s.MentorId == uid).OrderByDescending(s => s.InceputLa), uid)
            .Select(s => s.Slot).ToListAsync(ct);
        var slotIds = sloturi.Select(s => s.Id).ToList();
        var elevi = (await db.Rezervari.AsNoTracking().Where(r => slotIds.Contains(r.CameraId))
            .Select(r => new { r.CameraId, e = new ElevRezervare(r.Id, r.Elev.Nume, r.Status, r.Participare) }).ToListAsync(ct)).ToLookup(x => x.CameraId, x => x.e);
        var comisioane = await db.ComisioaneMentor.AsNoTracking().Where(c => c.MentorId == uid && c.Status != "anulat")
            .Select(c => new { c.SumaComision, c.Status }).ToListAsync(ct);
        return new PanouProfesor(profil, mentor.OnorariuNegociat, mentor.Moneda, await Pret(db, ct),
            sloturi.Select(s => new SlotProfesor(s, elevi[s.Id].ToList())).ToList(),
            new CastiguriDto(comisioane.Sum(c => c.SumaComision), comisioane.Where(c => c.Status == "in_asteptare").Sum(c => c.SumaComision), mentor.Moneda));
    }
}

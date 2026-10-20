using System.Net.Mail;
using MarkAi.Api.Infrastructura;
using MarkAi.Api.Servicii;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Endpoints;

public record InregistrareElev(string Nume, string Username, string Email, string Parola, string? Telefon);
public record Autentificare(string Identificator, string Parola);
public record ActualizareProfil(string? Nume);
public record UtilizatorCurent(int Id, string Nume, string Username, string Email, string? Telefon, string Rol,
    string? StareProfesor, string? MotivRespingere);

public static class AutentificareEndpoints
{
    public const string StareInAsteptare = "in_asteptare";
    public const string StareRespins = "respins";

    public static void Mapeaza(WebApplication app)
    {
        var g = app.MapGroup("").WithTags("Autentificare");

        g.MapPost("/autentificare/inregistrare", async (InregistrareElev c, MarkAiDbContext db, Tokenuri t, HttpResponse r, CancellationToken ct) =>
        {
            var u = await CreeazaUtilizator(db, c.Nume, c.Username, c.Email, c.Parola, Validare.Telefon(c.Telefon), Roluri.Elev, ct);
            await db.SaveChangesAsync(ct);
            t.SeteazaCookie(r, t.Emite(u, Roluri.Elev));
            return Results.Created("/utilizatori/me", await Curent(db, u.Id, ct));
        }).RequireRateLimiting(LimiteCereri.Inregistrare);

        // Multipart because the application includes a photo and a qualification document.
        g.MapPost("/autentificare/inregistrare-profesor", async ([FromForm] InregistrareProfesor c, MarkAiDbContext db, Stocare stocare,
            Tokenuri t, HttpResponse r, CancellationToken ct) =>
        {
            var telefon = Validare.Telefon(c.Telefon) ?? throw new CerereInvalida("Numărul de telefon este obligatoriu pentru profesori.");
            var materii = (c.MaterieIds ?? []).Distinct().ToList();
            if (materii.Count == 0) throw new CerereInvalida("Alege cel puțin o materie predată.");
            if (await db.Materii.CountAsync(m => materii.Contains(m.Id), ct) != materii.Count) throw new CerereInvalida("Materie inexistentă.");
            if (c.Poza is null) throw new CerereInvalida("Adaugă o fotografie.");
            if (c.Dovada is null) throw new CerereInvalida("Adaugă dovada calificării (diplomă sau certificat).");

            var u = await CreeazaUtilizator(db, c.Nume, c.Username, c.Email, c.Parola, telefon, Roluri.Profesor, ct);
            u.ProfilMentor = new ProfilMentor
            {
                StareVerificare = StareInAsteptare, Descriere = Validare.Obligatoriu(c.Descriere, "Despre tine", 2000),
                PozaUrl = await stocare.SalveazaPozaAsync(c.Poza, ct), DovadaCalificareUrl = await stocare.SalveazaDovadaAsync(c.Dovada, ct),
                Materii = materii.Select(id => new MentorMaterie { MaterieId = id }).ToList(),
            };
            try { await db.SaveChangesAsync(ct); }
            catch
            {
                stocare.Sterge(u.ProfilMentor.PozaUrl); stocare.Sterge(u.ProfilMentor.DovadaCalificareUrl);
                throw;
            }
            t.SeteazaCookie(r, t.Emite(u, Roluri.Profesor));
            return Results.Created("/utilizatori/me", await Curent(db, u.Id, ct));
        }).DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(Stocare.MaxCerereMb * 1024 * 1024))
            .RequireRateLimiting(LimiteCereri.Inregistrare);

        g.MapPost("/autentificare", async (Autentificare c, MarkAiDbContext db, Tokenuri t, HttpResponse r, CancellationToken ct) =>
        {
            var id = (c.Identificator ?? "").Trim().ToLower();
            var u = await db.Utilizatori.Include(x => x.Roluri).ThenInclude(x => x.Rol)
                .SingleOrDefaultAsync(x => x.Email.ToLower() == id || x.Username.ToLower() == id, ct);
            if (u is null || !Parole.Verifica(u, c.Parola ?? ""))
                return Results.Json(new { status = 401, mesaj = "Email/utilizator sau parolă greșită." }, statusCode: 401);
            t.SeteazaCookie(r, t.Emite(u, Roluri.Principal(u.Roluri.Select(x => x.Rol.Nume))));
            return Results.Ok(await Curent(db, u.Id, ct));
        }).RequireRateLimiting(LimiteCereri.Autentificare);

        g.MapPost("/autentificare/iesire", (Tokenuri t, HttpResponse r) => { t.StergeCookie(r); return Results.NoContent(); });

        // Ends every session of the account, on all devices (e.g. after a lost phone).
        g.MapPost("/autentificare/iesire-peste-tot", async (HttpContext h, MarkAiDbContext db, VersiuniSesiune versiuni, Tokenuri t, CancellationToken ct) =>
        {
            await versiuni.CresteAsync(db, h.User.Id(), ct);
            t.StergeCookie(h.Response);
            return Results.NoContent();
        }).RequireAuthorization();

        var me = app.MapGroup("/utilizatori/me").WithTags("Autentificare").RequireAuthorization();
        me.MapGet("", async (HttpContext h, MarkAiDbContext db, CancellationToken ct) => await Curent(db, h.User.Id(), ct));
        me.MapPatch("", async (ActualizareProfil c, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var u = await db.Utilizatori.SingleAsync(x => x.Id == h.User.Id(), ct);
            // The phone number changes only through the account settings, which check that it is not someone else's.
            if (c.Nume is not null) u.Nume = Validare.Obligatoriu(c.Nume, "Nume", 120);
            await db.SaveChangesAsync(ct);
            return await Curent(db, u.Id, ct);
        });
    }

    public sealed class InregistrareProfesor
    {
        public string Nume { get; set; } = "";
        public string Username { get; set; } = "";
        public string Email { get; set; } = "";
        public string Parola { get; set; } = "";
        public string? Telefon { get; set; }
        public string? Descriere { get; set; }
        public List<int>? MaterieIds { get; set; }
        public IFormFile? Poza { get; set; }
        public IFormFile? Dovada { get; set; }
    }

    private static async Task<Utilizator> CreeazaUtilizator(MarkAiDbContext db, string nume, string username, string email, string parola,
        string? telefon, string rol, CancellationToken ct)
    {
        nume = Validare.Obligatoriu(nume, "Nume", 120);
        username = Validare.Obligatoriu(username, "Utilizator", 40);
        email = Validare.Obligatoriu(email, "Email", 200);
        if (!username.All(ch => char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-'))
            throw new CerereInvalida("Numele de utilizator poate conține doar litere, cifre, „.”, „_” și „-”.");
        if (!MailAddress.TryCreate(email, out _)) throw new CerereInvalida("Adresa de email nu este validă.");
        if ((parola ?? "").Length < 8) throw new CerereInvalida("Parola trebuie să aibă cel puțin 8 caractere.");
        var e = email.ToLower();
        var un = username.ToLower();
        if (await db.Utilizatori.AnyAsync(x => x.Email.ToLower() == e, ct)) throw new Conflict("Există deja un cont cu acest email.");
        if (await db.Utilizatori.AnyAsync(x => x.Username.ToLower() == un, ct)) throw new Conflict("Numele de utilizator este deja folosit.");
        if (telefon is not null && await db.Utilizatori.AnyAsync(x => x.Telefon == telefon, ct))
            throw new Conflict("Numărul de telefon este deja asociat altui cont.");

        var u = new Utilizator { Nume = nume, Username = username, Email = email, Telefon = telefon };
        u.ParolaHash = Parole.Hash(u, parola!);
        u.Roluri.Add(new UtilizatorRol { Rol = await db.Roluri.SingleAsync(r => r.Nume == rol, ct) });
        db.Utilizatori.Add(u);
        return u;
    }

    public static async Task<UtilizatorCurent> Curent(MarkAiDbContext db, int id, CancellationToken ct)
    {
        var u = await db.Utilizatori.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.Id, x.Nume, x.Username, x.Email, x.Telefon, Roluri = x.Roluri.Select(r => r.Rol.Nume).ToList(),
                Stare = x.ProfilMentor == null ? null : x.ProfilMentor.StareVerificare,
                Motiv = x.ProfilMentor == null ? null : x.ProfilMentor.MotivRespingere })
            .SingleOrDefaultAsync(ct) ?? throw new UnauthorizedAccessException();
        var rol = Roluri.Principal(u.Roluri);
        return new UtilizatorCurent(u.Id, u.Nume, u.Username, u.Email, u.Telefon, rol,
            rol == Roluri.Profesor ? u.Stare : null, rol == Roluri.Profesor && u.Stare == StareRespins ? u.Motiv : null);
    }
}

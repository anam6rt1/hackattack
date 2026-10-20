using System.Net.Mail;
using MarkAi.Api.Endpoints;
using MarkAi.Api.Infrastructura;
using MarkAi.Api.Servicii;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Setari;

public record DateCont(string Nume, string Username, string? Telefon, string? Descriere, List<int>? MaterieIds);
public record CerereConfirmare(string Scop, string ParolaActuala, string ValoareNoua);
public record ConfirmareCod(string Id, string Cod);

public static class SetariContEndpoints
{
    public static IServiceCollection AdaugaSetariCont(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ConfirmariCont>();
        services.AddSingleton<IEmailConfirmare, EmailConfirmareSmtp>();
        return services;
    }

    public static void MapeazaSetariCont(this WebApplication app)
    {
        // Browser mutations carry the X-MarkAi header, checked for the whole API in Securitate.
        var g = app.MapGroup("/utilizatori/me/setari").RequireAuthorization().WithTags("Setările contului");
        g.MapGet("", async (HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var u = await AutentificareEndpoints.Curent(db, h.User.Id(), ct);
            var p = await db.ProfiluriMentor.AsNoTracking().Where(x => x.UtilizatorId == u.Id)
                .Select(x => new { x.Descriere, x.StareVerificare, ArePoza = x.PozaUrl != null,
                    AreDovada = x.DovadaCalificareUrl != null, MaterieIds = x.Materii.Select(m => m.MaterieId).ToList() }).SingleOrDefaultAsync(ct);
            return Results.Ok(new { utilizator = u, profesor = u.Rol == Roluri.Profesor ? p : null });
        });
        g.MapPatch("", async (DateCont c, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var u = await db.Utilizatori.Include(x => x.Roluri).ThenInclude(x => x.Rol)
                .Include(x => x.ProfilMentor).ThenInclude(p => p!.Materii).AsSplitQuery().SingleAsync(x => x.Id == h.User.Id(), ct);
            var rol = Roluri.Principal(u.Roluri.Select(x => x.Rol.Nume));
            var username = Validare.Obligatoriu(c.Username, "Utilizator", 40);
            if (!username.All(ch => char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-'))
                throw new CerereInvalida("Utilizatorul poate conține litere, cifre, punct, cratimă și underscore.");
            var phone = Validare.Telefon(c.Telefon);
            if (rol == Roluri.Profesor && phone is null) throw new CerereInvalida("Telefonul este obligatoriu pentru profesori.");
            if (await db.Utilizatori.AnyAsync(x => x.Id != u.Id && x.Username.ToLower() == username.ToLower(), ct))
                throw new CerereInvalida("Acest nume de utilizator este deja folosit.");
            if (phone is not null && await db.Utilizatori.AnyAsync(x => x.Id != u.Id && x.Telefon == phone, ct))
                throw new CerereInvalida("Acest telefon este deja asociat altui cont.");
            u.Nume = Validare.Obligatoriu(c.Nume, "Nume și prenume", 120);
            u.Username = username; u.Telefon = phone;
            if (rol == Roluri.Profesor)
            {
                var p = u.ProfilMentor ?? throw new CerereInvalida("Profilul de profesor nu există.");
                var subjects = (c.MaterieIds ?? []).Distinct().ToArray();
                if (subjects.Length == 0 || await db.Materii.CountAsync(m => subjects.Contains(m.Id), ct) != subjects.Length)
                    throw new CerereInvalida("Alege cel puțin o materie validă.");
                p.Descriere = Validare.Obligatoriu(c.Descriere, "Despre tine", 2000);
                if (!p.Materii.Select(m => m.MaterieId).Order().SequenceEqual(subjects.Order()))
                {
                    var removed = p.Materii.Where(x => !subjects.Contains(x.MaterieId)).ToArray();
                    db.RemoveRange(removed);
                    foreach (var id in subjects.Except(p.Materii.Select(m => m.MaterieId)))
                        p.Materii.Add(new MentorMaterie { MaterieId = id });
                    p.StareVerificare = AutentificareEndpoints.StareInAsteptare;
                    p.MotivRespingere = null; p.VerificatDeId = null;
                }
            }
            else if (c.Descriere is not null || c.MaterieIds is not null)
                throw new CerereInvalida("Câmpurile de profesor nu sunt disponibile acestui cont.");
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { mesaj = "Datele contului au fost salvate." });
        });
        // The only way to change the phone number used by "Sună-mă" (SMS verification is added in a later phase).
        g.MapPut("/telefon", async (DateTelefon c, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var u = await db.Utilizatori.Include(x => x.Roluri).ThenInclude(x => x.Rol).SingleAsync(x => x.Id == h.User.Id(), ct);
            var telefon = Validare.Telefon(c.Telefon);
            if (telefon is null && Roluri.Principal(u.Roluri.Select(x => x.Rol.Nume)) == Roluri.Profesor)
                throw new CerereInvalida("Telefonul este obligatoriu pentru profesori.");
            if (telefon is not null && await db.Utilizatori.AnyAsync(x => x.Id != u.Id && x.Telefon == telefon, ct))
                throw new CerereInvalida("Acest telefon este deja asociat altui cont.");
            u.Telefon = telefon;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { telefon, mesaj = telefon is null ? "Numărul a fost șters." : "Numărul a fost salvat." });
        });
        g.MapPost("/documente", async ([FromForm] DocumenteCont c, HttpContext h, MarkAiDbContext db, Stocare storage, CancellationToken ct) =>
        {
            var u = await AutentificareEndpoints.Curent(db, h.User.Id(), ct);
            if (u.Rol != Roluri.Profesor) return Results.Forbid();
            var p = await db.ProfiluriMentor.SingleAsync(x => x.UtilizatorId == u.Id, ct);
            if (c.Poza is null && c.Dovada is null) throw new CerereInvalida("Alege o fotografie sau o dovadă a calificării.");
            var (pozaVeche, dovadaVeche) = (p.PozaUrl, p.DovadaCalificareUrl);
            if (c.Poza is not null) p.PozaUrl = await storage.SalveazaPozaAsync(c.Poza, ct);
            if (c.Dovada is not null)
            {
                p.DovadaCalificareUrl = await storage.SalveazaDovadaAsync(c.Dovada, ct);
                p.StareVerificare = AutentificareEndpoints.StareInAsteptare;
                p.MotivRespingere = null; p.VerificatDeId = null;
            }
            await db.SaveChangesAsync(ct);
            // The replaced files are no longer referenced anywhere.
            if (c.Poza is not null) storage.Sterge(pozaVeche);
            if (c.Dovada is not null) storage.Sterge(dovadaVeche);
            return Results.Ok(new { mesaj = c.Dovada is null ? "Fotografia a fost actualizată." : "Documentul a fost trimis pentru verificare." });
        }).DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(Stocare.MaxCerereMb * 1024 * 1024));
        g.MapPost("/confirmari", async (CerereConfirmare c, HttpContext h, MarkAiDbContext db,
            ConfirmariCont codes, IEmailConfirmare mail, CancellationToken ct) =>
        {
            var u = await db.Utilizatori.SingleAsync(x => x.Id == h.User.Id(), ct);
            codes.Limiteaza(u.Id);
            if (!Parole.Verifica(u, c.ParolaActuala ?? "")) throw new CerereInvalida("Parola actuală nu este corectă.");
            string value;
            if (c.Scop == "parola")
            {
                if (c.ValoareNoua is null || c.ValoareNoua.Length is < 8 or > 128)
                    throw new CerereInvalida("Parola nouă trebuie să aibă între 8 și 128 de caractere.");
                if (Parole.Verifica(u, c.ValoareNoua)) throw new CerereInvalida("Alege o parolă diferită de cea actuală.");
                value = Parole.Hash(u, c.ValoareNoua);
            }
            else if (c.Scop == "email")
            {
                value = Validare.Obligatoriu(c.ValoareNoua, "Email", 200).ToLowerInvariant();
                if (!MailAddress.TryCreate(value, out var address) || address.Address != value) throw new CerereInvalida("Emailul nu este valid.");
                if (await db.Utilizatori.AnyAsync(x => x.Email.ToLower() == value, ct)) throw new CerereInvalida("Acest email este deja folosit.");
            }
            else throw new CerereInvalida("Tip de confirmare nevalid.");
            var challenge = codes.Creeaza(u.Id, new(c.Scop, value, u.ParolaHash, u.Email));
            try { await mail.Trimite(u.Email, challenge.Cod, c.Scop == "parola" ? "schimbarea parolei" : "schimbarea emailului", ct); }
            catch (EmailNeconfiguratException)
            {
                codes.Anuleaza(u.Id);
                return Results.Json(new { mesaj = "Emailul de confirmare nu poate fi trimis. Contactează administratorul pentru configurarea serviciului de email." }, statusCode: 503);
            }
            return Results.Ok(new { id = challenge.Id, expiraInSecunde = 300, mesaj = "Codul a fost trimis la adresa actuală de email." });
        }).RequireRateLimiting(LimiteCereri.Email);
        g.MapPost("/confirmari/finalizeaza", async (ConfirmareCod c, HttpContext h, MarkAiDbContext db, ConfirmariCont codes,
            VersiuniSesiune versiuni, Tokenuri tokenuri, CancellationToken ct) =>
        {
            var pending = codes.Consuma(h.User.Id(), c.Id ?? "", c.Cod ?? "");
            var q = db.Utilizatori.Where(x => x.Id == h.User.Id() && x.ParolaHash == pending.HashAnterior && x.Email == pending.EmailAnterior);
            var changed = pending.Scop == "parola"
                ? await q.ExecuteUpdateAsync(s => s.SetProperty(x => x.ParolaHash, pending.Valoare), ct)
                : await q.ExecuteUpdateAsync(s => s.SetProperty(x => x.Email, pending.Valoare), ct);
            if (changed != 1) throw new CerereInvalida("Datele de securitate s-au schimbat. Solicită un cod nou.");
            // Every other device is signed out; this one gets a fresh session.
            await versiuni.CresteAsync(db, h.User.Id(), ct);
            var u = await db.Utilizatori.Include(x => x.Roluri).ThenInclude(x => x.Rol).AsNoTracking().SingleAsync(x => x.Id == h.User.Id(), ct);
            tokenuri.SeteazaCookie(h.Response, tokenuri.Emite(u, Roluri.Principal(u.Roluri.Select(x => x.Rol.Nume))));
            return Results.Ok(new { mesaj = pending.Scop == "parola" ? "Parola a fost schimbată în siguranță." : "Emailul a fost actualizat." });
        });
    }

    public record DateTelefon(string? Telefon);

    public sealed class DocumenteCont { public IFormFile? Poza { get; set; } public IFormFile? Dovada { get; set; } }
}

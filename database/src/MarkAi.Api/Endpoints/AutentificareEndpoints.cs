using System.Net.Mail;
using MarkAi.Api.Infrastructura;
using MarkAi.Api.Servicii;
using MarkAi.Api.Servicii.Mesagerie;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Endpoints;

public record InregistrareElev(string Nume, string Username, string Email, string Parola,
    int? JudetId = null, int? LocalitateId = null, string? Clasa = null, int? OrganizatieId = null);
public record InregistrareOrganizatie(string Nume, string Username, string Email, string Parola, string Telefon,
    string NumeOrganizatie, string Adresa, int JudetId, int? LocalitateId);
public record OrganizatieSumar(int Id, string Nume, string Rol, string Stare);
public record OrganizatieAdministrata(int Id, string Nume, string Stare, string? MotivRespingere);
public record Autentificare(string Identificator, string Parola);
public record ActualizareProfil(string? Nume);
public record ConfirmareEmail(string Email, string Cod);
public record RetrimitereCod(string Email);
public record ParolaUitata(string Identificator, string? Canal);
public record ParolaNoua(string Identificator, string Cod, string Parola);
/// <summary>The same answer whether the email was new or already had an account, so registration does not reveal which emails exist.</summary>
public record InregistrarePrimita(string Email, string Mesaj);
public record UtilizatorCurent(int Id, string Nume, string Username, string Email, string? Telefon, bool TelefonVerificat, string Rol,
    string? StareProfesor, string? MotivRespingere, int? JudetId, int? LocalitateId, string? Clasa,
    IReadOnlyList<OrganizatieSumar> Organizatii, OrganizatieAdministrata? OrganizatieAdministrata);

public static class AutentificareEndpoints
{
    public const string StareInAsteptare = "in_asteptare";
    public const string StareRespins = "respins";

    public static void Mapeaza(WebApplication app)
    {
        var g = app.MapGroup("").WithTags("Autentificare");

        // Registering does not sign in: the account becomes usable once the emailed code is confirmed (/confirma-email).
        g.MapPost("/autentificare/inregistrare", async (InregistrareElev c, MarkAiDbContext db, Posta posta, CoduriVerificare coduri,
            ServiciuOrganizatii org, IConfiguration cfg, TimeProvider ceas, CancellationToken ct) =>
        {
            var clasa = Clasa(c.Clasa);
            var loc = c.JudetId is { } j ? await OrganizatiiEndpoints.ValideazaLocul(db, j, c.LocalitateId, ct) : ((int?)null, (int?)null);
            if (c.OrganizatieId is { } oid && !await db.Set<Organizatie>().AnyAsync(o => o.Id == oid && o.Stare == Organizatie.Aprobata, ct))
                throw new CerereInvalida("Organizația nu există sau nu este încă aprobată.");
            var u = await CreeazaUtilizator(db, posta, cfg, ceas, c.Nume, c.Username, c.Email, c.Parola, null, Roluri.Elev, ct);
            if (u is not null)
            {
                (u.JudetId, u.LocalitateId, u.Clasa) = (loc.Item1, loc.Item2, clasa);
                await db.SaveChangesAsync(ct);
                if (c.OrganizatieId is { } organizatieId) await org.CereAsync(u, organizatieId, MembruOrganizatie.RolElev, ct);
            }
            return await DupaInregistrare(db, posta, coduri, cfg, u, c.Email, ct);
        }).RequireRateLimiting(LimiteCereri.Inregistrare);

        // The admin of a new organisation: the account and the organisation wait for a platform admin's approval.
        g.MapPost("/autentificare/inregistrare-organizatie", async (InregistrareOrganizatie c, MarkAiDbContext db, Posta posta, CoduriVerificare coduri,
            IConfiguration cfg, TimeProvider ceas, CancellationToken ct) =>
        {
            var telefon = Validare.Telefon(c.Telefon) ?? throw new CerereInvalida("Numărul de telefon este obligatoriu.");
            var nume = Validare.Obligatoriu(c.NumeOrganizatie, "Numele organizației", 200);
            var adresa = Validare.Obligatoriu(c.Adresa, "Adresa", 300);
            var (judetId, localitateId) = await OrganizatiiEndpoints.ValideazaLocul(db, c.JudetId, c.LocalitateId, ct);
            var u = await CreeazaUtilizator(db, posta, cfg, ceas, c.Nume, c.Username, c.Email, c.Parola, telefon, Roluri.Organizatie, ct);
            if (u is not null)
            {
                u.Roluri.Add(new UtilizatorRol { Rol = await db.Roluri.SingleAsync(r => r.Nume == Roluri.Profesor, ct) });
                (u.JudetId, u.LocalitateId) = (judetId, localitateId);
                u.ProfilMentor = new ProfilMentor { StareVerificare = StareInAsteptare, Descriere = $"Administrator, {nume}." };
                await db.SaveChangesAsync(ct);
                db.Add(new Organizatie { Nume = nume, Adresa = adresa, JudetId = judetId, LocalitateId = localitateId, AdminId = u.Id, CreatLa = u.CreatLa });
                await db.SaveChangesAsync(ct);
            }
            return await DupaInregistrare(db, posta, coduri, cfg, u, c.Email, ct);
        }).RequireRateLimiting(LimiteCereri.Inregistrare);

        // Multipart because the application includes a photo and a qualification document.
        g.MapPost("/autentificare/inregistrare-profesor", async ([FromForm] InregistrareProfesor c, MarkAiDbContext db, Stocare stocare,
            Posta posta, CoduriVerificare coduri, ServiciuOrganizatii org, IConfiguration cfg, TimeProvider ceas, CancellationToken ct) =>
        {
            // Every professor belongs to at least one organisation, whose admin approves them.
            var organizatii = (c.OrganizatieIds ?? []).Distinct().ToList();
            if (organizatii.Count == 0) throw new CerereInvalida("Alege cel puțin o organizație în care predai.");
            if (await db.Set<Organizatie>().CountAsync(o => organizatii.Contains(o.Id) && o.Stare == Organizatie.Aprobata, ct) != organizatii.Count)
                throw new CerereInvalida("Una dintre organizații nu există sau nu este încă aprobată.");
            var (judetId, localitateId) = await OrganizatiiEndpoints.ValideazaLocul(db, c.JudetId ?? 0, c.LocalitateId, ct);
            var telefon = Validare.Telefon(c.Telefon) ?? throw new CerereInvalida("Numărul de telefon este obligatoriu pentru profesori.");
            var materii = (c.MaterieIds ?? []).Distinct().ToList();
            if (materii.Count == 0) throw new CerereInvalida("Alege cel puțin o materie predată.");
            if (await db.Materii.CountAsync(m => materii.Contains(m.Id), ct) != materii.Count) throw new CerereInvalida("Materie inexistentă.");
            if (c.Poza is null) throw new CerereInvalida("Adaugă o fotografie.");
            if (c.Dovada is null) throw new CerereInvalida("Adaugă dovada calificării (diplomă sau certificat).");

            var u = await CreeazaUtilizator(db, posta, cfg, ceas, c.Nume, c.Username, c.Email, c.Parola, telefon, Roluri.Profesor, ct);
            if (u is not null)
            {
                u.ProfilMentor = new ProfilMentor
                {
                    StareVerificare = StareInAsteptare, Descriere = Validare.Obligatoriu(c.Descriere, "Despre tine", 2000),
                    PozaUrl = await stocare.SalveazaPozaAsync(c.Poza, ct), DovadaCalificareUrl = await stocare.SalveazaDovadaAsync(c.Dovada, ct),
                    Materii = materii.Select(id => new MentorMaterie { MaterieId = id }).ToList(),
                };
                (u.JudetId, u.LocalitateId) = (judetId, localitateId);
                try { await db.SaveChangesAsync(ct); }
                catch
                {
                    stocare.Sterge(u.ProfilMentor.PozaUrl); stocare.Sterge(u.ProfilMentor.DovadaCalificareUrl);
                    throw;
                }
                foreach (var oid in organizatii) await org.CereAsync(u, oid, MembruOrganizatie.RolProfesor, ct);
            }
            return await DupaInregistrare(db, posta, coduri, cfg, u, c.Email, ct);
        }).DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(Stocare.MaxCerereMb * 1024 * 1024))
            .RequireRateLimiting(LimiteCereri.Inregistrare);

        // The emailed code confirms the account and signs in. A wrong code, an unknown email or a confirmed account all get the same answer.
        g.MapPost("/autentificare/confirma-email", async (ConfirmareEmail c, MarkAiDbContext db, CoduriVerificare coduri, Tokenuri t,
            ServiciuOrganizatii org, HttpResponse r, TimeProvider ceas, CancellationToken ct) =>
        {
            var email = (c.Email ?? "").Trim().ToLower();
            var u = await db.Utilizatori.Include(x => x.Roluri).ThenInclude(x => x.Rol)
                .SingleOrDefaultAsync(x => x.Email.ToLower() == email && x.EmailConfirmatLa == null, ct);
            if (u is null || !await coduri.VerificaAsync(u.Id, CoduriVerificare.ScopEmail, u.Email, c.Cod ?? "", ct))
                throw new CerereInvalida("Codul nu este corect sau a expirat. Verifică emailul cel mai recent sau cere un cod nou.");
            u.EmailConfirmatLa = ceas.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);
            await org.DupaConfirmareEmailAsync(u, ct);
            t.SeteazaCookie(r, t.Emite(u, Roluri.Principal(u.Roluri.Select(x => x.Rol.Nume))));
            return Results.Ok(await Curent(db, u.Id, ct));
        }).RequireRateLimiting(LimiteCereri.Autentificare);

        g.MapPost("/autentificare/retrimite-confirmare", async (RetrimitereCod c, MarkAiDbContext db, Posta posta, CoduriVerificare coduri,
            IConfiguration cfg, CancellationToken ct) =>
        {
            var email = (c.Email ?? "").Trim().ToLower();
            var u = await db.Utilizatori.SingleOrDefaultAsync(x => x.Email.ToLower() == email && x.EmailConfirmatLa == null, ct);
            if (u is not null) await TrimiteCodConfirmare(posta, coduri, cfg, u, ct);
            return Results.Ok(new { mesaj = "Dacă există un cont neconfirmat cu acest email, i-am trimis un cod nou." });
        }).RequireRateLimiting(LimiteCereri.Email);

        // Forgotten password: a code by email, or by SMS to the account's verified phone. The answer never says whether the account exists.
        g.MapPost("/autentificare/parola-uitata", async (ParolaUitata c, MarkAiDbContext db, CoduriVerificare coduri, Posta posta,
            TimeProvider ceas, ILogger<CoduriVerificare> log, CancellationToken ct) =>
        {
            var id = (c.Identificator ?? "").Trim().ToLower();
            var u = id.Length == 0 ? null : await db.Utilizatori.SingleOrDefaultAsync(x => x.Email.ToLower() == id || x.Username.ToLower() == id, ct);
            if (u is not null)
            {
                var cod = await coduri.CreeazaAsync(u.Id, CoduriVerificare.ScopParola, CoduriVerificare.DestinatieCont, ceas.GetUtcNow().UtcDateTime.AddMinutes(15), ct);
                try
                {
                    var prinSms = c.Canal == "sms" && u.TelefonVerificatLa is not null && u.Telefon is not null;
                    try { if (prinSms) await posta.TrimiteAcumAsync(Sabloane.CodParola(u.Telefon!, cod), "resetare_parola", ct); }
                    // Over the daily SMS cap: the code still arrives, by email.
                    catch (Conflict) { prinSms = false; }
                    if (!prinSms) await posta.TrimiteAcumAsync(Sabloane.ResetareParola(u.Email, u.Nume, cod), "resetare_parola", ct);
                }
                catch (TrimitereEsuata ex) { log.LogWarning(ex, "Password reset code could not be sent for account {Id}", u.Id); }
            }
            return Results.Ok(new { mesaj = "Dacă există un cont cu aceste date, i-am trimis un cod (prin SMS doar dacă ai ales SMS și contul are un telefon verificat; altfel pe email). Codul expiră în 15 minute." });
        }).RequireRateLimiting(LimiteCereri.Email);

        g.MapPost("/autentificare/parola-noua", async (ParolaNoua c, MarkAiDbContext db, CoduriVerificare coduri, Posta posta,
            VersiuniSesiune versiuni, TimeProvider ceas, CancellationToken ct) =>
        {
            if ((c.Parola ?? "").Length < 8) throw new CerereInvalida("Parola trebuie să aibă cel puțin 8 caractere.");
            var id = (c.Identificator ?? "").Trim().ToLower();
            var u = await db.Utilizatori.SingleOrDefaultAsync(x => x.Email.ToLower() == id || x.Username.ToLower() == id, ct);
            if (u is null || !await coduri.VerificaAsync(u.Id, CoduriVerificare.ScopParola, CoduriVerificare.DestinatieCont, c.Cod ?? "", ct))
                throw new CerereInvalida("Codul nu este corect sau a expirat. Cere un cod nou.");
            u.ParolaHash = Parole.Hash(u, c.Parola!);
            // The code reached the account's email or its verified phone, so the email is now proven too.
            u.EmailConfirmatLa ??= ceas.GetUtcNow().UtcDateTime;
            posta.Pune(Sabloane.ParolaSchimbata(u.Email, u.Nume), "parola_schimbata");
            await db.SaveChangesAsync(ct);
            await versiuni.CresteAsync(db, u.Id, ct);
            return Results.Ok(new { mesaj = "Parola a fost schimbată. Intră în cont cu parola nouă." });
        }).RequireRateLimiting(LimiteCereri.Autentificare);

        g.MapPost("/autentificare", async (Autentificare c, MarkAiDbContext db, Tokenuri t, HttpResponse r, CancellationToken ct) =>
        {
            var id = (c.Identificator ?? "").Trim().ToLower();
            var u = await db.Utilizatori.Include(x => x.Roluri).ThenInclude(x => x.Rol)
                .SingleOrDefaultAsync(x => x.Email.ToLower() == id || x.Username.ToLower() == id, ct);
            if (u is null || !Parole.Verifica(u, c.Parola ?? ""))
                return Results.Json(new { status = 401, mesaj = "Email/utilizator sau parolă greșită." }, statusCode: 401);
            // Only with the right password: tells the person to confirm, with the address the code went to.
            if (u.EmailConfirmatLa is null)
                return Results.Json(new { status = 403, cod = "email_neconfirmat", email = u.Email,
                    mesaj = "Contul nu este confirmat încă. Scrie codul primit pe email." }, statusCode: 403);
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
        public List<int>? OrganizatieIds { get; set; }
        public int? JudetId { get; set; }
        public int? LocalitateId { get; set; }
        public IFormFile? Poza { get; set; }
        public IFormFile? Dovada { get; set; }
    }

    /// <summary>
    /// Validates and adds a new, unconfirmed account. For an email that already has an account it adds nothing and returns
    /// null; the owner is told by email (at most once an hour) and the caller answers exactly as for a new account.
    /// A taken username or phone number is still reported, since those are chosen and shown publicly.
    /// </summary>
    private static async Task<Utilizator?> CreeazaUtilizator(MarkAiDbContext db, Posta posta, IConfiguration cfg, TimeProvider ceas,
        string nume, string username, string email, string parola, string? telefon, string rol, CancellationToken ct)
    {
        nume = Validare.Obligatoriu(nume, "Nume", 120);
        username = Validare.Obligatoriu(username, "Utilizator", 40);
        email = Validare.Obligatoriu(email, "Email", 200);
        if (!username.All(ch => char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-'))
            throw new CerereInvalida("Numele de utilizator poate conține doar litere, cifre, „.”, „_” și „-”.");
        if (!MailAddress.TryCreate(email, out _)) throw new CerereInvalida("Adresa de email nu este validă.");
        if ((parola ?? "").Length < 8) throw new CerereInvalida("Parola trebuie să aibă cel puțin 8 caractere.");
        var un = username.ToLower();
        if (await db.Utilizatori.AnyAsync(x => x.Username.ToLower() == un, ct)) throw new Conflict("Numele de utilizator este deja folosit.");
        if (telefon is not null && await db.Utilizatori.AnyAsync(x => x.Telefon == telefon, ct))
            throw new Conflict("Numărul de telefon este deja asociat altui cont.");

        var acum = ceas.GetUtcNow().UtcDateTime;
        var e = email.ToLower();
        var existent = await db.Utilizatori.Where(x => x.Email.ToLower() == e).Select(x => new { x.Email, x.Nume }).SingleOrDefaultAsync(ct);
        if (existent is not null)
        {
            var recent = acum.AddHours(-1);
            if (!await db.Set<MesajIesire>().AnyAsync(m => m.Catre == existent.Email && m.Tip == "incercare_inregistrare" && m.CreatLa >= recent, ct))
            {
                posta.Pune(Sabloane.IncercareInregistrare(existent.Email, existent.Nume), "incercare_inregistrare");
                await db.SaveChangesAsync(ct);
            }
            return null;
        }

        var u = new Utilizator { Nume = nume, Username = username, Email = email, Telefon = telefon, CreatLa = acum };
        u.ParolaHash = Parole.Hash(u, parola!);
        u.Roluri.Add(new UtilizatorRol { Rol = await db.Roluri.SingleAsync(r => r.Nume == rol, ct) });
        db.Utilizatori.Add(u);
        return u;
    }

    private static async Task<IResult> DupaInregistrare(MarkAiDbContext db, Posta posta, CoduriVerificare coduri, IConfiguration cfg,
        Utilizator? u, string email, CancellationToken ct)
    {
        var mesaj = $"Ți-am trimis un cod de confirmare pe {email.Trim()}. Scrie-l aici ca să intri în cont.";
        if (u is not null)
        {
            try { await TrimiteCodConfirmare(posta, coduri, cfg, u, ct); }
            catch (TrimitereEsuata)
            {
                mesaj = "Contul a fost creat, dar emailul cu codul nu a putut fi trimis acum. Apasă „Retrimite codul” peste câteva minute.";
            }
        }
        return Results.Json(new InregistrarePrimita(email.Trim(), mesaj), statusCode: StatusCodes.Status202Accepted);
    }

    /// <summary>A new code (the previous one stops working), valid until the unconfirmed account would be deleted.</summary>
    private static async Task TrimiteCodConfirmare(Posta posta, CoduriVerificare coduri, IConfiguration cfg, Utilizator u, CancellationToken ct)
    {
        var ore = cfg.GetValue("Conturi:OreConfirmare", 24);
        var cod = await coduri.CreeazaAsync(u.Id, CoduriVerificare.ScopEmail, u.Email, u.CreatLa.AddHours(ore), ct);
        await posta.TrimiteAcumAsync(Sabloane.ConfirmareCont(u.Email, u.Nume, cod, ore), "confirmare_email", ct);
    }

    public static async Task<UtilizatorCurent> Curent(MarkAiDbContext db, int id, CancellationToken ct)
    {
        var u = await db.Utilizatori.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.Id, x.Nume, x.Username, x.Email, x.Telefon, TelefonVerificat = x.TelefonVerificatLa != null, Roluri = x.Roluri.Select(r => r.Rol.Nume).ToList(),
                Stare = x.ProfilMentor == null ? null : x.ProfilMentor.StareVerificare,
                Motiv = x.ProfilMentor == null ? null : x.ProfilMentor.MotivRespingere, x.JudetId, x.LocalitateId, x.Clasa,
                Organizatii = x.Organizatii.OrderBy(m => m.Organizatie.Nume)
                    .Select(m => new OrganizatieSumar(m.OrganizatieId, m.Organizatie.Nume, m.Rol, m.Stare)).ToList() })
            .SingleOrDefaultAsync(ct) ?? throw new UnauthorizedAccessException();
        var administrata = await db.Set<Organizatie>().AsNoTracking().Where(o => o.AdminId == id)
            .Select(o => new OrganizatieAdministrata(o.Id, o.Nume, o.Stare, o.MotivRespingere)).SingleOrDefaultAsync(ct);
        var rol = Roluri.Principal(u.Roluri);
        var profesor = Roluri.EsteProfesor(rol);
        return new UtilizatorCurent(u.Id, u.Nume, u.Username, u.Email, u.Telefon, u.TelefonVerificat, rol,
            profesor ? u.Stare : null, profesor && u.Stare == StareRespins ? u.Motiv : null, u.JudetId, u.LocalitateId, u.Clasa,
            u.Organizatii, administrata);
    }

    /// <summary>A student's school year, or null.</summary>
    public static string? Clasa(string? valoare) => string.IsNullOrWhiteSpace(valoare) ? null
        : valoare.Trim().ToUpperInvariant() is "IX" or "X" or "XI" or "XII" ? valoare.Trim().ToUpperInvariant()
        : throw new CerereInvalida("Clasa trebuie să fie IX, X, XI sau XII.");
}

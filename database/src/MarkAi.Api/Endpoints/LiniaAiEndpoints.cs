using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using MarkAi.Api.Infrastructura;
using MarkAi.Api.Servicii;
using MarkAi.Data;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using MarkAi.Data.Servicii;

namespace MarkAi.Api.Endpoints;

public record LiniaAiDto(string Numar, string? TelefonulMeu, bool TelefonVerificat, SetariVoce Voce, IReadOnlyList<VoceTutor> Voci, bool PoateSuna);
public record ApelDto(int Id, DateTime InceputLa, DateTime? FinalizatLa, string Status, string? Curs, int NumarMesaje, string? Previzualizare);
public record MesajDto(int Id, string Expeditor, string Tip, string Continut, DateTime CreatLa);

public static class LiniaAiEndpoints
{
    public const string CanalTelefon = "telefon";

    public static void Mapeaza(WebApplication app)
    {
        var g = app.MapGroup("").WithTags("Linia AI").RequireAuthorization();

        g.MapGet("/linia-ai", async (HttpContext h, IConfiguration cfg, MarkAiDbContext db, TwilioApeluri apeluri, TutorTelefon tutor, CancellationToken ct) =>
        {
            var t = await db.Utilizatori.Where(u => u.Id == h.User.Id()).Select(u => new { u.Telefon, Verificat = u.TelefonVerificatLa != null }).SingleAsync(ct);
            return new LiniaAiDto(cfg["LiniaAi:Numar"] ?? "", t.Telefon, t.Verificat, await VociTutor.CitesteAsync(db, h.User.Id(), ct), VociTutor.Toate,
                apeluri.Configurat && tutor.Configurat);
        });

        // "Sună-mă": Twilio calls the student's saved number; the conversation then runs through the voice webhooks below.
        // The session exists before Twilio dials, with a random key for this call's webhooks (only its hash is stored).
        g.MapPost("/linia-ai/suna-ma", async (HttpContext h, IConfiguration cfg, MarkAiDbContext db, TwilioApeluri apeluri, TutorTelefon tutor, CancellationToken ct) =>
        {
            if (!tutor.Configurat) throw new Conflict("Tutorele nu este încă pornit pe server.");
            var uid = h.User.Id();
            // Only a number confirmed by SMS: otherwise anyone could type someone else's number and have it called.
            var telefon = await db.Utilizatori.Where(u => u.Id == uid && u.TelefonVerificatLa != null).Select(u => u.Telefon).SingleOrDefaultAsync(ct)
                ?? throw new CerereInvalida("Verifică mai întâi numărul tău de telefon cu codul primit prin SMS.");
            var acum = DateTime.UtcNow;
            var recent = acum.AddMinutes(-11);
            if (await db.Sesiuni.AnyAsync(s => s.UtilizatorId == uid && s.Canal == CanalTelefon && s.FinalizatLa == null && s.InceputLa > recent, ct))
                throw new Conflict("Ai deja un apel în curs.");
            if (await LimitaAtinsa(db, cfg, uid, acum, ct) is { } motiv) throw new Conflict(motiv);

            var (cheie, sesiune) = SesiuneNoua(cfg, uid, acum, StareInitiat);
            db.Sesiuni.Add(sesiune);
            await db.SaveChangesAsync(ct);
            try
            {
                var sid = await apeluri.SunaAsync(uid, telefon, sesiune.Id, cheie, ct);
                // The first webhook may already have recorded it; it is the same CallSid either way.
                await db.Sesiuni.Where(s => s.Id == sesiune.Id && s.TwilioCallSid == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.TwilioCallSid, sid), ct);
            }
            catch
            {
                await db.Sesiuni.Where(s => s.Id == sesiune.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, StareEsuat).SetProperty(x => x.FinalizatLa, DateTime.UtcNow), CancellationToken.None);
                throw;
            }
            return new { mesaj = $"Te sunăm acum pe {telefon}. Răspunde la telefon!" };
        }).RequireAuthorization(Politici.Elev).RequireRateLimiting(LimiteCereri.Telefon);

        // The same settings the student can change by voice during a call.
        g.MapPut("/utilizatori/me/voce-tutor", async (SetariVoce c, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            if (VociTutor.Toate.All(v => v.Cod != c.Voce)) throw new CerereInvalida("Vocea aleasă nu există.");
            await VociTutor.SalveazaAsync(db, h.User.Id(), c, ct);
            return await VociTutor.CitesteAsync(db, h.User.Id(), ct);
        });

        g.MapGet("/sesiuni", async (string? canal, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var uid = h.User.Id();
            var q = db.Sesiuni.AsNoTracking().Where(s => s.UtilizatorId == uid && s.Status != StareEsuat);
            if (!string.IsNullOrEmpty(canal)) q = q.Where(s => s.Canal == canal);
            var apeluri = await q.OrderByDescending(s => s.InceputLa)
                .Select(s => new ApelDto(s.Id, s.InceputLa, s.FinalizatLa, s.Status, s.Curs == null ? null : s.Curs.Titlu, s.Mesaje.Count,
                    s.Mesaje.OrderBy(m => m.CreatLa).Select(m => m.Continut).FirstOrDefault()))
                .ToListAsync(ct);
            return Lista<ApelDto>.Din(apeluri);
        });

        g.MapGet("/sesiuni/{id:int}/mesaje", async (int id, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var uid = h.User.Id();
            if (!await db.Sesiuni.AnyAsync(s => s.Id == id && s.UtilizatorId == uid, ct)) throw new KeyNotFoundException("Apelul nu există.");
            var mesaje = await db.Mesaje.AsNoTracking().Where(m => m.SesiuneId == id).OrderBy(m => m.CreatLa).ThenBy(m => m.Id)
                .Select(m => new MesajDto(m.Id, m.Expeditor, m.Tip, m.Continut, m.CreatLa)).ToListAsync(ct);
            return Lista<MesajDto>.Din(mesaje);
        });

        // Twilio voice webhooks. A call is a loop: the tutor speaks inside a <Gather> that listens for the
        // student's answer; Twilio posts the transcribed speech to /raspuns, which replies with the next turn.
        // Every request is tied to one session (?s=) and authenticated by Twilio's signature or by that session's key (?k=),
        // and must come from the session's own call (CallSid). A trial account fetches TwiML through a proxy whose
        // signature cannot match our URL, so the key is what authenticates those requests.
        var twilio = app.MapGroup("/twilio/voce").WithTags("Linia AI").DisableAntiforgery();
        var urlPublic = (app.Configuration["Twilio:UrlPublic"] ?? "").TrimEnd('/');
        if (urlPublic.StartsWith("https://")) UrlRaspuns = urlPublic + "/twilio/voce/raspuns";

        // GET as well as POST: a trial account fetches TwiML through Twilio's proxy, which may replay the request differently.
        twilio.MapMethods("", ["GET", "POST"], async (HttpRequest req, IConfiguration cfg, MarkAiDbContext db, TutorTelefon tutor, CancellationToken ct) =>
        {
            var form = await ParametriAsync(req, ct);
            Sesiune? sesiune;
            string cheie;
            if (req.Query.ContainsKey("s"))
            {
                // "Sună-mă": the session was created before dialling.
                sesiune = await SesiuneAutentica(req, form, db, cfg, ct);
                if (sesiune is null) return Results.StatusCode(403);
                cheie = req.Query["k"].ToString();
                if (sesiune.Status == StareInitiat)
                    await db.Sesiuni.Where(s => s.Id == sesiune.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, StareInCurs), ct);
            }
            else
            {
                // The student calls the number: only Twilio's own signature is accepted, and the caller must be a known student.
                if (!SemnaturaValida(req, form, cfg)) return Results.StatusCode(403);
                var callSid = form["CallSid"].ToString();
                if (callSid.Length == 0) return Results.StatusCode(403);
                var elevApelant = await db.Utilizatori.SingleOrDefaultAsync(u => u.Telefon == form["From"].ToString() && u.TelefonVerificatLa != null, ct);
                if (elevApelant is null)
                    return Twiml(cfg, Spune("Bună! Acest număr nu este asociat unui cont. Adaugă-ți numărul de telefon pe pagina Linia AI de pe site și sună din nou.", VociTutor.Implicit), new XElement("Hangup"));
                if (await LimitaAtinsa(db, cfg, elevApelant.Id, DateTime.UtcNow, ct) is { } motiv)
                    return Twiml(cfg, Spune(motiv, await VociTutor.CitesteAsync(db, elevApelant.Id, ct)), new XElement("Hangup"));
                sesiune = await db.Sesiuni.SingleOrDefaultAsync(s => s.TwilioCallSid == callSid, ct);
                if (sesiune is not null) return Results.StatusCode(403); // a replay of a call that already started
                (cheie, sesiune) = SesiuneNoua(cfg, elevApelant.Id, DateTime.UtcNow, StareInCurs);
                sesiune.TwilioCallSid = callSid;
                db.Sesiuni.Add(sesiune);
                await db.SaveChangesAsync(ct);
            }

            var elev = await db.Utilizatori.SingleAsync(u => u.Id == sesiune.UtilizatorId, ct);
            var voce = await VociTutor.CitesteAsync(db, elev.Id, ct);
            if (!tutor.Configurat)
                return Twiml(cfg, Spune($"Bună, {elev.Nume.Split(' ')[0]}! Tutorele nu este încă pornit pe server. Te rugăm să revii mai târziu.", voce), new XElement("Hangup"));
            var salut = await tutor.SalutAsync(elev, ct);
            await SalveazaMesaj(db, sesiune.Id, TutorTelefon.ExpeditorTutor, salut, ct);
            return Twiml(cfg, Asculta(salut, voce, await tutor.IndiciiVorbire(elev.Id, ct), sesiune.Id, cheie),
                Spune("Nu te-am auzit. Sună din nou oricând!", voce), new XElement("Hangup"));
        });

        twilio.MapMethods("/raspuns", ["GET", "POST"], async (HttpRequest req, IConfiguration cfg, MarkAiDbContext db, TutorTelefon tutor, ILogger<TutorTelefon> log, CancellationToken ct) =>
        {
            var form = await ParametriAsync(req, ct);
            var sesiune = await SesiuneAutentica(req, form, db, cfg, ct);
            if (sesiune is null) return Results.StatusCode(403);
            var cheie = req.Query["k"].ToString();
            var indicii = await tutor.IndiciiVorbire(sesiune.UtilizatorId, ct);
            var voce = await VociTutor.CitesteAsync(db, sesiune.UtilizatorId, ct);

            // Every TwiML document Twilio fetches is one "step"; each step adds exactly one tutor message, so
            // this request is step (tutor messages so far + 1). The last allowed step must end the call.
            var limite = LimiteApel.Din(cfg);
            var pas = await db.Mesaje.CountAsync(m => m.SesiuneId == sesiune.Id && m.Expeditor == TutorTelefon.ExpeditorTutor, ct) + 1;
            var ultimulPas = (limite.Pasi > 0 && pas >= limite.Pasi) || DateTime.UtcNow - sesiune.InceputLa >= limite.Durata - TimeSpan.FromMinutes(1);

            async Task<IResult> Inchide(string text, SetariVoce v)
            {
                await SalveazaMesaj(db, sesiune.Id, TutorTelefon.ExpeditorTutor, text, ct);
                await Incheie(db, sesiune.Id, ct);
                return Twiml(cfg, Spune(text, v), new XElement("Hangup"));
            }

            var vorbit = form["SpeechResult"].ToString().Trim();
            if (vorbit.Length == 0)
            {
                if (ultimulPas) return await Inchide(TutorTelefon.Incheiere, voce);
                const string repeta = "Nu te-am auzit bine. Poți să repeți?";
                await SalveazaMesaj(db, sesiune.Id, TutorTelefon.ExpeditorTutor, repeta, ct);
                return Twiml(cfg, Asculta(repeta, voce, indicii, sesiune.Id, cheie), Spune("Pe curând!", voce), new XElement("Hangup"));
            }
            await SalveazaMesaj(db, sesiune.Id, TutorTelefon.ExpeditorElev, vorbit, ct);

            RaspunsTutor raspuns;
            // Twilio only waits a few seconds (5 on a trial); answer with an apology instead of letting the call drop.
            using var limita = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limita.CancelAfter(limite.RaspunsMaxim);
            try { raspuns = await tutor.RaspundeAsync(sesiune, voce, ultimulPas, limita.Token); }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                log.LogWarning(ex, "Phone tutor failed for session {SesiuneId}", sesiune.Id);
                if (ultimulPas) return await Inchide(TutorTelefon.Incheiere, voce);
                const string scuze = "Îmi pare rău, am avut o problemă de conexiune. Poți să repeți ultima întrebare?";
                await SalveazaMesaj(db, sesiune.Id, TutorTelefon.ExpeditorTutor, scuze, ct);
                return Twiml(cfg, Asculta(scuze, voce, indicii, sesiune.Id, cheie));
            }

            await SalveazaMesaj(db, sesiune.Id, TutorTelefon.ExpeditorTutor, raspuns.Text, ct);
            // A voice change requested by the student is heard right away and remembered for the next calls.
            if (raspuns.Voce != voce) await VociTutor.SalveazaAsync(db, sesiune.UtilizatorId, raspuns.Voce, ct);
            if (!raspuns.Final) return Twiml(cfg, Asculta(raspuns.Text, raspuns.Voce, indicii, sesiune.Id, cheie), Spune("Pe curând!", raspuns.Voce), new XElement("Hangup"));
            await Incheie(db, sesiune.Id, ct);
            return Twiml(cfg, Spune(raspuns.Text, raspuns.Voce), new XElement("Hangup"));
        });

        // Status callback (set it on the Twilio number): closes the session when the student hangs up.
        twilio.MapPost("/stare", async (HttpRequest req, IConfiguration cfg, MarkAiDbContext db, CancellationToken ct) =>
        {
            if (!req.HasFormContentType) return Results.StatusCode(403);
            var form = await req.ReadFormAsync(ct);
            if (!SemnaturaValida(req, form, cfg)) return Results.StatusCode(403);
            if (form["CallStatus"].ToString() is "completed" or "busy" or "failed" or "no-answer" or "canceled")
            {
                var callSid = form["CallSid"].ToString();
                var id = await db.Sesiuni.Where(s => s.TwilioCallSid == callSid).Select(s => (int?)s.Id).SingleOrDefaultAsync(ct);
                if (id is not null)
                {
                    await Incheie(db, id.Value, ct);
                    // The call length counts toward the daily minute cap.
                    if (int.TryParse(form["CallDuration"], out var secunde))
                        await db.Sesiuni.Where(s => s.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.DurataSecunde, secunde), ct);
                }
            }
            return Results.NoContent();
        });
    }

    private static async Task SalveazaMesaj(MarkAiDbContext db, int sesiuneId, string expeditor, string text, CancellationToken ct)
    {
        db.Mesaje.Add(new Mesaj { SesiuneId = sesiuneId, Expeditor = expeditor, Tip = TutorTelefon.TipVoce, Continut = text, CreatLa = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
    }

    private static Task Incheie(MarkAiDbContext db, int sesiuneId, CancellationToken ct) =>
        db.Sesiuni.Where(s => s.Id == sesiuneId && s.FinalizatLa == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "incheiat").SetProperty(x => x.FinalizatLa, DateTime.UtcNow), ct);

    // Romanian text-to-speech in the student's voice; rate and pitch use SSML <prosody>.
    private static XElement Spune(string text, SetariVoce voce)
    {
        object continut = voce.Viteza == 100 && voce.Ton == 0
            ? text
            : new XElement("prosody", new XAttribute("rate", $"{voce.Viteza}%"), new XAttribute("pitch", $"{voce.Ton:+0;-0;+0}%"), text);
        return new("Say", new XAttribute("language", "ro-RO"), new XAttribute("voice", VociTutor.Gaseste(voce.Voce).Twilio), continut);
    }

    private static XElement Asculta(string text, SetariVoce voce, string indicii, int sesiuneId, string cheie) =>
        new("Gather", new XAttribute("input", "speech"), new XAttribute("language", "ro-RO"), new XAttribute("speechTimeout", "auto"),
            new XAttribute("action", $"{UrlRaspuns}?{ParametriApel(sesiuneId, cheie)}"), new XAttribute("method", "POST"), new XAttribute("hints", indicii),
            Spune(text, voce));

    // Absolute when the API sits behind a prefix (e.g. https://site/api via the website's proxy), else root-relative.
    private static string UrlRaspuns = "/twilio/voce/raspuns";

    private static IResult Twiml(IConfiguration cfg, params XElement[] verbe) =>
        Results.Content(new XDocument(new XElement("Response", verbe)).ToString(), "application/xml");

    /// <summary>Call parameters: the form body of a POST, or the query string of a GET.</summary>
    private static async Task<IFormCollection> ParametriAsync(HttpRequest req, CancellationToken ct) =>
        req.HasFormContentType ? await req.ReadFormAsync(ct) : new FormCollection(req.Query.ToDictionary(q => q.Key, q => q.Value));

    public const string StareInitiat = "initiat";
    public const string StareInCurs = "in_curs";
    public const string StareEsuat = "esuat";

    /// <summary>The query string of every webhook URL of one call.</summary>
    public static string ParametriApel(int sesiuneId, string cheie) => $"s={sesiuneId}&k={Uri.EscapeDataString(cheie)}";

    /// <summary>A phone session with a fresh random key; the key is valid until the longest allowed call would be over.</summary>
    private static (string Cheie, Sesiune Sesiune) SesiuneNoua(IConfiguration cfg, int utilizatorId, DateTime acum, string stare)
    {
        var cheie = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (cheie, new Sesiune
        {
            UtilizatorId = utilizatorId, Canal = CanalTelefon, Status = stare, InceputLa = acum,
            CheieHash = HashCheie(cheie), CheieExpiraLa = acum + LimiteApel.Din(cfg).Durata + TimeSpan.FromMinutes(5),
        });
    }

    private static string HashCheie(string cheie) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cheie)));

    /// <summary>
    /// The open session named by ?s=, if this request is authentic for it: Twilio's signature or the session's unexpired key,
    /// and the CallSid of the session's own call. The first webhook of a "Sună-mă" call may arrive before Twilio's API
    /// answer, so it records the CallSid.
    /// </summary>
    private static async Task<Sesiune?> SesiuneAutentica(HttpRequest req, IFormCollection form, MarkAiDbContext db, IConfiguration cfg, CancellationToken ct)
    {
        if (!int.TryParse(req.Query["s"], out var id)) return null;
        var sesiune = await db.Sesiuni.SingleOrDefaultAsync(s => s.Id == id && s.Canal == CanalTelefon, ct);
        var callSid = form["CallSid"].ToString();
        if (sesiune is null || sesiune.FinalizatLa is not null || callSid.Length == 0) return null;
        if (sesiune.TwilioCallSid is not null && sesiune.TwilioCallSid != callSid) return null;
        var cheie = req.Query["k"].ToString();
        var cheieValida = sesiune.CheieHash is not null && sesiune.CheieExpiraLa > DateTime.UtcNow && cheie.Length > 0
            && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(HashCheie(cheie)), Encoding.ASCII.GetBytes(sesiune.CheieHash));
        if (!cheieValida && !SemnaturaValida(req, form, cfg)) return null;
        if (sesiune.TwilioCallSid is null)
        {
            sesiune.TwilioCallSid = callSid;
            await db.SaveChangesAsync(ct);
        }
        return sesiune;
    }

    /// <summary>Why a student may not get another call right now (daily calls per student, daily minutes in total), or null.</summary>
    private static async Task<string?> LimitaAtinsa(MarkAiDbContext db, IConfiguration cfg, int utilizatorId, DateTime acum, CancellationToken ct)
    {
        var deLa = acum.AddHours(-24);
        var apeluriPeZi = cfg.GetValue("Twilio:ApeluriPeZi", 3);
        var aleElevului = await db.Sesiuni.CountAsync(s => s.UtilizatorId == utilizatorId && s.Canal == CanalTelefon
            && s.InceputLa >= deLa && s.Status != StareEsuat, ct);
        if (aleElevului >= apeluriPeZi)
            return $"Ai folosit toate cele {apeluriPeZi} apeluri din ultimele 24 de ore. Încearcă din nou mai târziu.";
        // Finished calls count with Twilio's reported length; calls in progress with the time elapsed so far.
        var apeluri = await db.Sesiuni.AsNoTracking().Where(s => s.Canal == CanalTelefon && s.InceputLa >= deLa && s.Status != StareEsuat)
            .Select(s => new { s.InceputLa, s.FinalizatLa, s.DurataSecunde }).ToListAsync(ct);
        var secunde = apeluri.Sum(s => s.DurataSecunde ?? ((s.FinalizatLa ?? acum) - s.InceputLa).TotalSeconds);
        return secunde >= cfg.GetValue("Twilio:MinutePeZi", 20) * 60
            ? "Linia AI și-a atins limita de minute pentru azi. Încearcă din nou mâine."
            : null;
    }

    /// <summary>
    /// Twilio's X-Twilio-Signature: base64(HMAC-SHA1(auth token, public URL + sorted POST key/values)).
    /// A GET carries its parameters in the URL, so nothing is appended.
    /// </summary>
    private static bool SemnaturaValida(HttpRequest req, IFormCollection form, IConfiguration cfg)
    {
        var token = Environment.GetEnvironmentVariable("TWILIO_AUTH_TOKEN");
        if (string.IsNullOrEmpty(token)) return false;
        var url = (cfg["Twilio:UrlPublic"] ?? "").TrimEnd('/') + req.Path + req.QueryString;
        var sb = new StringBuilder(url);
        if (req.HasFormContentType)
            foreach (var kv in form.OrderBy(k => k.Key, StringComparer.Ordinal)) sb.Append(kv.Key).Append(kv.Value.ToString());
        var asteptat = Convert.ToBase64String(HMACSHA1.HashData(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(sb.ToString())));
        var primit = req.Headers["X-Twilio-Signature"].ToString();
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(asteptat), Encoding.UTF8.GetBytes(primit));
    }
}

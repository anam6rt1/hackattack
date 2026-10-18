using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using MarkAi.Api.Infrastructura;
using MarkAi.Api.Servicii;
using MarkAi.Data;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Endpoints;

public record LiniaAiDto(string Numar, string? TelefonulMeu, SetariVoce Voce, IReadOnlyList<VoceTutor> Voci, bool PoateSuna);
public record ApelDto(int Id, DateTime InceputLa, DateTime? FinalizatLa, string Status, string? Curs, int NumarMesaje, string? Previzualizare);
public record MesajDto(int Id, string Expeditor, string Tip, string Continut, DateTime CreatLa);

public static class LiniaAiEndpoints
{
    public const string CanalTelefon = "telefon";

    public static void Mapeaza(WebApplication app)
    {
        var g = app.MapGroup("").WithTags("Linia AI").RequireAuthorization();

        g.MapGet("/linia-ai", async (HttpContext h, IConfiguration cfg, MarkAiDbContext db, TwilioApeluri apeluri, TutorTelefon tutor, CancellationToken ct) =>
            new LiniaAiDto(cfg["LiniaAi:Numar"] ?? "", await db.Utilizatori.Where(u => u.Id == h.User.Id()).Select(u => u.Telefon).SingleAsync(ct),
                await VociTutor.CitesteAsync(db, h.User.Id(), ct), VociTutor.Toate, apeluri.Configurat && tutor.Configurat));

        // "Sună-mă": Twilio calls the student's saved number; the conversation then runs through the voice webhooks below.
        g.MapPost("/linia-ai/suna-ma", async (HttpContext h, MarkAiDbContext db, TwilioApeluri apeluri, TutorTelefon tutor, CancellationToken ct) =>
        {
            if (!tutor.Configurat) throw new InvalidOperationException("Tutorele nu este încă pornit pe server.");
            var uid = h.User.Id();
            var telefon = await db.Utilizatori.Where(u => u.Id == uid).Select(u => u.Telefon).SingleAsync(ct)
                ?? throw new CerereInvalida("Salvează mai întâi numărul tău de telefon.");
            var recent = DateTime.UtcNow.AddMinutes(-11);
            if (await db.Sesiuni.AnyAsync(s => s.UtilizatorId == uid && s.Canal == CanalTelefon && s.FinalizatLa == null && s.InceputLa > recent, ct))
                throw new InvalidOperationException("Ai deja un apel în curs.");
            await apeluri.SunaAsync(uid, telefon, ct);
            return new { mesaj = $"Te sunăm acum pe {telefon}. Răspunde la telefon!" };
        }).RequireAuthorization(Politici.Elev);

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
            var q = db.Sesiuni.AsNoTracking().Where(s => s.UtilizatorId == uid);
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
        // Every request must carry a valid Twilio signature.
        var twilio = app.MapGroup("/twilio/voce").WithTags("Linia AI").DisableAntiforgery();
        var urlPublic = (app.Configuration["Twilio:UrlPublic"] ?? "").TrimEnd('/');
        if (urlPublic.StartsWith("https://")) UrlRaspuns = urlPublic + "/twilio/voce/raspuns";

        // GET as well as POST: a trial account fetches TwiML through Twilio's proxy, which may replay the request differently.
        twilio.MapMethods("", ["GET", "POST"], async (HttpRequest req, IConfiguration cfg, MarkAiDbContext db, TutorTelefon tutor, CancellationToken ct) =>
        {
            var form = await ParametriAsync(req, ct);
            var numar = NumarElev(form);
            if (!CerereValida(req, form, numar, cfg)) return Results.StatusCode(403);
            var callSid = form["CallSid"].ToString();
            var elev = await db.Utilizatori.SingleOrDefaultAsync(u => u.Telefon == numar, ct);
            if (elev is null)
                return Twiml(cfg, Spune("Bună! Acest număr nu este asociat unui cont. Adaugă-ți numărul de telefon pe pagina Linia AI de pe site și sună din nou.", VociTutor.Implicit), new XElement("Hangup"));
            if (!tutor.Configurat)
                return Twiml(cfg, Spune($"Bună, {elev.Nume.Split(' ')[0]}! Tutorele nu este încă pornit pe server. Te rugăm să revii mai târziu.", await VociTutor.CitesteAsync(db, elev.Id, ct)), new XElement("Hangup"));

            var sesiune = await db.Sesiuni.SingleOrDefaultAsync(s => s.TwilioCallSid == callSid, ct);
            if (sesiune is null)
            {
                sesiune = new Sesiune { UtilizatorId = elev.Id, Canal = CanalTelefon, Status = "in_curs", TwilioCallSid = callSid, InceputLa = DateTime.UtcNow };
                db.Sesiuni.Add(sesiune);
                await db.SaveChangesAsync(ct);
            }
            var salut = await tutor.SalutAsync(elev, ct);
            var voce = await VociTutor.CitesteAsync(db, elev.Id, ct);
            await SalveazaMesaj(db, sesiune.Id, TutorTelefon.ExpeditorTutor, salut, ct);
            return Twiml(cfg, Asculta(salut, voce, await tutor.IndiciiVorbire(ct), numar),
                Spune("Nu te-am auzit. Sună din nou oricând!", voce), new XElement("Hangup"));
        });

        twilio.MapMethods("/raspuns", ["GET", "POST"], async (HttpRequest req, IConfiguration cfg, MarkAiDbContext db, TutorTelefon tutor, ILogger<TutorTelefon> log, CancellationToken ct) =>
        {
            var form = await ParametriAsync(req, ct);
            var numar = NumarElev(form);
            if (!CerereValida(req, form, numar, cfg)) return Results.StatusCode(403);
            var sesiune = await db.Sesiuni.SingleOrDefaultAsync(s => s.TwilioCallSid == form["CallSid"].ToString(), ct);
            if (sesiune is null) return Twiml(cfg, new XElement("Hangup"));
            var indicii = await tutor.IndiciiVorbire(ct);
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
                return Twiml(cfg, Asculta(repeta, voce, indicii, numar), Spune("Pe curând!", voce), new XElement("Hangup"));
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
                return Twiml(cfg, Asculta(scuze, voce, indicii, numar));
            }

            await SalveazaMesaj(db, sesiune.Id, TutorTelefon.ExpeditorTutor, raspuns.Text, ct);
            // A voice change requested by the student is heard right away and remembered for the next calls.
            if (raspuns.Voce != voce) await VociTutor.SalveazaAsync(db, sesiune.UtilizatorId, raspuns.Voce, ct);
            if (!raspuns.Final) return Twiml(cfg, Asculta(raspuns.Text, raspuns.Voce, indicii, numar), Spune("Pe curând!", raspuns.Voce), new XElement("Hangup"));
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
                var id = await db.Sesiuni.Where(s => s.TwilioCallSid == form["CallSid"].ToString()).Select(s => (int?)s.Id).SingleOrDefaultAsync(ct);
                if (id is not null) await Incheie(db, id.Value, ct);
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

    private static XElement Asculta(string text, SetariVoce voce, string indicii, string numar) =>
        new("Gather", new XAttribute("input", "speech"), new XAttribute("language", "ro-RO"), new XAttribute("speechTimeout", "auto"),
            new XAttribute("action", $"{UrlRaspuns}?k={Uri.EscapeDataString(CheieApel(numar) ?? "")}"), new XAttribute("method", "POST"), new XAttribute("hints", indicii),
            Spune(text, voce));

    // Absolute when the API sits behind a prefix (e.g. https://site/api via the website's proxy), else root-relative.
    private static string UrlRaspuns = "/twilio/voce/raspuns";

    private static IResult Twiml(IConfiguration cfg, params XElement[] verbe) =>
        Results.Content(new XDocument(new XElement("Response", verbe)).ToString(), "application/xml");

    /// <summary>Call parameters: the form body of a POST, or the query string of a GET.</summary>
    private static async Task<IFormCollection> ParametriAsync(HttpRequest req, CancellationToken ct) =>
        req.HasFormContentType ? await req.ReadFormAsync(ct) : new FormCollection(req.Query.ToDictionary(q => q.Key, q => q.Value));

    // Incoming call: the student is the caller. "Sună-mă" (outbound-api): the student is the one being called.
    private static string NumarElev(IFormCollection form) =>
        form["Direction"].ToString().StartsWith("outbound") ? form["To"].ToString() : form["From"].ToString();

    /// <summary>
    /// A secret for one student's calls, put in the TwiML URLs (?k=...). It authenticates requests that reach us through the
    /// trial account's proxy, whose X-Twilio-Signature cannot match our URL; it is tied to the number, so it cannot start a call as someone else.
    /// </summary>
    public static string? CheieApel(string numar)
    {
        var token = Environment.GetEnvironmentVariable("TWILIO_AUTH_TOKEN");
        if (string.IsNullOrEmpty(token)) return null;
        return Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes("markai-voce:" + numar)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static bool CerereValida(HttpRequest req, IFormCollection form, string numar, IConfiguration cfg)
    {
        if (SemnaturaValida(req, form, cfg)) return true;
        var cheie = CheieApel(numar);
        var primita = req.Query["k"].ToString();
        return cheie is not null && numar.Length > 0 && primita.Length > 0
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(cheie), Encoding.UTF8.GetBytes(primita));
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

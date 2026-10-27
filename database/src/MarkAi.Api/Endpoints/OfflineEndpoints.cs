using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MarkAi.Api.Infrastructura;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Endpoints;

public record IntrebareOffline(int Id, string Tip, string Text, IReadOnlyList<VariantaPublica> Variante);
public record CheieIntrebare(int Id, int? Corecta, int? Numeric, string? Explicatie);
public record PachetOffline(int ParcursId, int MaterieId, string Clasa, string Titlu, int IntrebariPeTest, DateTime GeneratLa,
    IReadOnlyList<IntrebareOffline> Intrebari, string Cheie);
public record OperatieOffline(Guid IdClient, string Tip, JsonElement Date);
public record LotSincronizare(IReadOnlyList<OperatieOffline> Operatii);
public record RezultatOperatie(Guid IdClient, string Stare, string? Mesaj, JsonElement? Rezultat);

/// <summary>
/// The desktop and mobile apps work offline on downloaded Lecții (owner decision, plan 4): the text, chapters and files come from
/// the usual endpoints; this adds the question bank with its answer key, encrypted, so the app can grade a test on the spot, and
/// the batch that brings offline work back. The server grades every offline test again: its result is the one that counts (XP,
/// promotion, mistakes, statistics). The key only keeps the answers from sitting in plain text on the device; a determined
/// student who controls the device can still decrypt them, which the owner accepted.
/// </summary>
public static class OfflineEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public const int MaxOperatiiPeLot = 200;

    /// <summary>The student's offline key: derived from the API's signing key, the account and its session version, so revoking
    /// the sessions (password change, "log out everywhere") also makes the downloaded answer keys unreadable.</summary>
    public static byte[] CheieUtilizator(OptiuniJwt jwt, int uid, int versiuneSesiune) =>
        HMACSHA256.HashData(jwt.Cheie.Key, Encoding.UTF8.GetBytes($"markai-offline:{uid}:{versiuneSesiune}"));

    /// <summary>AES-GCM; the result is nonce (12) + ciphertext + tag (16), the layout WebCrypto decrypts.</summary>
    public static string Cripteaza(byte[] cheie, byte[] date)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var rezultat = new byte[12 + date.Length + 16];
        using var aes = new AesGcm(cheie, 16);
        aes.Encrypt(nonce, date, rezultat.AsSpan(12, date.Length), rezultat.AsSpan(12 + date.Length));
        nonce.CopyTo(rezultat, 0);
        return Convert.ToBase64String(rezultat);
    }

    public static void Mapeaza(WebApplication app)
    {
        var e = app.MapGroup("").RequireAuthorization(Politici.Elev).WithTags("Offline");

        e.MapGet("/utilizatori/me/cheie-offline", async (HttpContext h, MarkAiDbContext db, OptiuniJwt jwt, CancellationToken ct) =>
        {
            var versiune = await db.Utilizatori.Where(u => u.Id == h.User.Id()).Select(u => u.VersiuneSesiune).SingleAsync(ct);
            h.Response.Headers.CacheControl = "no-store";
            return new { Cheie = Convert.ToBase64String(CheieUtilizator(jwt, h.User.Id(), versiune)), Versiune = versiune };
        });

        e.MapGet("/parcursuri/{id:int}/offline", async (int id, HttpContext h, MarkAiDbContext db, AccesContinut acces, OptiuniJwt jwt,
            IConfiguration cfg, TimeProvider ceas, CancellationToken ct) =>
        {
            var a = await acces.PentruAsync(h.User, ct);
            var lectie = await acces.Parcursuri(a).Where(p => p.Id == id && p.Publicat).Select(p => new { p.Id, p.MaterieId, p.Clasa, p.Titlu })
                .SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Lecția nu există.");
            var intrebari = await Utilizabile(db).Where(q => q.ParcursId == id).OrderBy(q => q.Id)
                .Select(q => new { q.Id, q.Tip, q.Text, q.Explicatie, q.RaspunsNumeric,
                    Variante = q.Variante.OrderBy(v => v.Id).Select(v => new { v.Id, v.Text, v.Corecta }).ToList() })
                .ToListAsync(ct);
            var versiune = await db.Utilizatori.Where(u => u.Id == h.User.Id()).Select(u => u.VersiuneSesiune).SingleAsync(ct);
            var chei = intrebari.Select(q => new CheieIntrebare(q.Id, q.Variante.FirstOrDefault(v => v.Corecta)?.Id, q.RaspunsNumeric, q.Explicatie)).ToList();
            h.Response.Headers.CacheControl = "no-store";
            return new PachetOffline(lectie.Id, lectie.MaterieId, lectie.Clasa, lectie.Titlu, QuizEndpoints.IntrebariPeIncercare(cfg),
                ceas.GetUtcNow().UtcDateTime,
                intrebari.Select(q => new IntrebareOffline(q.Id, q.Tip, q.Text, q.Variante.Select(v => new VariantaPublica(v.Id, v.Text)).ToList())).ToList(),
                Cripteaza(CheieUtilizator(jwt, h.User.Id(), versiune), JsonSerializer.SerializeToUtf8Bytes(chei, Json)));
        });

        // One batch at a time per account (ContActiv serialises every change of an account): a retry that overlaps the first send
        // waits for it and then finds its operations done.
        e.MapPost("/sincronizare", async (LotSincronizare lot, HttpContext h, MarkAiDbContext db, AccesContinut acces, ProgresService progres,
            IConfiguration cfg, TimeProvider ceas, CancellationToken ct) =>
        {
            if (lot.Operatii is not { Count: > 0 and <= MaxOperatiiPeLot }) throw new CerereInvalida($"Trimite între 1 și {MaxOperatiiPeLot} operații.");
            var uid = h.User.Id();
            {
                var rezultate = new List<RezultatOperatie>();
                foreach (var op in lot.Operatii)
                {
                    var facuta = await db.Set<OperatieSincronizata>().AsNoTracking().SingleOrDefaultAsync(x => x.UtilizatorId == uid && x.IdClient == op.IdClient, ct);
                    if (facuta is not null)
                    {
                        rezultate.Add(new(op.IdClient, "duplicat", null, JsonSerializer.Deserialize<JsonElement>(facuta.RezultatJson)));
                        continue;
                    }
                    try
                    {
                        object rezultat = op.Tip switch
                        {
                            "capitol_deschis" => await CapitolDeschis(op.Date, h, acces, progres, ct),
                            "test" => await Test(op.Date, h, db, acces, progres, cfg, ceas, ct),
                            "simulare" => await Simulare(op.Date, h, db, acces, ceas, ct),
                            "recapitulare" => await Recapitulare(op.Date, h, db, acces, ceas, ct),
                            _ => throw new CerereInvalida("Tip de operație necunoscut."),
                        };
                        var json = JsonSerializer.Serialize(rezultat, Json);
                        db.Add(new OperatieSincronizata { UtilizatorId = uid, IdClient = op.IdClient, Tip = op.Tip, AplicataLa = ceas.GetUtcNow().UtcDateTime, RezultatJson = json });
                        await db.SaveChangesAsync(ct);
                        rezultate.Add(new(op.IdClient, "aplicata", null, JsonSerializer.Deserialize<JsonElement>(json)));
                    }
                    catch (Exception ex) when (ex is CerereInvalida or Conflict or KeyNotFoundException or JsonException or InvalidOperationException)
                    {
                        // Refused for good (the Lecție is gone, the answers do not match it): the app drops it and tells the student.
                        db.ChangeTracker.Clear();
                        rezultate.Add(new(op.IdClient, "respinsa", ex is JsonException or InvalidOperationException ? "Date invalide." : ex.Message, null));
                    }
                }
                h.Response.Headers.CacheControl = "no-store";
                return new { Rezultate = rezultate };
            }
        });
    }

    /// <summary>Questions a test or simulation can use: multiple choice with one right option among two or more, or a numeric estimate.</summary>
    private static IQueryable<Intrebare> Utilizabile(MarkAiDbContext db) => db.Set<Intrebare>().Where(q => q.Activa && q.ParcursId != null
        && ((q.Tip == Intrebare.Grila && q.Variante.Any(v => v.Corecta) && q.Variante.Any(v => !v.Corecta)) || (q.Tip == Intrebare.Estimare && q.RaspunsNumeric != null)));

    private static async Task<object> CapitolDeschis(JsonElement d, HttpContext h, AccesContinut acces, ProgresService progres, CancellationToken ct)
    {
        var cursId = d.GetProperty("cursId").GetInt32();
        await acces.AsiguraCapitolVizibilAsync(h.User, cursId, ct);
        await progres.AcceseazaAsync(h.User.Id(), cursId, ct);
        return new { CursId = cursId };
    }

    private static async Task AsiguraLectieAsync(AccesContinut acces, HttpContext h, int parcursId, CancellationToken ct)
    {
        var a = await acces.PentruAsync(h.User, ct);
        if (!await acces.Parcursuri(a).AnyAsync(p => p.Id == parcursId && p.Publicat, ct)) throw new KeyNotFoundException("Lecția nu mai este disponibilă.");
    }

    /// <summary>A Lecție test taken offline: the questions must be the Lecție's and as many as an online attempt; graded here again.</summary>
    private static async Task<object> Test(JsonElement d, HttpContext h, MarkAiDbContext db, AccesContinut acces, ProgresService progres,
        IConfiguration cfg, TimeProvider ceas, CancellationToken ct)
    {
        var parcursId = d.GetProperty("parcursId").GetInt32();
        var intrebari = d.GetProperty("intrebari").Deserialize<int[]>(Json)!;
        var raspunsuri = d.GetProperty("raspunsuri").Deserialize<int[]>(Json)!;
        await AsiguraLectieAsync(acces, h, parcursId, ct);
        var valide = await QuizEndpoints.IntrebariValide(db, parcursId).Select(q => q.Id).ToListAsync(ct);
        var asteptate = Math.Min(valide.Count, QuizEndpoints.IntrebariPeIncercare(cfg));
        if (intrebari.Length == 0 || intrebari.Length != raspunsuri.Length || intrebari.Distinct().Count() != intrebari.Length
            || intrebari.Any(q => !valide.Contains(q)) || intrebari.Length < asteptate)
            throw new CerereInvalida("Întrebările testului nu mai corespund lecției. Descarcă lecția din nou.");
        var variante = await db.Set<VariantaIntrebare>().AsNoTracking().Where(v => intrebari.Contains(v.IntrebareId)).ToListAsync(ct);
        if (intrebari.Select((q, i) => variante.Any(v => v.IntrebareId == q && v.Id == raspunsuri[i])).Any(ok => !ok))
            throw new CerereInvalida("Un răspuns nu aparține întrebării lui.");

        var uid = h.User.Id(); var acum = ceas.GetUtcNow().UtcDateTime;
        var inceput = Data(d, "inceputLa") ?? acum;
        var corecte = intrebari.Select((q, i) => variante.Single(v => v.Id == raspunsuri[i]).Corecta).ToArray();
        var incercare = new IncercareQuiz { UtilizatorId = uid, ParcursId = parcursId, IntrebariIds = intrebari, Raspunsuri = raspunsuri,
            InceputLa = inceput > acum ? acum : inceput, FinalizataLa = acum, Corecte = corecte.Count(x => x) };
        db.Add(incercare);
        await db.SaveChangesAsync(ct);
        for (var i = 0; i < intrebari.Length; i++)
        {
            if (!corecte[i]) await GreseliEndpoints.NoteazaAsync(db, uid, intrebari[i], acum, ct);
            db.Add(new RaspunsStatistica { IncercareId = incercare.Id, IntrebareId = intrebari[i], UtilizatorId = uid, Corect = corecte[i], RaspunsLa = acum });
        }
        await db.SaveChangesAsync(ct);
        var necesare = QuizEndpoints.Prag(intrebari.Length);
        var promovat = incercare.Corecte >= necesare;
        var p = promovat ? await progres.PromoveazaAsync(uid, parcursId, ct) : null;
        return new { ParcursId = parcursId, Promovat = promovat, incercare.Corecte, Total = intrebari.Length, Necesare = necesare, XpCastigat = p?.XpCastigat ?? 0, p?.PromovatLa };
    }

    /// <summary>A mock exam taken offline on downloaded questions: saved to the history (no XP), graded from the database.</summary>
    private static async Task<object> Simulare(JsonElement d, HttpContext h, MarkAiDbContext db, AccesContinut acces, TimeProvider ceas, CancellationToken ct)
    {
        var tip = d.GetProperty("tip").GetString(); var clasa = d.GetProperty("clasa").GetString()!; var materieId = d.GetProperty("materieId").GetInt32();
        if (tip is not ("bac" or "evaluare_nationala") || !Parcurs.Clase.Contains(clasa) || (tip == "evaluare_nationala") != (clasa == "VIII"))
            throw new CerereInvalida("Examen sau clasă nepotrivită.");
        var raspunsuri = d.GetProperty("raspunsuri").EnumerateArray().Select(r => (Id: r.GetProperty("intrebareId").GetInt32(),
            Varianta: r.TryGetProperty("variantaId", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : (int?)null,
            Valoare: r.TryGetProperty("valoare", out var n) && n.ValueKind == JsonValueKind.Number ? n.GetInt32() : (int?)null)).ToList();
        if (raspunsuri.Count is < 5 or > 50 || raspunsuri.Select(r => r.Id).Distinct().Count() != raspunsuri.Count) throw new CerereInvalida("O simulare are 5–50 de întrebări diferite.");
        var ids = raspunsuri.Select(r => r.Id).ToArray();
        var a = await acces.PentruAsync(h.User, ct);
        var intrebari = await Utilizabile(db).Where(q => ids.Contains(q.Id) && q.MaterieId == materieId && q.Parcurs!.Clasa == clasa
                && acces.Parcursuri(a).Any(p => p.Id == q.ParcursId && p.Publicat))
            .Include(q => q.Variante).Include(q => q.Parcurs).ToDictionaryAsync(q => q.Id, ct);
        if (intrebari.Count != ids.Length) throw new CerereInvalida("Unele întrebări nu mai sunt disponibile pentru această materie și clasă.");

        var acum = ceas.GetUtcNow().UtcDateTime;
        var inceput = Data(d, "inceputLa") ?? acum; if (inceput > acum) inceput = acum;
        var durata = Math.Clamp(d.TryGetProperty("durataMinute", out var dm) && dm.ValueKind == JsonValueKind.Number ? dm.GetInt32() : 60, 5, 180);
        var lista = raspunsuri.Select(r =>
        {
            var q = intrebari[r.Id];
            var valid = q.Tip == Intrebare.Grila ? r.Varianta is { } vid && q.Variante.Any(x => x.Id == vid) : r.Valoare is not null;
            return new IntrebareSimulare { Id = q.Id, ParcursId = q.ParcursId!.Value, Lectie = q.Parcurs!.Titlu, Text = q.Text, Tip = q.Tip,
                Variante = q.Variante.OrderBy(x => x.Id).Select(x => new VariantaPublica(x.Id, x.Text)).ToList(), VariantaCorectaId = q.Variante.SingleOrDefault(x => x.Corecta)?.Id,
                RaspunsNumeric = q.RaspunsNumeric, Explicatie = q.Explicatie, Raspunsa = valid,
                VariantaAleasaId = valid && q.Tip == Intrebare.Grila ? r.Varianta : null, ValoareAleasa = valid && q.Tip == Intrebare.Estimare ? r.Valoare : null };
        }).ToList();
        var s = new Simulare { UtilizatorId = h.User.Id(), MaterieId = materieId, Tip = tip, Clasa = clasa, InceputLa = inceput,
            ExpiraLa = inceput.AddMinutes(durata), Total = lista.Count, IntrebariJson = JsonSerializer.Serialize(lista, Json) };
        s.Corecte = lista.Count(x => x.Corect);
        var final = Data(d, "finalizatLa") ?? acum;
        s.FinalizatLa = final < inceput ? inceput : final > s.ExpiraLa ? s.ExpiraLa : final > acum ? acum : final;
        db.Add(s); await db.SaveChangesAsync(ct);
        return new { s.Id, s.Corecte, s.Total };
    }

    /// <summary>A mistake reviewed offline: counts like an online review if it was due; otherwise it is simply not recorded.</summary>
    private static async Task<object> Recapitulare(JsonElement d, HttpContext h, MarkAiDbContext db, AccesContinut acces, TimeProvider ceas, CancellationToken ct)
    {
        var id = d.GetProperty("intrebareId").GetInt32(); var variantaId = d.GetProperty("variantaId").GetInt32();
        var uid = h.User.Id(); var acum = ceas.GetUtcNow().UtcDateTime;
        var a = await acces.PentruAsync(h.User, ct);
        var g = await db.Set<Greseala>().SingleOrDefaultAsync(g => g.UtilizatorId == uid && g.IntrebareId == id && g.CorecteConsecutive < 2
            && acces.Parcursuri(a).Any(p => p.Id == g.Intrebare.ParcursId && p.Publicat), ct) ?? throw new KeyNotFoundException("Întrebarea nu mai este de recapitulat.");
        var variante = await db.Set<VariantaIntrebare>().Where(v => v.IntrebareId == id).ToListAsync(ct);
        if (variante.All(v => v.Id != variantaId)) throw new CerereInvalida("Varianta nu aparține întrebării.");
        if (g.ScadentaLa > acum) return new { IntrebareId = id, Inregistrata = false, g.ScadentaLa };
        var corect = variante.Single(v => v.Id == variantaId).Corecta;
        g.CorecteConsecutive = corect ? g.CorecteConsecutive + 1 : 0;
        g.Nivel = corect ? Math.Min(g.Nivel + 1, 3) : 0;
        g.ScadentaLa = acum.AddDays(GreseliEndpoints.IntervalZile(g.Nivel)); g.Token = null; g.EmisaLa = null;
        await db.SaveChangesAsync(ct);
        return new { IntrebareId = id, Inregistrata = true, Corect = corect, g.ScadentaLa, Rezolvata = g.CorecteConsecutive == 2 };
    }

    private static DateTime? Data(JsonElement d, string nume) =>
        d.TryGetProperty(nume, out var v) && v.ValueKind == JsonValueKind.String && v.TryGetDateTime(out var t) ? t.ToUniversalTime() : null;
}

using MarkAi.Api.Infrastructura;
using MarkAi.Api.Servicii;
using Microsoft.AspNetCore.Mvc;
using MarkAi.Data;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Endpoints;

public record ConfirmareDatePersonale(string ParolaActuala, string? Confirmare = null);

public static class DatePersonaleEndpoints
{
    public static void Mapeaza(WebApplication app)
    {
        app.MapGet("/confidentialitate", (IConfiguration cfg) => Results.Ok(new
        {
            versiune = DatePersonale.VersiuneInformare,
            operatorDate = cfg["Confidentialitate:Operator"], contact = cfg["Confidentialitate:Contact"],
            scopuri = new[] { "Administrarea contului și accesul la materiale", "Progres educațional, teste și rezervări", "Tutore AI și comunicări solicitate" },
            categorii = new[] { "Identificare și contact", "Apartenență la organizații și progres", "Documente încărcate", "Rezervări și evidențe financiare", "Conversații cu tutorele" },
            procesatori = new[] { cfg["Confidentialitate:Gazduire"] ?? "Găzduire locală", "Furnizorul AI configurat (Gemini/Anthropic)", "Twilio, când apelurile/SMS sunt activate", "Furnizorul email configurat" },
            retentie = new { conversatiiZile = cfg.GetValue<int>("Confidentialitate:ConversatiiZile"), mesajeTrimiseZile = cfg.GetValue<int>("Confidentialitate:MesajeTrimiseZile"), zeroInseamna = "Curățarea automată este dezactivată; perioada urmează să fie stabilită." },
            drepturi = new[] { "Acces și export", "Rectificarea datelor în setări", "Solicitarea ștergerii contului", "Contactarea operatorului pentru celelalte solicitări" },
            stergere = "Accesul și datele de profil sunt eliminate. Documentele personale și upload-urile neterminate sunt curățate printr-o cerere persistentă. Materialele organizației și evidențele operaționale/financiare cu identificator pseudonimizat rămân; ele nu sunt declarate anonime.",
            minori = "Declarația acordului părintelui este înregistrată separat; nu reprezintă verificarea identității sau autorității părintești.",
        })).AllowAnonymous().WithTags("Date personale");

        var g = app.MapGroup("/utilizatori/me/date").RequireAuthorization().RequireRateLimiting(LimiteCereri.Autentificare).WithTags("Date personale");
        g.MapGet("/documente/{tip}", async (string tip, HttpContext h, MarkAiDbContext db, Stocare stocare, CancellationToken ct) =>
        {
            var profil = await db.ProfiluriMentor.AsNoTracking().Where(x => x.UtilizatorId == h.User.Id())
                .Select(x => new { x.PozaUrl, x.DovadaCalificareUrl }).SingleOrDefaultAsync(ct);
            var cheie = tip switch { "fotografie" => profil?.PozaUrl, "dovada" => profil?.DovadaCalificareUrl, _ => null };
            h.Response.Headers.CacheControl = "no-store";
            return stocare.Serveste(cheie, tip == "dovada" ? "Dovada" : "Fotografie");
        });
        g.MapPost("/export", async (ConfirmareDatePersonale c, HttpContext h, DatePersonale date, CancellationToken ct) =>
        {
            h.Response.Headers.CacheControl = "no-store";
            return Results.File(await date.ExportaAsync(h.User.Id(), c.ParolaActuala, ct), "application/json", "datele-mele.json");
        });
        g.MapDelete("", async ([FromBody] ConfirmareDatePersonale c, HttpContext h, DatePersonale date, Tokenuri tokenuri, CancellationToken ct) =>
        {
            await date.StergeAsync(h.User.Id(), c.ParolaActuala, c.Confirmare ?? "", ct);
            tokenuri.StergeCookie(h.Response);
            h.Response.Headers.CacheControl = "no-store";
            return Results.Json(new { mesaj = "Contul este închis. Ștergerea documentelor personale este programată.", stare = "stergere_programata" }, statusCode: 202);
        });
    }
}

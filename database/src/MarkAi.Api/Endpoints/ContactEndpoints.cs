using System.Net.Mail;
using MarkAi.Api.Infrastructura;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Endpoints;

/// <param name="Site">A field hidden from people (the contact form's honeypot): filled in only by spam bots.</param>
public record MesajContactNou(string? Subiect, string? Text, string? Pagina, string? Nume, string? Email, string? Site);

/// <summary>
/// The contact page: anyone can write to the platform's administrators, with or without an account. Messages stay in the
/// database for the admin page (<c>/admin/contact</c>), where the administrator answers by email from their own mail client;
/// the server sends nothing, so the form cannot be used to send email to someone else.
/// </summary>
public static class ContactEndpoints
{
    /// <summary>Messages per day from one account, or from one email address without an account.</summary>
    public const int MaxPeZiCont = 5, MaxPeZiEmail = 3;
    private static readonly Pagini.Marime Marime = new(20, 20);

    public static void Mapeaza(WebApplication app)
    {
        app.MapPost("/contact", async (MesajContactNou c, HttpContext h, MarkAiDbContext db, TimeProvider ceas, CancellationToken ct) =>
        {
            // A bot that filled in the hidden field gets the same answer as a person, so it does not learn to skip it.
            if (!string.IsNullOrEmpty(c.Site)) return Results.NoContent();
            var now = ceas.GetUtcNow().UtcDateTime;
            var subiect = MesajContact.Subiecte.Contains(c.Subiect) ? c.Subiect! : throw new CerereInvalida("Alege subiectul mesajului.");
            var mesaj = new MesajContact { Subiect = subiect, Text = Validare.Obligatoriu(c.Text, "Mesaj", 4000), Pagina = Pagina(c.Pagina), CreatLa = now };
            if (h.User.Identity?.IsAuthenticated == true)
            {
                mesaj.UtilizatorId = h.User.Id();
                if (await db.Set<MesajContact>().CountAsync(f => f.UtilizatorId == mesaj.UtilizatorId && f.CreatLa > now.AddDays(-1), ct) >= MaxPeZiCont)
                    throw new Conflict($"Ai trimis deja {MaxPeZiCont} mesaje astăzi. Le citim pe toate; poți scrie din nou mâine.");
            }
            else
            {
                mesaj.Nume = Validare.Obligatoriu(c.Nume, "Nume", 120);
                var email = Validare.Obligatoriu(c.Email, "Email", 200).ToLowerInvariant();
                if (!MailAddress.TryCreate(email, out var adresa) || adresa.Address != email) throw new CerereInvalida("Adresa de email nu este validă.");
                mesaj.Email = email;
                if (await db.Set<MesajContact>().CountAsync(f => f.Email == email && f.CreatLa > now.AddDays(-1), ct) >= MaxPeZiEmail)
                    throw new Conflict($"Am primit deja {MaxPeZiEmail} mesaje de la această adresă astăzi. Le citim pe toate; poți scrie din nou mâine.");
            }
            db.Add(mesaj);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).AllowAnonymous().RequireRateLimiting(LimiteCereri.Contact).WithTags("Contact");

        var a = app.MapGroup("/admin/contact").RequireAuthorization(Politici.Admin).WithTags("Contact");
        a.MapGet("", async (int? pagina, bool? rezolvate, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var q = db.Set<MesajContact>().AsNoTracking().Where(f => (f.RezolvatLa != null) == (rezolvate ?? false));
            h.Response.Headers.CacheControl = "no-store";
            return (object)new
            {
                Total = await q.CountAsync(ct),
                Elemente = await q.OrderByDescending(f => f.Id).Pagina(Marime, pagina, 20).Select(f => new
                {
                    f.Id, f.Subiect, f.Text, f.Pagina, f.CreatLa, f.RezolvatLa, Cont = f.UtilizatorId != null,
                    // An account's current name and address (it may have changed them since writing); a visitor's as written.
                    Nume = f.UtilizatorId == null ? f.Nume : db.Utilizatori.Where(u => u.Id == f.UtilizatorId).Select(u => u.Nume).FirstOrDefault(),
                    Email = f.UtilizatorId == null ? f.Email : db.Utilizatori.Where(u => u.Id == f.UtilizatorId).Select(u => u.Email).FirstOrDefault(),
                    Rol = db.Set<UtilizatorRol>().Where(r => r.UtilizatorId == f.UtilizatorId).Select(r => r.Rol.Nume).FirstOrDefault(),
                }).ToListAsync(ct),
            };
        });
        a.MapPost("/{id:int}/rezolvat", (int id, MarkAiDbContext db, TimeProvider ceas, CancellationToken ct) => Marcheaza(db, id, ceas.GetUtcNow().UtcDateTime, ct));
        a.MapPost("/{id:int}/redeschis", (int id, MarkAiDbContext db, CancellationToken ct) => Marcheaza(db, id, null, ct));
    }

    private static async Task<IResult> Marcheaza(MarkAiDbContext db, int id, DateTime? la, CancellationToken ct) =>
        await db.Set<MesajContact>().Where(f => f.Id == id).ExecuteUpdateAsync(s => s.SetProperty(f => f.RezolvatLa, la), ct) == 0
            ? throw new KeyNotFoundException("Mesajul nu există.") : Results.NoContent();

    // Only a path on this site, without its query or fragment (they may hold tokens).
    private static string Pagina(string? pagina)
    {
        var p = (pagina ?? "").Split('?', '#')[0];
        return p.Length > 200 || !p.StartsWith('/') || p.StartsWith("//") ? "" : p;
    }
}

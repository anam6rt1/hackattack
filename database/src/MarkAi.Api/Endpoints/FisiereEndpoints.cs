using MarkAi.Api.Infrastructura;
using MarkAi.Api.Servicii;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Endpoints;

public record FisierDto(int Id, string Nume, string Tip, long MarimeOcteti, DateTime IncarcatLa);
public record FisierEditabil(int Id, string Nume, string Tip, long MarimeOcteti, DateTime IncarcatLa, string StareText, int CaractereText,
    string? IncarcatDe);
public record FisiereEditare(IReadOnlyList<FisierEditabil> Elemente, int MaxMb);

/// <summary>
/// A Lecție's files: at most one PDF, one DOCX and one PPTX. Whoever sees the Lecție (<see cref="AccesContinut"/>) may open
/// them; whoever may edit it uploads, replaces and deletes them. Their text is part of the material the professor generates
/// quiz questions from (only when they ask).
/// </summary>
public static class FisiereEndpoints
{
    public static void Mapeaza(WebApplication app)
    {
        var g = app.MapGroup("").WithTags("Fișiere lecție").RequireAuthorization();

        g.MapGet("/parcursuri/{id:int}/fisiere", async (int id, HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) =>
        {
            await AsiguraVizibilAsync(acces, h, id, ct);
            return Lista<FisierDto>.Din(await db.Set<FisierLectie>().AsNoTracking().Where(f => f.ParcursId == id).OrderBy(f => f.Tip)
                .Select(f => new FisierDto(f.Id, f.Nume, f.Tip, f.MarimeOcteti, f.IncarcatLa)).ToListAsync(ct));
        });

        // PDFs open in the browser unless ?descarca=true; the other types are always downloaded.
        g.MapGet("/fisiere/{id:int}", async (int id, bool? descarca, HttpContext h, MarkAiDbContext db, AccesContinut acces, Stocare stocare, CancellationToken ct) =>
        {
            var f = await db.Set<FisierLectie>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Fișierul nu există.");
            try { await AsiguraVizibilAsync(acces, h, f.ParcursId, ct); }
            catch (KeyNotFoundException) { throw new KeyNotFoundException("Fișierul nu există."); }
            return stocare.ServesteFisierLectie(h, f.Cheie, f.Tip, f.Nume, inline: descarca != true);
        });

        var e = app.MapGroup("").WithTags("Fișiere lecție").RequireAuthorization(Politici.Continut);
        IncarcariReluabile.Mapeaza(e);

        g.MapGet("/fisiere/{id:int}/previzualizare", async (int id, int? foaie, HttpContext h, MarkAiDbContext db, AccesContinut acces,
            Stocare stocare, PrevizualizareOffice office, CancellationToken ct) =>
        {
            var f = await db.Set<FisierLectie>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new KeyNotFoundException("Fișierul nu există.");
            await AsiguraVizibilAsync(acces, h, f.ParcursId, ct);
            var pdf = await office.PdfAsync(stocare.CaleFisier(f.Cheie) ?? throw new KeyNotFoundException("Fișierul nu există."), f.Tip, ct, foaie);
            h.Response.Headers.CacheControl = "no-store";
            return Results.File(pdf, "application/pdf");
        });

        // Temporary validated input, no lesson or stored file changes.
        g.MapGet("/fisiere/{id:int}/foi", async (int id, HttpContext h, MarkAiDbContext db, AccesContinut acces, Stocare stocare, CancellationToken ct) =>
        {
            var f = await db.Set<FisierLectie>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Fișierul nu există.");
            await AsiguraVizibilAsync(acces, h, f.ParcursId, ct);
            if (f.Tip != "xlsx") throw new CerereInvalida("Documentul nu este un Excel.");
            return PrevizualizareOffice.Foi(stocare.CaleFisier(f.Cheie)!);
        });

        e.MapPost("/fisiere/previzualizare", async (int? foaie, bool? foi, HttpContext h, Stocare stocare, PrevizualizareOffice office, CancellationToken ct) =>
        {
            var fisier = (await h.Request.ReadFormAsync(ct)).Files.GetFile("fisier") ?? throw new CerereInvalida("Alege un document.");
            var salvat = await stocare.SalveazaFisierLectieAsync(fisier, ct, deduplica: false);
            try
            {
                if (foi == true && salvat.Tip == "xlsx") return Results.Json(PrevizualizareOffice.Foi(stocare.CaleFisier(salvat.Cheie)!));
                var pdf = await office.PdfAsync(stocare.CaleFisier(salvat.Cheie) ?? throw new KeyNotFoundException(), salvat.Tip, ct, foaie);
                h.Response.Headers.CacheControl = "no-store";
                return Results.File(pdf, "application/pdf");
            }
            finally { stocare.Sterge(salvat.Cheie); }
        }).DisableAntiforgery().AddEndpointFilter(LimitaMarime);

        e.MapGet("/continut/parcursuri/{id:int}/fisiere", async (int id, HttpContext h, MarkAiDbContext db, AccesContinut acces, Stocare stocare,
            CancellationToken ct) =>
        {
            await acces.PentruEditareAsync(h.User, id, ct);
            return await PentruEditare(db, stocare, id, ct);
        });

        // One file per request. A file of a type the Lecție already has replaces it only with ?inlocuieste=true
        // (the editor asks first); otherwise 409, so nothing is replaced by mistake.
        e.MapPost("/parcursuri/{id:int}/fisiere", async (int id, bool? inlocuieste, HttpContext h, MarkAiDbContext db, AccesContinut acces, Stocare stocare,
            ProcesorFisiere procesor, CancellationToken ct) =>
        {
            await acces.PentruEditareAsync(h.User, id, ct);
            var fisier = (await h.Request.ReadFormAsync(ct)).Files.GetFile("fisier") ?? throw new CerereInvalida("Alege un fișier PDF, DOCX, PPTX sau XLSX.");
            return await SalveazaAsync(id, inlocuieste, fisier, h, db, acces, stocare, procesor, ct);
        }).DisableAntiforgery().AddEndpointFilter(LimitaMarime);

        e.MapDelete("/fisiere/{id:int}", async (int id, HttpContext h, MarkAiDbContext db, AccesContinut acces, Stocare stocare, CancellationToken ct) =>
        {
            var f = await db.Set<FisierLectie>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Fișierul nu există.");
            await acces.PentruEditareAsync(h.User, f.ParcursId, ct);
            db.Remove(f);
            await db.SaveChangesAsync(ct);
            stocare.Sterge(f.Cheie);
            return Results.NoContent();
        });
    }

    internal static async Task<IResult> SalveazaAsync(int id, bool? inlocuieste, IFormFile fisier, HttpContext h, MarkAiDbContext db, AccesContinut acces, Stocare stocare, ProcesorFisiere procesor, CancellationToken ct)
    {
        await acces.PentruEditareAsync(h.User, id, ct);
            var tip = Path.GetExtension(fisier.FileName).TrimStart('.').ToLowerInvariant();
            if (inlocuieste != true && await db.Set<FisierLectie>().AnyAsync(f => f.ParcursId == id && f.Tip == tip, ct))
                throw new Conflict(ExistaDeja(tip));

            var salvat = await stocare.SalveazaFisierLectieAsync(fisier, ct);
            string? inlocuit = null;
            try
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                // Locks the Lecție, so two uploads of the same type at once cannot both pass the check.
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM mark_ai.parcurs WHERE id = {id} FOR UPDATE", ct);
                var vechi = await db.Set<FisierLectie>().SingleOrDefaultAsync(f => f.ParcursId == id && f.Tip == salvat.Tip, ct);
                if (vechi is not null)
                {
                    if (inlocuieste != true) throw new Conflict(ExistaDeja(salvat.Tip));
                    inlocuit = vechi.Cheie;
                    db.Remove(vechi);
                    await db.SaveChangesAsync(ct);
                }
                var f = new FisierLectie
                {
                    ParcursId = id, Nume = NumeCurat(fisier.FileName, salvat.Tip), Tip = salvat.Tip, Cheie = salvat.Cheie, MarimeOcteti = salvat.Marime,
                    Sha256 = salvat.Sha256, IncarcatDeId = h.User.Id(), IncarcatLa = DateTime.UtcNow,
                };
                db.Add(f);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                stocare.Sterge(inlocuit);
                procesor.Extrage(f.Id);
                return Results.Created($"/fisiere/{f.Id}", (await PentruEditare(db, stocare, id, ct)).Elemente.Single(x => x.Id == f.Id));
            }
            catch
            {
                stocare.Sterge(salvat.Cheie);
                throw;
            }
    }

    /// <summary>
    /// The upload's size limit follows <c>Fisiere:MaxMb</c> (plus 1 MB of multipart framing). A request that says up front it is
    /// larger gets a 413 with a message. A client only sees that answer once it has finished sending, so a body up to 4 times the
    /// limit is read and thrown away first; a larger one is cut off (the client then sees a network error instead).
    /// </summary>
    private static async ValueTask<object?> LimitaMarime(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var h = context.HttpContext;
        var maxMb = h.RequestServices.GetRequiredService<Stocare>().MaxFisierLectieMb;
        var limita = (maxMb + 1L) * 1024 * 1024;
        var marime = h.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } f ? f : null;
        if (h.Request.ContentLength is { } lungime && lungime > limita)
        {
            if (lungime <= 4 * limita && marime is not null)
            {
                marime.MaxRequestBodySize = lungime;
                await h.Request.Body.CopyToAsync(Stream.Null, h.RequestAborted);
            }
            return Results.Json(new { mesaj = $"Fișierul trebuie să aibă cel mult {maxMb} MB." }, statusCode: StatusCodes.Status413PayloadTooLarge);
        }
        if (marime is not null) marime.MaxRequestBodySize = limita;
        return await next(context);
    }

    private static string ExistaDeja(string tip) =>
        $"Lecția are deja un fișier {tip.ToUpperInvariant()}. Confirmă înlocuirea lui sau șterge-l înainte.";

    // The Lecție, if the user may see it (published, or editable by them); otherwise 404.
    private static async Task AsiguraVizibilAsync(AccesContinut acces, HttpContext h, int parcursId, CancellationToken ct)
    {
        var a = await acces.PentruAsync(h.User, ct);
        if (!await acces.Parcursuri(a).AnyAsync(p => p.Id == parcursId, ct)) throw new KeyNotFoundException("Lecția nu există.");
    }

    private static async Task<FisiereEditare> PentruEditare(MarkAiDbContext db, Stocare stocare, int parcursId, CancellationToken ct)
    {
        var lista = await db.Set<FisierLectie>().AsNoTracking().Where(f => f.ParcursId == parcursId).OrderBy(f => f.Tip)
            .Select(f => new FisierEditabil(f.Id, f.Nume, f.Tip, f.MarimeOcteti, f.IncarcatLa, f.StareText, f.TextExtras == null ? 0 : f.TextExtras.Length,
                f.IncarcatDe == null ? null : f.IncarcatDe.Nume))
            .ToListAsync(ct);
        return new FisiereEditare(lista, stocare.MaxFisierLectieMb);
    }

    /// <summary>The uploader's file name without folders, control or reserved characters, at most 120 characters, with its real extension.</summary>
    internal static string NumeCurat(string? original, string tip)
    {
        var baza = Path.GetFileNameWithoutExtension((original ?? "").Replace('\\', '/').Split('/')[^1]);
        var interzise = Path.GetInvalidFileNameChars().Concat(['"', '<', '>', '|', ':', '*', '?']).ToHashSet();
        var curat = string.Join(' ', new string(baza.Select(c => char.IsControl(c) || interzise.Contains(c) ? ' ' : c).ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim('.', ' ');
        if (curat.Length > 120) curat = curat[..120].TrimEnd();
        return (curat.Length == 0 ? "document" : curat) + "." + tip;
    }
}

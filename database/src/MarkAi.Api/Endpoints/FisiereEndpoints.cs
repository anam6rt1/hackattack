using MarkAi.Api.Infrastructura;
using MarkAi.Api.Servicii;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Endpoints;

public record FisierDto(int Id, string Nume, string Tip, long MarimeOcteti, DateTime IncarcatLa);
public record FisierEditabil(int Id, string Nume, string Tip, long MarimeOcteti, DateTime IncarcatLa, string StareText, int CaractereText,
    string? IncarcatDe);
public record FisiereEditare(IReadOnlyList<FisierEditabil> Elemente, int Maxim, int MaxMb, bool RegenerareProgramata);

/// <summary>
/// Chapter files (PDF, DOCX, PPTX). Whoever sees the chapter (<see cref="AccesContinut"/>) may open its files; whoever may edit
/// it uploads and deletes them. Their text feeds AI question generation through <see cref="ProcesorFisiere"/>.
/// </summary>
public static class FisiereEndpoints
{
    public static void Mapeaza(WebApplication app)
    {
        var g = app.MapGroup("").WithTags("Fișiere capitol").RequireAuthorization();

        g.MapGet("/cursuri/{id:int}/fisiere", async (int id, HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct) =>
        {
            await acces.AsiguraCapitolVizibilAsync(h.User, id, ct);
            return Lista<FisierDto>.Din(await db.Set<FisierCapitol>().AsNoTracking().Where(f => f.CursId == id).OrderBy(f => f.Id)
                .Select(f => new FisierDto(f.Id, f.Nume, f.Tip, f.MarimeOcteti, f.IncarcatLa)).ToListAsync(ct));
        });

        // PDFs open in the browser unless ?descarca=true; the other types are always downloaded.
        g.MapGet("/fisiere/{id:int}", async (int id, bool? descarca, HttpContext h, MarkAiDbContext db, AccesContinut acces, Stocare stocare, CancellationToken ct) =>
        {
            var f = await db.Set<FisierCapitol>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Fișierul nu există.");
            try { await acces.AsiguraCapitolVizibilAsync(h.User, f.CursId, ct); }
            catch (KeyNotFoundException) { throw new KeyNotFoundException("Fișierul nu există."); }
            return stocare.ServesteFisierCapitol(h, f.Cheie, f.Tip, f.Nume, inline: descarca != true);
        });

        var e = app.MapGroup("").WithTags("Fișiere capitol").RequireAuthorization(Politici.Continut);

        e.MapGet("/continut/cursuri/{id:int}/fisiere", async (int id, HttpContext h, MarkAiDbContext db, AccesContinut acces, ProcesorFisiere procesor,
            IConfiguration cfg, CancellationToken ct) =>
        {
            await acces.CapitolPentruEditareAsync(h.User, id, ct);
            return await PentruEditare(db, procesor, cfg, id, ct);
        });

        // One file per request (the editor uploads several one after another, each with its own progress and error).
        e.MapPost("/cursuri/{id:int}/fisiere", async (int id, IFormFile? fisier, HttpContext h, MarkAiDbContext db, AccesContinut acces, Stocare stocare,
            ProcesorFisiere procesor, IConfiguration cfg, CancellationToken ct) =>
        {
            await acces.CapitolPentruEditareAsync(h.User, id, ct);
            if (fisier is null) throw new CerereInvalida("Alege un fișier PDF, DOCX sau PPTX.");
            var maxim = Maxim(cfg);
            if (await db.Set<FisierCapitol>().CountAsync(f => f.CursId == id, ct) >= maxim) throw new Conflict(PreaMulte(maxim));

            var salvat = await stocare.SalveazaFisierCapitolAsync(fisier, ct);
            try
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                // Locks the chapter, so two uploads at once cannot both pass the count.
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM mark_ai.curs WHERE id = {id} FOR UPDATE", ct);
                if (await db.Set<FisierCapitol>().CountAsync(f => f.CursId == id, ct) >= maxim) throw new Conflict(PreaMulte(maxim));
                var f = new FisierCapitol
                {
                    CursId = id, Nume = NumeCurat(fisier.FileName, salvat.Tip), Tip = salvat.Tip, Cheie = salvat.Cheie, MarimeOcteti = salvat.Marime,
                    Sha256 = salvat.Sha256, IncarcatDeId = h.User.Id(), IncarcatLa = DateTime.UtcNow,
                };
                db.Add(f);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                procesor.Extrage(f.Id);
                return Results.Created($"/fisiere/{f.Id}", (await PentruEditare(db, procesor, cfg, id, ct)).Elemente.Single(x => x.Id == f.Id));
            }
            catch
            {
                stocare.Sterge(salvat.Cheie);
                throw;
            }
        }).DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute((Stocare.MaxFisierCapitolMb + 1) * 1024 * 1024));

        e.MapDelete("/fisiere/{id:int}", async (int id, HttpContext h, MarkAiDbContext db, AccesContinut acces, Stocare stocare, ProcesorFisiere procesor,
            CancellationToken ct) =>
        {
            var f = await db.Set<FisierCapitol>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Fișierul nu există.");
            await acces.CapitolPentruEditareAsync(h.User, f.CursId, ct);
            db.Remove(f);
            await db.SaveChangesAsync(ct);
            stocare.Sterge(f.Cheie);
            // The questions came partly from this text; regenerate them without it.
            if (f.StareText == FisierCapitol.Gata) procesor.ProgrameazaRegenerare(f.CursId, h.User.Id());
            return Results.NoContent();
        });
    }

    private static int Maxim(IConfiguration cfg) => cfg.GetValue("Fisiere:MaxPeCapitol", 10);

    private static string PreaMulte(int maxim) => $"Un capitol poate avea cel mult {maxim} fișiere. Șterge unul înainte să adaugi altul.";

    private static async Task<FisiereEditare> PentruEditare(MarkAiDbContext db, ProcesorFisiere procesor, IConfiguration cfg, int cursId, CancellationToken ct)
    {
        var lista = await db.Set<FisierCapitol>().AsNoTracking().Where(f => f.CursId == cursId).OrderBy(f => f.Id)
            .Select(f => new FisierEditabil(f.Id, f.Nume, f.Tip, f.MarimeOcteti, f.IncarcatLa, f.StareText, f.TextExtras == null ? 0 : f.TextExtras.Length,
                f.IncarcatDe == null ? null : f.IncarcatDe.Nume))
            .ToListAsync(ct);
        return new FisiereEditare(lista, Maxim(cfg), Stocare.MaxFisierCapitolMb, procesor.AreRegenerareProgramata(cursId));
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

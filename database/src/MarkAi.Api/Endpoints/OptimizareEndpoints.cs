using MarkAi.Api.Servicii;

namespace MarkAi.Api.Endpoints;

/// <summary>Client-side processing policy. Bytes are processed in the browser, never in this endpoint.</summary>
public static class OptimizareEndpoints
{
    public static void Mapeaza(WebApplication app)
    {
        app.MapGet("/fisiere/optimizare/config", (Stocare stocare) => Results.Ok(new
        {
            versiune = 1,
            procesare = "browser",
            documente = new { extensii = new[] { "pdf", "docx", "pptx", "xlsx" }, maxUploadOcteti = stocare.MaxFisierLectieMb * 1024L * 1024,
                maxProcesareOcteti = 20 * 1024 * 1024, pragMinimOcteti = 32 * 1024, economieMinimaProcent = 5, faraPierderi = true, pastreazaSemnaturile = true },
            fotografii = new { extensii = new[] { "jpg", "jpeg", "png", "webp" }, maxUploadOcteti = 5 * 1024 * 1024,
                maxProcesareOcteti = 16 * 1024 * 1024, maxPixeli = 24_000_000, pragMinimOcteti = 128 * 1024,
                laturaMaxima = 2048, format = "image/webp", calitate = 0.88, economieMinimaProcent = 5 },
            incarcare = new { bucataOcteti = IncarcariReluabile.Bucata, reluabila = true, expirareOre = 24,
                endpoint = "/parcursuri/{id}/incarcari", uploadDirect = "/parcursuri/{id}/fisiere",
                campFisier = "fisier", validarePeServer = true },
        })).WithTags("Fișiere lecție").AllowAnonymous();
    }
}

using MarkAi.Data;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Servicii;

/// <summary>A Romanian Twilio text-to-speech voice the phone tutor can use.</summary>
public record VoceTutor(string Cod, string Nume, string Twilio, string Descriere);

/// <summary>How the tutor speaks: voice, rate (% of normal) and pitch shift (%).</summary>
public record SetariVoce(string Voce, int Viteza, int Ton)
{
    public const int VitezaMin = 70, VitezaMax = 130, TonMin = -20, TonMax = 20;

    public SetariVoce Normalizat() => new(VociTutor.Gaseste(Voce).Cod, Math.Clamp(Viteza, VitezaMin, VitezaMax), Math.Clamp(Ton, TonMin, TonMax));
}

/// <summary>
/// Twilio's built-in Romanian voices. All of them are female; rate and pitch (SSML prosody) give the variety,
/// e.g. a slower, slightly lower voice for "like a grandma".
/// </summary>
public static class VociTutor
{
    public static readonly IReadOnlyList<VoceTutor> Toate =
    [
        new("ioana", "Ioana", "Google.ro-RO-Wavenet-B", "cea mai naturală, caldă"),
        new("maria", "Maria", "Google.ro-RO-Standard-B", "clară, puțin mai neutră"),
        new("carmen", "Carmen", "Polly.Carmen", "mai mecanică, foarte clară"),
    ];

    public static readonly SetariVoce Implicit = new("ioana", 100, 0);

    public static VoceTutor Gaseste(string? cod) => Toate.FirstOrDefault(v => v.Cod == cod) ?? Toate[0];

    public static async Task<SetariVoce> CitesteAsync(MarkAiDbContext db, int utilizatorId, CancellationToken ct) =>
        await db.PreferinteUtilizatori.AsNoTracking().Where(p => p.UtilizatorId == utilizatorId)
            .Select(p => new SetariVoce(p.VoceTutor ?? Implicit.Voce, p.VitezaTutor, p.TonTutor)).SingleOrDefaultAsync(ct)
        ?? Implicit;

    public static async Task SalveazaAsync(MarkAiDbContext db, int utilizatorId, SetariVoce setari, CancellationToken ct)
    {
        setari = setari.Normalizat();
        var p = await db.PreferinteUtilizatori.SingleOrDefaultAsync(x => x.UtilizatorId == utilizatorId, ct);
        if (p is null)
            db.PreferinteUtilizatori.Add(p = new PreferinteUtilizator { UtilizatorId = utilizatorId, ModBackground = "auto", Timezone = "Europe/Bucharest" });
        p.VoceTutor = setari.Voce;
        p.VitezaTutor = setari.Viteza;
        p.TonTutor = setari.Ton;
        p.ActualizatLa = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}

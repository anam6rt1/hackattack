using MarkAi.Data;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Infrastructura;

/// <summary>Where the signed-in user is (from Settings), for "near me"; both null for anonymous users or when not set.</summary>
public sealed record LocUtilizator(int? JudetId, int? LocalitateId)
{
    public static readonly LocUtilizator Necunoscut = new(null, null);

    public static async Task<LocUtilizator> PentruAsync(MarkAiDbContext db, int uid, CancellationToken ct) =>
        uid == 0 ? Necunoscut
            : await db.Utilizatori.Where(u => u.Id == uid).Select(u => new LocUtilizator(u.JudetId, u.LocalitateId)).SingleOrDefaultAsync(ct) ?? Necunoscut;

    /// <summary>The county for a "local" filter; refused with a pointer to Settings when the user has not set one.</summary>
    public int JudetObligatoriu => JudetId ?? throw new CerereInvalida("Alege-ți județul în Setări → „Organizație și loc” ca să vezi ce e aproape de tine.");
}

/// <summary>
/// The filters shared by the Lecții list and the search: <c>toate</c>, <c>organizatia-mea</c> (home or shared with one of the
/// user's organisations), <c>public</c>, <c>local</c> (the home organisation is in the user's county; platform Lecții have
/// no place). Visibility itself is <see cref="AccesContinut"/>'s; these only narrow what is already visible.
/// </summary>
public static class FiltreLectii
{
    public const string Mesaj = "Filtrul trebuie să fie „toate”, „organizatia-mea”, „public” sau „local”.";

    public static IQueryable<Parcurs> Aplica(IQueryable<Parcurs> q, ContextAcces a, LocUtilizator loc, string? filtru, string? clasa)
    {
        var clase = (clasa ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (clase.Any(c => !Parcurs.Clase.Contains(c))) throw new CerereInvalida("Clasa trebuie să fie IX, X, XI, XII sau Bac.");
        if (clase.Length > 0) q = q.Where(p => clase.Contains(p.Clasa));
        var org = a.Organizatii;
        return filtru switch
        {
            null or "" or "toate" => q,
            "organizatia-mea" => q.Where(p => (p.OrganizatieId != null && org.Contains(p.OrganizatieId.Value)) || p.Partajari.Any(s => org.Contains(s.OrganizatieId))),
            "public" => q.Where(p => p.Vizibilitate == Parcurs.Public),
            "local" => FiltreazaLocal(q, loc.JudetObligatoriu),
            _ => throw new CerereInvalida(Mesaj),
        };
    }

    private static IQueryable<Parcurs> FiltreazaLocal(IQueryable<Parcurs> q, int judet) =>
        q.Where(p => p.Organizatie != null && p.Organizatie.JudetId == judet);
}

/// <summary>Search terms: the query normalised (<see cref="CautareText"/>), split into words, each escaped for LIKE.</summary>
public static class TermeniCautare
{
    public static string[] Din(string? q)
    {
        var text = CautareText.Normalizeaza((q ?? "").Trim());
        if (text.Length < 2) throw new CerereInvalida("Scrie cel puțin două litere.");
        if (text.Length > 100) throw new CerereInvalida("Căutarea poate avea cel mult 100 de caractere.");
        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().Take(6)
            .Select(t => "%" + t.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%").ToArray();
    }
}

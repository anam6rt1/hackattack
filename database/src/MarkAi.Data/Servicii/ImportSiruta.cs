using System.Globalization;
using System.Text;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Data.Servicii;

/// <summary>
/// Loads counties and localities from the official SIRUTA register (INS, CSV with ';'): level 1 = counties,
/// level 2 = municipalities/towns/communes, level 3 = localities. Names are converted from upper case with cedillas
/// ("BĂRĂBANŢ") to "Bărăbanț". Re-running it updates names and adds new rows; nothing is deleted (accounts point at them).
/// </summary>
public static class ImportSiruta
{
    private static readonly CultureInfo Ro = CultureInfo.GetCultureInfo("ro-RO");
    private static readonly HashSet<string> CuvinteMici = ["de", "din", "la", "lui", "cu", "pe", "sub", "peste", "în", "si", "și"];

    public static async Task<(int Judete, int Localitati)> ImportaAsync(MarkAiDbContext db, string caleCsv, CancellationToken ct = default)
    {
        var randuri = (await File.ReadAllLinesAsync(caleCsv, Encoding.UTF8, ct)).Skip(1).Where(l => l.Length > 0)
            .Select(l => l.Split(';')).Where(c => c.Length >= 8).ToList();
        int Col(string[] c, int i) => int.Parse(c[i], CultureInfo.InvariantCulture);
        // Columns: SIRUTA;DENLOC;CODP;JUD;SIRSUP;TIP;NIV;MED;...
        var judete = randuri.Where(c => c[6] == "1").ToDictionary(c => Col(c, 3), c => NumeJudet(c[1]));
        var uat = randuri.Where(c => c[6] == "2").ToDictionary(c => Col(c, 0), c => NumeUat(c[1], c[5]));
        var localitati = randuri.Where(c => c[6] == "3").Select(c => new Localitate
        {
            Id = Col(c, 0), JudetId = Col(c, 3), Nume = NumeLocalitate(c[1]), UatId = Col(c, 4),
            UatNume = uat.GetValueOrDefault(Col(c, 4), ""), Urban = c[7] == "1",
        }).ToList();
        foreach (var l in localitati) l.Cautare = PentruCautare(l.Nume);

        var existenteJ = await db.Set<Judet>().ToDictionaryAsync(j => j.Id, ct);
        foreach (var (id, nume) in judete)
            if (existenteJ.TryGetValue(id, out var j)) j.Nume = nume; else db.Add(new Judet { Id = id, Nume = nume });
        await db.SaveChangesAsync(ct);

        var existenteL = await db.Set<Localitate>().ToDictionaryAsync(l => l.Id, ct);
        foreach (var l in localitati)
        {
            if (existenteL.TryGetValue(l.Id, out var e)) (e.JudetId, e.Nume, e.UatId, e.UatNume, e.Urban, e.Cautare) = (l.JudetId, l.Nume, l.UatId, l.UatNume, l.Urban, l.Cautare);
            else db.Add(l);
        }
        await db.SaveChangesAsync(ct);
        return (judete.Count, localitati.Count);
    }

    /// <summary>Lower case without diacritics (also the old cedilla letters), for "starts with"/"contains" search.</summary>
    public static string PentruCautare(string s)
    {
        var d = s.ToLowerInvariant().Replace('-', ' ').Normalize(NormalizationForm.FormD);
        return new string(d.Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray());
    }

    private static string NumeJudet(string s) => Titlu(s.Replace("JUDEŢUL ", "").Replace("JUDEȚUL ", "").Replace("MUNICIPIUL ", ""));

    private static string NumeUat(string s, string tip) => tip switch
    {
        "1" or "4" or "9" => "Municipiul " + Titlu(s.Replace("MUNICIPIUL ", "")),
        "2" or "5" => "Orașul " + Titlu(s.Replace("ORAŞ ", "").Replace("ORAȘ ", "")),
        _ => "Comuna " + Titlu(s),
    };

    // Bucharest's sectors are listed as "BUCUREŞTI SECTORUL 1".
    private static string NumeLocalitate(string s) => Titlu(s.Replace("BUCUREŞTI SECTORUL", "SECTORUL").Replace("BUCUREȘTI SECTORUL", "SECTORUL"));

    /// <summary>"BAIA DE ARIEŞ" → "Baia de Arieș", "I.C.BRĂTIANU" → "I.C.Brătianu", "CAROL I" → "Carol I".</summary>
    public static string Titlu(string s)
    {
        s = s.Trim().Replace('Ş', 'Ș').Replace('Ţ', 'Ț').Replace('ş', 'ș').Replace('ţ', 'ț');
        var cuvinte = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', cuvinte.Select((c, i) =>
        {
            if (c.All(ch => ch is 'I' or 'V' or 'X') && i > 0) return c;
            var mic = c.ToLower(Ro);
            if (i > 0 && CuvinteMici.Contains(mic)) return mic;
            var sb = new StringBuilder(mic);
            for (var k = 0; k < sb.Length; k++)
                if (k == 0 || sb[k - 1] is '-' or '.') sb[k] = char.ToUpper(sb[k], Ro);
            return sb.ToString();
        }));
    }
}

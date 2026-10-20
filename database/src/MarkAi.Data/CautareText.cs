using System.Globalization;
using System.Text;

namespace MarkAi.Data;

/// <summary>
/// Text for diacritic-insensitive search: lower case, without diacritics (ș/ț with comma or cedilla alike), "-" as a space.
/// The database computes the <c>cautare</c> columns with <see cref="Sql"/>; queries are normalised with <see cref="Normalizeaza"/>,
/// so "sarpe" finds "Șarpe". Letters are translated before <c>lower()</c>, which only handles ASCII under some collations.
/// </summary>
public static class CautareText
{
    private const string De = "ĂÂÎȘŞȚŢăâîșşțţÁÀÄÉÈËÍÌÏÓÒÖÚÙÜÇÑáàäéèëíìïóòöúùüçñ-";
    private const string La = "aaissttaaissttaaaeeeiiiooouuucnaaaeeeiiiooouuucn ";

    /// <summary>The SQL expression for a stored generated column over <paramref name="expresie"/>.</summary>
    public static string Sql(string expresie) => $"lower(translate({expresie}, '{De}', '{La}'))";

    public static string Normalizeaza(string s)
    {
        var d = s.ToLowerInvariant().Replace('-', ' ').Normalize(NormalizationForm.FormD);
        return new string(d.Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray()).Normalize(NormalizationForm.FormC);
    }
}

using MarkAi.Data;

namespace MarkAi.Api.Servicii;

/// <summary>
/// Checks on the questions the AI model proposes, before they are added to a Lecție's quiz: a question about the document itself
/// ("Cum se numește capitolul…", "conform materialului…") instead of the subject is dropped, and so is one too close to a question
/// the Lecție already has or to one earlier in the same batch (the same idea asked again with other words).
/// </summary>
public static class CalitateIntrebari
{
    /// <summary>Two questions sharing at least this share of their content words count as the same question.</summary>
    public const double PragAsemanare = 0.6;

    // Phrases that point at the material given to the model (notes, files, chapter titles), not at the subject. Matched as whole
    // words on the normalised text (lower case, without diacritics, punctuation as spaces): "în materialul genetic" (biology)
    // or "scrie în fișier" (computer science) are real subject questions and do not match.
    private static readonly string[] DespreDocument =
    [
        "din material", "in material", "materialul primit", "materialul de mai sus", "materialul dat", "materialului primit",
        "conform materialului", "potrivit materialului", "mentionat in material", "prezentat in material", "din notite", "in notite",
        "conform notitelor", "fisierul primit", "fisierul atasat", "documentul primit", "textul de mai sus", "textul dat",
        "acest capitol", "capitolul acesta", "capitolul de mai sus", "cum se numeste capitolul", "titlul capitolului",
        "aceasta lectie", "lectia aceasta", "lectia de mai sus",
    ];

    // Small words that say nothing about what a question asks.
    private static readonly HashSet<string> Marunte =
    [
        "care", "este", "sunt", "fost", "din", "pentru", "prin", "despre", "unde", "cand", "cine", "cum", "cat", "cate", "cati", "ce",
        "una", "unei", "unui", "unor", "lui", "lor", "sau", "dar", "iar", "mai", "foarte", "acest", "aceasta", "acesta", "aceste",
        "acestei", "acestui", "intre", "dupa", "inainte", "asupra", "catre", "peste", "fara", "decat", "cel", "cea", "cei", "cele",
        "urmatoarele", "urmatorul", "urmatoarea", "afirmatii", "afirmatia", "corecta", "corect", "adevarata", "variante", "varianta",
        "numeste", "numesc", "reprezinta", "avea", "are", "au", "fi", "poate", "pot",
    ];

    private static string[] Vorbe(string text) =>
        new string(CautareText.Normalizeaza(text).Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray()).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    public static bool EsteDespreDocument(string text)
    {
        var t = " " + string.Join(' ', Vorbe(text)) + " ";
        return DespreDocument.Any(f => t.Contains(" " + f + " "));
    }

    /// <summary>The content words of a question: normalised, without punctuation and small words.</summary>
    public static HashSet<string> Cuvinte(string text) => new(Vorbe(text).Where(w => w.Length >= 3 && !Marunte.Contains(w)));

    /// <summary>How much two questions overlap (Jaccard on their content words, 0–1).</summary>
    public static double Asemanare(string a, string b) => Asemanare(Cuvinte(a), Cuvinte(b));

    private static double Asemanare(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0;
        var comune = a.Count(b.Contains);
        return (double)comune / (a.Count + b.Count - comune);
    }

    /// <summary>
    /// Keeps the candidates (in order, at most <paramref name="maxim"/>) that are about the subject and not too close to an
    /// existing question or to one already kept; says how many were dropped for each reason.
    /// </summary>
    public static (List<T> Pastrate, int Asemanatoare, int DespreDocument) Filtreaza<T>(IEnumerable<T> candidati, Func<T, string> text,
        IEnumerable<string> existente, int maxim)
    {
        var vazute = existente.Select(Cuvinte).Where(c => c.Count > 0).ToList();
        var pastrate = new List<T>();
        int asemanatoare = 0, despreDocument = 0;
        foreach (var q in candidati)
        {
            if (pastrate.Count == maxim) break;
            var t = text(q);
            if (EsteDespreDocument(t)) { despreDocument++; continue; }
            var c = Cuvinte(t);
            if (vazute.Any(v => Asemanare(v, c) >= PragAsemanare)) { asemanatoare++; continue; }
            vazute.Add(c);
            pastrate.Add(q);
        }
        return (pastrate, asemanatoare, despreDocument);
    }
}

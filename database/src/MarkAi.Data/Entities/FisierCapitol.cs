namespace MarkAi.Data.Entities;

/// <summary>
/// A document attached to a chapter (PDF, DOCX or PPTX). The file itself lives in the API's storage under <see cref="Cheie"/>;
/// its text, once extracted in the background, is added to the chapter's material for AI question generation.
/// </summary>
public class FisierCapitol
{
    public const string Pdf = "pdf", Docx = "docx", Pptx = "pptx";
    public static readonly string[] Tipuri = [Pdf, Docx, Pptx];

    /// <summary>Text extraction: queued, done, no text found (e.g. a scanned PDF), or failed.</summary>
    public const string InAsteptare = "in_asteptare", Gata = "gata", FaraText = "fara_text", Eroare = "eroare";
    public static readonly string[] StariText = [InAsteptare, Gata, FaraText, Eroare];

    public int Id { get; set; }

    public int CursId { get; set; }

    /// <summary>The uploader's file name, cleaned up; shown to students and used as the download name.</summary>
    public string Nume { get; set; } = null!;

    public string Tip { get; set; } = null!;

    /// <summary>Storage key (random name), never shown to clients.</summary>
    public string Cheie { get; set; } = null!;

    public long MarimeOcteti { get; set; }

    /// <summary>SHA-256 of the content, hex.</summary>
    public string Sha256 { get; set; } = null!;

    public string StareText { get; set; } = InAsteptare;

    public string? TextExtras { get; set; }

    /// <summary>Null once the uploader's account is deleted; the file stays with its chapter.</summary>
    public int? IncarcatDeId { get; set; }

    public DateTime IncarcatLa { get; set; }

    public virtual Curs Curs { get; set; } = null!;

    public virtual Utilizator? IncarcatDe { get; set; }
}

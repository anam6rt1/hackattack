namespace MarkAi.Data.Entities;

/// <summary>
/// A document attached to a Lecție: at most one of each type (PDF, DOCX, PPTX); a new upload of the same type replaces it.
/// The file itself lives in the API's storage under <see cref="Cheie"/>; its text, once extracted in the background, is part
/// of the material the professor generates quiz questions from.
/// </summary>
public class FisierLectie
{
    public const string Pdf = "pdf", Docx = "docx", Pptx = "pptx";
    public static readonly string[] Tipuri = [Pdf, Docx, Pptx];

    /// <summary>Text extraction: queued, done, no text found (e.g. a scanned PDF), or failed.</summary>
    public const string InAsteptare = "in_asteptare", Gata = "gata", FaraText = "fara_text", Eroare = "eroare";
    public static readonly string[] StariText = [InAsteptare, Gata, FaraText, Eroare];

    public int Id { get; set; }

    public int ParcursId { get; set; }

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

    /// <summary>Null once the uploader's account is deleted; the file stays with its Lecție.</summary>
    public int? IncarcatDeId { get; set; }

    public DateTime IncarcatLa { get; set; }

    public virtual Parcurs Parcurs { get; set; } = null!;

    public virtual Utilizator? IncarcatDe { get; set; }
}

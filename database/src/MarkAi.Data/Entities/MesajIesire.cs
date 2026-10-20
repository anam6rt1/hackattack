namespace MarkAi.Data.Entities;

/// <summary>
/// Every email or SMS the app sends: notifications are queued here and sent by a background job (with retries),
/// codes are sent at once and recorded. The body is kept only while unsent, or when the "jurnal" (development)
/// provider handled it, so codes do not stay readable in the database once they reached a real inbox.
/// </summary>
public class MesajIesire
{
    public int Id { get; set; }

    /// <summary>"email" or "sms".</summary>
    public string Canal { get; set; } = null!;

    public string Catre { get; set; } = null!;

    public string? Subiect { get; set; }

    public string? Html { get; set; }

    public string? Text { get; set; }

    /// <summary>What the message is for (e.g. "confirmare_email", "avertizare_stergere"), for the admin list.</summary>
    public string Tip { get; set; } = null!;

    public DateTime CreatLa { get; set; }

    public DateTime? TrimisLa { get; set; }

    /// <summary>The provider that sent it ("smtp", "twilio", "jurnal").</summary>
    public string? Furnizor { get; set; }

    public int Incercari { get; set; }

    public DateTime UrmatoareaIncercareLa { get; set; }

    public string? Eroare { get; set; }
}

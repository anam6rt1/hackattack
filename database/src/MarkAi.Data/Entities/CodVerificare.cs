namespace MarkAi.Data.Entities;

/// <summary>
/// A one-time code sent by email or SMS (email confirmation, phone verification). Only a keyed hash of the code is stored;
/// a code stops working when it expires, is used, or after too many wrong attempts.
/// </summary>
public class CodVerificare
{
    public int Id { get; set; }

    public int UtilizatorId { get; set; }

    /// <summary>"email" (confirming the account's email) or "telefon" (verifying a phone number).</summary>
    public string Scop { get; set; } = null!;

    /// <summary>Where the code went (the email address or phone number it confirms).</summary>
    public string Destinatie { get; set; } = null!;

    public string HashCod { get; set; } = null!;

    public int Incercari { get; set; }

    public DateTime CreatLa { get; set; }

    public DateTime ExpiraLa { get; set; }

    public DateTime? FolositLa { get; set; }
}

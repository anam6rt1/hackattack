namespace MarkAi.Data.Entities;

/// <summary>
/// A message sent from the contact page to the platform's administrators. A signed-in sender is the account
/// (<see cref="UtilizatorId"/>; name and email are read from it, not copied); a visitor without an account leaves
/// <see cref="Nume"/> and <see cref="Email"/> so the administrator can answer.
/// </summary>
public class MesajContact
{
    public const string Problema = "problema", Cont = "cont", Organizatii = "organizatii", Sugestie = "sugestie", Altceva = "altceva";
    public static readonly string[] Subiecte = [Problema, Cont, Organizatii, Sugestie, Altceva];

    public int Id { get; set; }
    public int? UtilizatorId { get; set; }
    public string? Nume { get; set; }
    public string? Email { get; set; }
    public string Subiect { get; set; } = Altceva;
    public string Text { get; set; } = "";
    /// <summary>The page the sender came from (path only), when the contact page was opened from one.</summary>
    public string Pagina { get; set; } = "";
    public DateTime CreatLa { get; set; }
    public DateTime? RezolvatLa { get; set; }
}

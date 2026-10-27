namespace MarkAi.Data.Entities;

/// <summary>
/// An action done offline in the desktop/mobile app and sent later (POST /sincronizare). The client numbers each one with its own
/// id, so sending the same batch twice (a lost connection mid-reply) applies it once; the stored result answers the repeat.
/// </summary>
public class OperatieSincronizata
{
    public int UtilizatorId { get; set; }
    public Guid IdClient { get; set; }
    public string Tip { get; set; } = "";
    public DateTime AplicataLa { get; set; }
    public string RezultatJson { get; set; } = "{}";
}

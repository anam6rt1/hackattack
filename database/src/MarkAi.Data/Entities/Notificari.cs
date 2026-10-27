namespace MarkAi.Data.Entities;

public class PreferinteNotificari
{
    public int UtilizatorId { get; set; }
    public bool Sedinte { get; set; }
    /// <summary>On by default: it answers a request the person made themselves (join an organisation); the other types are opt-in.</summary>
    public bool Decizii { get; set; } = true;
    public bool TemeNoi { get; set; }
    public bool TermeneTeme { get; set; }
    public DateTime ActualizatLa { get; set; }
}
public class AbonamentPush
{
    public int Id { get; set; }
    public int UtilizatorId { get; set; }
    public string Endpoint { get; set; } = "";
    public string P256dh { get; set; } = "";
    public string Auth { get; set; } = "";
    public DateTime CreatLa { get; set; }
}
public class Notificare
{
    public int Id { get; set; }
    public int UtilizatorId { get; set; }
    public string Cheie { get; set; } = "";
    public string Tip { get; set; } = "";
    public int ResursaId { get; set; }
    public string Titlu { get; set; } = "";
    public string Text { get; set; } = "";
    public string Url { get; set; } = "/";
    public DateTime CreatLa { get; set; }
    public DateTime UrmatoareaIncercareLa { get; set; }
    public DateTime? FinalizatLa { get; set; }
    public int Incercari { get; set; }
    public string? Canal { get; set; }
    public int? MesajEmailId { get; set; }
    public MesajIesire? MesajEmail { get; set; }
}

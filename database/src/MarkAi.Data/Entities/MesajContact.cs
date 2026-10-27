namespace MarkAi.Data.Entities;
public class FeedbackPilot
{
    public int Id { get; set; }
    public int? UtilizatorId { get; set; }
    public string Text { get; set; } = "";
    public string Pagina { get; set; } = "";
    public DateTime CreatLa { get; set; }
    public DateTime? RezolvatLa { get; set; }
}

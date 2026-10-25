namespace MarkAi.Data.Entities;

public class Simulare
{
    public int Id { get; set; }
    public int UtilizatorId { get; set; }
    public int MaterieId { get; set; }
    public string Tip { get; set; } = "bac";
    public string Clasa { get; set; } = "Bac";
    public DateTime InceputLa { get; set; }
    public DateTime ExpiraLa { get; set; }
    public DateTime? FinalizatLa { get; set; }
    // Immutable question/solution snapshots and the student's saved answers; never serialized directly.
    public string IntrebariJson { get; set; } = "[]";
    public int Total { get; set; }
    public int Corecte { get; set; }
}

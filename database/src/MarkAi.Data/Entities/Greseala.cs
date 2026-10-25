namespace MarkAi.Data.Entities;

public class Greseala
{
    public int UtilizatorId { get; set; }
    public int IntrebareId { get; set; }
    public DateTime GresitLa { get; set; }
    public DateTime ScadentaLa { get; set; }
    public int Nivel { get; set; }
    public int CorecteConsecutive { get; set; }
    public Guid? Token { get; set; }
    public DateTime? EmisaLa { get; set; }
    public Intrebare Intrebare { get; set; } = null!;
}

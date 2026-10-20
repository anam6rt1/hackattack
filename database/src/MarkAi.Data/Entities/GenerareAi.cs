namespace MarkAi.Data.Entities;

/// <summary>One AI question generation started by an editor; counted for the daily limit per person.</summary>
public class GenerareAi
{
    public int Id { get; set; }

    public int UtilizatorId { get; set; }

    public int CursId { get; set; }

    public DateTime CreatLa { get; set; }
}

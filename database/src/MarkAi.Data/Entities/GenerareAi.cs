namespace MarkAi.Data.Entities;

/// <summary>One AI question generation a professor asked for (<see cref="Numar"/> questions for a Lecție); counted for the daily limit per person.</summary>
public class GenerareAi
{
    public int Id { get; set; }

    public int UtilizatorId { get; set; }

    public int ParcursId { get; set; }

    public int Numar { get; set; }

    public DateTime CreatLa { get; set; }
}

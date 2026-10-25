namespace MarkAi.Data.Entities;

/// <summary>Durable cleanup of uploaded personal documents after account erasure. No names, addresses or tokens.</summary>
public sealed class StergereDateCont
{
    public int Id { get; set; }
    public int UtilizatorId { get; set; }
    public DateTime CerutLa { get; set; }
    public DateTime? FinalizatLa { get; set; }
    public string[] CheiFisiere { get; set; } = [];
}

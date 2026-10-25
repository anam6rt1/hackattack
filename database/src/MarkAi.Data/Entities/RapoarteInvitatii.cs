namespace MarkAi.Data.Entities;

public class InvitatieElev
{
    public int Id { get; set; }
    public int OrganizatieId { get; set; }
    public string Nume { get; set; } = "";
    public string Email { get; set; } = "";
    public string Clasa { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public DateTime CreatLa { get; set; }
    public DateTime ExpiraLa { get; set; }
    public DateTime? AcceptataLa { get; set; }
}

public class SumarParinte
{
    public int UtilizatorId { get; set; }
    public string Email { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public string OprireHash { get; set; } = "";
    public string OprireProtejat { get; set; } = "";
    public DateTime CreatLa { get; set; }
    public DateTime ExpiraLa { get; set; }
    public DateTime? ConfirmatLa { get; set; }
    public DateTime? UltimaSaptamana { get; set; }
}

public class EmailSumar
{
    public int MesajId { get; set; }
    public MesajIesire Mesaj { get; set; } = null!;
    public int UtilizatorId { get; set; }
    public string Versiune { get; set; } = "";
    public bool Confirmare { get; set; }
}

// Immutable correctness at submission time; edits to the answer key do not rewrite statistics.
public class RaspunsStatistica
{
    public int IncercareId { get; set; }
    public int IntrebareId { get; set; }
    public int UtilizatorId { get; set; }
    public bool Corect { get; set; }
    public DateTime RaspunsLa { get; set; }
}

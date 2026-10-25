namespace MarkAi.Data.Entities;

public class GrupaElevi
{
    public int Id { get; set; }
    public int OrganizatieId { get; set; }
    public int? ProfesorId { get; set; }
    public string Nume { get; set; } = "";
    public Organizatie Organizatie { get; set; } = null!;
    public ICollection<GrupaMembru> Membri { get; set; } = [];
}
public class GrupaMembru
{
    public int GrupaId { get; set; }
    public int ElevId { get; set; }
    public GrupaElevi Grupa { get; set; } = null!;
    public Utilizator Elev { get; set; } = null!;
}
public class Tema
{
    public int Id { get; set; }
    public int OrganizatieId { get; set; }
    public int? ProfesorId { get; set; }
    public string Titlu { get; set; } = "";
    public string? Descriere { get; set; }
    public DateTime CreatLa { get; set; }
    public DateTime TermenLa { get; set; }
    public bool ToataOrganizatia { get; set; }
    public bool Anulata { get; set; }
    public Organizatie Organizatie { get; set; } = null!;
    public Utilizator? Profesor { get; set; }
    public ICollection<TemaLectie> Lectii { get; set; } = [];
    public ICollection<TemaGrupa> Grupe { get; set; } = [];
}
public class TemaLectie
{
    public int TemaId { get; set; }
    public int ParcursId { get; set; }
    public Tema Tema { get; set; } = null!;
    public Parcurs Parcurs { get; set; } = null!;
}
public class TemaGrupa
{
    public int TemaId { get; set; }
    public int GrupaId { get; set; }
    public Tema Tema { get; set; } = null!;
    public GrupaElevi Grupa { get; set; } = null!;
}

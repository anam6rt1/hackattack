namespace MarkAi.Data.Entities;

/// <summary>
/// A professor's path through a subject, shown in the app as a "Lecție": an ordered set of chapters (<see cref="Curs"/>),
/// one island each. It belongs to one home organisation (whose admin may edit it and whose publishing switch applies) and
/// may be shared, view-only, with the author's other organisations. Platform content has no home organisation.
/// </summary>
public class Parcurs
{
    public const string Public = "public", DoarOrganizatie = "organizatie";
    public static readonly string[] Clase = ["IX", "X", "XI", "XII", "Bac"];

    public int Id { get; set; }

    public int MaterieId { get; set; }

    public string Titlu { get; set; } = null!;

    public string? Descriere { get; set; }

    /// <summary>IX, X, XI, XII or "Bac".</summary>
    public string Clasa { get; set; } = "Bac";

    /// <summary>Null once the author's account is deleted; the home organisation's admin keeps editing it.</summary>
    public int? AutorId { get; set; }

    public int? OrganizatieId { get; set; }

    public string Vizibilitate { get; set; } = Public;

    public bool Publicat { get; set; } = true;

    /// <summary>Order among the subject's paths.</summary>
    public int Ordine { get; set; }

    public DateTime CreatLa { get; set; }

    /// <summary>Title + description in lower case without diacritics, computed by the database (<see cref="MarkAi.Data.CautareText"/>).</summary>
    public string Cautare { get; set; } = "";

    public virtual Materie Materie { get; set; } = null!;

    public virtual Utilizator? Autor { get; set; }

    public virtual Organizatie? Organizatie { get; set; }

    /// <summary>Extra organisations that may see it (never edit it).</summary>
    public virtual ICollection<ParcursOrganizatie> Partajari { get; set; } = new List<ParcursOrganizatie>();

    public virtual ICollection<Curs> Capitole { get; set; } = new List<Curs>();
}

public class ParcursOrganizatie
{
    public int ParcursId { get; set; }

    public int OrganizatieId { get; set; }

    public virtual Parcurs Parcurs { get; set; } = null!;

    public virtual Organizatie Organizatie { get; set; } = null!;
}

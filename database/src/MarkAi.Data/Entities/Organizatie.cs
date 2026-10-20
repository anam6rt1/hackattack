namespace MarkAi.Data.Entities;

/// <summary>
/// A school or tutoring centre. It is created by the person who applies as its admin and becomes active only when a
/// platform admin approves it. Each organisation has exactly one admin.
/// </summary>
public class Organizatie
{
    public const string InAsteptare = "in_asteptare", Aprobata = "aprobata", Respinsa = "respinsa";

    public int Id { get; set; }

    public string Nume { get; set; } = null!;

    public string Adresa { get; set; } = null!;

    public int JudetId { get; set; }

    public int? LocalitateId { get; set; }

    public string Stare { get; set; } = InAsteptare;

    public int AdminId { get; set; }

    public string? MotivRespingere { get; set; }

    public int? DecisDeId { get; set; }

    public DateTime? DecisLa { get; set; }

    public DateTime CreatLa { get; set; }

    public virtual Utilizator Admin { get; set; } = null!;

    public virtual Judet Judet { get; set; } = null!;

    public virtual Localitate? Localitate { get; set; }

    public virtual ICollection<MembruOrganizatie> Membri { get; set; } = new List<MembruOrganizatie>();
}

/// <summary>
/// A professor (in any number of organisations) or student (in at most one) and their status in the organisation.
/// A request expires after a week if nobody decides on it.
/// </summary>
public class MembruOrganizatie
{
    public const string RolProfesor = "profesor", RolElev = "elev";
    public const string InAsteptare = "in_asteptare", Aprobat = "aprobat", Respins = "respins", Expirat = "expirat";

    public int OrganizatieId { get; set; }

    public int UtilizatorId { get; set; }

    public string Rol { get; set; } = null!;

    public string Stare { get; set; } = InAsteptare;

    /// <summary>For professors: whether the organisation lets them publish courses publicly (used by the course pages).</summary>
    public bool PoatePublicaPublic { get; set; } = true;

    public DateTime CerutLa { get; set; }

    public DateTime? ExpiraLa { get; set; }

    /// <summary>When the organisation's admin was reminded that this request is about to expire.</summary>
    public DateTime? AmintitLa { get; set; }

    public int? DecisDeId { get; set; }

    public DateTime? DecisLa { get; set; }

    public string? MotivRespingere { get; set; }

    public virtual Organizatie Organizatie { get; set; } = null!;

    public virtual Utilizator Utilizator { get; set; } = null!;
}

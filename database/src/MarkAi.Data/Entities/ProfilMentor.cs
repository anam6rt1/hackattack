using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class ProfilMentor
{
    public int UtilizatorId { get; set; }

    public string? Descriere { get; set; }

    public string StareVerificare { get; set; } = null!;

    public decimal ProcentComision { get; set; }

    public DateTime? ActivatLa { get; set; }

    /// <summary>Suma platita mentorului pentru o sedinta, negociata cu administratorul.</summary>
    public decimal OnorariuNegociat { get; set; }

    public string Moneda { get; set; } = "RON";

    public string? PozaUrl { get; set; }

    public string? DovadaCalificareUrl { get; set; }

    public string? MotivRespingere { get; set; }

    public int? VerificatDeId { get; set; }

    /// <summary>
    /// Where the professor teaches, for "near me": one of their organisations, or null for their own county and locality
    /// (from their account). An organisation they no longer belong to counts as null.
    /// </summary>
    public int? LocOrganizatieId { get; set; }

    public virtual Organizatie? LocOrganizatie { get; set; }

    public virtual ICollection<MentorMaterie> Materii { get; set; } = new List<MentorMaterie>();

    public virtual ICollection<CameraTutoring> CamereTutoring { get; set; } = new List<CameraTutoring>();

    public virtual ICollection<ComisionMentor> Comisioane { get; set; } = new List<ComisionMentor>();

    public virtual ICollection<Decontare> Decontari { get; set; } = new List<Decontare>();

    public virtual Utilizator Utilizator { get; set; } = null!;
}


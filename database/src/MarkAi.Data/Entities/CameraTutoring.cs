using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class CameraTutoring
{
    public int Id { get; set; }

    public int MentorId { get; set; }

    public int MaterieId { get; set; }

    public int? CursId { get; set; }

    public int? TarifId { get; set; }

    public string Titlu { get; set; } = null!;

    public string? Descriere { get; set; }

    public int Capacitate { get; set; }

    public bool ProBono { get; set; }

    /// <summary>Online meeting address (Meet, Zoom, ...), shown only to the professor and booked students.</summary>
    public string? LinkIntalnire { get; set; }

    public const string Online = "online", FataInFata = "fata_in_fata", Hibrid = "hibrid";
    public static readonly string[] Tipuri = [Online, FataInFata, Hibrid];

    /// <summary>online, fata_in_fata (in person) or hibrid (each booking says how the student attends).</summary>
    public string Tip { get; set; } = Online;

    /// <summary>For in-person and hybrid sessions: the organisation it takes place at (null = the professor's own place).</summary>
    public int? LocOrganizatieId { get; set; }

    /// <summary>The place's county and locality, copied when the session is created (required unless online).</summary>
    public int? LocJudetId { get; set; }

    public int? LocLocalitateId { get; set; }

    /// <summary>A short note about the place ("Sala 12"), shown only to the professor and booked students.</summary>
    public string? NotaLoc { get; set; }

    public virtual Organizatie? LocOrganizatie { get; set; }

    public virtual Judet? LocJudet { get; set; }

    public virtual Localitate? LocLocalitate { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? InceputLa { get; set; }

    public DateTime? FinalizatLa { get; set; }

    public virtual Curs? Curs { get; set; }

    public virtual Materie Materie { get; set; } = null!;

    public virtual ProfilMentor Mentor { get; set; } = null!;

    public virtual ICollection<Rezervare> Rezervari { get; set; } = new List<Rezervare>();

    public virtual TarifFix? Tarif { get; set; }
}


using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class CameraTutoring
{
    public int Id { get; set; }

    public int MentorId { get; set; }

    public int MaterieId { get; set; }

    public int? CursId { get; set; }

    public int TarifId { get; set; }

    public string Titlu { get; set; } = null!;

    public string? Descriere { get; set; }

    public int Capacitate { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? InceputLa { get; set; }

    public DateTime? FinalizatLa { get; set; }

    public virtual Curs? Curs { get; set; }

    public virtual Materie Materie { get; set; } = null!;

    public virtual ProfilMentor Mentor { get; set; } = null!;

    public virtual ICollection<Rezervare> Rezervares { get; set; } = new List<Rezervare>();

    public virtual TarifFix Tarif { get; set; } = null!;
}


using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class Decontare
{
    public int Id { get; set; }

    public int MentorId { get; set; }

    public decimal SumaTotala { get; set; }

    public string Moneda { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? ReferintaPlata { get; set; }

    public DateTime CreataLa { get; set; }

    public DateTime? PlatitaLa { get; set; }

    public virtual ICollection<ComisionMentor> ComisionMentors { get; set; } = new List<ComisionMentor>();

    public virtual ProfilMentor Mentor { get; set; } = null!;
}


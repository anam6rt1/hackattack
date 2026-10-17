using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class ComisionMentor
{
    public int Id { get; set; }

    public int RezervareId { get; set; }

    public int MentorId { get; set; }

    public int? DecontareId { get; set; }

    public decimal TarifBaza { get; set; }

    public decimal ProcentSnapshot { get; set; }

    public decimal SumaComision { get; set; }

    public string Moneda { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime CalculatLa { get; set; }

    public virtual Decontare? Decontare { get; set; }

    public virtual ProfilMentor Mentor { get; set; } = null!;

    public virtual Rezervare Rezervare { get; set; } = null!;
}

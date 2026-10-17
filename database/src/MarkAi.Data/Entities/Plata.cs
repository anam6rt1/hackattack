using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class Plata
{
    public int Id { get; set; }

    public int RezervareId { get; set; }

    public decimal Suma { get; set; }

    public string Moneda { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string Procesator { get; set; } = null!;

    public string? TranzactieExterna { get; set; }

    public DateTime? PlatitaLa { get; set; }

    public virtual Rezervare Rezervare { get; set; } = null!;
}


using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class TarifFix
{
    public int Id { get; set; }

    public decimal Suma { get; set; }

    public string Moneda { get; set; } = null!;

    public int DurataMinute { get; set; }

    public bool Activ { get; set; }

    public virtual ICollection<CameraTutoring> CamereTutoring { get; set; } = new List<CameraTutoring>();
}


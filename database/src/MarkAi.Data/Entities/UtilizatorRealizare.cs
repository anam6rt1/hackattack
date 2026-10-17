using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class UtilizatorRealizare
{
    public int UtilizatorId { get; set; }

    public int RealizareId { get; set; }

    public DateTime DeblocataLa { get; set; }

    public virtual Realizare Realizare { get; set; } = null!;

    public virtual Utilizator Utilizator { get; set; } = null!;
}


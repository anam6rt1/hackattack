using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class Realizare
{
    public int Id { get; set; }

    public string Cod { get; set; } = null!;

    public string Titlu { get; set; } = null!;

    public string? Descriere { get; set; }

    public int XpBonus { get; set; }

    public string? Conditie { get; set; }

    public bool Activa { get; set; }

    public virtual ICollection<UtilizatorRealizare> UtilizatoriRealizari { get; set; } = new List<UtilizatorRealizare>();
}


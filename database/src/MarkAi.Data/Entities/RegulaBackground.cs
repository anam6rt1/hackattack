using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class RegulaBackground
{
    public int Id { get; set; }

    public int BackgroundId { get; set; }

    public int? InterfataId { get; set; }

    public string? Anotimp { get; set; }

    public string? PerioadaZilei { get; set; }

    public int Prioritate { get; set; }

    public bool Activa { get; set; }

    public virtual Background Background { get; set; } = null!;

    public virtual Interfata? Interfata { get; set; }
}


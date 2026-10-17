using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class Interfata
{
    public int Id { get; set; }

    public string Cod { get; set; } = null!;

    public string Nume { get; set; } = null!;

    public string? Descriere { get; set; }

    public string? PreviewUrl { get; set; }

    public bool Activa { get; set; }

    public virtual ICollection<PreferinteUtilizator> PreferinteUtilizators { get; set; } = new List<PreferinteUtilizator>();

    public virtual ICollection<RegulaBackground> RegulaBackgrounds { get; set; } = new List<RegulaBackground>();
}


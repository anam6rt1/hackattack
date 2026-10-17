using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class Background
{
    public int Id { get; set; }

    public string Cod { get; set; } = null!;

    public string Nume { get; set; } = null!;

    public string AssetUrl { get; set; } = null!;

    public bool Activ { get; set; }

    public bool SelectabilStatic { get; set; }

    public DateTime? DisponibilDeLa { get; set; }

    public DateTime? DisponibilPanaLa { get; set; }

    public virtual ICollection<PreferinteUtilizator> PreferinteUtilizators { get; set; } = new List<PreferinteUtilizator>();

    public virtual ICollection<RegulaBackground> RegulaBackgrounds { get; set; } = new List<RegulaBackground>();
}


using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class UtilizatorMaterie
{
    public int UtilizatorId { get; set; }

    public int MaterieId { get; set; }

    public string? NivelScolar { get; set; }

    public DateTime SelectataLa { get; set; }

    public virtual ICollection<EvenimentRecompensa> EvenimenteRecompensa { get; set; } = new List<EvenimentRecompensa>();

    public virtual Materie Materie { get; set; } = null!;

    public virtual Utilizator Utilizator { get; set; } = null!;
}


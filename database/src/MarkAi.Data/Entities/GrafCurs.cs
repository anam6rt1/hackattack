using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class GrafCurs
{
    public int GrafId { get; set; }

    public int CursId { get; set; }

    public int MaterieId { get; set; }

    public DateTime AdaugatLa { get; set; }

    public virtual Curs Curs { get; set; } = null!;

    public virtual Graf Graf { get; set; } = null!;

    public virtual ICollection<MuchieGraf> MuchiiCaSursa { get; set; } = new List<MuchieGraf>();

    public virtual ICollection<MuchieGraf> MuchiiCaDestinatie { get; set; } = new List<MuchieGraf>();
}


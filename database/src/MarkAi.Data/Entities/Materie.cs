using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class Materie
{
    public int Id { get; set; }

    public string Denumire { get; set; } = null!;

    public string? Descriere { get; set; }

    public virtual ICollection<CameraTutoring> CamereTutoring { get; set; } = new List<CameraTutoring>();

    public virtual ICollection<Curs> Cursuri { get; set; } = new List<Curs>();

    public virtual ICollection<Graf> Grafuri { get; set; } = new List<Graf>();

    public virtual ICollection<UtilizatorMaterie> UtilizatoriMaterii { get; set; } = new List<UtilizatorMaterie>();
}


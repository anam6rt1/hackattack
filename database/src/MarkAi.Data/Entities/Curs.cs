using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class Curs
{
    public int Id { get; set; }

    public int MaterieId { get; set; }

    public string Titlu { get; set; } = null!;

    public string? Descriere { get; set; }

    public string? Continut { get; set; }

    public virtual ICollection<CameraTutoring> CameraTutorings { get; set; } = new List<CameraTutoring>();

    public virtual ICollection<GrafCurs> GrafCurs { get; set; } = new List<GrafCurs>();

    public virtual Materie Materie { get; set; } = null!;

    public virtual ICollection<Sesiune> Sesiunes { get; set; } = new List<Sesiune>();
}


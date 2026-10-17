using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class Graf
{
    public int Id { get; set; }

    public int MaterieId { get; set; }

    public int OwnerId { get; set; }

    public string Titlu { get; set; } = null!;

    public string? Descriere { get; set; }

    public DateTime CreatLa { get; set; }

    public virtual ICollection<GrafCurs> GrafCurs { get; set; } = new List<GrafCurs>();

    public virtual Materie Materie { get; set; } = null!;

    public virtual Utilizator Owner { get; set; } = null!;
}


using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class ProfilMentor
{
    public int UtilizatorId { get; set; }

    public string? Descriere { get; set; }

    public string StareVerificare { get; set; } = null!;

    public decimal ProcentComision { get; set; }

    public DateTime? ActivatLa { get; set; }

    public virtual ICollection<CameraTutoring> CamereTutoring { get; set; } = new List<CameraTutoring>();

    public virtual ICollection<ComisionMentor> Comisioane { get; set; } = new List<ComisionMentor>();

    public virtual ICollection<Decontare> Decontari { get; set; } = new List<Decontare>();

    public virtual Utilizator Utilizator { get; set; } = null!;
}


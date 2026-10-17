using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class Rol
{
    public int Id { get; set; }

    public string Nume { get; set; } = null!;

    public virtual ICollection<UtilizatorRol> UtilizatoriRoluri { get; set; } = new List<UtilizatorRol>();
}


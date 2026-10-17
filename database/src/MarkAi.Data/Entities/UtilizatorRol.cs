using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class UtilizatorRol
{
    public int UtilizatorId { get; set; }

    public int RolId { get; set; }

    public DateTime AtribuitLa { get; set; }

    public virtual Rol Rol { get; set; } = null!;

    public virtual Utilizator Utilizator { get; set; } = null!;
}


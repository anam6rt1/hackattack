using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class PreferinteUtilizator
{
    public int UtilizatorId { get; set; }

    public int? InterfataId { get; set; }

    public int? BackgroundStaticId { get; set; }

    public string ModBackground { get; set; } = null!;

    public string Timezone { get; set; } = null!;

    public DateTime ActualizatLa { get; set; }

    public virtual Background? BackgroundStatic { get; set; }

    public virtual Interfata? Interfata { get; set; }

    public virtual Utilizator Utilizator { get; set; } = null!;
}


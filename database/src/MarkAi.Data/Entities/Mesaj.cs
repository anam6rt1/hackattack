using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class Mesaj
{
    public int Id { get; set; }

    public int SesiuneId { get; set; }

    public string Expeditor { get; set; } = null!;

    public string Tip { get; set; } = null!;

    public string Continut { get; set; } = null!;

    public DateTime CreatLa { get; set; }

    public virtual Sesiune Sesiune { get; set; } = null!;

    public virtual ICollection<SmsLog> JurnaleSms { get; set; } = new List<SmsLog>();

    public virtual ICollection<TablaPas> PasiTabla { get; set; } = new List<TablaPas>();
}


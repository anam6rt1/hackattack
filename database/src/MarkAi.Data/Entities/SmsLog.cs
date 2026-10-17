using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class SmsLog
{
    public int Id { get; set; }

    public int SesiuneId { get; set; }

    public int? MesajId { get; set; }

    public string Directie { get; set; } = null!;

    public string NumarDestinatie { get; set; } = null!;

    public string Continut { get; set; } = null!;

    public string StatusLivrare { get; set; } = null!;

    public string? ProviderSid { get; set; }

    public DateTime? TrimisLa { get; set; }

    public virtual Mesaj? Mesaj { get; set; }

    public virtual Sesiune Sesiune { get; set; } = null!;
}


using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class MuchieGraf
{
    public int GrafId { get; set; }

    public int CursSursaId { get; set; }

    public int CursDestinatieId { get; set; }

    public string TipLegatura { get; set; } = null!;

    public string? Descriere { get; set; }

    public virtual GrafCurs GrafCurs { get; set; } = null!;

    public virtual GrafCurs GrafCurNavigation { get; set; } = null!;
}


using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class TablaPas
{
    public int Id { get; set; }

    public int SesiuneId { get; set; }

    public int? MesajId { get; set; }

    public int Ordine { get; set; }

    public string? Explicatie { get; set; }

    public string? FormulaLatex { get; set; }

    public DateTime CreatLa { get; set; }

    public virtual Mesaj? Mesaj { get; set; }

    public virtual Sesiune Sesiune { get; set; } = null!;
}


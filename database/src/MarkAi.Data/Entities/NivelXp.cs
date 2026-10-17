using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class NivelXp
{
    public int Nivel { get; set; }

    public int XpNecesar { get; set; }

    public string Titlu { get; set; } = null!;

    public string? Descriere { get; set; }
}


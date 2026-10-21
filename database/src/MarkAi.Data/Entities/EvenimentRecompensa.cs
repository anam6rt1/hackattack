using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

/// <summary>
/// Jurnal XP; nivelul se deriva din SUM(xp_acordat) si pragurile nivel_xp.
/// </summary>
public partial class EvenimentRecompensa
{
    public int Id { get; set; }

    public int UtilizatorId { get; set; }

    public int? MaterieId { get; set; }

    public int? SesiuneId { get; set; }

    public int XpAcordat { get; set; }

    public int PuncteMaterie { get; set; }

    public string TipEveniment { get; set; } = null!;

    public string CheieUnica { get; set; } = null!;

    public DateTime CreatLa { get; set; }

    public virtual Sesiune? Sesiune { get; set; }

    public virtual Utilizator Utilizator { get; set; } = null!;

    public virtual Materie? Materie { get; set; }
}


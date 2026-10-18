using System;

namespace MarkAi.Data.Entities;

public partial class ProgresCurs
{
    public int UtilizatorId { get; set; }

    public int CursId { get; set; }

    public bool VideoVazut { get; set; }

    public bool MaterialeCitite { get; set; }

    public bool QuizPromovat { get; set; }

    public DateTime? CompletatLa { get; set; }

    public DateTime UltimaAccesare { get; set; }

    public virtual Curs Curs { get; set; } = null!;

    public virtual Utilizator Utilizator { get; set; } = null!;
}

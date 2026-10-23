namespace MarkAi.Data.Entities;

/// <summary>
/// A student's progress on a Lecție: the chapter read last (for "continue where you left off") and when the Lecție's quiz
/// was passed. Passing the quiz is the only thing that completes a Lecție.
/// </summary>
public class ProgresLectie
{
    public int UtilizatorId { get; set; }

    public int ParcursId { get; set; }

    /// <summary>Null when that chapter was deleted.</summary>
    public int? UltimulCursId { get; set; }

    public DateTime? PromovatLa { get; set; }

    public DateTime UltimaAccesare { get; set; }

    public virtual Utilizator Utilizator { get; set; } = null!;

    public virtual Parcurs Parcurs { get; set; } = null!;

    public virtual Curs? UltimulCurs { get; set; }
}

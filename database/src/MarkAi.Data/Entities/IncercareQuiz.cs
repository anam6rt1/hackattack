namespace MarkAi.Data.Entities;

/// <summary>
/// One quiz attempt on a lesson: a fixed random set of questions, answered one at a time. The correct answers are
/// revealed only after the last one, and the lesson is passed (XP awarded) only with enough correct answers.
/// </summary>
public class IncercareQuiz
{
    public int Id { get; set; }

    public int UtilizatorId { get; set; }

    public int CursId { get; set; }

    public int[] IntrebariIds { get; set; } = [];

    /// <summary>The chosen option for each question, in the same order; 0 while unanswered.</summary>
    public int[] Raspunsuri { get; set; } = [];

    public int Corecte { get; set; }

    public DateTime InceputLa { get; set; }

    public DateTime? FinalizataLa { get; set; }

    public virtual Utilizator Utilizator { get; set; } = null!;

    public virtual Curs Curs { get; set; } = null!;
}

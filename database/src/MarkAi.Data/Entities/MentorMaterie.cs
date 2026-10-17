namespace MarkAi.Data.Entities;

public partial class MentorMaterie
{
    public int MentorId { get; set; }

    public int MaterieId { get; set; }

    public virtual Materie Materie { get; set; } = null!;

    public virtual ProfilMentor Mentor { get; set; } = null!;
}

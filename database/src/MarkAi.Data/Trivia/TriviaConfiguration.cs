using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Data.Trivia;

internal static class TriviaConfiguration
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<TriviaRoom>(e =>
        {
            e.ToTable("trivia_camera", "mark_ai", t =>
            {
                t.HasCheckConstraint("ck_trivia_camera_cod", "cod ~ '^[A-Z0-9]{6}$'");
                t.HasCheckConstraint("ck_trivia_camera_capacitate", "capacitate BETWEEN 2 AND 16");
                t.HasCheckConstraint("ck_trivia_camera_status", "status IN ('waiting','active','finished')");
                t.HasCheckConstraint("ck_trivia_camera_final", "(status = 'finished') = (finalizata_la IS NOT NULL) AND (finalizata_la IS NULL OR finalizata_la >= creata_la)");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(x => x.Code).HasColumnName("cod").HasMaxLength(6);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.HostId).HasColumnName("gazda_id");
            e.HasOne<Utilizator>().WithMany().HasForeignKey(x => x.HostId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.SubjectId).HasColumnName("materie_id");
            e.HasOne<Materie>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Capacity).HasColumnName("capacitate");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.CreatedAt).HasColumnName("creata_la").HasDefaultValueSql("clock_timestamp()");
            e.Property(x => x.FinishedAt).HasColumnName("finalizata_la");
        });
        model.Entity<TriviaParticipant>(e =>
        {
            e.ToTable("trivia_participant", "mark_ai");
            e.HasKey(x => new { x.RoomId, x.UserId });
            e.Property(x => x.RoomId).HasColumnName("camera_id");
            e.Property(x => x.UserId).HasColumnName("utilizator_id");
            e.Property(x => x.JoinedAt).HasColumnName("intrat_la").HasDefaultValueSql("clock_timestamp()");
            e.HasOne<TriviaRoom>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Utilizator>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TriviaQuestion>(e =>
        {
            e.ToTable("trivia_intrebare", "mark_ai", t =>
            {
                t.HasCheckConstraint("ck_trivia_intrebare_text", "length(btrim(text)) > 0");
                t.HasCheckConstraint("ck_trivia_intrebare_tip", "tip IN ('grila','estimare')");
                t.HasCheckConstraint("ck_trivia_intrebare_sursa", "sursa IN ('ai','manual')");
                t.HasCheckConstraint("ck_trivia_intrebare_estimare", "(tip = 'estimare') = (raspuns_numeric IS NOT NULL)");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(x => x.SubjectId).HasColumnName("materie_id");
            e.Property(x => x.CourseId).HasColumnName("curs_id");
            e.Property(x => x.Text).HasColumnName("text");
            e.Property(x => x.Active).HasColumnName("activa");
            e.Property(x => x.Kind).HasColumnName("tip").HasDefaultValue("grila");
            e.Property(x => x.NumericAnswer).HasColumnName("raspuns_numeric");
            e.Property(x => x.Explanation).HasColumnName("explicatie");
            e.Property(x => x.Source).HasColumnName("sursa").HasDefaultValue("manual");
            e.Property(x => x.CreatedAt).HasColumnName("creata_la").HasDefaultValueSql("now()");
            e.HasOne<Materie>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            // The composite key keeps a question's course inside the question's subject.
            e.HasOne<Curs>().WithMany().HasForeignKey(x => new { x.CourseId, x.SubjectId })
                .HasPrincipalKey(c => new { c.Id, c.MaterieId }).OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("trivia_intrebare_curs_id_materie_id_fkey");
            e.HasIndex(x => new { x.CourseId, x.SubjectId });
        });
        model.Entity<TriviaOption>(e =>
        {
            e.ToTable("trivia_varianta", "mark_ai", t => t.HasCheckConstraint("ck_trivia_varianta_text", "length(btrim(text)) > 0"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(x => x.QuestionId).HasColumnName("intrebare_id");
            e.Property(x => x.Text).HasColumnName("text");
            e.Property(x => x.IsCorrect).HasColumnName("corecta");
            e.HasOne<TriviaQuestion>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.QuestionId).IsUnique().HasFilter("corecta");
        });
        model.Entity<TriviaRound>(e =>
        {
            e.ToTable("trivia_runda", "mark_ai", t =>
            {
                t.HasCheckConstraint("ck_trivia_runda_numar", "numar > 0");
                t.HasCheckConstraint("ck_trivia_runda_interval", "termen_la > inceputa_la AND termen_la <= inceputa_la + interval '5 minutes'");
                t.HasCheckConstraint("ck_trivia_runda_puncte", "puncte > 0");
            });
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.Id, x.RoomId });
            e.HasIndex(x => new { x.RoomId, x.Number }).IsUnique();
            e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(x => x.RoomId).HasColumnName("camera_id");
            e.Property(x => x.Number).HasColumnName("numar");
            e.Property(x => x.QuestionId).HasColumnName("intrebare_id");
            e.Property(x => x.QuestionText).HasColumnName("text_intrebare");
            e.Property(x => x.StartedAt).HasColumnName("inceputa_la");
            e.Property(x => x.DeadlineAt).HasColumnName("termen_la");
            e.Property(x => x.Points).HasColumnName("puncte");
            e.HasOne<TriviaRoom>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<TriviaQuestion>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TriviaRoundOption>(e =>
        {
            e.ToTable("trivia_runda_varianta", "mark_ai");
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.Id, x.RoundId });
            e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(x => x.RoundId).HasColumnName("runda_id");
            e.Property(x => x.Text).HasColumnName("text");
            e.Property(x => x.IsCorrect).HasColumnName("corecta");
            e.HasIndex(x => x.RoundId).IsUnique().HasFilter("corecta");
            e.HasOne<TriviaRound>().WithMany().HasForeignKey(x => x.RoundId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TriviaAnswer>(e =>
        {
            e.ToTable("trivia_raspuns", "mark_ai", t => t.HasCheckConstraint("ck_trivia_raspuns_scor", "scor >= 0"));
            e.HasKey(x => new { x.RoundId, x.UserId });
            e.Property(x => x.RoundId).HasColumnName("runda_id");
            e.Property(x => x.UserId).HasColumnName("utilizator_id");
            e.Property(x => x.RoomId).HasColumnName("camera_id");
            e.Property(x => x.OptionId).HasColumnName("varianta_id");
            e.Property(x => x.SubmittedAt).HasColumnName("trimis_la").HasDefaultValueSql("clock_timestamp()");
            e.Property(x => x.Score).HasColumnName("scor");
            e.HasOne<TriviaRound>().WithMany().HasForeignKey(x => new { x.RoundId, x.RoomId })
                .HasPrincipalKey(x => new { x.Id, x.RoomId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<TriviaParticipant>().WithMany().HasForeignKey(x => new { x.RoomId, x.UserId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<TriviaRoundOption>().WithMany().HasForeignKey(x => new { x.OptionId, x.RoundId })
                .HasPrincipalKey(x => new { x.Id, x.RoundId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

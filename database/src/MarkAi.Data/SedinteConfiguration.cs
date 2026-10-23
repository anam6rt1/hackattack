using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Data;

/// <summary>Model additions for tutoring sessions: online / in-person / hybrid, the session's place, and the professor's teaching place.</summary>
internal static class SedinteConfiguration
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<CameraTutoring>(e =>
        {
            e.ToTable("camera_tutoring", "mark_ai", t =>
            {
                t.HasCheckConstraint("camera_tutoring_tip_check", "tip IN ('online', 'fata_in_fata', 'hibrid')");
                // Anything but online happens somewhere.
                t.HasCheckConstraint("camera_tutoring_loc_check", "tip = 'online' OR loc_judet_id IS NOT NULL");
            });
            e.Property(x => x.Tip).HasColumnName("tip").HasDefaultValue(CameraTutoring.Online);
            e.Property(x => x.LocOrganizatieId).HasColumnName("loc_organizatie_id");
            e.Property(x => x.LocJudetId).HasColumnName("loc_judet_id");
            e.Property(x => x.LocLocalitateId).HasColumnName("loc_localitate_id");
            e.Property(x => x.NotaLoc).HasColumnName("nota_loc");
            e.HasIndex(x => x.LocOrganizatieId, "idx_camera_tutoring_loc_organizatie_id_fkey");
            e.HasIndex(x => x.LocJudetId, "idx_camera_tutoring_loc_judet_id_fkey");
            e.HasIndex(x => x.LocLocalitateId, "idx_camera_tutoring_loc_localitate_id_fkey");
            // The county/locality are a copy, so the session keeps its place if the organisation goes.
            e.HasOne(x => x.LocOrganizatie).WithMany().HasForeignKey(x => x.LocOrganizatieId)
                .OnDelete(DeleteBehavior.SetNull).HasConstraintName("camera_tutoring_loc_organizatie_id_fkey");
            e.HasOne(x => x.LocJudet).WithMany().HasForeignKey(x => x.LocJudetId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("camera_tutoring_loc_judet_id_fkey");
            e.HasOne(x => x.LocLocalitate).WithMany().HasForeignKey(x => x.LocLocalitateId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("camera_tutoring_loc_localitate_id_fkey");
        });

        model.Entity<Rezervare>(e =>
        {
            e.ToTable("rezervare", "mark_ai", t => t.HasCheckConstraint("rezervare_participare_check", "participare IN ('online', 'la_sala')"));
            e.Property(x => x.Participare).HasColumnName("participare").HasDefaultValue(Rezervare.Online);
        });

        model.Entity<ProfilMentor>(e =>
        {
            e.Property(x => x.LocOrganizatieId).HasColumnName("loc_organizatie_id");
            e.Property(x => x.RetrasLa).HasColumnName("retras_la");
            e.HasIndex(x => x.LocOrganizatieId, "idx_profil_mentor_loc_organizatie_id_fkey");
            e.HasOne(x => x.LocOrganizatie).WithMany().HasForeignKey(x => x.LocOrganizatieId)
                .OnDelete(DeleteBehavior.SetNull).HasConstraintName("profil_mentor_loc_organizatie_id_fkey");
        });
    }
}

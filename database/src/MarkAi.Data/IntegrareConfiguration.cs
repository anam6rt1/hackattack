using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Data;

/// <summary>
/// Model additions needed by the web frontend: login, course content and progress,
/// professor applications and pro bono tutoring slots.
/// </summary>
internal static class IntegrareConfiguration
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<Utilizator>(e =>
        {
            e.Property(x => x.ParolaHash).HasColumnName("parola_hash").HasDefaultValue("");
        });

        model.Entity<Materie>(e =>
        {
            e.Property(x => x.Cod).HasColumnName("cod");
            e.Property(x => x.Ordine).HasColumnName("ordine").HasDefaultValue(0);
            e.HasIndex(x => x.Cod, "materie_cod_key").IsUnique();
        });

        model.Entity<Curs>(e =>
        {
            e.Property(x => x.Ordine).HasColumnName("ordine").HasDefaultValue(0);
            e.Property(x => x.Capitol).HasColumnName("capitol");
            e.Property(x => x.VideoUrl).HasColumnName("video_url");
            e.Property(x => x.Publicat).HasColumnName("publicat");
            e.Property(x => x.CreatDeId).HasColumnName("creat_de_id");
            e.Property(x => x.CreatLa).HasColumnName("creat_la").HasDefaultValueSql("now()");
            e.OwnsMany(x => x.Materiale, b => b.ToJson("materiale"));
            e.HasOne(x => x.CreatDe).WithMany()
                .HasForeignKey(x => x.CreatDeId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("curs_creat_de_id_fkey");
            e.HasIndex(x => new { x.MaterieId, x.Ordine }, "idx_curs_materie_id_ordine");
        });


        model.Entity<ProfilMentor>(e =>
        {
            e.ToTable("profil_mentor", "mark_ai", t =>
            {
                t.HasCheckConstraint("profil_mentor_onorariu_negociat_check", "onorariu_negociat >= (0)::numeric");
                t.HasCheckConstraint("profil_mentor_moneda_check", "moneda ~ '^[A-Z]{3}$'::text");
            });
            e.Property(x => x.OnorariuNegociat).HasPrecision(14, 2).HasColumnName("onorariu_negociat").HasDefaultValue(0m);
            e.Property(x => x.Moneda).HasColumnName("moneda").HasDefaultValue("RON");
            e.Property(x => x.PozaUrl).HasColumnName("poza_url");
            e.Property(x => x.DovadaCalificareUrl).HasColumnName("dovada_calificare_url");
            e.Property(x => x.MotivRespingere).HasColumnName("motiv_respingere");
            e.Property(x => x.VerificatDeId).HasColumnName("verificat_de_id");
            e.HasOne<Utilizator>().WithMany()
                .HasForeignKey(x => x.VerificatDeId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("profil_mentor_verificat_de_id_fkey");
        });

        model.Entity<MentorMaterie>(e =>
        {
            e.ToTable("mentor_materie", "mark_ai");
            e.HasKey(x => new { x.MentorId, x.MaterieId }).HasName("mentor_materie_pkey");
            e.HasIndex(x => x.MaterieId, "idx_mentor_materie_materie_id_fkey");
            e.Property(x => x.MentorId).HasColumnName("mentor_id");
            e.Property(x => x.MaterieId).HasColumnName("materie_id");
            e.HasOne(x => x.Mentor).WithMany(m => m.Materii)
                .HasForeignKey(x => x.MentorId)
                .HasConstraintName("mentor_materie_mentor_id_fkey");
            e.HasOne(x => x.Materie).WithMany(m => m.Mentori)
                .HasForeignKey(x => x.MaterieId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("mentor_materie_materie_id_fkey");
        });

        model.Entity<PreferinteUtilizator>(e =>
        {
            e.ToTable("preferinte_utilizator", "mark_ai", t =>
            {
                t.HasCheckConstraint("preferinte_utilizator_viteza_tutor_check", "viteza_tutor BETWEEN 70 AND 130");
                t.HasCheckConstraint("preferinte_utilizator_ton_tutor_check", "ton_tutor BETWEEN -20 AND 20");
            });
            e.Property(x => x.VoceTutor).HasColumnName("voce_tutor");
            e.Property(x => x.VitezaTutor).HasColumnName("viteza_tutor").HasDefaultValue(100);
            e.Property(x => x.TonTutor).HasColumnName("ton_tutor").HasDefaultValue(0);
            e.Property(x => x.Avatar).HasColumnName("avatar");
        });

        model.Entity<CameraTutoring>(e =>
        {
            e.ToTable("camera_tutoring", "mark_ai", t =>
            {
                t.HasCheckConstraint("camera_tutoring_pro_bono_check", "pro_bono = (tarif_id IS NULL)");
                t.HasCheckConstraint("camera_tutoring_link_intalnire_check", "link_intalnire IS NULL OR link_intalnire ~ '^https://'");
            });
            e.Property(x => x.ProBono).HasColumnName("pro_bono").HasDefaultValue(false);
            e.Property(x => x.LinkIntalnire).HasColumnName("link_intalnire");
        });
    }
}

using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Data;

/// <summary>Model additions for course paths ("Lecții"): the path, its view-only shares, and the chapter's link to it.</summary>
internal static class ParcursuriConfiguration
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<Parcurs>(e =>
        {
            e.ToTable("parcurs", "mark_ai", t =>
            {
                t.HasCheckConstraint("parcurs_clasa_check", "clasa IN ('IX', 'X', 'XI', 'XII', 'Bac')");
                t.HasCheckConstraint("parcurs_vizibilitate_check", "vizibilitate IN ('public', 'organizatie')");
                // Organisation-only content needs an organisation.
                t.HasCheckConstraint("parcurs_organizatie_check", "vizibilitate = 'public' OR organizatie_id IS NOT NULL");
            });
            e.HasKey(x => x.Id).HasName("parcurs_pkey");
            e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(x => x.MaterieId).HasColumnName("materie_id");
            e.Property(x => x.Titlu).HasColumnName("titlu");
            e.Property(x => x.Descriere).HasColumnName("descriere");
            e.Property(x => x.Clasa).HasColumnName("clasa");
            e.Property(x => x.AutorId).HasColumnName("autor_id");
            e.Property(x => x.OrganizatieId).HasColumnName("organizatie_id");
            e.Property(x => x.Vizibilitate).HasColumnName("vizibilitate").HasDefaultValue(Parcurs.Public);
            e.Property(x => x.Publicat).HasColumnName("publicat").HasDefaultValue(true);
            e.Property(x => x.Ordine).HasColumnName("ordine").HasDefaultValue(0);
            e.Property(x => x.CreatLa).HasColumnName("creat_la").HasDefaultValueSql("now()");
            // Target of the chapters' composite key, so a chapter's subject always matches its path's.
            e.HasAlternateKey(x => new { x.Id, x.MaterieId }).HasName("parcurs_id_materie_id_key");
            e.HasIndex(x => new { x.MaterieId, x.Clasa }, "idx_parcurs_materie_clasa");
            e.HasIndex(x => x.AutorId, "idx_parcurs_autor_id_fkey");
            e.HasIndex(x => x.OrganizatieId, "idx_parcurs_organizatie_id_fkey");
            e.HasOne(x => x.Materie).WithMany(m => m.Parcursuri).HasForeignKey(x => x.MaterieId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("parcurs_materie_id_fkey");
            e.HasOne(x => x.Autor).WithMany().HasForeignKey(x => x.AutorId)
                .OnDelete(DeleteBehavior.SetNull).HasConstraintName("parcurs_autor_id_fkey");
            e.HasOne(x => x.Organizatie).WithMany().HasForeignKey(x => x.OrganizatieId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("parcurs_organizatie_id_fkey");
        });

        model.Entity<ParcursOrganizatie>(e =>
        {
            e.ToTable("parcurs_organizatie", "mark_ai");
            e.HasKey(x => new { x.ParcursId, x.OrganizatieId }).HasName("parcurs_organizatie_pkey");
            e.Property(x => x.ParcursId).HasColumnName("parcurs_id");
            e.Property(x => x.OrganizatieId).HasColumnName("organizatie_id");
            e.HasIndex(x => x.OrganizatieId, "idx_parcurs_organizatie_organizatie_id_fkey");
            e.HasOne(x => x.Parcurs).WithMany(p => p.Partajari).HasForeignKey(x => x.ParcursId)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("parcurs_organizatie_parcurs_id_fkey");
            e.HasOne(x => x.Organizatie).WithMany().HasForeignKey(x => x.OrganizatieId)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("parcurs_organizatie_organizatie_id_fkey");
        });

        model.Entity<Curs>(e =>
        {
            e.Property(x => x.ParcursId).HasColumnName("parcurs_id");
            e.HasIndex(x => new { x.ParcursId, x.MaterieId }, "idx_curs_parcurs_id_materie_id_fkey");
            // Chapters are deleted explicitly (their questions' options block a cascade), never by deleting the path.
            e.HasOne(x => x.Parcurs).WithMany(p => p.Capitole)
                .HasForeignKey(x => new { x.ParcursId, x.MaterieId }).HasPrincipalKey(p => new { p.Id, p.MaterieId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("curs_parcurs_id_materie_id_fkey");
        });
    }
}

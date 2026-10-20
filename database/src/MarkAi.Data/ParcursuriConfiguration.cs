using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Data;

/// <summary>Model additions for course paths ("Lecții"): the path, its view-only shares, the chapter's link to it, and chapter files.</summary>
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
            e.Property(x => x.Cautare).HasColumnName("cautare")
                .HasComputedColumnSql(CautareText.Sql("titlu || ' ' || coalesce(descriere, '')"), stored: true);
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
            e.Property(x => x.Cautare).HasColumnName("cautare")
                .HasComputedColumnSql(CautareText.Sql("titlu || ' ' || coalesce(capitol, '') || ' ' || coalesce(descriere, '')"), stored: true);
            e.HasIndex(x => new { x.ParcursId, x.MaterieId }, "idx_curs_parcurs_id_materie_id_fkey");
            // Chapters are deleted explicitly (their questions' options block a cascade), never by deleting the path.
            e.HasOne(x => x.Parcurs).WithMany(p => p.Capitole)
                .HasForeignKey(x => new { x.ParcursId, x.MaterieId }).HasPrincipalKey(p => new { p.Id, p.MaterieId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("curs_parcurs_id_materie_id_fkey");
        });

        model.Entity<FisierCapitol>(e =>
        {
            e.ToTable("fisier_capitol", "mark_ai", t =>
            {
                t.HasCheckConstraint("fisier_capitol_tip_check", "tip IN ('pdf', 'docx', 'pptx')");
                t.HasCheckConstraint("fisier_capitol_stare_text_check", "stare_text IN ('in_asteptare', 'gata', 'fara_text', 'eroare')");
                t.HasCheckConstraint("fisier_capitol_marime_check", "marime_octeti > 0");
            });
            e.HasKey(x => x.Id).HasName("fisier_capitol_pkey");
            e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(x => x.CursId).HasColumnName("curs_id");
            e.Property(x => x.Nume).HasColumnName("nume");
            e.Property(x => x.Tip).HasColumnName("tip");
            e.Property(x => x.Cheie).HasColumnName("cheie");
            e.Property(x => x.MarimeOcteti).HasColumnName("marime_octeti");
            e.Property(x => x.Sha256).HasColumnName("sha256");
            e.Property(x => x.StareText).HasColumnName("stare_text").HasDefaultValue(FisierCapitol.InAsteptare);
            e.Property(x => x.TextExtras).HasColumnName("text_extras");
            e.Property(x => x.IncarcatDeId).HasColumnName("incarcat_de_id");
            e.Property(x => x.IncarcatLa).HasColumnName("incarcat_la").HasDefaultValueSql("now()");
            e.HasIndex(x => x.Cheie, "fisier_capitol_cheie_key").IsUnique();
            e.HasIndex(x => x.CursId, "idx_fisier_capitol_curs_id_fkey");
            e.HasIndex(x => x.IncarcatDeId, "idx_fisier_capitol_incarcat_de_id_fkey");
            // Rows go with their chapter; StergereContinut removes the stored files after the commit.
            e.HasOne(x => x.Curs).WithMany(c => c.Fisiere).HasForeignKey(x => x.CursId)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fisier_capitol_curs_id_fkey");
            e.HasOne(x => x.IncarcatDe).WithMany().HasForeignKey(x => x.IncarcatDeId)
                .OnDelete(DeleteBehavior.SetNull).HasConstraintName("fisier_capitol_incarcat_de_id_fkey");
        });
    }
}

using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Data;

/// <summary>Model additions for course paths ("Lecții"): the path, its view-only shares, the chapter's link to it, and the Lecție's files, quiz and progress.</summary>
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
            // Search uses ILIKE '%...%' on cautare: a trigram index keeps it fast with many Lecții.
            e.HasIndex(x => x.Cautare, "idx_parcurs_cautare_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
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
            e.HasIndex(x => x.Cautare, "idx_curs_cautare_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
            e.HasOne(x => x.Parcurs).WithMany(p => p.Capitole)
                .HasForeignKey(x => new { x.ParcursId, x.MaterieId }).HasPrincipalKey(p => new { p.Id, p.MaterieId })
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("curs_parcurs_id_materie_id_fkey");
        });

        model.Entity<FisierLectie>(e =>
        {
            e.ToTable("fisier_lectie", "mark_ai", t =>
            {
                t.HasCheckConstraint("fisier_lectie_tip_check", "tip IN ('pdf', 'docx', 'pptx', 'xlsx')");
                t.HasCheckConstraint("fisier_lectie_stare_text_check", "stare_text IN ('in_asteptare', 'gata', 'fara_text', 'eroare')");
                t.HasCheckConstraint("fisier_lectie_marime_check", "marime_octeti > 0");
            });
            e.HasKey(x => x.Id).HasName("fisier_lectie_pkey");
            e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(x => x.ParcursId).HasColumnName("parcurs_id");
            e.Property(x => x.Nume).HasColumnName("nume");
            e.Property(x => x.Tip).HasColumnName("tip");
            e.Property(x => x.Cheie).HasColumnName("cheie");
            e.Property(x => x.MarimeOcteti).HasColumnName("marime_octeti");
            e.Property(x => x.Sha256).HasColumnName("sha256");
            e.Property(x => x.StareText).HasColumnName("stare_text").HasDefaultValue(FisierLectie.InAsteptare);
            e.Property(x => x.TextExtras).HasColumnName("text_extras");
            e.Property(x => x.IncarcatDeId).HasColumnName("incarcat_de_id");
            e.Property(x => x.IncarcatLa).HasColumnName("incarcat_la").HasDefaultValueSql("now()");
            e.HasIndex(x => x.Cheie, "fisier_lectie_cheie_key").IsUnique();
            // One file of each type per Lecție.
            e.HasIndex(x => new { x.ParcursId, x.Tip }, "fisier_lectie_parcurs_id_tip_key").IsUnique();
            e.HasIndex(x => x.IncarcatDeId, "idx_fisier_lectie_incarcat_de_id_fkey");
            // Rows go with their Lecție; StergereContinut removes the stored files after the commit.
            e.HasOne(x => x.Parcurs).WithMany(p => p.Fisiere).HasForeignKey(x => x.ParcursId)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fisier_lectie_parcurs_id_fkey");
            e.HasOne(x => x.IncarcatDe).WithMany().HasForeignKey(x => x.IncarcatDeId)
                .OnDelete(DeleteBehavior.SetNull).HasConstraintName("fisier_lectie_incarcat_de_id_fkey");
        });

        model.Entity<Intrebare>(e =>
        {
            e.ToTable("intrebare", "mark_ai", t =>
            {
                t.HasCheckConstraint("intrebare_text_check", "length(btrim(text)) > 0");
                t.HasCheckConstraint("intrebare_tip_check", "tip IN ('grila', 'estimare')");
                t.HasCheckConstraint("intrebare_sursa_check", "sursa IN ('ai', 'manual')");
                t.HasCheckConstraint("intrebare_estimare_check", "(tip = 'estimare') = (raspuns_numeric IS NOT NULL)");
            });
            e.HasKey(x => x.Id).HasName("intrebare_pkey");
            e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(x => x.MaterieId).HasColumnName("materie_id");
            e.Property(x => x.ParcursId).HasColumnName("parcurs_id");
            e.Property(x => x.Text).HasColumnName("text");
            e.Property(x => x.Activa).HasColumnName("activa").HasDefaultValue(true);
            e.Property(x => x.Tip).HasColumnName("tip").HasDefaultValue(Intrebare.Grila);
            e.Property(x => x.RaspunsNumeric).HasColumnName("raspuns_numeric");
            e.Property(x => x.Explicatie).HasColumnName("explicatie");
            e.Property(x => x.Sursa).HasColumnName("sursa").HasDefaultValue(Intrebare.SursaManual);
            e.Property(x => x.CreataLa).HasColumnName("creata_la").HasDefaultValueSql("now()");
            e.HasIndex(x => x.MaterieId, "idx_intrebare_materie_id_fkey");
            e.HasIndex(x => new { x.ParcursId, x.MaterieId }, "idx_intrebare_parcurs_id_materie_id_fkey");
            e.HasOne<Materie>().WithMany().HasForeignKey(x => x.MaterieId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("intrebare_materie_id_fkey");
            // The composite key keeps a question's Lecție inside the question's subject; questions go with their Lecție.
            e.HasOne(x => x.Parcurs).WithMany(p => p.Intrebari).HasForeignKey(x => new { x.ParcursId, x.MaterieId })
                .HasPrincipalKey(p => new { p.Id, p.MaterieId }).OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("intrebare_parcurs_id_materie_id_fkey");
        });

        model.Entity<VariantaIntrebare>(e =>
        {
            e.ToTable("intrebare_varianta", "mark_ai", t => t.HasCheckConstraint("intrebare_varianta_text_check", "length(btrim(text)) > 0"));
            e.HasKey(x => x.Id).HasName("intrebare_varianta_pkey");
            e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(x => x.IntrebareId).HasColumnName("intrebare_id");
            e.Property(x => x.Text).HasColumnName("text");
            e.Property(x => x.Corecta).HasColumnName("corecta");
            e.HasIndex(x => x.IntrebareId, "idx_intrebare_varianta_intrebare_id_fkey");
            // At most one correct option per question.
            e.HasIndex(x => x.IntrebareId, "intrebare_varianta_o_corecta").IsUnique().HasFilter("corecta");
            e.HasOne(x => x.Intrebare).WithMany(q => q.Variante).HasForeignKey(x => x.IntrebareId)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("intrebare_varianta_intrebare_id_fkey");
        });

        model.Entity<ProgresLectie>(e =>
        {
            e.ToTable("progres_lectie", "mark_ai");
            e.HasKey(x => new { x.UtilizatorId, x.ParcursId }).HasName("progres_lectie_pkey");
            e.Property(x => x.UtilizatorId).HasColumnName("utilizator_id");
            e.Property(x => x.ParcursId).HasColumnName("parcurs_id");
            e.Property(x => x.UltimulCursId).HasColumnName("ultimul_curs_id");
            e.Property(x => x.PromovatLa).HasColumnName("promovat_la");
            e.Property(x => x.UltimaAccesare).HasColumnName("ultima_accesare").HasDefaultValueSql("now()");
            e.HasIndex(x => x.ParcursId, "idx_progres_lectie_parcurs_id_fkey");
            e.HasIndex(x => x.UltimulCursId, "idx_progres_lectie_ultimul_curs_id_fkey");
            e.HasOne(x => x.Utilizator).WithMany().HasForeignKey(x => x.UtilizatorId)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("progres_lectie_utilizator_id_fkey");
            e.HasOne(x => x.Parcurs).WithMany(p => p.Progrese).HasForeignKey(x => x.ParcursId)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("progres_lectie_parcurs_id_fkey");
            e.HasOne(x => x.UltimulCurs).WithMany().HasForeignKey(x => x.UltimulCursId)
                .OnDelete(DeleteBehavior.SetNull).HasConstraintName("progres_lectie_ultimul_curs_id_fkey");
        });
    }
}

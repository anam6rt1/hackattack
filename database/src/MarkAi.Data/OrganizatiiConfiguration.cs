using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Data;

/// <summary>Model additions for organisations: counties and localities (SIRUTA), organisations and their members.</summary>
internal static class OrganizatiiConfiguration
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<Judet>(e =>
        {
            e.ToTable("judet", "mark_ai");
            e.HasKey(x => x.Id).HasName("judet_pkey");
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.Nume).HasColumnName("nume");
        });

        model.Entity<Localitate>(e =>
        {
            e.ToTable("localitate", "mark_ai");
            e.HasKey(x => x.Id).HasName("localitate_pkey");
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.JudetId).HasColumnName("judet_id");
            e.Property(x => x.Nume).HasColumnName("nume");
            e.Property(x => x.UatId).HasColumnName("uat_id");
            e.Property(x => x.UatNume).HasColumnName("uat_nume");
            e.Property(x => x.Urban).HasColumnName("urban");
            e.Property(x => x.Cautare).HasColumnName("cautare");
            e.HasIndex(x => new { x.JudetId, x.Cautare }, "idx_localitate_judet_cautare");
            e.HasOne(x => x.Judet).WithMany(j => j.Localitati).HasForeignKey(x => x.JudetId).HasConstraintName("localitate_judet_id_fkey");
        });

        model.Entity<Utilizator>(e =>
        {
            e.Property(x => x.AvertizatAprobareLa).HasColumnName("avertizat_aprobare_la");
            e.Property(x => x.JudetId).HasColumnName("judet_id");
            e.Property(x => x.LocalitateId).HasColumnName("localitate_id");
            e.Property(x => x.Clasa).HasColumnName("clasa");
            e.HasOne<Judet>().WithMany().HasForeignKey(x => x.JudetId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("utilizator_judet_id_fkey");
            e.HasOne<Localitate>().WithMany().HasForeignKey(x => x.LocalitateId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("utilizator_localitate_id_fkey");
            e.ToTable(t => t.HasCheckConstraint("utilizator_clasa_check", "clasa IS NULL OR clasa IN ('IX', 'X', 'XI', 'XII')"));
        });

        model.Entity<Organizatie>(e =>
        {
            e.ToTable("organizatie", "mark_ai", t => t.HasCheckConstraint("organizatie_stare_check", "stare IN ('in_asteptare', 'aprobata', 'respinsa')"));
            e.HasKey(x => x.Id).HasName("organizatie_pkey");
            e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(x => x.Nume).HasColumnName("nume");
            e.Property(x => x.Adresa).HasColumnName("adresa");
            e.Property(x => x.JudetId).HasColumnName("judet_id");
            e.Property(x => x.LocalitateId).HasColumnName("localitate_id");
            e.Property(x => x.Stare).HasColumnName("stare");
            e.Property(x => x.AdminId).HasColumnName("admin_id");
            e.Property(x => x.MotivRespingere).HasColumnName("motiv_respingere");
            e.Property(x => x.DecisDeId).HasColumnName("decis_de_id");
            e.Property(x => x.DecisLa).HasColumnName("decis_la");
            e.Property(x => x.CreatLa).HasColumnName("creat_la");
            // One admin per organisation, and one organisation per admin account.
            e.HasIndex(x => x.AdminId, "organizatie_admin_id_key").IsUnique();
            e.HasIndex(x => new { x.Stare, x.JudetId }, "idx_organizatie_stare_judet");
            e.HasOne(x => x.Admin).WithMany().HasForeignKey(x => x.AdminId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("organizatie_admin_id_fkey");
            e.HasOne<Utilizator>().WithMany().HasForeignKey(x => x.DecisDeId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("organizatie_decis_de_id_fkey");
            e.HasOne(x => x.Judet).WithMany().HasForeignKey(x => x.JudetId).HasConstraintName("organizatie_judet_id_fkey");
            e.HasOne(x => x.Localitate).WithMany().HasForeignKey(x => x.LocalitateId).HasConstraintName("organizatie_localitate_id_fkey");
        });

        model.Entity<MembruOrganizatie>(e =>
        {
            e.ToTable("membru_organizatie", "mark_ai", t =>
            {
                t.HasCheckConstraint("membru_organizatie_rol_check", "rol IN ('profesor', 'elev')");
                t.HasCheckConstraint("membru_organizatie_stare_check", "stare IN ('in_asteptare', 'aprobat', 'respins', 'expirat')");
            });
            e.HasKey(x => new { x.OrganizatieId, x.UtilizatorId }).HasName("membru_organizatie_pkey");
            e.Property(x => x.OrganizatieId).HasColumnName("organizatie_id");
            e.Property(x => x.UtilizatorId).HasColumnName("utilizator_id");
            e.Property(x => x.Rol).HasColumnName("rol");
            e.Property(x => x.Stare).HasColumnName("stare");
            e.Property(x => x.PoatePublicaPublic).HasColumnName("poate_publica_public").HasDefaultValue(true);
            e.Property(x => x.CerutLa).HasColumnName("cerut_la");
            e.Property(x => x.ExpiraLa).HasColumnName("expira_la");
            e.Property(x => x.AmintitLa).HasColumnName("amintit_la");
            e.Property(x => x.DecisDeId).HasColumnName("decis_de_id");
            e.Property(x => x.DecisLa).HasColumnName("decis_la");
            e.Property(x => x.MotivRespingere).HasColumnName("motiv_respingere");
            // A student belongs to (or asks to join) at most one organisation at a time.
            e.HasIndex(x => x.UtilizatorId, "membru_organizatie_un_elev_key").IsUnique()
                .HasFilter("rol = 'elev' AND stare IN ('in_asteptare', 'aprobat')");
            e.HasIndex(x => new { x.OrganizatieId, x.Rol, x.Stare }, "idx_membru_organizatie_org_rol_stare");
            e.HasOne(x => x.Organizatie).WithMany(o => o.Membri).HasForeignKey(x => x.OrganizatieId)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("membru_organizatie_organizatie_id_fkey");
            e.HasOne(x => x.Utilizator).WithMany(u => u.Organizatii).HasForeignKey(x => x.UtilizatorId)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("membru_organizatie_utilizator_id_fkey");
            e.HasOne<Utilizator>().WithMany().HasForeignKey(x => x.DecisDeId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("membru_organizatie_decis_de_id_fkey");
        });
    }
}

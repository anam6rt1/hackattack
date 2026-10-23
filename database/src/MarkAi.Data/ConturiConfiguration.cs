using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Data;

/// <summary>Model additions for verified accounts: email confirmation, phone verification, one-time codes, outgoing messages.</summary>
internal static class ConturiConfiguration
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<Utilizator>(e =>
        {
            e.Property(x => x.EmailConfirmatLa).HasColumnName("email_confirmat_la");
            e.Property(x => x.AvertizatLa).HasColumnName("avertizat_la");
            e.Property(x => x.TelefonVerificatLa).HasColumnName("telefon_verificat_la");
            // Search columns (ILIKE '%…%' on them): trigram indexes keep it fast with many accounts.
            e.Property(x => x.CautareNume).HasColumnName("cautare_nume").HasComputedColumnSql(CautareText.Sql("nume"), stored: true);
            e.Property(x => x.CautareCont).HasColumnName("cautare_cont")
                .HasComputedColumnSql(CautareText.Sql("nume || ' ' || username || ' ' || email"), stored: true);
            e.HasIndex(x => x.CautareNume, "idx_utilizator_cautare_nume_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
            e.HasIndex(x => x.CautareCont, "idx_utilizator_cautare_cont_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
        });

        model.Entity<CodVerificare>(e =>
        {
            e.ToTable("cod_verificare", "mark_ai");
            e.HasKey(x => x.Id).HasName("cod_verificare_pkey");
            e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(x => x.UtilizatorId).HasColumnName("utilizator_id");
            e.Property(x => x.Scop).HasColumnName("scop");
            e.Property(x => x.Destinatie).HasColumnName("destinatie");
            e.Property(x => x.HashCod).HasColumnName("hash_cod");
            e.Property(x => x.Incercari).HasColumnName("incercari");
            e.Property(x => x.CreatLa).HasColumnName("creat_la");
            e.Property(x => x.ExpiraLa).HasColumnName("expira_la");
            e.Property(x => x.FolositLa).HasColumnName("folosit_la");
            e.HasIndex(x => new { x.UtilizatorId, x.Scop }, "idx_cod_verificare_utilizator_scop");
            e.HasOne<Utilizator>().WithMany().HasForeignKey(x => x.UtilizatorId)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("cod_verificare_utilizator_id_fkey");
        });

        model.Entity<MesajIesire>(e =>
        {
            e.ToTable("mesaj_iesire", "mark_ai");
            e.HasKey(x => x.Id).HasName("mesaj_iesire_pkey");
            e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(x => x.Canal).HasColumnName("canal");
            e.Property(x => x.Catre).HasColumnName("catre");
            e.Property(x => x.Subiect).HasColumnName("subiect");
            e.Property(x => x.Html).HasColumnName("html");
            e.Property(x => x.Text).HasColumnName("text");
            e.Property(x => x.Tip).HasColumnName("tip");
            e.Property(x => x.CreatLa).HasColumnName("creat_la");
            e.Property(x => x.TrimisLa).HasColumnName("trimis_la");
            e.Property(x => x.Furnizor).HasColumnName("furnizor");
            e.Property(x => x.Incercari).HasColumnName("incercari");
            e.Property(x => x.UrmatoareaIncercareLa).HasColumnName("urmatoarea_incercare_la");
            e.Property(x => x.Eroare).HasColumnName("eroare");
            // The sender picks up unsent messages that are due; the admin list and the SMS cap read by date.
            e.HasIndex(x => new { x.TrimisLa, x.UrmatoareaIncercareLa }, "idx_mesaj_iesire_de_trimis");
            e.HasIndex(x => new { x.Canal, x.CreatLa }, "idx_mesaj_iesire_canal_data");
        });
    }
}

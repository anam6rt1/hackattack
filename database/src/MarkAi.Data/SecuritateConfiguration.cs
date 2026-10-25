using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Data;

/// <summary>Model additions for the security work: session revocation, signed call links, quiz attempts and AI limits.</summary>
internal static class SecuritateConfiguration
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<Utilizator>(e => {
            e.Property(x => x.VersiuneSesiune).HasColumnName("versiune_sesiune").HasDefaultValue(0);
            e.Property(x => x.UltimaCerereApel).HasColumnName("ultima_cerere_apel");
        });

        model.Entity<Sesiune>(e =>
        {
            e.Property(x => x.CheieHash).HasColumnName("cheie_hash");
            e.Property(x => x.CheieExpiraLa).HasColumnName("cheie_expira_la");
            e.Property(x => x.DurataSecunde).HasColumnName("durata_secunde");
        });

        model.Entity<IncercareQuiz>(e =>
        {
            e.ToTable("incercare_quiz", "mark_ai");
            e.HasKey(x => x.Id).HasName("incercare_quiz_pkey");
            e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(x => x.UtilizatorId).HasColumnName("utilizator_id");
            e.Property(x => x.ParcursId).HasColumnName("parcurs_id");
            e.Property(x => x.IntrebariIds).HasColumnName("intrebari_ids");
            e.Property(x => x.Raspunsuri).HasColumnName("raspunsuri");
            e.Property(x => x.Corecte).HasColumnName("corecte");
            e.Property(x => x.InceputLa).HasColumnName("inceput_la").HasDefaultValueSql("now()");
            e.Property(x => x.FinalizataLa).HasColumnName("finalizata_la");
            e.HasIndex(x => new { x.UtilizatorId, x.ParcursId, x.InceputLa }, "idx_incercare_quiz_utilizator_parcurs");
            e.HasIndex(x => x.ParcursId, "idx_incercare_quiz_parcurs_id_fkey");
            e.HasOne(x => x.Utilizator).WithMany().HasForeignKey(x => x.UtilizatorId)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("incercare_quiz_utilizator_id_fkey");
            e.HasOne(x => x.Parcurs).WithMany().HasForeignKey(x => x.ParcursId)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("incercare_quiz_parcurs_id_fkey");
        });

        model.Entity<GenerareAi>(e =>
        {
            e.Property(x => x.Stare).HasColumnName("stare").HasDefaultValue("gata");
            e.Property(x => x.Mesaj).HasColumnName("mesaj");
            e.Property(x => x.IntrebariGenerate).HasColumnName("intrebari_generate");
            e.Property(x => x.Actualizat).HasColumnName("actualizat");
            e.HasIndex(x => x.Stare).HasFilter("stare IN ('in_asteptare', 'in_lucru')");
            e.ToTable("generare_ai", "mark_ai");
            e.HasKey(x => x.Id).HasName("generare_ai_pkey");
            e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(x => x.UtilizatorId).HasColumnName("utilizator_id");
            e.Property(x => x.ParcursId).HasColumnName("parcurs_id");
            e.Property(x => x.Numar).HasColumnName("numar");
            e.Property(x => x.CreatLa).HasColumnName("creat_la").HasDefaultValueSql("now()");
            e.HasIndex(x => new { x.UtilizatorId, x.CreatLa }, "idx_generare_ai_utilizator_data");
            // A log only: no foreign keys, so deleting an account or a Lecție never has to touch it first.
        });
    }
}

using Microsoft.EntityFrameworkCore;
using MarkAi.Data.Entities;
namespace MarkAi.Data;

public partial class MarkAiDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        MarkAi.Data.Trivia.TriviaConfiguration.Configure(modelBuilder);
        IntegrareConfiguration.Configure(modelBuilder);
        SecuritateConfiguration.Configure(modelBuilder);
        ConturiConfiguration.Configure(modelBuilder);
        OrganizatiiConfiguration.Configure(modelBuilder);
        ParcursuriConfiguration.Configure(modelBuilder);
        modelBuilder.Entity<EvenimentRecompensa>().Property(x => x.XpAcordat).HasDefaultValue(0);
        modelBuilder.Entity<EvenimentRecompensa>().Property(x => x.PuncteMaterie).HasDefaultValue(0);
        modelBuilder.Entity<Realizare>().Property(x => x.XpBonus).HasDefaultValue(0);
        modelBuilder.Entity<RegulaBackground>().Property(x => x.Prioritate).HasDefaultValue(0);
        modelBuilder.Entity<Background>().ToTable("background", "mark_ai", t => t.HasCheckConstraint("background_check", @"((disponibil_de_la IS NULL) OR (disponibil_pana_la IS NULL) OR (disponibil_pana_la > disponibil_de_la))"));
        modelBuilder.Entity<CameraTutoring>().ToTable("camera_tutoring", "mark_ai", t => t.HasCheckConstraint("camera_tutoring_capacitate_check", @"(capacitate > 0)"));
        modelBuilder.Entity<CameraTutoring>().ToTable("camera_tutoring", "mark_ai", t => t.HasCheckConstraint("camera_tutoring_check", @"((finalizat_la IS NULL) OR ((inceput_la IS NOT NULL) AND (finalizat_la >= inceput_la)))"));
        modelBuilder.Entity<ComisionMentor>().ToTable("comision_mentor", "mark_ai", t => t.HasCheckConstraint("comision_mentor_check", @"((suma_comision >= (0)::numeric) AND (suma_comision <= tarif_baza))"));
        modelBuilder.Entity<ComisionMentor>().ToTable("comision_mentor", "mark_ai", t => t.HasCheckConstraint("comision_mentor_moneda_check", @"(moneda ~ '^[A-Z]{3}$'::text)"));
        modelBuilder.Entity<ComisionMentor>().ToTable("comision_mentor", "mark_ai", t => t.HasCheckConstraint("comision_mentor_procent_snapshot_check", @"((procent_snapshot >= (0)::numeric) AND (procent_snapshot <= (100)::numeric))"));
        modelBuilder.Entity<ComisionMentor>().ToTable("comision_mentor", "mark_ai", t => t.HasCheckConstraint("comision_mentor_tarif_baza_check", @"(tarif_baza >= (0)::numeric)"));
        modelBuilder.Entity<Decontare>().ToTable("decontare", "mark_ai", t => t.HasCheckConstraint("decontare_check", @"((platita_la IS NULL) OR (platita_la >= creata_la))"));
        modelBuilder.Entity<Decontare>().ToTable("decontare", "mark_ai", t => t.HasCheckConstraint("decontare_moneda_check", @"(moneda ~ '^[A-Z]{3}$'::text)"));
        modelBuilder.Entity<Decontare>().ToTable("decontare", "mark_ai", t => t.HasCheckConstraint("decontare_suma_totala_check", @"(suma_totala >= (0)::numeric)"));
        modelBuilder.Entity<EvenimentRecompensa>().ToTable("eveniment_recompensa", "mark_ai", t => t.HasCheckConstraint("eveniment_recompensa_check", @"((puncte_materie = 0) OR (materie_id IS NOT NULL))"));
        modelBuilder.Entity<EvenimentRecompensa>().ToTable("eveniment_recompensa", "mark_ai", t => t.HasCheckConstraint("eveniment_recompensa_puncte_materie_check", @"(puncte_materie >= 0)"));
        modelBuilder.Entity<EvenimentRecompensa>().ToTable("eveniment_recompensa", "mark_ai", t => t.HasCheckConstraint("eveniment_recompensa_xp_acordat_check", @"(xp_acordat >= 0)"));
        modelBuilder.Entity<MuchieGraf>().ToTable("muchie_graf", "mark_ai", t => t.HasCheckConstraint("muchie_graf_check", @"(curs_sursa_id <> curs_destinatie_id)"));
        modelBuilder.Entity<NivelXp>().ToTable("nivel_xp", "mark_ai", t => t.HasCheckConstraint("nivel_xp_nivel_check", @"(nivel >= 0)"));
        modelBuilder.Entity<NivelXp>().ToTable("nivel_xp", "mark_ai", t => t.HasCheckConstraint("nivel_xp_xp_necesar_check", @"(xp_necesar >= 0)"));
        modelBuilder.Entity<Plata>().ToTable("plata", "mark_ai", t => t.HasCheckConstraint("plata_moneda_check", @"(moneda ~ '^[A-Z]{3}$'::text)"));
        modelBuilder.Entity<Plata>().ToTable("plata", "mark_ai", t => t.HasCheckConstraint("plata_suma_check", @"(suma >= (0)::numeric)"));
        modelBuilder.Entity<ProfilMentor>().ToTable("profil_mentor", "mark_ai", t => t.HasCheckConstraint("profil_mentor_procent_comision_check", @"((procent_comision >= (0)::numeric) AND (procent_comision <= (100)::numeric))"));
        modelBuilder.Entity<Realizare>().ToTable("realizare", "mark_ai", t => t.HasCheckConstraint("realizare_xp_bonus_check", @"(xp_bonus >= 0)"));
        modelBuilder.Entity<Rezervare>().ToTable("rezervare", "mark_ai", t => t.HasCheckConstraint("rezervare_check", @"((finalizata_la IS NULL) OR (finalizata_la >= rezervata_la))"));
        modelBuilder.Entity<Rezervare>().ToTable("rezervare", "mark_ai", t => t.HasCheckConstraint("rezervare_moneda_check", @"(moneda ~ '^[A-Z]{3}$'::text)"));
        modelBuilder.Entity<Rezervare>().ToTable("rezervare", "mark_ai", t => t.HasCheckConstraint("rezervare_tarif_achitat_check", @"(tarif_achitat >= (0)::numeric)"));
        modelBuilder.Entity<Sesiune>().ToTable("sesiune", "mark_ai", t => t.HasCheckConstraint("sesiune_check", @"((finalizat_la IS NULL) OR (finalizat_la >= inceput_la))"));
        modelBuilder.Entity<TablaPas>().ToTable("tabla_pas", "mark_ai", t => t.HasCheckConstraint("tabla_pas_ordine_check", @"(ordine >= 0)"));
        modelBuilder.Entity<TarifFix>().ToTable("tarif_fix", "mark_ai", t => t.HasCheckConstraint("tarif_fix_durata_minute_check", @"(durata_minute > 0)"));
        modelBuilder.Entity<TarifFix>().ToTable("tarif_fix", "mark_ai", t => t.HasCheckConstraint("tarif_fix_moneda_check", @"(moneda ~ '^[A-Z]{3}$'::text)"));
        modelBuilder.Entity<TarifFix>().ToTable("tarif_fix", "mark_ai", t => t.HasCheckConstraint("tarif_fix_suma_check", @"(suma >= (0)::numeric)"));
    }
}



using System;
using System.Collections.Generic;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Data;

public partial class MarkAiDbContext : DbContext
{
    public MarkAiDbContext(DbContextOptions<MarkAiDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Background> Backgrounds { get; set; }

    public virtual DbSet<CameraTutoring> CameraTutorings { get; set; }

    public virtual DbSet<ComisionMentor> ComisionMentors { get; set; }

    public virtual DbSet<Curs> Curs { get; set; }

    public virtual DbSet<Decontare> Decontares { get; set; }

    public virtual DbSet<EvenimentRecompensa> EvenimentRecompensas { get; set; }

    public virtual DbSet<Graf> Grafs { get; set; }

    public virtual DbSet<GrafCurs> GrafCurs { get; set; }

    public virtual DbSet<Interfata> Interfata { get; set; }

    public virtual DbSet<Materie> Materies { get; set; }

    public virtual DbSet<Mesaj> Mesajs { get; set; }

    public virtual DbSet<MuchieGraf> MuchieGrafs { get; set; }

    public virtual DbSet<NivelXp> NivelXps { get; set; }

    public virtual DbSet<Plata> Plata { get; set; }

    public virtual DbSet<PreferinteUtilizator> PreferinteUtilizators { get; set; }

    public virtual DbSet<ProfilMentor> ProfilMentors { get; set; }

    public virtual DbSet<Realizare> Realizares { get; set; }

    public virtual DbSet<RegulaBackground> RegulaBackgrounds { get; set; }

    public virtual DbSet<Rezervare> Rezervares { get; set; }

    public virtual DbSet<Rol> Rols { get; set; }

    public virtual DbSet<Sesiune> Sesiunes { get; set; }

    public virtual DbSet<SmsLog> SmsLogs { get; set; }

    public virtual DbSet<TablaPas> TablaPas { get; set; }

    public virtual DbSet<TarifFix> TarifFixes { get; set; }

    public virtual DbSet<Utilizator> Utilizators { get; set; }

    public virtual DbSet<UtilizatorMaterie> UtilizatorMateries { get; set; }

    public virtual DbSet<UtilizatorRealizare> UtilizatorRealizares { get; set; }

    public virtual DbSet<UtilizatorRol> UtilizatorRols { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Background>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("background_pkey");

            entity.ToTable("background", "mark_ai");

            entity.HasIndex(e => e.Cod, "background_cod_key").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.Activ)
                .HasDefaultValue(true)
                .HasColumnName("activ");
            entity.Property(e => e.AssetUrl).HasColumnName("asset_url");
            entity.Property(e => e.Cod).HasColumnName("cod");
            entity.Property(e => e.DisponibilDeLa).HasColumnName("disponibil_de_la");
            entity.Property(e => e.DisponibilPanaLa).HasColumnName("disponibil_pana_la");
            entity.Property(e => e.Nume).HasColumnName("nume");
            entity.Property(e => e.SelectabilStatic)
                .HasDefaultValue(true)
                .HasColumnName("selectabil_static");
        });

        modelBuilder.Entity<CameraTutoring>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("camera_tutoring_pkey");

            entity.ToTable("camera_tutoring", "mark_ai");

            entity.HasIndex(e => new { e.Id, e.MentorId }, "camera_tutoring_id_mentor_id_key").IsUnique();

            entity.HasIndex(e => new { e.CursId, e.MaterieId }, "idx_camera_tutoring_curs_id_materie_id_fkey");

            entity.HasIndex(e => e.MaterieId, "idx_camera_tutoring_materie_id_fkey");

            entity.HasIndex(e => e.MentorId, "idx_camera_tutoring_mentor_id_fkey");

            entity.HasIndex(e => e.TarifId, "idx_camera_tutoring_tarif_id_fkey");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.Capacitate).HasColumnName("capacitate");
            entity.Property(e => e.CursId).HasColumnName("curs_id");
            entity.Property(e => e.Descriere).HasColumnName("descriere");
            entity.Property(e => e.FinalizatLa).HasColumnName("finalizat_la");
            entity.Property(e => e.InceputLa).HasColumnName("inceput_la");
            entity.Property(e => e.MaterieId).HasColumnName("materie_id");
            entity.Property(e => e.MentorId).HasColumnName("mentor_id");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.TarifId).HasColumnName("tarif_id");
            entity.Property(e => e.Titlu).HasColumnName("titlu");

            entity.HasOne(d => d.Materie).WithMany(p => p.CameraTutorings)
                .HasForeignKey(d => d.MaterieId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("camera_tutoring_materie_id_fkey");

            entity.HasOne(d => d.Mentor).WithMany(p => p.CameraTutorings)
                .HasForeignKey(d => d.MentorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("camera_tutoring_mentor_id_fkey");

            entity.HasOne(d => d.Tarif).WithMany(p => p.CameraTutorings)
                .HasForeignKey(d => d.TarifId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("camera_tutoring_tarif_id_fkey");

            entity.HasOne(d => d.Curs).WithMany(p => p.CameraTutorings)
                .HasPrincipalKey(p => new { p.Id, p.MaterieId })
                .HasForeignKey(d => new { d.CursId, d.MaterieId })
                .HasConstraintName("camera_tutoring_curs_id_materie_id_fkey");
        });

        modelBuilder.Entity<ComisionMentor>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("comision_mentor_pkey");

            entity.ToTable("comision_mentor", "mark_ai");

            entity.HasIndex(e => e.RezervareId, "comision_mentor_rezervare_id_key").IsUnique();

            entity.HasIndex(e => new { e.DecontareId, e.MentorId, e.Moneda }, "idx_comision_mentor_decontare_id_mentor_id_moneda_fkey");

            entity.HasIndex(e => e.MentorId, "idx_comision_mentor_mentor_id_fkey");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.CalculatLa)
                .HasDefaultValueSql("now()")
                .HasColumnName("calculat_la");
            entity.Property(e => e.DecontareId).HasColumnName("decontare_id");
            entity.Property(e => e.MentorId).HasColumnName("mentor_id");
            entity.Property(e => e.Moneda).HasColumnName("moneda");
            entity.Property(e => e.ProcentSnapshot)
                .HasPrecision(5, 2)
                .HasColumnName("procent_snapshot");
            entity.Property(e => e.RezervareId).HasColumnName("rezervare_id");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.SumaComision)
                .HasPrecision(14, 2)
                .HasColumnName("suma_comision");
            entity.Property(e => e.TarifBaza)
                .HasPrecision(14, 2)
                .HasColumnName("tarif_baza");

            entity.HasOne(d => d.Mentor).WithMany(p => p.ComisionMentors)
                .HasForeignKey(d => d.MentorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("comision_mentor_mentor_id_fkey");

            entity.HasOne(d => d.Rezervare).WithOne(p => p.ComisionMentor)
                .HasForeignKey<ComisionMentor>(d => d.RezervareId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("comision_mentor_rezervare_id_fkey");

            entity.HasOne(d => d.Decontare).WithMany(p => p.ComisionMentors)
                .HasPrincipalKey(p => new { p.Id, p.MentorId, p.Moneda })
                .HasForeignKey(d => new { d.DecontareId, d.MentorId, d.Moneda })
                .HasConstraintName("comision_mentor_decontare_id_mentor_id_moneda_fkey");
        });

        modelBuilder.Entity<Curs>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("curs_pkey");

            entity.ToTable("curs", "mark_ai");

            entity.HasIndex(e => new { e.Id, e.MaterieId }, "curs_id_materie_id_key").IsUnique();

            entity.HasIndex(e => e.MaterieId, "idx_curs_materie_id_fkey");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.Continut).HasColumnName("continut");
            entity.Property(e => e.Descriere).HasColumnName("descriere");
            entity.Property(e => e.MaterieId).HasColumnName("materie_id");
            entity.Property(e => e.Titlu).HasColumnName("titlu");

            entity.HasOne(d => d.Materie).WithMany(p => p.Curs)
                .HasForeignKey(d => d.MaterieId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("curs_materie_id_fkey");
        });

        modelBuilder.Entity<Decontare>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("decontare_pkey");

            entity.ToTable("decontare", "mark_ai");

            entity.HasIndex(e => new { e.Id, e.MentorId, e.Moneda }, "decontare_id_mentor_id_moneda_key").IsUnique();

            entity.HasIndex(e => e.ReferintaPlata, "decontare_referinta_plata_key").IsUnique();

            entity.HasIndex(e => e.MentorId, "idx_decontare_mentor_id_fkey");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.CreataLa)
                .HasDefaultValueSql("now()")
                .HasColumnName("creata_la");
            entity.Property(e => e.MentorId).HasColumnName("mentor_id");
            entity.Property(e => e.Moneda).HasColumnName("moneda");
            entity.Property(e => e.PlatitaLa).HasColumnName("platita_la");
            entity.Property(e => e.ReferintaPlata).HasColumnName("referinta_plata");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.SumaTotala)
                .HasPrecision(14, 2)
                .HasColumnName("suma_totala");

            entity.HasOne(d => d.Mentor).WithMany(p => p.Decontares)
                .HasForeignKey(d => d.MentorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("decontare_mentor_id_fkey");
        });

        modelBuilder.Entity<EvenimentRecompensa>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("eveniment_recompensa_pkey");

            entity.ToTable("eveniment_recompensa", "mark_ai", tb => tb.HasComment("Jurnal XP; nivelul se deriva din SUM(xp_acordat) si pragurile nivel_xp. Referinta la utilizator_materie foloseste cheia compusa, fara utilizator_id_materie inexistent."));

            entity.HasIndex(e => e.CheieUnica, "eveniment_recompensa_cheie_unica_key").IsUnique();

            entity.HasIndex(e => new { e.UtilizatorId, e.CreatLa }, "eveniment_recompensa_utilizator_data");

            entity.HasIndex(e => new { e.SesiuneId, e.UtilizatorId }, "idx_eveniment_recompensa_sesiune_id_utilizator_id_fkey");

            entity.HasIndex(e => e.UtilizatorId, "idx_eveniment_recompensa_utilizator_id_fkey");

            entity.HasIndex(e => new { e.UtilizatorId, e.MaterieId }, "idx_eveniment_recompensa_utilizator_id_materie_id_fkey");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.CheieUnica).HasColumnName("cheie_unica");
            entity.Property(e => e.CreatLa)
                .HasDefaultValueSql("now()")
                .HasColumnName("creat_la");
            entity.Property(e => e.MaterieId).HasColumnName("materie_id");
            entity.Property(e => e.PuncteMaterie).HasColumnName("puncte_materie");
            entity.Property(e => e.SesiuneId).HasColumnName("sesiune_id");
            entity.Property(e => e.TipEveniment).HasColumnName("tip_eveniment");
            entity.Property(e => e.UtilizatorId).HasColumnName("utilizator_id");
            entity.Property(e => e.XpAcordat).HasColumnName("xp_acordat");

            entity.HasOne(d => d.Utilizator).WithMany(p => p.EvenimentRecompensas)
                .HasForeignKey(d => d.UtilizatorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("eveniment_recompensa_utilizator_id_fkey");

            entity.HasOne(d => d.Sesiune).WithMany(p => p.EvenimentRecompensas)
                .HasPrincipalKey(p => new { p.Id, p.UtilizatorId })
                .HasForeignKey(d => new { d.SesiuneId, d.UtilizatorId })
                .HasConstraintName("eveniment_recompensa_sesiune_id_utilizator_id_fkey");

            entity.HasOne(d => d.UtilizatorMaterie).WithMany(p => p.EvenimentRecompensas)
                .HasForeignKey(d => new { d.UtilizatorId, d.MaterieId })
                .HasConstraintName("eveniment_recompensa_utilizator_id_materie_id_fkey");
        });

        modelBuilder.Entity<Graf>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("graf_pkey");

            entity.ToTable("graf", "mark_ai");

            entity.HasIndex(e => new { e.Id, e.MaterieId }, "graf_id_materie_id_key").IsUnique();

            entity.HasIndex(e => e.MaterieId, "idx_graf_materie_id_fkey");

            entity.HasIndex(e => e.OwnerId, "idx_graf_owner_id_fkey");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.CreatLa)
                .HasDefaultValueSql("now()")
                .HasColumnName("creat_la");
            entity.Property(e => e.Descriere).HasColumnName("descriere");
            entity.Property(e => e.MaterieId).HasColumnName("materie_id");
            entity.Property(e => e.OwnerId).HasColumnName("owner_id");
            entity.Property(e => e.Titlu).HasColumnName("titlu");

            entity.HasOne(d => d.Materie).WithMany(p => p.Grafs)
                .HasForeignKey(d => d.MaterieId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("graf_materie_id_fkey");

            entity.HasOne(d => d.Owner).WithMany(p => p.Grafs)
                .HasForeignKey(d => d.OwnerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("graf_owner_id_fkey");
        });

        modelBuilder.Entity<GrafCurs>(entity =>
        {
            entity.HasKey(e => new { e.GrafId, e.CursId }).HasName("graf_curs_pkey");

            entity.ToTable("graf_curs", "mark_ai");

            entity.HasIndex(e => new { e.CursId, e.MaterieId }, "idx_graf_curs_curs_id_materie_id_fkey");

            entity.HasIndex(e => new { e.GrafId, e.MaterieId }, "idx_graf_curs_graf_id_materie_id_fkey");

            entity.Property(e => e.GrafId).HasColumnName("graf_id");
            entity.Property(e => e.CursId).HasColumnName("curs_id");
            entity.Property(e => e.AdaugatLa)
                .HasDefaultValueSql("now()")
                .HasColumnName("adaugat_la");
            entity.Property(e => e.MaterieId).HasColumnName("materie_id");

            entity.HasOne(d => d.Curs).WithMany(p => p.GrafCurs)
                .HasPrincipalKey(p => new { p.Id, p.MaterieId })
                .HasForeignKey(d => new { d.CursId, d.MaterieId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("graf_curs_curs_id_materie_id_fkey");

            entity.HasOne(d => d.Graf).WithMany(p => p.GrafCurs)
                .HasPrincipalKey(p => new { p.Id, p.MaterieId })
                .HasForeignKey(d => new { d.GrafId, d.MaterieId })
                .HasConstraintName("graf_curs_graf_id_materie_id_fkey");
        });

        modelBuilder.Entity<Interfata>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("interfata_pkey");

            entity.ToTable("interfata", "mark_ai");

            entity.HasIndex(e => e.Cod, "interfata_cod_key").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.Activa)
                .HasDefaultValue(true)
                .HasColumnName("activa");
            entity.Property(e => e.Cod).HasColumnName("cod");
            entity.Property(e => e.Descriere).HasColumnName("descriere");
            entity.Property(e => e.Nume).HasColumnName("nume");
            entity.Property(e => e.PreviewUrl).HasColumnName("preview_url");
        });

        modelBuilder.Entity<Materie>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("materie_pkey");

            entity.ToTable("materie", "mark_ai");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.Denumire).HasColumnName("denumire");
            entity.Property(e => e.Descriere).HasColumnName("descriere");
        });

        modelBuilder.Entity<Mesaj>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("mesaj_pkey");

            entity.ToTable("mesaj", "mark_ai");

            entity.HasIndex(e => e.SesiuneId, "idx_mesaj_sesiune_id_fkey");

            entity.HasIndex(e => new { e.Id, e.SesiuneId }, "mesaj_id_sesiune_id_key").IsUnique();

            entity.HasIndex(e => new { e.SesiuneId, e.CreatLa }, "mesaj_sesiune_data");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.Continut).HasColumnName("continut");
            entity.Property(e => e.CreatLa)
                .HasDefaultValueSql("now()")
                .HasColumnName("creat_la");
            entity.Property(e => e.Expeditor).HasColumnName("expeditor");
            entity.Property(e => e.SesiuneId).HasColumnName("sesiune_id");
            entity.Property(e => e.Tip).HasColumnName("tip");

            entity.HasOne(d => d.Sesiune).WithMany(p => p.Mesajs)
                .HasForeignKey(d => d.SesiuneId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("mesaj_sesiune_id_fkey");
        });

        modelBuilder.Entity<MuchieGraf>(entity =>
        {
            entity.HasKey(e => new { e.GrafId, e.CursSursaId, e.CursDestinatieId }).HasName("muchie_graf_pkey");

            entity.ToTable("muchie_graf", "mark_ai");

            entity.HasIndex(e => new { e.GrafId, e.CursDestinatieId }, "idx_muchie_graf_graf_id_curs_destinatie_id_fkey");

            entity.Property(e => e.GrafId).HasColumnName("graf_id");
            entity.Property(e => e.CursSursaId).HasColumnName("curs_sursa_id");
            entity.Property(e => e.CursDestinatieId).HasColumnName("curs_destinatie_id");
            entity.Property(e => e.Descriere).HasColumnName("descriere");
            entity.Property(e => e.TipLegatura).HasColumnName("tip_legatura");

            entity.HasOne(d => d.GrafCurs).WithMany(p => p.MuchieGrafGrafCurs)
                .HasForeignKey(d => new { d.GrafId, d.CursDestinatieId })
                .HasConstraintName("muchie_graf_graf_id_curs_destinatie_id_fkey");

            entity.HasOne(d => d.GrafCurNavigation).WithMany(p => p.MuchieGrafGrafCurNavigations)
                .HasForeignKey(d => new { d.GrafId, d.CursSursaId })
                .HasConstraintName("muchie_graf_graf_id_curs_sursa_id_fkey");
        });

        modelBuilder.Entity<NivelXp>(entity =>
        {
            entity.HasKey(e => e.Nivel).HasName("nivel_xp_pkey");

            entity.ToTable("nivel_xp", "mark_ai");

            entity.HasIndex(e => e.XpNecesar, "nivel_xp_xp_necesar_key").IsUnique();

            entity.Property(e => e.Nivel)
                .ValueGeneratedNever()
                .HasColumnName("nivel");
            entity.Property(e => e.Descriere).HasColumnName("descriere");
            entity.Property(e => e.Titlu).HasColumnName("titlu");
            entity.Property(e => e.XpNecesar).HasColumnName("xp_necesar");
        });

        modelBuilder.Entity<Plata>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("plata_pkey");

            entity.ToTable("plata", "mark_ai");

            entity.HasIndex(e => e.RezervareId, "idx_plata_rezervare_id_fkey");

            entity.HasIndex(e => e.TranzactieExterna, "plata_tranzactie_externa_key").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.Moneda).HasColumnName("moneda");
            entity.Property(e => e.PlatitaLa).HasColumnName("platita_la");
            entity.Property(e => e.Procesator).HasColumnName("procesator");
            entity.Property(e => e.RezervareId).HasColumnName("rezervare_id");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.Suma)
                .HasPrecision(14, 2)
                .HasColumnName("suma");
            entity.Property(e => e.TranzactieExterna).HasColumnName("tranzactie_externa");

            entity.HasOne(d => d.Rezervare).WithMany(p => p.Plata)
                .HasForeignKey(d => d.RezervareId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("plata_rezervare_id_fkey");
        });

        modelBuilder.Entity<PreferinteUtilizator>(entity =>
        {
            entity.HasKey(e => e.UtilizatorId).HasName("preferinte_utilizator_pkey");

            entity.ToTable("preferinte_utilizator", "mark_ai");

            entity.HasIndex(e => e.BackgroundStaticId, "idx_preferinte_utilizator_background_static_id_fkey");

            entity.HasIndex(e => e.InterfataId, "idx_preferinte_utilizator_interfata_id_fkey");

            entity.Property(e => e.UtilizatorId)
                .ValueGeneratedNever()
                .HasColumnName("utilizator_id");
            entity.Property(e => e.ActualizatLa)
                .HasDefaultValueSql("now()")
                .HasColumnName("actualizat_la");
            entity.Property(e => e.BackgroundStaticId).HasColumnName("background_static_id");
            entity.Property(e => e.InterfataId).HasColumnName("interfata_id");
            entity.Property(e => e.ModBackground).HasColumnName("mod_background");
            entity.Property(e => e.Timezone)
                .HasDefaultValueSql("'Europe/Bucharest'::text")
                .HasColumnName("timezone");

            entity.HasOne(d => d.BackgroundStatic).WithMany(p => p.PreferinteUtilizators)
                .HasForeignKey(d => d.BackgroundStaticId)
                .HasConstraintName("preferinte_utilizator_background_static_id_fkey");

            entity.HasOne(d => d.Interfata).WithMany(p => p.PreferinteUtilizators)
                .HasForeignKey(d => d.InterfataId)
                .HasConstraintName("preferinte_utilizator_interfata_id_fkey");

            entity.HasOne(d => d.Utilizator).WithOne(p => p.PreferinteUtilizator)
                .HasForeignKey<PreferinteUtilizator>(d => d.UtilizatorId)
                .HasConstraintName("preferinte_utilizator_utilizator_id_fkey");
        });

        modelBuilder.Entity<ProfilMentor>(entity =>
        {
            entity.HasKey(e => e.UtilizatorId).HasName("profil_mentor_pkey");

            entity.ToTable("profil_mentor", "mark_ai");

            entity.Property(e => e.UtilizatorId)
                .ValueGeneratedNever()
                .HasColumnName("utilizator_id");
            entity.Property(e => e.ActivatLa).HasColumnName("activat_la");
            entity.Property(e => e.Descriere).HasColumnName("descriere");
            entity.Property(e => e.ProcentComision)
                .HasPrecision(5, 2)
                .HasColumnName("procent_comision");
            entity.Property(e => e.StareVerificare).HasColumnName("stare_verificare");

            entity.HasOne(d => d.Utilizator).WithOne(p => p.ProfilMentor)
                .HasForeignKey<ProfilMentor>(d => d.UtilizatorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("profil_mentor_utilizator_id_fkey");
        });

        modelBuilder.Entity<Realizare>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("realizare_pkey");

            entity.ToTable("realizare", "mark_ai");

            entity.HasIndex(e => e.Cod, "realizare_cod_key").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.Activa)
                .HasDefaultValue(true)
                .HasColumnName("activa");
            entity.Property(e => e.Cod).HasColumnName("cod");
            entity.Property(e => e.Conditie).HasColumnName("conditie");
            entity.Property(e => e.Descriere).HasColumnName("descriere");
            entity.Property(e => e.Titlu).HasColumnName("titlu");
            entity.Property(e => e.XpBonus).HasColumnName("xp_bonus");
        });

        modelBuilder.Entity<RegulaBackground>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("regula_background_pkey");

            entity.ToTable("regula_background", "mark_ai");

            entity.HasIndex(e => e.BackgroundId, "idx_regula_background_background_id_fkey");

            entity.HasIndex(e => e.InterfataId, "idx_regula_background_interfata_id_fkey");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.Activa)
                .HasDefaultValue(true)
                .HasColumnName("activa");
            entity.Property(e => e.Anotimp).HasColumnName("anotimp");
            entity.Property(e => e.BackgroundId).HasColumnName("background_id");
            entity.Property(e => e.InterfataId).HasColumnName("interfata_id");
            entity.Property(e => e.PerioadaZilei).HasColumnName("perioada_zilei");
            entity.Property(e => e.Prioritate).HasColumnName("prioritate");

            entity.HasOne(d => d.Background).WithMany(p => p.RegulaBackgrounds)
                .HasForeignKey(d => d.BackgroundId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("regula_background_background_id_fkey");

            entity.HasOne(d => d.Interfata).WithMany(p => p.RegulaBackgrounds)
                .HasForeignKey(d => d.InterfataId)
                .HasConstraintName("regula_background_interfata_id_fkey");
        });

        modelBuilder.Entity<Rezervare>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("rezervare_pkey");

            entity.ToTable("rezervare", "mark_ai");

            entity.HasIndex(e => e.CameraId, "idx_rezervare_camera_id_fkey");

            entity.HasIndex(e => e.ElevId, "idx_rezervare_elev_id_fkey");

            entity.HasIndex(e => new { e.Id, e.ElevId }, "rezervare_id_elev_id_key").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.CameraId).HasColumnName("camera_id");
            entity.Property(e => e.ElevId).HasColumnName("elev_id");
            entity.Property(e => e.FinalizataLa).HasColumnName("finalizata_la");
            entity.Property(e => e.Moneda).HasColumnName("moneda");
            entity.Property(e => e.RezervataLa)
                .HasDefaultValueSql("now()")
                .HasColumnName("rezervata_la");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.TarifAchitat)
                .HasPrecision(14, 2)
                .HasColumnName("tarif_achitat");

            entity.HasOne(d => d.Camera).WithMany(p => p.Rezervares)
                .HasForeignKey(d => d.CameraId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("rezervare_camera_id_fkey");

            entity.HasOne(d => d.Elev).WithMany(p => p.Rezervares)
                .HasForeignKey(d => d.ElevId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("rezervare_elev_id_fkey");
        });

        modelBuilder.Entity<Rol>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("rol_pkey");

            entity.ToTable("rol", "mark_ai");

            entity.HasIndex(e => e.Nume, "rol_nume_key").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.Nume).HasColumnName("nume");
        });

        modelBuilder.Entity<Sesiune>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("sesiune_pkey");

            entity.ToTable("sesiune", "mark_ai");

            entity.HasIndex(e => e.CursId, "idx_sesiune_curs_id_fkey");

            entity.HasIndex(e => new { e.RezervareId, e.UtilizatorId }, "idx_sesiune_rezervare_id_utilizator_id_fkey");

            entity.HasIndex(e => e.UtilizatorId, "idx_sesiune_utilizator_id_fkey");

            entity.HasIndex(e => new { e.Id, e.UtilizatorId }, "sesiune_id_utilizator_id_key").IsUnique();

            entity.HasIndex(e => e.TwilioCallSid, "sesiune_twilio_call_sid_key").IsUnique();

            entity.HasIndex(e => new { e.UtilizatorId, e.InceputLa }, "sesiune_utilizator_data");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.Canal).HasColumnName("canal");
            entity.Property(e => e.CursId).HasColumnName("curs_id");
            entity.Property(e => e.FinalizatLa).HasColumnName("finalizat_la");
            entity.Property(e => e.InceputLa)
                .HasDefaultValueSql("now()")
                .HasColumnName("inceput_la");
            entity.Property(e => e.RezervareId).HasColumnName("rezervare_id");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.TwilioCallSid).HasColumnName("twilio_call_sid");
            entity.Property(e => e.UtilizatorId).HasColumnName("utilizator_id");

            entity.HasOne(d => d.Curs).WithMany(p => p.Sesiunes)
                .HasForeignKey(d => d.CursId)
                .HasConstraintName("sesiune_curs_id_fkey");

            entity.HasOne(d => d.Utilizator).WithMany(p => p.Sesiunes)
                .HasForeignKey(d => d.UtilizatorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("sesiune_utilizator_id_fkey");

            entity.HasOne(d => d.Rezervare).WithMany(p => p.Sesiunes)
                .HasPrincipalKey(p => new { p.Id, p.ElevId })
                .HasForeignKey(d => new { d.RezervareId, d.UtilizatorId })
                .HasConstraintName("sesiune_rezervare_id_utilizator_id_fkey");
        });

        modelBuilder.Entity<SmsLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("sms_log_pkey");

            entity.ToTable("sms_log", "mark_ai");

            entity.HasIndex(e => new { e.MesajId, e.SesiuneId }, "idx_sms_log_mesaj_id_sesiune_id_fkey");

            entity.HasIndex(e => e.SesiuneId, "idx_sms_log_sesiune_id_fkey");

            entity.HasIndex(e => e.ProviderSid, "sms_log_provider_sid_key").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.Continut).HasColumnName("continut");
            entity.Property(e => e.Directie).HasColumnName("directie");
            entity.Property(e => e.MesajId).HasColumnName("mesaj_id");
            entity.Property(e => e.NumarDestinatie).HasColumnName("numar_destinatie");
            entity.Property(e => e.ProviderSid).HasColumnName("provider_sid");
            entity.Property(e => e.SesiuneId).HasColumnName("sesiune_id");
            entity.Property(e => e.StatusLivrare).HasColumnName("status_livrare");
            entity.Property(e => e.TrimisLa).HasColumnName("trimis_la");

            entity.HasOne(d => d.Sesiune).WithMany(p => p.SmsLogs)
                .HasForeignKey(d => d.SesiuneId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("sms_log_sesiune_id_fkey");

            entity.HasOne(d => d.Mesaj).WithMany(p => p.SmsLogs)
                .HasPrincipalKey(p => new { p.Id, p.SesiuneId })
                .HasForeignKey(d => new { d.MesajId, d.SesiuneId })
                .HasConstraintName("sms_log_mesaj_id_sesiune_id_fkey");
        });

        modelBuilder.Entity<TablaPas>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tabla_pas_pkey");

            entity.ToTable("tabla_pas", "mark_ai");

            entity.HasIndex(e => new { e.MesajId, e.SesiuneId }, "idx_tabla_pas_mesaj_id_sesiune_id_fkey");

            entity.HasIndex(e => new { e.SesiuneId, e.Ordine }, "tabla_pas_sesiune_id_ordine_key").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.CreatLa)
                .HasDefaultValueSql("now()")
                .HasColumnName("creat_la");
            entity.Property(e => e.Explicatie).HasColumnName("explicatie");
            entity.Property(e => e.FormulaLatex).HasColumnName("formula_latex");
            entity.Property(e => e.MesajId).HasColumnName("mesaj_id");
            entity.Property(e => e.Ordine).HasColumnName("ordine");
            entity.Property(e => e.SesiuneId).HasColumnName("sesiune_id");

            entity.HasOne(d => d.Sesiune).WithMany(p => p.TablaPas)
                .HasForeignKey(d => d.SesiuneId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("tabla_pas_sesiune_id_fkey");

            entity.HasOne(d => d.Mesaj).WithMany(p => p.TablaPas)
                .HasPrincipalKey(p => new { p.Id, p.SesiuneId })
                .HasForeignKey(d => new { d.MesajId, d.SesiuneId })
                .HasConstraintName("tabla_pas_mesaj_id_sesiune_id_fkey");
        });

        modelBuilder.Entity<TarifFix>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tarif_fix_pkey");

            entity.ToTable("tarif_fix", "mark_ai");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.Activ)
                .HasDefaultValue(true)
                .HasColumnName("activ");
            entity.Property(e => e.DurataMinute).HasColumnName("durata_minute");
            entity.Property(e => e.Moneda).HasColumnName("moneda");
            entity.Property(e => e.Suma)
                .HasPrecision(14, 2)
                .HasColumnName("suma");
        });

        modelBuilder.Entity<Utilizator>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("utilizator_pkey");

            entity.ToTable("utilizator", "mark_ai");

            entity.HasIndex(e => e.Email, "utilizator_email_key").IsUnique();

            entity.HasIndex(e => e.Telefon, "utilizator_telefon_key").IsUnique();

            entity.HasIndex(e => e.Username, "utilizator_username_key").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.CreatLa)
                .HasDefaultValueSql("now()")
                .HasColumnName("creat_la");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.Nume).HasColumnName("nume");
            entity.Property(e => e.Telefon).HasColumnName("telefon");
            entity.Property(e => e.Username).HasColumnName("username");
        });

        modelBuilder.Entity<UtilizatorMaterie>(entity =>
        {
            entity.HasKey(e => new { e.UtilizatorId, e.MaterieId }).HasName("utilizator_materie_pkey");

            entity.ToTable("utilizator_materie", "mark_ai");

            entity.HasIndex(e => e.MaterieId, "idx_utilizator_materie_materie_id_fkey");

            entity.Property(e => e.UtilizatorId).HasColumnName("utilizator_id");
            entity.Property(e => e.MaterieId).HasColumnName("materie_id");
            entity.Property(e => e.NivelScolar).HasColumnName("nivel_scolar");
            entity.Property(e => e.SelectataLa)
                .HasDefaultValueSql("now()")
                .HasColumnName("selectata_la");

            entity.HasOne(d => d.Materie).WithMany(p => p.UtilizatorMateries)
                .HasForeignKey(d => d.MaterieId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("utilizator_materie_materie_id_fkey");

            entity.HasOne(d => d.Utilizator).WithMany(p => p.UtilizatorMateries)
                .HasForeignKey(d => d.UtilizatorId)
                .HasConstraintName("utilizator_materie_utilizator_id_fkey");
        });

        modelBuilder.Entity<UtilizatorRealizare>(entity =>
        {
            entity.HasKey(e => new { e.UtilizatorId, e.RealizareId }).HasName("utilizator_realizare_pkey");

            entity.ToTable("utilizator_realizare", "mark_ai");

            entity.HasIndex(e => e.RealizareId, "idx_utilizator_realizare_realizare_id_fkey");

            entity.Property(e => e.UtilizatorId).HasColumnName("utilizator_id");
            entity.Property(e => e.RealizareId).HasColumnName("realizare_id");
            entity.Property(e => e.DeblocataLa)
                .HasDefaultValueSql("now()")
                .HasColumnName("deblocata_la");

            entity.HasOne(d => d.Realizare).WithMany(p => p.UtilizatorRealizares)
                .HasForeignKey(d => d.RealizareId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("utilizator_realizare_realizare_id_fkey");

            entity.HasOne(d => d.Utilizator).WithMany(p => p.UtilizatorRealizares)
                .HasForeignKey(d => d.UtilizatorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("utilizator_realizare_utilizator_id_fkey");
        });

        modelBuilder.Entity<UtilizatorRol>(entity =>
        {
            entity.HasKey(e => new { e.UtilizatorId, e.RolId }).HasName("utilizator_rol_pkey");

            entity.ToTable("utilizator_rol", "mark_ai");

            entity.HasIndex(e => e.RolId, "idx_utilizator_rol_rol_id_fkey");

            entity.Property(e => e.UtilizatorId).HasColumnName("utilizator_id");
            entity.Property(e => e.RolId).HasColumnName("rol_id");
            entity.Property(e => e.AtribuitLa)
                .HasDefaultValueSql("now()")
                .HasColumnName("atribuit_la");

            entity.HasOne(d => d.Rol).WithMany(p => p.UtilizatorRols)
                .HasForeignKey(d => d.RolId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("utilizator_rol_rol_id_fkey");

            entity.HasOne(d => d.Utilizator).WithMany(p => p.UtilizatorRols)
                .HasForeignKey(d => d.UtilizatorId)
                .HasConstraintName("utilizator_rol_utilizator_id_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}


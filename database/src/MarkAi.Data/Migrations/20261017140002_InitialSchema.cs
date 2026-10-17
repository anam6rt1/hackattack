using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MarkAi.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "mark_ai");

            migrationBuilder.CreateTable(
                name: "background",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    cod = table.Column<string>(type: "text", nullable: false),
                    nume = table.Column<string>(type: "text", nullable: false),
                    asset_url = table.Column<string>(type: "text", nullable: false),
                    activ = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    selectabil_static = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    disponibil_de_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    disponibil_pana_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("background_pkey", x => x.id);
                    table.CheckConstraint("background_check", "((disponibil_de_la IS NULL) OR (disponibil_pana_la IS NULL) OR (disponibil_pana_la > disponibil_de_la))");
                });

            migrationBuilder.CreateTable(
                name: "interfata",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    cod = table.Column<string>(type: "text", nullable: false),
                    nume = table.Column<string>(type: "text", nullable: false),
                    descriere = table.Column<string>(type: "text", nullable: true),
                    preview_url = table.Column<string>(type: "text", nullable: true),
                    activa = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("interfata_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "materie",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    denumire = table.Column<string>(type: "text", nullable: false),
                    descriere = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("materie_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "nivel_xp",
                schema: "mark_ai",
                columns: table => new
                {
                    nivel = table.Column<int>(type: "integer", nullable: false),
                    xp_necesar = table.Column<int>(type: "integer", nullable: false),
                    titlu = table.Column<string>(type: "text", nullable: false),
                    descriere = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("nivel_xp_pkey", x => x.nivel);
                    table.CheckConstraint("nivel_xp_nivel_check", "(nivel >= 0)");
                    table.CheckConstraint("nivel_xp_xp_necesar_check", "(xp_necesar >= 0)");
                });

            migrationBuilder.CreateTable(
                name: "realizare",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    cod = table.Column<string>(type: "text", nullable: false),
                    titlu = table.Column<string>(type: "text", nullable: false),
                    descriere = table.Column<string>(type: "text", nullable: true),
                    xp_bonus = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    conditie = table.Column<string>(type: "text", nullable: true),
                    activa = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("realizare_pkey", x => x.id);
                    table.CheckConstraint("realizare_xp_bonus_check", "(xp_bonus >= 0)");
                });

            migrationBuilder.CreateTable(
                name: "rol",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    nume = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("rol_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tarif_fix",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    suma = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    moneda = table.Column<string>(type: "text", nullable: false),
                    durata_minute = table.Column<int>(type: "integer", nullable: false),
                    activ = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tarif_fix_pkey", x => x.id);
                    table.CheckConstraint("tarif_fix_durata_minute_check", "(durata_minute > 0)");
                    table.CheckConstraint("tarif_fix_moneda_check", "(moneda ~ '^[A-Z]{3}$'::text)");
                    table.CheckConstraint("tarif_fix_suma_check", "(suma >= (0)::numeric)");
                });

            migrationBuilder.CreateTable(
                name: "utilizator",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    nume = table.Column<string>(type: "text", nullable: false),
                    username = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    telefon = table.Column<string>(type: "text", nullable: true),
                    creat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("utilizator_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "regula_background",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    background_id = table.Column<int>(type: "integer", nullable: false),
                    interfata_id = table.Column<int>(type: "integer", nullable: true),
                    anotimp = table.Column<string>(type: "text", nullable: true),
                    perioada_zilei = table.Column<string>(type: "text", nullable: true),
                    prioritate = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    activa = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("regula_background_pkey", x => x.id);
                    table.ForeignKey(
                        name: "regula_background_background_id_fkey",
                        column: x => x.background_id,
                        principalSchema: "mark_ai",
                        principalTable: "background",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "regula_background_interfata_id_fkey",
                        column: x => x.interfata_id,
                        principalSchema: "mark_ai",
                        principalTable: "interfata",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "curs",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    materie_id = table.Column<int>(type: "integer", nullable: false),
                    titlu = table.Column<string>(type: "text", nullable: false),
                    descriere = table.Column<string>(type: "text", nullable: true),
                    continut = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("curs_pkey", x => x.id);
                    table.UniqueConstraint("AK_curs_id_materie_id", x => new { x.id, x.materie_id });
                    table.ForeignKey(
                        name: "curs_materie_id_fkey",
                        column: x => x.materie_id,
                        principalSchema: "mark_ai",
                        principalTable: "materie",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "graf",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    materie_id = table.Column<int>(type: "integer", nullable: false),
                    owner_id = table.Column<int>(type: "integer", nullable: false),
                    titlu = table.Column<string>(type: "text", nullable: false),
                    descriere = table.Column<string>(type: "text", nullable: true),
                    creat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("graf_pkey", x => x.id);
                    table.UniqueConstraint("AK_graf_id_materie_id", x => new { x.id, x.materie_id });
                    table.ForeignKey(
                        name: "graf_materie_id_fkey",
                        column: x => x.materie_id,
                        principalSchema: "mark_ai",
                        principalTable: "materie",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "graf_owner_id_fkey",
                        column: x => x.owner_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "preferinte_utilizator",
                schema: "mark_ai",
                columns: table => new
                {
                    utilizator_id = table.Column<int>(type: "integer", nullable: false),
                    interfata_id = table.Column<int>(type: "integer", nullable: true),
                    background_static_id = table.Column<int>(type: "integer", nullable: true),
                    mod_background = table.Column<string>(type: "text", nullable: false),
                    timezone = table.Column<string>(type: "text", nullable: false, defaultValueSql: "'Europe/Bucharest'::text"),
                    actualizat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("preferinte_utilizator_pkey", x => x.utilizator_id);
                    table.ForeignKey(
                        name: "preferinte_utilizator_background_static_id_fkey",
                        column: x => x.background_static_id,
                        principalSchema: "mark_ai",
                        principalTable: "background",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "preferinte_utilizator_interfata_id_fkey",
                        column: x => x.interfata_id,
                        principalSchema: "mark_ai",
                        principalTable: "interfata",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "preferinte_utilizator_utilizator_id_fkey",
                        column: x => x.utilizator_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "profil_mentor",
                schema: "mark_ai",
                columns: table => new
                {
                    utilizator_id = table.Column<int>(type: "integer", nullable: false),
                    descriere = table.Column<string>(type: "text", nullable: true),
                    stare_verificare = table.Column<string>(type: "text", nullable: false),
                    procent_comision = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    activat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("profil_mentor_pkey", x => x.utilizator_id);
                    table.CheckConstraint("profil_mentor_procent_comision_check", "((procent_comision >= (0)::numeric) AND (procent_comision <= (100)::numeric))");
                    table.ForeignKey(
                        name: "profil_mentor_utilizator_id_fkey",
                        column: x => x.utilizator_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "utilizator_materie",
                schema: "mark_ai",
                columns: table => new
                {
                    utilizator_id = table.Column<int>(type: "integer", nullable: false),
                    materie_id = table.Column<int>(type: "integer", nullable: false),
                    nivel_scolar = table.Column<string>(type: "text", nullable: true),
                    selectata_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("utilizator_materie_pkey", x => new { x.utilizator_id, x.materie_id });
                    table.ForeignKey(
                        name: "utilizator_materie_materie_id_fkey",
                        column: x => x.materie_id,
                        principalSchema: "mark_ai",
                        principalTable: "materie",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "utilizator_materie_utilizator_id_fkey",
                        column: x => x.utilizator_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "utilizator_realizare",
                schema: "mark_ai",
                columns: table => new
                {
                    utilizator_id = table.Column<int>(type: "integer", nullable: false),
                    realizare_id = table.Column<int>(type: "integer", nullable: false),
                    deblocata_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("utilizator_realizare_pkey", x => new { x.utilizator_id, x.realizare_id });
                    table.ForeignKey(
                        name: "utilizator_realizare_realizare_id_fkey",
                        column: x => x.realizare_id,
                        principalSchema: "mark_ai",
                        principalTable: "realizare",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "utilizator_realizare_utilizator_id_fkey",
                        column: x => x.utilizator_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "utilizator_rol",
                schema: "mark_ai",
                columns: table => new
                {
                    utilizator_id = table.Column<int>(type: "integer", nullable: false),
                    rol_id = table.Column<int>(type: "integer", nullable: false),
                    atribuit_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("utilizator_rol_pkey", x => new { x.utilizator_id, x.rol_id });
                    table.ForeignKey(
                        name: "utilizator_rol_rol_id_fkey",
                        column: x => x.rol_id,
                        principalSchema: "mark_ai",
                        principalTable: "rol",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "utilizator_rol_utilizator_id_fkey",
                        column: x => x.utilizator_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "graf_curs",
                schema: "mark_ai",
                columns: table => new
                {
                    graf_id = table.Column<int>(type: "integer", nullable: false),
                    curs_id = table.Column<int>(type: "integer", nullable: false),
                    materie_id = table.Column<int>(type: "integer", nullable: false),
                    adaugat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("graf_curs_pkey", x => new { x.graf_id, x.curs_id });
                    table.ForeignKey(
                        name: "graf_curs_curs_id_materie_id_fkey",
                        columns: x => new { x.curs_id, x.materie_id },
                        principalSchema: "mark_ai",
                        principalTable: "curs",
                        principalColumns: new[] { "id", "materie_id" });
                    table.ForeignKey(
                        name: "graf_curs_graf_id_materie_id_fkey",
                        columns: x => new { x.graf_id, x.materie_id },
                        principalSchema: "mark_ai",
                        principalTable: "graf",
                        principalColumns: new[] { "id", "materie_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "camera_tutoring",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    mentor_id = table.Column<int>(type: "integer", nullable: false),
                    materie_id = table.Column<int>(type: "integer", nullable: false),
                    curs_id = table.Column<int>(type: "integer", nullable: true),
                    tarif_id = table.Column<int>(type: "integer", nullable: false),
                    titlu = table.Column<string>(type: "text", nullable: false),
                    descriere = table.Column<string>(type: "text", nullable: true),
                    capacitate = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    inceput_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    finalizat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("camera_tutoring_pkey", x => x.id);
                    table.CheckConstraint("camera_tutoring_capacitate_check", "(capacitate > 0)");
                    table.CheckConstraint("camera_tutoring_check", "((finalizat_la IS NULL) OR ((inceput_la IS NOT NULL) AND (finalizat_la >= inceput_la)))");
                    table.ForeignKey(
                        name: "camera_tutoring_curs_id_materie_id_fkey",
                        columns: x => new { x.curs_id, x.materie_id },
                        principalSchema: "mark_ai",
                        principalTable: "curs",
                        principalColumns: new[] { "id", "materie_id" });
                    table.ForeignKey(
                        name: "camera_tutoring_materie_id_fkey",
                        column: x => x.materie_id,
                        principalSchema: "mark_ai",
                        principalTable: "materie",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "camera_tutoring_mentor_id_fkey",
                        column: x => x.mentor_id,
                        principalSchema: "mark_ai",
                        principalTable: "profil_mentor",
                        principalColumn: "utilizator_id");
                    table.ForeignKey(
                        name: "camera_tutoring_tarif_id_fkey",
                        column: x => x.tarif_id,
                        principalSchema: "mark_ai",
                        principalTable: "tarif_fix",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "decontare",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    mentor_id = table.Column<int>(type: "integer", nullable: false),
                    suma_totala = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    moneda = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    referinta_plata = table.Column<string>(type: "text", nullable: true),
                    creata_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    platita_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("decontare_pkey", x => x.id);
                    table.UniqueConstraint("AK_decontare_id_mentor_id_moneda", x => new { x.id, x.mentor_id, x.moneda });
                    table.CheckConstraint("decontare_check", "((platita_la IS NULL) OR (platita_la >= creata_la))");
                    table.CheckConstraint("decontare_moneda_check", "(moneda ~ '^[A-Z]{3}$'::text)");
                    table.CheckConstraint("decontare_suma_totala_check", "(suma_totala >= (0)::numeric)");
                    table.ForeignKey(
                        name: "decontare_mentor_id_fkey",
                        column: x => x.mentor_id,
                        principalSchema: "mark_ai",
                        principalTable: "profil_mentor",
                        principalColumn: "utilizator_id");
                });

            migrationBuilder.CreateTable(
                name: "muchie_graf",
                schema: "mark_ai",
                columns: table => new
                {
                    graf_id = table.Column<int>(type: "integer", nullable: false),
                    curs_sursa_id = table.Column<int>(type: "integer", nullable: false),
                    curs_destinatie_id = table.Column<int>(type: "integer", nullable: false),
                    tip_legatura = table.Column<string>(type: "text", nullable: false),
                    descriere = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("muchie_graf_pkey", x => new { x.graf_id, x.curs_sursa_id, x.curs_destinatie_id });
                    table.CheckConstraint("muchie_graf_check", "(curs_sursa_id <> curs_destinatie_id)");
                    table.ForeignKey(
                        name: "muchie_graf_graf_id_curs_destinatie_id_fkey",
                        columns: x => new { x.graf_id, x.curs_destinatie_id },
                        principalSchema: "mark_ai",
                        principalTable: "graf_curs",
                        principalColumns: new[] { "graf_id", "curs_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "muchie_graf_graf_id_curs_sursa_id_fkey",
                        columns: x => new { x.graf_id, x.curs_sursa_id },
                        principalSchema: "mark_ai",
                        principalTable: "graf_curs",
                        principalColumns: new[] { "graf_id", "curs_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rezervare",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    camera_id = table.Column<int>(type: "integer", nullable: false),
                    elev_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    tarif_achitat = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    moneda = table.Column<string>(type: "text", nullable: false),
                    rezervata_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    finalizata_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("rezervare_pkey", x => x.id);
                    table.UniqueConstraint("AK_rezervare_id_elev_id", x => new { x.id, x.elev_id });
                    table.CheckConstraint("rezervare_check", "((finalizata_la IS NULL) OR (finalizata_la >= rezervata_la))");
                    table.CheckConstraint("rezervare_moneda_check", "(moneda ~ '^[A-Z]{3}$'::text)");
                    table.CheckConstraint("rezervare_tarif_achitat_check", "(tarif_achitat >= (0)::numeric)");
                    table.ForeignKey(
                        name: "rezervare_camera_id_fkey",
                        column: x => x.camera_id,
                        principalSchema: "mark_ai",
                        principalTable: "camera_tutoring",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "rezervare_elev_id_fkey",
                        column: x => x.elev_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "comision_mentor",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    rezervare_id = table.Column<int>(type: "integer", nullable: false),
                    mentor_id = table.Column<int>(type: "integer", nullable: false),
                    decontare_id = table.Column<int>(type: "integer", nullable: true),
                    tarif_baza = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    procent_snapshot = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    suma_comision = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    moneda = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    calculat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("comision_mentor_pkey", x => x.id);
                    table.CheckConstraint("comision_mentor_check", "((suma_comision >= (0)::numeric) AND (suma_comision <= tarif_baza))");
                    table.CheckConstraint("comision_mentor_moneda_check", "(moneda ~ '^[A-Z]{3}$'::text)");
                    table.CheckConstraint("comision_mentor_procent_snapshot_check", "((procent_snapshot >= (0)::numeric) AND (procent_snapshot <= (100)::numeric))");
                    table.CheckConstraint("comision_mentor_tarif_baza_check", "(tarif_baza >= (0)::numeric)");
                    table.ForeignKey(
                        name: "comision_mentor_decontare_id_mentor_id_moneda_fkey",
                        columns: x => new { x.decontare_id, x.mentor_id, x.moneda },
                        principalSchema: "mark_ai",
                        principalTable: "decontare",
                        principalColumns: new[] { "id", "mentor_id", "moneda" });
                    table.ForeignKey(
                        name: "comision_mentor_mentor_id_fkey",
                        column: x => x.mentor_id,
                        principalSchema: "mark_ai",
                        principalTable: "profil_mentor",
                        principalColumn: "utilizator_id");
                    table.ForeignKey(
                        name: "comision_mentor_rezervare_id_fkey",
                        column: x => x.rezervare_id,
                        principalSchema: "mark_ai",
                        principalTable: "rezervare",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "plata",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    rezervare_id = table.Column<int>(type: "integer", nullable: false),
                    suma = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    moneda = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    procesator = table.Column<string>(type: "text", nullable: false),
                    tranzactie_externa = table.Column<string>(type: "text", nullable: true),
                    platita_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("plata_pkey", x => x.id);
                    table.CheckConstraint("plata_moneda_check", "(moneda ~ '^[A-Z]{3}$'::text)");
                    table.CheckConstraint("plata_suma_check", "(suma >= (0)::numeric)");
                    table.ForeignKey(
                        name: "plata_rezervare_id_fkey",
                        column: x => x.rezervare_id,
                        principalSchema: "mark_ai",
                        principalTable: "rezervare",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "sesiune",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    utilizator_id = table.Column<int>(type: "integer", nullable: false),
                    curs_id = table.Column<int>(type: "integer", nullable: true),
                    rezervare_id = table.Column<int>(type: "integer", nullable: true),
                    canal = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    twilio_call_sid = table.Column<string>(type: "text", nullable: true),
                    inceput_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    finalizat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("sesiune_pkey", x => x.id);
                    table.UniqueConstraint("AK_sesiune_id_utilizator_id", x => new { x.id, x.utilizator_id });
                    table.CheckConstraint("sesiune_check", "((finalizat_la IS NULL) OR (finalizat_la >= inceput_la))");
                    table.ForeignKey(
                        name: "sesiune_curs_id_fkey",
                        column: x => x.curs_id,
                        principalSchema: "mark_ai",
                        principalTable: "curs",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "sesiune_rezervare_id_utilizator_id_fkey",
                        columns: x => new { x.rezervare_id, x.utilizator_id },
                        principalSchema: "mark_ai",
                        principalTable: "rezervare",
                        principalColumns: new[] { "id", "elev_id" });
                    table.ForeignKey(
                        name: "sesiune_utilizator_id_fkey",
                        column: x => x.utilizator_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "eveniment_recompensa",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    utilizator_id = table.Column<int>(type: "integer", nullable: false),
                    materie_id = table.Column<int>(type: "integer", nullable: true),
                    sesiune_id = table.Column<int>(type: "integer", nullable: true),
                    xp_acordat = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    puncte_materie = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tip_eveniment = table.Column<string>(type: "text", nullable: false),
                    cheie_unica = table.Column<string>(type: "text", nullable: false),
                    creat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("eveniment_recompensa_pkey", x => x.id);
                    table.CheckConstraint("eveniment_recompensa_check", "((puncte_materie = 0) OR (materie_id IS NOT NULL))");
                    table.CheckConstraint("eveniment_recompensa_puncte_materie_check", "(puncte_materie >= 0)");
                    table.CheckConstraint("eveniment_recompensa_xp_acordat_check", "(xp_acordat >= 0)");
                    table.ForeignKey(
                        name: "eveniment_recompensa_sesiune_id_utilizator_id_fkey",
                        columns: x => new { x.sesiune_id, x.utilizator_id },
                        principalSchema: "mark_ai",
                        principalTable: "sesiune",
                        principalColumns: new[] { "id", "utilizator_id" });
                    table.ForeignKey(
                        name: "eveniment_recompensa_utilizator_id_fkey",
                        column: x => x.utilizator_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "eveniment_recompensa_utilizator_id_materie_id_fkey",
                        columns: x => new { x.utilizator_id, x.materie_id },
                        principalSchema: "mark_ai",
                        principalTable: "utilizator_materie",
                        principalColumns: new[] { "utilizator_id", "materie_id" });
                },
                comment: "Jurnal XP; nivelul se deriva din SUM(xp_acordat) si pragurile nivel_xp. Referinta la utilizator_materie foloseste cheia compusa, fara utilizator_id_materie inexistent.");

            migrationBuilder.CreateTable(
                name: "mesaj",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    sesiune_id = table.Column<int>(type: "integer", nullable: false),
                    expeditor = table.Column<string>(type: "text", nullable: false),
                    tip = table.Column<string>(type: "text", nullable: false),
                    continut = table.Column<string>(type: "text", nullable: false),
                    creat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("mesaj_pkey", x => x.id);
                    table.UniqueConstraint("AK_mesaj_id_sesiune_id", x => new { x.id, x.sesiune_id });
                    table.ForeignKey(
                        name: "mesaj_sesiune_id_fkey",
                        column: x => x.sesiune_id,
                        principalSchema: "mark_ai",
                        principalTable: "sesiune",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "sms_log",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    sesiune_id = table.Column<int>(type: "integer", nullable: false),
                    mesaj_id = table.Column<int>(type: "integer", nullable: true),
                    directie = table.Column<string>(type: "text", nullable: false),
                    numar_destinatie = table.Column<string>(type: "text", nullable: false),
                    continut = table.Column<string>(type: "text", nullable: false),
                    status_livrare = table.Column<string>(type: "text", nullable: false),
                    provider_sid = table.Column<string>(type: "text", nullable: true),
                    trimis_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("sms_log_pkey", x => x.id);
                    table.ForeignKey(
                        name: "sms_log_mesaj_id_sesiune_id_fkey",
                        columns: x => new { x.mesaj_id, x.sesiune_id },
                        principalSchema: "mark_ai",
                        principalTable: "mesaj",
                        principalColumns: new[] { "id", "sesiune_id" });
                    table.ForeignKey(
                        name: "sms_log_sesiune_id_fkey",
                        column: x => x.sesiune_id,
                        principalSchema: "mark_ai",
                        principalTable: "sesiune",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tabla_pas",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    sesiune_id = table.Column<int>(type: "integer", nullable: false),
                    mesaj_id = table.Column<int>(type: "integer", nullable: true),
                    ordine = table.Column<int>(type: "integer", nullable: false),
                    explicatie = table.Column<string>(type: "text", nullable: true),
                    formula_latex = table.Column<string>(type: "text", nullable: true),
                    creat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("tabla_pas_pkey", x => x.id);
                    table.CheckConstraint("tabla_pas_ordine_check", "(ordine >= 0)");
                    table.ForeignKey(
                        name: "tabla_pas_mesaj_id_sesiune_id_fkey",
                        columns: x => new { x.mesaj_id, x.sesiune_id },
                        principalSchema: "mark_ai",
                        principalTable: "mesaj",
                        principalColumns: new[] { "id", "sesiune_id" });
                    table.ForeignKey(
                        name: "tabla_pas_sesiune_id_fkey",
                        column: x => x.sesiune_id,
                        principalSchema: "mark_ai",
                        principalTable: "sesiune",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "background_cod_key",
                schema: "mark_ai",
                table: "background",
                column: "cod",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "camera_tutoring_id_mentor_id_key",
                schema: "mark_ai",
                table: "camera_tutoring",
                columns: new[] { "id", "mentor_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_camera_tutoring_curs_id_materie_id_fkey",
                schema: "mark_ai",
                table: "camera_tutoring",
                columns: new[] { "curs_id", "materie_id" });

            migrationBuilder.CreateIndex(
                name: "idx_camera_tutoring_materie_id_fkey",
                schema: "mark_ai",
                table: "camera_tutoring",
                column: "materie_id");

            migrationBuilder.CreateIndex(
                name: "idx_camera_tutoring_mentor_id_fkey",
                schema: "mark_ai",
                table: "camera_tutoring",
                column: "mentor_id");

            migrationBuilder.CreateIndex(
                name: "idx_camera_tutoring_tarif_id_fkey",
                schema: "mark_ai",
                table: "camera_tutoring",
                column: "tarif_id");

            migrationBuilder.CreateIndex(
                name: "comision_mentor_rezervare_id_key",
                schema: "mark_ai",
                table: "comision_mentor",
                column: "rezervare_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_comision_mentor_decontare_id_mentor_id_moneda_fkey",
                schema: "mark_ai",
                table: "comision_mentor",
                columns: new[] { "decontare_id", "mentor_id", "moneda" });

            migrationBuilder.CreateIndex(
                name: "idx_comision_mentor_mentor_id_fkey",
                schema: "mark_ai",
                table: "comision_mentor",
                column: "mentor_id");

            migrationBuilder.CreateIndex(
                name: "curs_id_materie_id_key",
                schema: "mark_ai",
                table: "curs",
                columns: new[] { "id", "materie_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_curs_materie_id_fkey",
                schema: "mark_ai",
                table: "curs",
                column: "materie_id");

            migrationBuilder.CreateIndex(
                name: "decontare_id_mentor_id_moneda_key",
                schema: "mark_ai",
                table: "decontare",
                columns: new[] { "id", "mentor_id", "moneda" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "decontare_referinta_plata_key",
                schema: "mark_ai",
                table: "decontare",
                column: "referinta_plata",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_decontare_mentor_id_fkey",
                schema: "mark_ai",
                table: "decontare",
                column: "mentor_id");

            migrationBuilder.CreateIndex(
                name: "eveniment_recompensa_cheie_unica_key",
                schema: "mark_ai",
                table: "eveniment_recompensa",
                column: "cheie_unica",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "eveniment_recompensa_utilizator_data",
                schema: "mark_ai",
                table: "eveniment_recompensa",
                columns: new[] { "utilizator_id", "creat_la" });

            migrationBuilder.CreateIndex(
                name: "idx_eveniment_recompensa_sesiune_id_utilizator_id_fkey",
                schema: "mark_ai",
                table: "eveniment_recompensa",
                columns: new[] { "sesiune_id", "utilizator_id" });

            migrationBuilder.CreateIndex(
                name: "idx_eveniment_recompensa_utilizator_id_fkey",
                schema: "mark_ai",
                table: "eveniment_recompensa",
                column: "utilizator_id");

            migrationBuilder.CreateIndex(
                name: "idx_eveniment_recompensa_utilizator_id_materie_id_fkey",
                schema: "mark_ai",
                table: "eveniment_recompensa",
                columns: new[] { "utilizator_id", "materie_id" });

            migrationBuilder.CreateIndex(
                name: "graf_id_materie_id_key",
                schema: "mark_ai",
                table: "graf",
                columns: new[] { "id", "materie_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_graf_materie_id_fkey",
                schema: "mark_ai",
                table: "graf",
                column: "materie_id");

            migrationBuilder.CreateIndex(
                name: "idx_graf_owner_id_fkey",
                schema: "mark_ai",
                table: "graf",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "idx_graf_curs_curs_id_materie_id_fkey",
                schema: "mark_ai",
                table: "graf_curs",
                columns: new[] { "curs_id", "materie_id" });

            migrationBuilder.CreateIndex(
                name: "idx_graf_curs_graf_id_materie_id_fkey",
                schema: "mark_ai",
                table: "graf_curs",
                columns: new[] { "graf_id", "materie_id" });

            migrationBuilder.CreateIndex(
                name: "interfata_cod_key",
                schema: "mark_ai",
                table: "interfata",
                column: "cod",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_mesaj_sesiune_id_fkey",
                schema: "mark_ai",
                table: "mesaj",
                column: "sesiune_id");

            migrationBuilder.CreateIndex(
                name: "mesaj_id_sesiune_id_key",
                schema: "mark_ai",
                table: "mesaj",
                columns: new[] { "id", "sesiune_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "mesaj_sesiune_data",
                schema: "mark_ai",
                table: "mesaj",
                columns: new[] { "sesiune_id", "creat_la" });

            migrationBuilder.CreateIndex(
                name: "idx_muchie_graf_graf_id_curs_destinatie_id_fkey",
                schema: "mark_ai",
                table: "muchie_graf",
                columns: new[] { "graf_id", "curs_destinatie_id" });

            migrationBuilder.CreateIndex(
                name: "nivel_xp_xp_necesar_key",
                schema: "mark_ai",
                table: "nivel_xp",
                column: "xp_necesar",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_plata_rezervare_id_fkey",
                schema: "mark_ai",
                table: "plata",
                column: "rezervare_id");

            migrationBuilder.CreateIndex(
                name: "plata_tranzactie_externa_key",
                schema: "mark_ai",
                table: "plata",
                column: "tranzactie_externa",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_preferinte_utilizator_background_static_id_fkey",
                schema: "mark_ai",
                table: "preferinte_utilizator",
                column: "background_static_id");

            migrationBuilder.CreateIndex(
                name: "idx_preferinte_utilizator_interfata_id_fkey",
                schema: "mark_ai",
                table: "preferinte_utilizator",
                column: "interfata_id");

            migrationBuilder.CreateIndex(
                name: "realizare_cod_key",
                schema: "mark_ai",
                table: "realizare",
                column: "cod",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_regula_background_background_id_fkey",
                schema: "mark_ai",
                table: "regula_background",
                column: "background_id");

            migrationBuilder.CreateIndex(
                name: "idx_regula_background_interfata_id_fkey",
                schema: "mark_ai",
                table: "regula_background",
                column: "interfata_id");

            migrationBuilder.CreateIndex(
                name: "idx_rezervare_camera_id_fkey",
                schema: "mark_ai",
                table: "rezervare",
                column: "camera_id");

            migrationBuilder.CreateIndex(
                name: "idx_rezervare_elev_id_fkey",
                schema: "mark_ai",
                table: "rezervare",
                column: "elev_id");

            migrationBuilder.CreateIndex(
                name: "rezervare_id_elev_id_key",
                schema: "mark_ai",
                table: "rezervare",
                columns: new[] { "id", "elev_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "rol_nume_key",
                schema: "mark_ai",
                table: "rol",
                column: "nume",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_sesiune_curs_id_fkey",
                schema: "mark_ai",
                table: "sesiune",
                column: "curs_id");

            migrationBuilder.CreateIndex(
                name: "idx_sesiune_rezervare_id_utilizator_id_fkey",
                schema: "mark_ai",
                table: "sesiune",
                columns: new[] { "rezervare_id", "utilizator_id" });

            migrationBuilder.CreateIndex(
                name: "idx_sesiune_utilizator_id_fkey",
                schema: "mark_ai",
                table: "sesiune",
                column: "utilizator_id");

            migrationBuilder.CreateIndex(
                name: "sesiune_id_utilizator_id_key",
                schema: "mark_ai",
                table: "sesiune",
                columns: new[] { "id", "utilizator_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "sesiune_twilio_call_sid_key",
                schema: "mark_ai",
                table: "sesiune",
                column: "twilio_call_sid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "sesiune_utilizator_data",
                schema: "mark_ai",
                table: "sesiune",
                columns: new[] { "utilizator_id", "inceput_la" });

            migrationBuilder.CreateIndex(
                name: "idx_sms_log_mesaj_id_sesiune_id_fkey",
                schema: "mark_ai",
                table: "sms_log",
                columns: new[] { "mesaj_id", "sesiune_id" });

            migrationBuilder.CreateIndex(
                name: "idx_sms_log_sesiune_id_fkey",
                schema: "mark_ai",
                table: "sms_log",
                column: "sesiune_id");

            migrationBuilder.CreateIndex(
                name: "sms_log_provider_sid_key",
                schema: "mark_ai",
                table: "sms_log",
                column: "provider_sid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_tabla_pas_mesaj_id_sesiune_id_fkey",
                schema: "mark_ai",
                table: "tabla_pas",
                columns: new[] { "mesaj_id", "sesiune_id" });

            migrationBuilder.CreateIndex(
                name: "tabla_pas_sesiune_id_ordine_key",
                schema: "mark_ai",
                table: "tabla_pas",
                columns: new[] { "sesiune_id", "ordine" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "utilizator_email_key",
                schema: "mark_ai",
                table: "utilizator",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "utilizator_telefon_key",
                schema: "mark_ai",
                table: "utilizator",
                column: "telefon",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "utilizator_username_key",
                schema: "mark_ai",
                table: "utilizator",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_utilizator_materie_materie_id_fkey",
                schema: "mark_ai",
                table: "utilizator_materie",
                column: "materie_id");

            migrationBuilder.CreateIndex(
                name: "idx_utilizator_realizare_realizare_id_fkey",
                schema: "mark_ai",
                table: "utilizator_realizare",
                column: "realizare_id");

            migrationBuilder.CreateIndex(
                name: "idx_utilizator_rol_rol_id_fkey",
                schema: "mark_ai",
                table: "utilizator_rol",
                column: "rol_id");
            migrationBuilder.Sql("CREATE UNIQUE INDEX utilizator_email_ci ON mark_ai.utilizator (lower(email));");
            migrationBuilder.Sql("CREATE UNIQUE INDEX utilizator_username_ci ON mark_ai.utilizator (lower(username));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "comision_mentor",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "eveniment_recompensa",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "muchie_graf",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "nivel_xp",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "plata",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "preferinte_utilizator",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "regula_background",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "sms_log",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "tabla_pas",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "utilizator_realizare",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "utilizator_rol",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "decontare",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "utilizator_materie",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "graf_curs",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "background",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "interfata",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "mesaj",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "realizare",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "rol",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "graf",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "sesiune",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "rezervare",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "camera_tutoring",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "curs",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "profil_mentor",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "tarif_fix",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "materie",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "utilizator",
                schema: "mark_ai");
        }
    }
}


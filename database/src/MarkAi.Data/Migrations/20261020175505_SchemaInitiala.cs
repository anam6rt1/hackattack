using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MarkAi.Data.Migrations
{
    /// <inheritdoc />
    public partial class SchemaInitiala : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "mark_ai");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

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
                name: "generare_ai",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    utilizator_id = table.Column<int>(type: "integer", nullable: false),
                    parcurs_id = table.Column<int>(type: "integer", nullable: false),
                    numar = table.Column<int>(type: "integer", nullable: false),
                    creat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("generare_ai_pkey", x => x.id);
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
                name: "judet",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    nume = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("judet_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "materie",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    denumire = table.Column<string>(type: "text", nullable: false),
                    descriere = table.Column<string>(type: "text", nullable: true),
                    cod = table.Column<string>(type: "text", nullable: true),
                    ordine = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("materie_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mesaj_iesire",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    canal = table.Column<string>(type: "text", nullable: false),
                    catre = table.Column<string>(type: "text", nullable: false),
                    subiect = table.Column<string>(type: "text", nullable: true),
                    html = table.Column<string>(type: "text", nullable: true),
                    text = table.Column<string>(type: "text", nullable: true),
                    tip = table.Column<string>(type: "text", nullable: false),
                    creat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    trimis_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    furnizor = table.Column<string>(type: "text", nullable: true),
                    incercari = table.Column<int>(type: "integer", nullable: false),
                    urmatoarea_incercare_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    eroare = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("mesaj_iesire_pkey", x => x.id);
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
                name: "localitate",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    judet_id = table.Column<int>(type: "integer", nullable: false),
                    nume = table.Column<string>(type: "text", nullable: false),
                    uat_id = table.Column<int>(type: "integer", nullable: false),
                    uat_nume = table.Column<string>(type: "text", nullable: false),
                    urban = table.Column<bool>(type: "boolean", nullable: false),
                    cautare = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("localitate_pkey", x => x.id);
                    table.ForeignKey(
                        name: "localitate_judet_id_fkey",
                        column: x => x.judet_id,
                        principalSchema: "mark_ai",
                        principalTable: "judet",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                    creat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    parola_hash = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    versiune_sesiune = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    email_confirmat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    avertizat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    telefon_verificat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    avertizat_aprobare_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    judet_id = table.Column<int>(type: "integer", nullable: true),
                    localitate_id = table.Column<int>(type: "integer", nullable: true),
                    clasa = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("utilizator_pkey", x => x.id);
                    table.CheckConstraint("utilizator_clasa_check", "clasa IS NULL OR clasa IN ('IX', 'X', 'XI', 'XII')");
                    table.ForeignKey(
                        name: "utilizator_judet_id_fkey",
                        column: x => x.judet_id,
                        principalSchema: "mark_ai",
                        principalTable: "judet",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "utilizator_localitate_id_fkey",
                        column: x => x.localitate_id,
                        principalSchema: "mark_ai",
                        principalTable: "localitate",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "cod_verificare",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    utilizator_id = table.Column<int>(type: "integer", nullable: false),
                    scop = table.Column<string>(type: "text", nullable: false),
                    destinatie = table.Column<string>(type: "text", nullable: false),
                    hash_cod = table.Column<string>(type: "text", nullable: false),
                    incercari = table.Column<int>(type: "integer", nullable: false),
                    creat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expira_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    folosit_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("cod_verificare_pkey", x => x.id);
                    table.ForeignKey(
                        name: "cod_verificare_utilizator_id_fkey",
                        column: x => x.utilizator_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "organizatie",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    nume = table.Column<string>(type: "text", nullable: false),
                    adresa = table.Column<string>(type: "text", nullable: false),
                    judet_id = table.Column<int>(type: "integer", nullable: false),
                    localitate_id = table.Column<int>(type: "integer", nullable: true),
                    stare = table.Column<string>(type: "text", nullable: false),
                    admin_id = table.Column<int>(type: "integer", nullable: false),
                    motiv_respingere = table.Column<string>(type: "text", nullable: true),
                    decis_de_id = table.Column<int>(type: "integer", nullable: true),
                    decis_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    creat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("organizatie_pkey", x => x.id);
                    table.CheckConstraint("organizatie_stare_check", "stare IN ('in_asteptare', 'aprobata', 'respinsa')");
                    table.ForeignKey(
                        name: "organizatie_admin_id_fkey",
                        column: x => x.admin_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "organizatie_decis_de_id_fkey",
                        column: x => x.decis_de_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "organizatie_judet_id_fkey",
                        column: x => x.judet_id,
                        principalSchema: "mark_ai",
                        principalTable: "judet",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "organizatie_localitate_id_fkey",
                        column: x => x.localitate_id,
                        principalSchema: "mark_ai",
                        principalTable: "localitate",
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
                    actualizat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    voce_tutor = table.Column<string>(type: "text", nullable: true),
                    viteza_tutor = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                    ton_tutor = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    avatar = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("preferinte_utilizator_pkey", x => x.utilizator_id);
                    table.CheckConstraint("preferinte_utilizator_ton_tutor_check", "ton_tutor BETWEEN -20 AND 20");
                    table.CheckConstraint("preferinte_utilizator_viteza_tutor_check", "viteza_tutor BETWEEN 70 AND 130");
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
                name: "membru_organizatie",
                schema: "mark_ai",
                columns: table => new
                {
                    organizatie_id = table.Column<int>(type: "integer", nullable: false),
                    utilizator_id = table.Column<int>(type: "integer", nullable: false),
                    rol = table.Column<string>(type: "text", nullable: false),
                    stare = table.Column<string>(type: "text", nullable: false),
                    poate_publica_public = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    cerut_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expira_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    amintit_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    decis_de_id = table.Column<int>(type: "integer", nullable: true),
                    decis_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    motiv_respingere = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("membru_organizatie_pkey", x => new { x.organizatie_id, x.utilizator_id });
                    table.CheckConstraint("membru_organizatie_rol_check", "rol IN ('profesor', 'elev')");
                    table.CheckConstraint("membru_organizatie_stare_check", "stare IN ('in_asteptare', 'aprobat', 'respins', 'expirat')");
                    table.ForeignKey(
                        name: "membru_organizatie_decis_de_id_fkey",
                        column: x => x.decis_de_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "membru_organizatie_organizatie_id_fkey",
                        column: x => x.organizatie_id,
                        principalSchema: "mark_ai",
                        principalTable: "organizatie",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "membru_organizatie_utilizator_id_fkey",
                        column: x => x.utilizator_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "parcurs",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    materie_id = table.Column<int>(type: "integer", nullable: false),
                    titlu = table.Column<string>(type: "text", nullable: false),
                    descriere = table.Column<string>(type: "text", nullable: true),
                    clasa = table.Column<string>(type: "text", nullable: false),
                    autor_id = table.Column<int>(type: "integer", nullable: true),
                    organizatie_id = table.Column<int>(type: "integer", nullable: true),
                    vizibilitate = table.Column<string>(type: "text", nullable: false, defaultValue: "public"),
                    publicat = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ordine = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    creat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    cautare = table.Column<string>(type: "text", nullable: false, computedColumnSql: "lower(translate(titlu || ' ' || coalesce(descriere, ''), 'ĂÂÎȘŞȚŢăâîșşțţÁÀÄÉÈËÍÌÏÓÒÖÚÙÜÇÑáàäéèëíìïóòöúùüçñ-', 'aaissttaaissttaaaeeeiiiooouuucnaaaeeeiiiooouuucn '))", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("parcurs_pkey", x => x.id);
                    table.UniqueConstraint("parcurs_id_materie_id_key", x => new { x.id, x.materie_id });
                    table.CheckConstraint("parcurs_clasa_check", "clasa IN ('IX', 'X', 'XI', 'XII', 'Bac')");
                    table.CheckConstraint("parcurs_organizatie_check", "vizibilitate = 'public' OR organizatie_id IS NOT NULL");
                    table.CheckConstraint("parcurs_vizibilitate_check", "vizibilitate IN ('public', 'organizatie')");
                    table.ForeignKey(
                        name: "parcurs_autor_id_fkey",
                        column: x => x.autor_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "parcurs_materie_id_fkey",
                        column: x => x.materie_id,
                        principalSchema: "mark_ai",
                        principalTable: "materie",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "parcurs_organizatie_id_fkey",
                        column: x => x.organizatie_id,
                        principalSchema: "mark_ai",
                        principalTable: "organizatie",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
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
                    activat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    onorariu_negociat = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    moneda = table.Column<string>(type: "text", nullable: false, defaultValue: "RON"),
                    poza_url = table.Column<string>(type: "text", nullable: true),
                    dovada_calificare_url = table.Column<string>(type: "text", nullable: true),
                    motiv_respingere = table.Column<string>(type: "text", nullable: true),
                    verificat_de_id = table.Column<int>(type: "integer", nullable: true),
                    loc_organizatie_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("profil_mentor_pkey", x => x.utilizator_id);
                    table.CheckConstraint("profil_mentor_moneda_check", "moneda ~ '^[A-Z]{3}$'::text");
                    table.CheckConstraint("profil_mentor_onorariu_negociat_check", "onorariu_negociat >= (0)::numeric");
                    table.CheckConstraint("profil_mentor_procent_comision_check", "((procent_comision >= (0)::numeric) AND (procent_comision <= (100)::numeric))");
                    table.ForeignKey(
                        name: "profil_mentor_loc_organizatie_id_fkey",
                        column: x => x.loc_organizatie_id,
                        principalSchema: "mark_ai",
                        principalTable: "organizatie",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "profil_mentor_utilizator_id_fkey",
                        column: x => x.utilizator_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "profil_mentor_verificat_de_id_fkey",
                        column: x => x.verificat_de_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "curs",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    materie_id = table.Column<int>(type: "integer", nullable: false),
                    parcurs_id = table.Column<int>(type: "integer", nullable: false),
                    titlu = table.Column<string>(type: "text", nullable: false),
                    descriere = table.Column<string>(type: "text", nullable: true),
                    continut = table.Column<string>(type: "text", nullable: true),
                    ordine = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    capitol = table.Column<string>(type: "text", nullable: true),
                    video_url = table.Column<string>(type: "text", nullable: true),
                    publicat = table.Column<bool>(type: "boolean", nullable: false),
                    creat_de_id = table.Column<int>(type: "integer", nullable: true),
                    creat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    cautare = table.Column<string>(type: "text", nullable: false, computedColumnSql: "lower(translate(titlu || ' ' || coalesce(capitol, '') || ' ' || coalesce(descriere, ''), 'ĂÂÎȘŞȚŢăâîșşțţÁÀÄÉÈËÍÌÏÓÒÖÚÙÜÇÑáàäéèëíìïóòöúùüçñ-', 'aaissttaaissttaaaeeeiiiooouuucnaaaeeeiiiooouuucn '))", stored: true),
                    materiale = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("curs_pkey", x => x.id);
                    table.UniqueConstraint("AK_curs_id_materie_id", x => new { x.id, x.materie_id });
                    table.ForeignKey(
                        name: "curs_creat_de_id_fkey",
                        column: x => x.creat_de_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "curs_materie_id_fkey",
                        column: x => x.materie_id,
                        principalSchema: "mark_ai",
                        principalTable: "materie",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "curs_parcurs_id_materie_id_fkey",
                        columns: x => new { x.parcurs_id, x.materie_id },
                        principalSchema: "mark_ai",
                        principalTable: "parcurs",
                        principalColumns: new[] { "id", "materie_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fisier_lectie",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    parcurs_id = table.Column<int>(type: "integer", nullable: false),
                    nume = table.Column<string>(type: "text", nullable: false),
                    tip = table.Column<string>(type: "text", nullable: false),
                    cheie = table.Column<string>(type: "text", nullable: false),
                    marime_octeti = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "text", nullable: false),
                    stare_text = table.Column<string>(type: "text", nullable: false, defaultValue: "in_asteptare"),
                    text_extras = table.Column<string>(type: "text", nullable: true),
                    incarcat_de_id = table.Column<int>(type: "integer", nullable: true),
                    incarcat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("fisier_lectie_pkey", x => x.id);
                    table.CheckConstraint("fisier_lectie_marime_check", "marime_octeti > 0");
                    table.CheckConstraint("fisier_lectie_stare_text_check", "stare_text IN ('in_asteptare', 'gata', 'fara_text', 'eroare')");
                    table.CheckConstraint("fisier_lectie_tip_check", "tip IN ('pdf', 'docx', 'pptx')");
                    table.ForeignKey(
                        name: "fisier_lectie_incarcat_de_id_fkey",
                        column: x => x.incarcat_de_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fisier_lectie_parcurs_id_fkey",
                        column: x => x.parcurs_id,
                        principalSchema: "mark_ai",
                        principalTable: "parcurs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "incercare_quiz",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    utilizator_id = table.Column<int>(type: "integer", nullable: false),
                    parcurs_id = table.Column<int>(type: "integer", nullable: false),
                    intrebari_ids = table.Column<int[]>(type: "integer[]", nullable: false),
                    raspunsuri = table.Column<int[]>(type: "integer[]", nullable: false),
                    corecte = table.Column<int>(type: "integer", nullable: false),
                    inceput_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    finalizata_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("incercare_quiz_pkey", x => x.id);
                    table.ForeignKey(
                        name: "incercare_quiz_parcurs_id_fkey",
                        column: x => x.parcurs_id,
                        principalSchema: "mark_ai",
                        principalTable: "parcurs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "incercare_quiz_utilizator_id_fkey",
                        column: x => x.utilizator_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "intrebare",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    materie_id = table.Column<int>(type: "integer", nullable: false),
                    parcurs_id = table.Column<int>(type: "integer", nullable: true),
                    text = table.Column<string>(type: "text", nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    tip = table.Column<string>(type: "text", nullable: false, defaultValue: "grila"),
                    raspuns_numeric = table.Column<int>(type: "integer", nullable: true),
                    explicatie = table.Column<string>(type: "text", nullable: true),
                    sursa = table.Column<string>(type: "text", nullable: false, defaultValue: "manual"),
                    creata_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("intrebare_pkey", x => x.id);
                    table.CheckConstraint("intrebare_estimare_check", "(tip = 'estimare') = (raspuns_numeric IS NOT NULL)");
                    table.CheckConstraint("intrebare_sursa_check", "sursa IN ('ai', 'manual')");
                    table.CheckConstraint("intrebare_text_check", "length(btrim(text)) > 0");
                    table.CheckConstraint("intrebare_tip_check", "tip IN ('grila', 'estimare')");
                    table.ForeignKey(
                        name: "intrebare_materie_id_fkey",
                        column: x => x.materie_id,
                        principalSchema: "mark_ai",
                        principalTable: "materie",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "intrebare_parcurs_id_materie_id_fkey",
                        columns: x => new { x.parcurs_id, x.materie_id },
                        principalSchema: "mark_ai",
                        principalTable: "parcurs",
                        principalColumns: new[] { "id", "materie_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "parcurs_organizatie",
                schema: "mark_ai",
                columns: table => new
                {
                    parcurs_id = table.Column<int>(type: "integer", nullable: false),
                    organizatie_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("parcurs_organizatie_pkey", x => new { x.parcurs_id, x.organizatie_id });
                    table.ForeignKey(
                        name: "parcurs_organizatie_organizatie_id_fkey",
                        column: x => x.organizatie_id,
                        principalSchema: "mark_ai",
                        principalTable: "organizatie",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "parcurs_organizatie_parcurs_id_fkey",
                        column: x => x.parcurs_id,
                        principalSchema: "mark_ai",
                        principalTable: "parcurs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "mentor_materie",
                schema: "mark_ai",
                columns: table => new
                {
                    mentor_id = table.Column<int>(type: "integer", nullable: false),
                    materie_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("mentor_materie_pkey", x => new { x.mentor_id, x.materie_id });
                    table.ForeignKey(
                        name: "mentor_materie_materie_id_fkey",
                        column: x => x.materie_id,
                        principalSchema: "mark_ai",
                        principalTable: "materie",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "mentor_materie_mentor_id_fkey",
                        column: x => x.mentor_id,
                        principalSchema: "mark_ai",
                        principalTable: "profil_mentor",
                        principalColumn: "utilizator_id",
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
                    tarif_id = table.Column<int>(type: "integer", nullable: true),
                    titlu = table.Column<string>(type: "text", nullable: false),
                    descriere = table.Column<string>(type: "text", nullable: true),
                    capacitate = table.Column<int>(type: "integer", nullable: false),
                    pro_bono = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    link_intalnire = table.Column<string>(type: "text", nullable: true),
                    tip = table.Column<string>(type: "text", nullable: false, defaultValue: "online"),
                    loc_organizatie_id = table.Column<int>(type: "integer", nullable: true),
                    loc_judet_id = table.Column<int>(type: "integer", nullable: true),
                    loc_localitate_id = table.Column<int>(type: "integer", nullable: true),
                    nota_loc = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    inceput_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    finalizat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("camera_tutoring_pkey", x => x.id);
                    table.CheckConstraint("camera_tutoring_capacitate_check", "(capacitate > 0)");
                    table.CheckConstraint("camera_tutoring_check", "((finalizat_la IS NULL) OR ((inceput_la IS NOT NULL) AND (finalizat_la >= inceput_la)))");
                    table.CheckConstraint("camera_tutoring_link_intalnire_check", "link_intalnire IS NULL OR link_intalnire ~ '^https://'");
                    table.CheckConstraint("camera_tutoring_loc_check", "tip = 'online' OR loc_judet_id IS NOT NULL");
                    table.CheckConstraint("camera_tutoring_pro_bono_check", "pro_bono = (tarif_id IS NULL)");
                    table.CheckConstraint("camera_tutoring_tip_check", "tip IN ('online', 'fata_in_fata', 'hibrid')");
                    table.ForeignKey(
                        name: "camera_tutoring_curs_id_materie_id_fkey",
                        columns: x => new { x.curs_id, x.materie_id },
                        principalSchema: "mark_ai",
                        principalTable: "curs",
                        principalColumns: new[] { "id", "materie_id" });
                    table.ForeignKey(
                        name: "camera_tutoring_loc_judet_id_fkey",
                        column: x => x.loc_judet_id,
                        principalSchema: "mark_ai",
                        principalTable: "judet",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "camera_tutoring_loc_localitate_id_fkey",
                        column: x => x.loc_localitate_id,
                        principalSchema: "mark_ai",
                        principalTable: "localitate",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "camera_tutoring_loc_organizatie_id_fkey",
                        column: x => x.loc_organizatie_id,
                        principalSchema: "mark_ai",
                        principalTable: "organizatie",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
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
                name: "progres_lectie",
                schema: "mark_ai",
                columns: table => new
                {
                    utilizator_id = table.Column<int>(type: "integer", nullable: false),
                    parcurs_id = table.Column<int>(type: "integer", nullable: false),
                    ultimul_curs_id = table.Column<int>(type: "integer", nullable: true),
                    promovat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ultima_accesare = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("progres_lectie_pkey", x => new { x.utilizator_id, x.parcurs_id });
                    table.ForeignKey(
                        name: "progres_lectie_parcurs_id_fkey",
                        column: x => x.parcurs_id,
                        principalSchema: "mark_ai",
                        principalTable: "parcurs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "progres_lectie_ultimul_curs_id_fkey",
                        column: x => x.ultimul_curs_id,
                        principalSchema: "mark_ai",
                        principalTable: "curs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "progres_lectie_utilizator_id_fkey",
                        column: x => x.utilizator_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "intrebare_varianta",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    intrebare_id = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    corecta = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("intrebare_varianta_pkey", x => x.id);
                    table.CheckConstraint("intrebare_varianta_text_check", "length(btrim(text)) > 0");
                    table.ForeignKey(
                        name: "intrebare_varianta_intrebare_id_fkey",
                        column: x => x.intrebare_id,
                        principalSchema: "mark_ai",
                        principalTable: "intrebare",
                        principalColumn: "id",
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
                    finalizata_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    participare = table.Column<string>(type: "text", nullable: false, defaultValue: "online")
                },
                constraints: table =>
                {
                    table.PrimaryKey("rezervare_pkey", x => x.id);
                    table.UniqueConstraint("AK_rezervare_id_elev_id", x => new { x.id, x.elev_id });
                    table.CheckConstraint("rezervare_check", "((finalizata_la IS NULL) OR (finalizata_la >= rezervata_la))");
                    table.CheckConstraint("rezervare_moneda_check", "(moneda ~ '^[A-Z]{3}$'::text)");
                    table.CheckConstraint("rezervare_participare_check", "participare IN ('online', 'la_sala')");
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
                    finalizat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cheie_hash = table.Column<string>(type: "text", nullable: true),
                    cheie_expira_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    durata_secunde = table.Column<int>(type: "integer", nullable: true)
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
                        name: "eveniment_recompensa_materie_id_fkey",
                        column: x => x.materie_id,
                        principalSchema: "mark_ai",
                        principalTable: "materie",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
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
                },
                comment: "Jurnal XP; nivelul se deriva din SUM(xp_acordat) si pragurile nivel_xp.");

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
                    table.ForeignKey(
                        name: "mesaj_sesiune_id_fkey",
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
                name: "idx_camera_tutoring_loc_judet_id_fkey",
                schema: "mark_ai",
                table: "camera_tutoring",
                column: "loc_judet_id");

            migrationBuilder.CreateIndex(
                name: "idx_camera_tutoring_loc_localitate_id_fkey",
                schema: "mark_ai",
                table: "camera_tutoring",
                column: "loc_localitate_id");

            migrationBuilder.CreateIndex(
                name: "idx_camera_tutoring_loc_organizatie_id_fkey",
                schema: "mark_ai",
                table: "camera_tutoring",
                column: "loc_organizatie_id");

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
                name: "idx_cod_verificare_utilizator_scop",
                schema: "mark_ai",
                table: "cod_verificare",
                columns: new[] { "utilizator_id", "scop" });

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
                name: "idx_curs_cautare_trgm",
                schema: "mark_ai",
                table: "curs",
                column: "cautare")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "idx_curs_materie_id_fkey",
                schema: "mark_ai",
                table: "curs",
                column: "materie_id");

            migrationBuilder.CreateIndex(
                name: "idx_curs_materie_id_ordine",
                schema: "mark_ai",
                table: "curs",
                columns: new[] { "materie_id", "ordine" });

            migrationBuilder.CreateIndex(
                name: "idx_curs_parcurs_id_materie_id_fkey",
                schema: "mark_ai",
                table: "curs",
                columns: new[] { "parcurs_id", "materie_id" });

            migrationBuilder.CreateIndex(
                name: "IX_curs_creat_de_id",
                schema: "mark_ai",
                table: "curs",
                column: "creat_de_id");

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
                name: "idx_eveniment_recompensa_materie_id_fkey",
                schema: "mark_ai",
                table: "eveniment_recompensa",
                column: "materie_id");

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
                name: "fisier_lectie_cheie_key",
                schema: "mark_ai",
                table: "fisier_lectie",
                column: "cheie",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "fisier_lectie_parcurs_id_tip_key",
                schema: "mark_ai",
                table: "fisier_lectie",
                columns: new[] { "parcurs_id", "tip" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_fisier_lectie_incarcat_de_id_fkey",
                schema: "mark_ai",
                table: "fisier_lectie",
                column: "incarcat_de_id");

            migrationBuilder.CreateIndex(
                name: "idx_generare_ai_utilizator_data",
                schema: "mark_ai",
                table: "generare_ai",
                columns: new[] { "utilizator_id", "creat_la" });

            migrationBuilder.CreateIndex(
                name: "idx_incercare_quiz_parcurs_id_fkey",
                schema: "mark_ai",
                table: "incercare_quiz",
                column: "parcurs_id");

            migrationBuilder.CreateIndex(
                name: "idx_incercare_quiz_utilizator_parcurs",
                schema: "mark_ai",
                table: "incercare_quiz",
                columns: new[] { "utilizator_id", "parcurs_id", "inceput_la" });

            migrationBuilder.CreateIndex(
                name: "interfata_cod_key",
                schema: "mark_ai",
                table: "interfata",
                column: "cod",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_intrebare_materie_id_fkey",
                schema: "mark_ai",
                table: "intrebare",
                column: "materie_id");

            migrationBuilder.CreateIndex(
                name: "idx_intrebare_parcurs_id_materie_id_fkey",
                schema: "mark_ai",
                table: "intrebare",
                columns: new[] { "parcurs_id", "materie_id" });

            migrationBuilder.CreateIndex(
                name: "idx_intrebare_varianta_intrebare_id_fkey",
                schema: "mark_ai",
                table: "intrebare_varianta",
                column: "intrebare_id");

            migrationBuilder.CreateIndex(
                name: "intrebare_varianta_o_corecta",
                schema: "mark_ai",
                table: "intrebare_varianta",
                column: "intrebare_id",
                unique: true,
                filter: "corecta");

            migrationBuilder.CreateIndex(
                name: "idx_localitate_judet_cautare",
                schema: "mark_ai",
                table: "localitate",
                columns: new[] { "judet_id", "cautare" });

            migrationBuilder.CreateIndex(
                name: "materie_cod_key",
                schema: "mark_ai",
                table: "materie",
                column: "cod",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_membru_organizatie_org_rol_stare",
                schema: "mark_ai",
                table: "membru_organizatie",
                columns: new[] { "organizatie_id", "rol", "stare" });

            migrationBuilder.CreateIndex(
                name: "IX_membru_organizatie_decis_de_id",
                schema: "mark_ai",
                table: "membru_organizatie",
                column: "decis_de_id");

            migrationBuilder.CreateIndex(
                name: "membru_organizatie_un_elev_key",
                schema: "mark_ai",
                table: "membru_organizatie",
                column: "utilizator_id",
                unique: true,
                filter: "rol = 'elev' AND stare IN ('in_asteptare', 'aprobat')");

            migrationBuilder.CreateIndex(
                name: "idx_mentor_materie_materie_id_fkey",
                schema: "mark_ai",
                table: "mentor_materie",
                column: "materie_id");

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
                name: "idx_mesaj_iesire_canal_data",
                schema: "mark_ai",
                table: "mesaj_iesire",
                columns: new[] { "canal", "creat_la" });

            migrationBuilder.CreateIndex(
                name: "idx_mesaj_iesire_de_trimis",
                schema: "mark_ai",
                table: "mesaj_iesire",
                columns: new[] { "trimis_la", "urmatoarea_incercare_la" });

            migrationBuilder.CreateIndex(
                name: "nivel_xp_xp_necesar_key",
                schema: "mark_ai",
                table: "nivel_xp",
                column: "xp_necesar",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_organizatie_stare_judet",
                schema: "mark_ai",
                table: "organizatie",
                columns: new[] { "stare", "judet_id" });

            migrationBuilder.CreateIndex(
                name: "IX_organizatie_decis_de_id",
                schema: "mark_ai",
                table: "organizatie",
                column: "decis_de_id");

            migrationBuilder.CreateIndex(
                name: "IX_organizatie_judet_id",
                schema: "mark_ai",
                table: "organizatie",
                column: "judet_id");

            migrationBuilder.CreateIndex(
                name: "IX_organizatie_localitate_id",
                schema: "mark_ai",
                table: "organizatie",
                column: "localitate_id");

            migrationBuilder.CreateIndex(
                name: "organizatie_admin_id_key",
                schema: "mark_ai",
                table: "organizatie",
                column: "admin_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_parcurs_autor_id_fkey",
                schema: "mark_ai",
                table: "parcurs",
                column: "autor_id");

            migrationBuilder.CreateIndex(
                name: "idx_parcurs_cautare_trgm",
                schema: "mark_ai",
                table: "parcurs",
                column: "cautare")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "idx_parcurs_materie_clasa",
                schema: "mark_ai",
                table: "parcurs",
                columns: new[] { "materie_id", "clasa" });

            migrationBuilder.CreateIndex(
                name: "idx_parcurs_organizatie_id_fkey",
                schema: "mark_ai",
                table: "parcurs",
                column: "organizatie_id");

            migrationBuilder.CreateIndex(
                name: "idx_parcurs_organizatie_organizatie_id_fkey",
                schema: "mark_ai",
                table: "parcurs_organizatie",
                column: "organizatie_id");

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
                name: "idx_profil_mentor_loc_organizatie_id_fkey",
                schema: "mark_ai",
                table: "profil_mentor",
                column: "loc_organizatie_id");

            migrationBuilder.CreateIndex(
                name: "IX_profil_mentor_verificat_de_id",
                schema: "mark_ai",
                table: "profil_mentor",
                column: "verificat_de_id");

            migrationBuilder.CreateIndex(
                name: "idx_progres_lectie_parcurs_id_fkey",
                schema: "mark_ai",
                table: "progres_lectie",
                column: "parcurs_id");

            migrationBuilder.CreateIndex(
                name: "idx_progres_lectie_ultimul_curs_id_fkey",
                schema: "mark_ai",
                table: "progres_lectie",
                column: "ultimul_curs_id");

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
                name: "IX_utilizator_judet_id",
                schema: "mark_ai",
                table: "utilizator",
                column: "judet_id");

            migrationBuilder.CreateIndex(
                name: "IX_utilizator_localitate_id",
                schema: "mark_ai",
                table: "utilizator",
                column: "localitate_id");

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
                name: "idx_utilizator_rol_rol_id_fkey",
                schema: "mark_ai",
                table: "utilizator_rol",
                column: "rol_id");

            // Case-insensitive uniqueness for sign-in names (expression indexes, not expressible in the EF model).
            migrationBuilder.Sql("CREATE UNIQUE INDEX utilizator_email_ci ON mark_ai.utilizator (lower(email));");
            migrationBuilder.Sql("CREATE UNIQUE INDEX utilizator_username_ci ON mark_ai.utilizator (lower(username));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cod_verificare",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "comision_mentor",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "eveniment_recompensa",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "fisier_lectie",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "generare_ai",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "incercare_quiz",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "intrebare_varianta",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "membru_organizatie",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "mentor_materie",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "mesaj",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "mesaj_iesire",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "nivel_xp",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "parcurs_organizatie",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "plata",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "preferinte_utilizator",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "progres_lectie",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "regula_background",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "utilizator_rol",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "decontare",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "intrebare",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "sesiune",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "background",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "interfata",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "rol",
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
                name: "parcurs",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "materie",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "organizatie",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "utilizator",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "localitate",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "judet",
                schema: "mark_ai");
        }
    }
}

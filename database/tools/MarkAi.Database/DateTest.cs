using System.IO.Compression;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Test data for a test or demo database (never a real one), all with one password (<c>MARKAI_PAROLA_TEST</c>, "HackAttack" by
/// default) and @hackattack.com addresses, emails confirmed:
/// <list type="bullet">
/// <item>accounts admin (platform admin), organizatie (admin of "Organizație Demo"), profesor (Romanian) and elev (grade XII);</item>
/// <item>one professor per subject (profesor-matematica, profesor-romana, ...), approved in "Organizație Demo";</item>
/// <item>20 students elev1–elev20 (grades IX–XII): 16 in the organisation, 2 with a pending request, 2 in no organisation;
/// București, Alba Iulia and Cluj-Napoca, for the "near me" filters;</item>
/// <item>the subjects, Lecții (per grade), chapters, quiz questions and History files from <c>date/date-test.json</c>, authored by
/// the subject's professor in "Organizație Demo" (one Lecție per subject organisation-only, one without questions);</item>
/// <item>some progress: elev1 and elev2 have passed a few Lecții; platform Lecții without an author get the admin.</item>
/// </list>
/// Run after <c>migrate</c>, <c>seed-localitati</c> and <c>seed</c>. Everything is matched by username, subject code, Lecție or
/// chapter title, question text or file type, and skipped when it already exists. Files are written to <paramref name="stocare"/>
/// (the API's storage folder) with their text already extracted.
/// </summary>
static class DateTest
{
    private const string Domeniu = "hackattack.com";
    private const int Bucuresti = 40, Sector1 = 179141, Sector2 = 179150;
    private static readonly string[] Clase = ["IX", "X", "XI", "XII"];

    private static readonly (string Username, string Nume)[] Elevi =
    [
        ("elev1", "Andrei Popa"), ("elev2", "Maria Ionescu"), ("elev3", "Alexandru Stoica"), ("elev4", "Ioana Dumitru"),
        ("elev5", "Ștefan Constantin"), ("elev6", "Ana Mihăilescu"), ("elev7", "Matei Rusu"), ("elev8", "Elena Tudor"),
        ("elev9", "David Georgescu"), ("elev10", "Sofia Marin"), ("elev11", "Luca Neagu"), ("elev12", "Daria Ene"),
        ("elev13", "Mihai Lazăr"), ("elev14", "Bianca Toma"), ("elev15", "Radu Șerban"), ("elev16", "Irina Barbu"),
        ("elev17", "Vlad Moldovan"), ("elev18", "Carla Pop"), ("elev19", "Tudor Nistor"), ("elev20", "Alesia Gavrilă"),
    ];

    public static async Task Run(string connection, string dataDir, string stocare)
    {
        var parola = Environment.GetEnvironmentVariable("MARKAI_PAROLA_TEST") is { Length: >= 8 } p ? p : "HackAttack";
        var date = JsonSerializer.Deserialize<DateJson>(await File.ReadAllTextAsync(Path.Combine(dataDir, "date-test.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidOperationException("date-test.json is empty.");

        await using var db = DatabaseTools.Open(connection);
        if (!await db.Set<Judet>().AnyAsync(j => j.Id == Bucuresti)) throw new InvalidOperationException("Counties not found. Run 'seed-localitati' first.");
        if (!await db.Roluri.AnyAsync(r => r.Nume == "organizatie")) throw new InvalidOperationException("Roles not found. Run 'seed' first.");
        var acum = DateTime.UtcNow;
        var roluri = await db.Roluri.ToDictionaryAsync(r => r.Nume, r => r.Id);
        var noi = new Numaratoare();

        async Task<Utilizator> Cont(string username, string nume, string[] rol, Action<Utilizator>? configureaza = null)
        {
            var u = await db.Utilizatori.Include(x => x.ProfilMentor).SingleOrDefaultAsync(x => x.Username.ToLower() == username);
            if (u is not null) return u;
            u = new Utilizator { Nume = nume, Username = username, Email = $"{username}@{Domeniu}", CreatLa = acum, EmailConfirmatLa = acum,
                JudetId = Bucuresti, LocalitateId = Sector1 };
            u.ParolaHash = Parole.Hash(u, parola);
            foreach (var r in rol) u.Roluri.Add(new UtilizatorRol { RolId = roluri[r] });
            configureaza?.Invoke(u);
            db.Utilizatori.Add(u);
            await db.SaveChangesAsync();
            noi.Conturi++;
            return u;
        }
        ProfilMentor Profesor(string descriere) => new() { Descriere = descriere, StareVerificare = "aprobat", ActivatLa = acum };

        // The four base accounts and the demo organisation.
        await Cont("admin", "Admin", ["admin"]);
        var adminOrg = await Cont("organizatie", "Admin Organizație", ["organizatie", "profesor"], u => u.ProfilMentor = Profesor("Administratorul organizației (cont de test)."));
        var romana = await Materie(db, date.Materii.Single(m => m.Cod == "romana"), noi);
        await Cont("profesor", "Profesor", ["profesor"], u => { u.ProfilMentor = Profesor("Profesor de română (cont de test)."); u.ProfilMentor.Materii.Add(new MentorMaterie { MaterieId = romana.Id }); });
        await Cont("elev", "Elev", ["elev"], u => u.Clasa = "XII");

        var org = await db.Set<Organizatie>().FirstOrDefaultAsync(o => o.Nume == "Organizație Demo");
        if (org is null)
        {
            db.Add(org = new Organizatie { Nume = "Organizație Demo", Adresa = "Strada Școlii 1", JudetId = Bucuresti, LocalitateId = Sector1,
                Stare = Organizatie.Aprobata, AdminId = adminOrg.Id, DecisLa = acum, CreatLa = acum });
            await db.SaveChangesAsync();
        }
        var membri = await db.Set<MembruOrganizatie>().Where(m => m.OrganizatieId == org.Id).Select(m => m.UtilizatorId).ToListAsync();
        void Membru(Utilizator u, string rol, bool aprobat = true)
        {
            if (membri.Contains(u.Id)) return;
            membri.Add(u.Id);
            db.Add(new MembruOrganizatie { OrganizatieId = org.Id, UtilizatorId = u.Id, Rol = rol, CerutLa = acum,
                Stare = aprobat ? MembruOrganizatie.Aprobat : MembruOrganizatie.InAsteptare,
                DecisLa = aprobat ? acum : null, DecisDeId = aprobat ? org.AdminId : null, ExpiraLa = aprobat ? null : acum.AddDays(7) });
        }
        Membru(adminOrg, MembruOrganizatie.RolProfesor);
        Membru(await db.Utilizatori.SingleAsync(u => u.Username == "profesor"), MembruOrganizatie.RolProfesor);
        Membru(await db.Utilizatori.SingleAsync(u => u.Username == "elev"), MembruOrganizatie.RolElev);
        await db.SaveChangesAsync();

        // Subjects with their professor, Lecții, chapters, questions and files.
        foreach (var mj in date.Materii)
        {
            var materie = await Materie(db, mj, noi);
            var profesor = await Cont(mj.Profesor.Username, mj.Profesor.Nume, ["profesor"], u =>
            {
                u.ProfilMentor = Profesor($"{mj.Profesor.Nume}, profesor de {mj.Denumire.ToLowerInvariant()} (cont de test).");
                u.ProfilMentor.LocOrganizatieId = org.Id;
                u.ProfilMentor.Materii.Add(new MentorMaterie { MaterieId = materie.Id });
            });
            Membru(profesor, MembruOrganizatie.RolProfesor);
            await db.SaveChangesAsync();

            var ordine = await db.Set<Parcurs>().Where(x => x.MaterieId == materie.Id).MaxAsync(x => (int?)x.Ordine) ?? 0;
            foreach (var lj in mj.Lectii)
            {
                var lectie = await db.Set<Parcurs>().SingleOrDefaultAsync(x => x.MaterieId == materie.Id && x.Titlu == lj.Titlu);
                if (lectie is null)
                {
                    db.Add(lectie = new Parcurs { MaterieId = materie.Id, Titlu = lj.Titlu, Descriere = lj.Descriere, Clasa = lj.Clasa, AutorId = profesor.Id,
                        OrganizatieId = org.Id, Vizibilitate = lj.Vizibilitate, Ordine = ++ordine, CreatLa = acum });
                    await db.SaveChangesAsync();
                    noi.Lectii++;
                }
                var capitole = await db.Curs.Where(c => c.ParcursId == lectie.Id).Select(c => c.Titlu).ToListAsync();
                foreach (var (cj, i) in lj.Capitole.Select((c, i) => (c, i)).Where(x => !capitole.Contains(x.c.Titlu)))
                {
                    db.Curs.Add(new Curs { MaterieId = materie.Id, ParcursId = lectie.Id, Titlu = cj.Titlu, Ordine = i + 1, CreatDeId = profesor.Id, CreatLa = acum,
                        Materiale = cj.Notite.Select(n => new SectiuneMaterial { Titlu = n.Titlu, Text = n.Text }).ToList() });
                    noi.Capitole++;
                }
                var intrebari = await db.Set<Intrebare>().Where(q => q.ParcursId == lectie.Id).Select(q => q.Text).ToListAsync();
                foreach (var qj in lj.Intrebari.Where(q => !intrebari.Contains(q.Text)))
                {
                    db.Add(new Intrebare { MaterieId = materie.Id, ParcursId = lectie.Id, Text = qj.Text, Explicatie = qj.Explicatie, CreataLa = acum,
                        Variante = qj.Variante.Select((v, k) => new VariantaIntrebare { Text = v, Corecta = k == qj.Corect }).ToList() });
                    noi.Intrebari++;
                }
                var tipuri = await db.Set<FisierLectie>().Where(f => f.ParcursId == lectie.Id).Select(f => f.Tip).ToListAsync();
                foreach (var fj in lj.Fisiere.Where(f => !tipuri.Contains(f.Tip)))
                {
                    var continut = fj.Tip == FisierLectie.Pdf ? Pdf(fj.Text) : Docx(fj.Text);
                    var cheie = $"lectii/{Guid.NewGuid():N}.{fj.Tip}";
                    var cale = Path.Combine(stocare, cheie);
                    Directory.CreateDirectory(Path.GetDirectoryName(cale)!);
                    await File.WriteAllBytesAsync(cale, continut);
                    // The PDF uses a standard font, so its text has no diacritics; the DOCX keeps them.
                    db.Add(new FisierLectie { ParcursId = lectie.Id, Nume = fj.Nume, Tip = fj.Tip, Cheie = cheie, MarimeOcteti = continut.Length,
                        Sha256 = Convert.ToHexStringLower(SHA256.HashData(continut)), StareText = FisierLectie.Gata,
                        TextExtras = fj.Tip == FisierLectie.Pdf ? FaraDiacritice(fj.Text) : fj.Text, IncarcatDeId = profesor.Id, IncarcatLa = acum });
                    noi.Fisiere++;
                }
                await db.SaveChangesAsync();
            }
        }

        // Students: 1–16 in the organisation, 17–18 waiting for approval, 19–20 in no organisation.
        var alba = await Loc(db, "Alba", "Alba Iulia");
        var cluj = await Loc(db, "Cluj", "Cluj-Napoca");
        foreach (var ((username, nume), i) in Elevi.Select((e, i) => (e, i)))
        {
            var nr = i + 1;
            (int? judet, int? localitate) = nr switch { <= 10 => (Bucuresti, Sector1), <= 14 => (Bucuresti, Sector2), 15 or 16 or 19 => alba, 17 or 18 => cluj, _ => ((int?)null, (int?)null) };
            var elev = await Cont(username, nume, ["elev"], u => { u.Clasa = Clase[i % 4]; u.JudetId = judet; u.LocalitateId = localitate; });
            if (nr <= 18) Membru(elev, MembruOrganizatie.RolElev, aprobat: nr <= 16);
        }
        await db.SaveChangesAsync();

        // Platform Lecții from `seed` (no organisation) belong to the platform admin.
        var admin = await db.Utilizatori.Where(u => u.Username == "admin").Select(u => u.Id).SingleAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE mark_ai.parcurs SET autor_id = {admin} WHERE autor_id IS NULL AND organizatie_id IS NULL");

        // Some progress, so dashboards are not empty: elev1 passed three Lecții, elev2 one.
        var progres = new ProgresService(db);
        foreach (var (username, titluri) in new[] { ("elev1", new[] { "Mișcarea rectilinie", "Antichitatea: Atena și Roma", "Funcția de gradul al II-lea" }),
                                                   ("elev2", new[] { "Pământul, planetă a Sistemului Solar" }) })
        {
            var uid = await db.Utilizatori.Where(u => u.Username == username).Select(u => u.Id).SingleAsync();
            foreach (var titlu in titluri)
            {
                var lectie = await db.Set<Parcurs>().Where(x => x.Titlu == titlu).Select(x => new { x.Id, Capitol = x.Capitole.OrderBy(c => c.Ordine).Select(c => c.Id).First() }).SingleAsync();
                if (await db.Set<ProgresLectie>().AnyAsync(x => x.UtilizatorId == uid && x.ParcursId == lectie.Id)) continue;
                await progres.AcceseazaAsync(uid, lectie.Capitol);
                await progres.PromoveazaAsync(uid, lectie.Id);
            }
        }

        Console.WriteLine($"Test data: {noi.Conturi} account(s), {noi.Materii} subject(s), {noi.Lectii} Lecție(s), {noi.Capitole} chapter(s), "
            + $"{noi.Intrebari} question(s), {noi.Fisiere} file(s) added; existing ones left as they were. Password of new accounts: MARKAI_PAROLA_TEST or the default.");
    }

    private sealed class Numaratoare { public int Conturi, Materii, Lectii, Capitole, Intrebari, Fisiere; }

    private static async Task<Materie> Materie(MarkAiDbContext db, MaterieJson mj, Numaratoare noi)
    {
        var m = await db.Materii.FirstOrDefaultAsync(x => x.Cod == mj.Cod || x.Denumire == mj.Denumire);
        if (m is not null) return m;
        db.Materii.Add(m = new Materie { Cod = mj.Cod, Denumire = mj.Denumire, Descriere = mj.Descriere, Ordine = await db.Materii.CountAsync() + 1 });
        await db.SaveChangesAsync();
        noi.Materii++;
        return m;
    }

    private static async Task<(int?, int?)> Loc(MarkAiDbContext db, string judet, string localitate)
    {
        var l = await db.Set<Localitate>().Where(x => x.Nume == localitate && db.Set<Judet>().Any(j => j.Id == x.JudetId && j.Nume == judet))
            .Select(x => new { x.Id, x.JudetId }).FirstOrDefaultAsync();
        return l is null ? (Bucuresti, Sector1) : (l.JudetId, l.Id);
    }

    private static string FaraDiacritice(string s)
    {
        var d = s.Replace('–', '-').Normalize(NormalizationForm.FormD);
        return new string(d.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray())
            .Normalize(NormalizationForm.FormC);
    }

    // A one-page PDF, one line per text line, in Helvetica (ASCII only), with a correct cross-reference table.
    private static byte[] Pdf(string text)
    {
        var linii = FaraDiacritice(text).Split('\n').Select(l => new string(l.Where(c => c < 128).ToArray()).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)"));
        var flux = "BT /F1 12 Tf 50 790 Td 16 TL\n" + string.Concat(linii.Select(l => $"({l}) Tj T*\n")) + "ET";
        string[] obiecte =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(flux)} >>\nstream\n{flux}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
        ];
        var sb = new StringBuilder("%PDF-1.4\n");
        var pozitii = new List<int>();
        for (var i = 0; i < obiecte.Length; i++)
        {
            pozitii.Add(Encoding.ASCII.GetByteCount(sb.ToString()));
            sb.Append($"{i + 1} 0 obj\n{obiecte[i]}\nendobj\n");
        }
        var xref = Encoding.ASCII.GetByteCount(sb.ToString());
        sb.Append($"xref\n0 {obiecte.Length + 1}\n0000000000 65535 f \n");
        foreach (var p in pozitii) sb.Append($"{p:D10} 00000 n \n");
        sb.Append($"trailer\n<< /Size {obiecte.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    // A minimal Word document (one paragraph per line, the first one bold) that Word and LibreOffice open.
    private static byte[] Docx(string text)
    {
        var paragrafe = text.Split('\n').Select((l, i) =>
            $"<w:p><w:r>{(i == 0 ? "<w:rPr><w:b/></w:rPr>" : "")}<w:t xml:space=\"preserve\">{SecurityElement.Escape(l)}</w:t></w:r></w:p>");
        (string, string)[] parti =
        [
            ("[Content_Types].xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>"""),
            ("_rels/.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>"""),
            ("word/document.xml", $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>{string.Concat(paragrafe)}</w:body></w:document>"""),
        ];
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (nume, continut) in parti)
            {
                using var s = zip.CreateEntry(nume, CompressionLevel.Optimal).Open();
                s.Write(Encoding.UTF8.GetBytes(continut));
            }
        return ms.ToArray();
    }

    private sealed record DateJson(List<MaterieJson> Materii);
    private sealed record MaterieJson(string Cod, string Denumire, string? Descriere, ProfesorJson Profesor, List<LectieJson> Lectii);
    private sealed record ProfesorJson(string Username, string Nume);
    private sealed record LectieJson(string Titlu, string Clasa, string? Descriere, string Vizibilitate, List<CapitolJson> Capitole,
        List<IntrebareJson> Intrebari, List<FisierJson> Fisiere);
    private sealed record CapitolJson(string Titlu, List<NotitaJson> Notite);
    private sealed record NotitaJson(string Titlu, string Text);
    private sealed record IntrebareJson(string Text, List<string> Variante, int Corect, string? Explicatie);
    private sealed record FisierJson(string Tip, string Nume, string Text);
}

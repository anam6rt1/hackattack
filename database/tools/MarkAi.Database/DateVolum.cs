using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Volume test data, to look at the lists and their pages with realistic numbers (TEST or demo databases only): every high-school
/// subject, 180 organisations in localities across the country (approved, pending and refused), 1 000 professors (approved,
/// pending, refused; most with upcoming sessions), 5–10 Lecții with chapters and questions by every approved professor (about 7 500),
/// and 50 000 students (most in an
/// organisation, some with passed Lecții), enough to load-test the API (tools/MarkAi.Stres signs in as vol-elev00001, vol-elev00002, …).
/// Every generated account has a username starting with <see cref="Prefix"/> (numbered per kind: vol-org…, vol-prof…, vol-elev…) and an email
/// at <see cref="Domeniu"/>; everything else hangs off those accounts, so <c>date-volum --sterge</c> removes exactly this data
/// (and the subjects it added, when nothing else uses them). Accounts share the date-test password (MARKAI_PAROLA_TEST or the
/// default). Generated from a fixed seed, so two runs on empty databases give the same data. Run after migrate, seed-localitati,
/// seed (and date-test, optionally).
/// </summary>
static class DateVolum
{
    public const string Prefix = "vol-", Domeniu = "volum.hackattack.com";
    private const int Organizatii = 180, Profesori = 1000, Elevi = 50000, MinLectii = 5, MaxLectii = 10;
    // Students are saved this many at a time, with the change tracker emptied in between (50 000 tracked accounts slow every save).
    private const int Lot = 2000;
    private const string Marcaj = "Generat pentru testarea listelor (date de volum).";

    // The high-school subjects (Romanian national curriculum), with a few topics each for the generated Lecții.
    private static readonly (string Cod, string Denumire, string[] Teme)[] Materii =
    [
        ("romana", "Limba și literatura română", ["Romantismul", "Luceafărul", "Ion de Liviu Rebreanu", "Baltagul", "Moara cu noroc", "Plumb", "Testament", "Enigma Otiliei", "Comentariul de text", "Eseul argumentativ"]),
        ("engleza", "Limba engleză", ["Tenses", "Conditionals", "Reported speech", "Essay writing", "Phrasal verbs", "Reading comprehension"]),
        ("franceza", "Limba franceză", ["Le passé composé", "Le subjonctif", "La lettre formelle", "Compréhension écrite", "Les pronoms"]),
        ("germana", "Limba germană", ["Perfekt", "Nebensätze", "Der Brief", "Leseverstehen", "Präpositionen"]),
        ("latina", "Limba latină", ["Declinarea I", "Verbul sum", "Traducerea textului", "Cultura romană"]),
        ("matematica", "Matematică", ["Funcția de gradul al II-lea", "Progresii", "Logaritmi", "Matrice", "Determinanți", "Derivate", "Integrale", "Geometrie analitică", "Trigonometrie", "Probabilități"]),
        ("fizica", "Fizică", ["Mișcarea rectilinie", "Legile lui Newton", "Energia mecanică", "Termodinamică", "Electricitate", "Optică", "Oscilații", "Fizică nucleară"]),
        ("chimie", "Chimie", ["Structura atomului", "Tabelul periodic", "Legături chimice", "Soluții", "Echilibrul chimic", "Hidrocarburi", "Alcooli", "Acizi carboxilici"]),
        ("biologie", "Biologie", ["Celula", "Diviziunea celulară", "Genetica", "Ereditatea", "Sistemul nervos", "Fotosinteza", "Evoluția", "Ecologie"]),
        ("informatica", "Informatică", ["Algoritmi", "Tablouri", "Subprograme", "Recursivitate", "Backtracking", "Grafuri", "Arbori", "Fișiere text"]),
        ("tic", "Tehnologia informației și a comunicațiilor", ["Editoare de text", "Calcul tabelar", "Prezentări", "Baze de date", "Internet și siguranță"]),
        ("istorie", "Istorie", ["Antichitatea", "Evul Mediu românesc", "Statul modern", "Marea Unire", "Perioada interbelică", "Comunismul", "România după 1989", "Relații internaționale"]),
        ("geografie", "Geografie", ["Relieful României", "Clima", "Hidrografia", "Populația", "Așezările", "Economia", "Europa", "Probleme globale"]),
        ("logica", "Logică, argumentare și comunicare", ["Termenii", "Propozițiile", "Silogismul", "Argumentarea", "Erori logice"]),
        ("psihologie", "Psihologie", ["Procesele cognitive", "Memoria", "Învățarea", "Personalitatea", "Emoțiile"]),
        ("economie", "Economie", ["Piața", "Cererea și oferta", "Banii", "Inflația", "Firma"]),
        ("filosofie", "Filosofie", ["Omul", "Morala", "Politica", "Cunoașterea", "Filosofia antică"]),
        ("sociologie", "Sociologie", ["Socializarea", "Grupurile sociale", "Instituțiile", "Cercetarea sociologică"]),
        ("antreprenoriala", "Educație antreprenorială", ["Ideea de afaceri", "Planul de afaceri", "Marketingul", "Finanțarea"]),
        ("religie", "Religie", ["Istoria religiilor", "Morala creștină", "Valorile"]),
        ("sport", "Educație fizică și sport", ["Atletism", "Gimnastică", "Jocuri sportive"]),
        ("muzica", "Educație muzicală", ["Teoria muzicii", "Istoria muzicii", "Genuri muzicale"]),
        ("plastica", "Educație plastică", ["Elemente de limbaj plastic", "Istoria artei", "Compoziția"]),
        ("consiliere", "Consiliere și orientare", ["Cunoașterea de sine", "Cariera", "Managementul timpului"]),
        ("studii-sociale", "Studii sociale", ["Drepturile omului", "Democrația", "Cetățenia europeană"]),
    ];

    private static readonly string[] Prenume =
    [
        "Andrei", "Maria", "Alexandru", "Elena", "Ștefan", "Ioana", "Mihai", "Ana", "Tudor", "Sofia", "David", "Daria", "Luca", "Irina", "Matei", "Bianca",
        "Radu", "Alesia", "Vlad", "Carla", "George", "Diana", "Cristian", "Andreea", "Bogdan", "Raluca", "Sorin", "Gabriela", "Victor", "Teodora",
        "Florin", "Mădălina", "Dragoș", "Alina", "Ionuț", "Cătălina", "Răzvan", "Larisa", "Ovidiu", "Iulia", "Paul", "Oana", "Cosmin", "Roxana",
    ];
    private static readonly string[] NumeFamilie =
    [
        "Popescu", "Ionescu", "Popa", "Dumitru", "Stoica", "Stan", "Gheorghe", "Matei", "Ciobanu", "Rusu", "Munteanu", "Constantin", "Marin", "Tudor",
        "Dobre", "Barbu", "Nistor", "Florea", "Ene", "Dinu", "Lazăr", "Toma", "Mihai", "Neagu", "Moldovan", "Pop", "Șerban", "Gavrilă", "Mihăilescu",
        "Georgescu", "Dumitrescu", "Răducanu", "Vlad", "Ilie", "Cristea", "Oprea", "Sandu", "Voicu", "Ungureanu", "Bălan", "Țurcanu", "Mocanu",
    ];
    private static readonly string[] Personalitati =
    [
        "Mihai Eminescu", "Ion Creangă", "George Coșbuc", "Nicolae Bălcescu", "Spiru Haret", "Gheorghe Lazăr", "Grigore Moisil", "Tudor Vianu",
        "Mihail Sadoveanu", "Lucian Blaga", "Ion Luca Caragiale", "Vasile Alecsandri", "Octav Onicescu", "Emil Racoviță", "Grigore Antipa", "Ana Aslan",
        "Henri Coandă", "Traian Vuia", "Constantin Brâncuși", "Mihai Viteazul", "Ștefan cel Mare", "Alexandru Ioan Cuza", "Avram Iancu", "Andrei Mureșanu",
        "Titu Maiorescu", "Gheorghe Șincai", "Petru Maior", "Dimitrie Cantemir", "Nicolae Iorga", "George Enescu",
    ];
    private static readonly string[] Tipuri = ["Colegiul Național", "Liceul Teoretic", "Liceul Tehnologic", "Școala Gimnazială", "Centrul de pregătire"];
    private static readonly string[] Aspecte = ["introducere", "teorie", "exerciții", "aplicații", "recapitulare", "subiecte de bac", "probleme alese", "fișă de lucru"];
    private static readonly string[] Clase = ["IX", "X", "XI", "XII", "Bac"];

    public static async Task Run(string connection, bool sterge)
    {
        await using var db = DatabaseTools.Open(connection);
        if (sterge)
        {
            await StergeAsync(db);
            return;
        }
        if (await db.Utilizatori.AnyAsync(u => u.Username.StartsWith(Prefix)))
            throw new InvalidOperationException($"Volume data already exists (usernames {Prefix}…). Run `date-volum --sterge` first to generate it again.");
        var roluri = await db.Roluri.ToDictionaryAsync(r => r.Nume, r => r.Id);
        if (!roluri.ContainsKey("organizatie")) throw new InvalidOperationException("Roles not found. Run 'seed' first.");
        var localitati = await db.Set<Localitate>().AsNoTracking().Where(l => l.Urban).Select(l => new { l.Id, l.JudetId }).ToListAsync();
        if (localitati.Count == 0) throw new InvalidOperationException("Localities not found. Run 'seed-localitati' first.");
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        var rnd = new Random(2026);
        T Una<T>(IReadOnlyList<T> lista) => lista[rnd.Next(lista.Count)];
        var acum = DateTime.UtcNow;
        var parola = Environment.GetEnvironmentVariable("MARKAI_PAROLA_TEST") is { Length: >= 8 } dinMediu ? dinMediu : "HackAttack";
        // One hash for every generated account (the hasher salts it; any account can verify it): hashing 51 000 passwords would take hours.
        var hash = Parole.Hash(new Utilizator(), parola);

        // Subjects.
        var materii = new Dictionary<string, (int Id, string[] Teme)>();
        foreach (var (cod, denumire, teme) in Materii)
        {
            var m = await db.Materii.FirstOrDefaultAsync(x => x.Cod == cod || x.Denumire == denumire);
            if (m is null)
            {
                db.Materii.Add(m = new Materie { Cod = cod, Denumire = denumire, Ordine = await db.Materii.CountAsync() + 1 });
                await db.SaveChangesAsync();
            }
            materii[cod] = (m.Id, teme);
        }
        var coduri = materii.Keys.ToList();

        var numere = new Dictionary<string, int>();
        Utilizator Cont(string tip, string? clasa, (int Id, int JudetId)? loc, params string[] rol)
        {
            var nr = numere[tip] = numere.GetValueOrDefault(tip) + 1;
            var prenume = Una(Prenume);
            var nume = Una(NumeFamilie);
            var username = $"{Prefix}{tip}{nr:00000}";
            var u = new Utilizator { Nume = $"{prenume} {nume}", Username = username, Email = $"{username}@{Domeniu}", ParolaHash = hash, Clasa = clasa,
                CreatLa = acum.AddDays(-rnd.Next(1, 120)).AddMinutes(-rnd.Next(0, 1440)), JudetId = loc?.JudetId, LocalitateId = loc?.Id };
            u.EmailConfirmatLa = u.CreatLa;
            foreach (var r in rol) u.Roluri.Add(new UtilizatorRol { RolId = roluri[r] });
            return u;
        }
        ProfilMentor Profil(string stare, IEnumerable<string> materiiProfesor) => new()
        {
            StareVerificare = stare, ActivatLa = stare == RezervareService.MentorAprobat ? acum.AddDays(-rnd.Next(1, 90)) : null,
            Descriere = $"Profesor (cont generat). {Marcaj}",
            MotivRespingere = stare == "respins" ? "Dovada calificării nu a putut fi verificată." : null,
            Materii = materiiProfesor.Distinct().Select(c => new MentorMaterie { MaterieId = materii[c].Id }).ToList(),
        };

        // Organisations, each with its admin (who teaches there too, as the app does on approval).
        var org = new List<(Organizatie O, Utilizator Admin, (int Id, int JudetId) Loc, string Materie)>();
        for (var i = 0; i < Organizatii; i++)
        {
            var loc = Una(localitati);
            var stare = i % 10 < 8 ? Organizatie.Aprobata : i % 10 == 8 ? Organizatie.InAsteptare : Organizatie.Respinsa;
            var admin = Cont("org", null, (loc.Id, loc.JudetId), "organizatie", "profesor");
            var materie = Una(coduri);
            admin.ProfilMentor = Profil(stare == Organizatie.Aprobata ? RezervareService.MentorAprobat : "in_asteptare", [materie]);
            var o = new Organizatie
            {
                Nume = $"{Una(Tipuri)} „{Una(Personalitati)}” {i + 1}", Adresa = $"Strada Școlii {rnd.Next(1, 120)}", JudetId = loc.JudetId, LocalitateId = loc.Id,
                Stare = stare, Admin = admin, CreatLa = admin.CreatLa, DecisLa = stare == Organizatie.InAsteptare ? null : admin.CreatLa.AddDays(1),
                MotivRespingere = stare == Organizatie.Respinsa ? "Datele organizației nu au putut fi verificate." : null,
            };
            if (stare == Organizatie.Aprobata)
                o.Membri.Add(new MembruOrganizatie { Utilizator = admin, Rol = MembruOrganizatie.RolProfesor, Stare = MembruOrganizatie.Aprobat, CerutLa = o.CreatLa, DecisLa = o.DecisLa });
            db.Add(o);
            org.Add((o, admin, (loc.Id, loc.JudetId), materie));
        }
        await SalveazaAsync(db);
        var aprobate = org.Where(x => x.O.Stare == Organizatie.Aprobata).ToList();

        // Professors: 85 % approved in one or two organisations, 10 % waiting, 5 % refused.
        var profesori = new List<(Utilizator U, List<Organizatie> Org, string[] Materii)>();
        for (var i = 0; i < Profesori; i++)
        {
            var acasa = Una(aprobate);
            var stare = i % 20 < 17 ? RezervareService.MentorAprobat : i % 20 < 19 ? "in_asteptare" : "respins";
            var mat = Enumerable.Range(0, rnd.Next(1, 4)).Select(_ => Una(coduri)).Distinct().ToArray();
            var u = Cont("prof", null, acasa.Loc, "profesor");
            u.ProfilMentor = Profil(stare, mat);
            var ale = new List<Organizatie> { acasa.O };
            if (rnd.Next(4) == 0) ale.Add(Una(aprobate).O);
            foreach (var o in ale.Distinct())
                o.Membri.Add(new MembruOrganizatie
                {
                    Utilizator = u, Rol = MembruOrganizatie.RolProfesor, CerutLa = u.CreatLa,
                    Stare = stare == RezervareService.MentorAprobat ? MembruOrganizatie.Aprobat : stare == "respins" ? MembruOrganizatie.Respins : MembruOrganizatie.InAsteptare,
                    DecisLa = stare == "in_asteptare" ? null : u.CreatLa.AddDays(1), ExpiraLa = stare == "in_asteptare" ? acum.AddDays(5) : null,
                    MotivRespingere = stare == "respins" ? u.ProfilMentor.MotivRespingere : null, PoatePublicaPublic = rnd.Next(10) > 0,
                });
            if (stare == RezervareService.MentorAprobat && rnd.Next(3) == 0) u.ProfilMentor.LocOrganizatie = acasa.O;
            db.Add(u);
            profesori.Add((u, ale.Distinct().ToList(), mat));
        }
        await SalveazaAsync(db);

        // Students: 85 % members of an organisation (in its locality), 5 % waiting for approval, 10 % in none. From here on the
        // organisations are referred to by id, so the tracker can be emptied after every batch.
        db.ChangeTracker.Clear();
        var elevi = new List<Utilizator>();
        for (var i = 0; i < Elevi; i++)
        {
            var o = Una(aprobate);
            var fel = i % 20;
            var altundeva = Una(localitati);
            var e = Cont("elev", Una(Clase[..4]), fel < 18 ? o.Loc : (altundeva.Id, altundeva.JudetId), "elev");
            if (fel < 18)
                db.Add(new MembruOrganizatie
                {
                    OrganizatieId = o.O.Id, Utilizator = e, Rol = MembruOrganizatie.RolElev, CerutLa = e.CreatLa, Stare = fel < 17 ? MembruOrganizatie.Aprobat : MembruOrganizatie.InAsteptare,
                    DecisLa = fel < 17 ? e.CreatLa.AddDays(1) : null, ExpiraLa = fel < 17 ? null : acum.AddDays(5),
                });
            db.Add(e);
            elevi.Add(e);
            if (i % Lot == Lot - 1)
            {
                await SalveazaAsync(db);
                db.ChangeTracker.Clear();
                Console.WriteLine($"  {i + 1} / {Elevi} students");
            }
        }
        await SalveazaAsync(db);
        db.ChangeTracker.Clear();

        // Lecții: 5–10 by every approved professor (organisation admins too), in one of their organisations; chapters and questions;
        // some organisation-only, some drafts. Saved in batches, with the tracker emptied in between.
        var autori = profesori.Where(x => x.U.ProfilMentor!.StareVerificare == RezervareService.MentorAprobat).ToList();
        var lectii = new List<Parcurs>();
        var dePublicat = autori.Concat(aprobate.Select(x => (U: x.Admin, Org: new List<Organizatie> { x.O }, Materii: new[] { x.Materie })))
            .SelectMany(a => Enumerable.Repeat(a, rnd.Next(MinLectii, MaxLectii + 1))).ToList();
        for (var i = 0; i < dePublicat.Count; i++)
        {
            var (u, ale, mat) = dePublicat[i];
            var cod = Una(mat);
            var tema = Una(materii[cod].Teme);
            var p = new Parcurs
            {
                MaterieId = materii[cod].Id, Titlu = $"{tema}: {Una(Aspecte)}", Descriere = Marcaj, Clasa = Una(Clase), AutorId = u.Id, OrganizatieId = Una(ale).Id,
                Vizibilitate = rnd.Next(100) < 15 ? Parcurs.DoarOrganizatie : Parcurs.Public, Publicat = rnd.Next(100) >= 8, Ordine = i, CreatLa = acum.AddDays(-rnd.Next(1, 100)),
            };
            for (var c = 1; c <= rnd.Next(1, 4); c++)
                p.Capitole.Add(new Curs { MaterieId = p.MaterieId, Titlu = $"{tema} — partea {c}", Ordine = c, CreatDeId = u.Id, CreatLa = p.CreatLa,
                    Materiale = [new SectiuneMaterial { Titlu = "Notițe", Text = $"Notițe despre {tema.ToLowerInvariant()}. {Marcaj}" }] });
            for (var q = 1; q <= rnd.Next(3, 6); q++)
            {
                var corect = rnd.Next(4);
                p.Intrebari.Add(new Intrebare { MaterieId = p.MaterieId, Text = $"Întrebarea {q} despre {tema.ToLowerInvariant()} (generată)", CreataLa = p.CreatLa,
                    Variante = Enumerable.Range(0, 4).Select(k => new VariantaIntrebare { Text = k == corect ? "Varianta corectă" : $"Varianta {k + 1}", Corecta = k == corect }).ToList() });
            }
            db.Add(p);
            lectii.Add(p);
            if (i % 500 == 499)
            {
                await SalveazaAsync(db);
                db.ChangeTracker.Clear();
            }
        }
        await SalveazaAsync(db);
        db.ChangeTracker.Clear();

        // Upcoming sessions for most approved professors; some booked by students (through the booking service, as in the app).
        var pret = await db.TarifeFixe.Where(t => t.Activ).OrderBy(t => t.Id).FirstOrDefaultAsync();
        var sedinte = new List<CameraTutoring>();
        foreach (var (u, ale, mat) in autori.Where(_ => rnd.Next(10) < 7))
            for (var s = 0; s < rnd.Next(1, 4); s++)
            {
                var inceput = acum.Date.AddDays(rnd.Next(1, 30)).AddHours(rnd.Next(8, 20));
                var tip = CameraTutoring.Tipuri[rnd.Next(3)];
                var proBono = pret is null || rnd.Next(4) == 0;
                var c = new CameraTutoring
                {
                    MentorId = u.Id, MaterieId = materii[Una(mat)].Id, Titlu = $"Ședință de {Una(Aspecte)}", Descriere = Marcaj, Capacitate = rnd.Next(1, 12), ProBono = proBono,
                    TarifId = proBono ? null : pret!.Id, Status = RezervareService.SlotDeschis, InceputLa = inceput, FinalizatLa = inceput.AddMinutes(pret?.DurataMinute ?? 60), Tip = tip,
                    LinkIntalnire = tip == CameraTutoring.FataInFata ? null : "https://example.com/sedinta-volum",
                };
                if (tip != CameraTutoring.Online)
                {
                    var o = Una(ale);
                    (c.LocOrganizatieId, c.LocJudetId, c.LocLocalitateId, c.NotaLoc) = (o.Id, o.JudetId, o.LocalitateId, $"Sala {rnd.Next(1, 40)}");
                }
                db.Add(c);
                sedinte.Add(c);
            }
        await SalveazaAsync(db);
        // The booking service adds the payment after the booking, so it needs change detection; and a small tracker keeps it fast.
        db.ChangeTracker.Clear();
        db.ChangeTracker.AutoDetectChangesEnabled = true;
        var rezervari = new RezervareService(db);
        var rezervate = 0;
        foreach (var c in sedinte.Where(_ => rnd.Next(3) == 0))
            for (var r = 0; r < Math.Min(c.Capacitate, rnd.Next(1, 4)); r++)
            {
                try { await rezervari.RezervaAsync(Una(elevi).Id, c.Id, rnd.Next(2) == 0 ? Rezervare.Online : Rezervare.LaSala); rezervate++; }
                catch (Conflict) { } // the same student twice, or a full session
            }

        // Passed Lecții for some students (progress only; no XP is awarded for generated data), saved in batches as well.
        db.ChangeTracker.Clear();
        var publice = lectii.Where(l => l.Publicat && l.Vizibilitate == Parcurs.Public).ToList();
        var progrese = 0;
        foreach (var e in elevi.Where(_ => rnd.Next(5) == 0))
            foreach (var l in Enumerable.Range(0, rnd.Next(1, 5)).Select(_ => Una(publice)).DistinctBy(x => x.Id))
            {
                db.Add(new ProgresLectie { UtilizatorId = e.Id, ParcursId = l.Id, PromovatLa = acum.AddDays(-rnd.Next(1, 30)), UltimaAccesare = acum.AddDays(-rnd.Next(0, 30)) });
                if (++progrese % Lot == 0)
                {
                    await SalveazaAsync(db);
                    db.ChangeTracker.Clear();
                }
            }
        await SalveazaAsync(db);

        Console.WriteLine($"Volume data: {materii.Count} subjects, {org.Count} organisations, {profesori.Count} professors, {elevi.Count} students, "
            + $"{lectii.Count} Lecții, {sedinte.Count} sessions, {rezervate} bookings, {progrese} passed Lecții. Remove it with `date-volum --sterge`.");
    }

    private static async Task SalveazaAsync(MarkAiDbContext db)
    {
        db.ChangeTracker.DetectChanges();
        await db.SaveChangesAsync();
    }

    /// <summary>Removes the generated data: everything hanging off the generated accounts, in the order the foreign keys allow.</summary>
    private static async Task StergeAsync(MarkAiDbContext db)
    {
        // Removing 51 000 accounts with everything attached takes longer than the default 30 s per command.
        db.Database.SetCommandTimeout(TimeSpan.FromMinutes(15));
        await using var tx = await db.Database.BeginTransactionAsync();
        var u = db.Utilizatori.Where(x => x.Username.StartsWith(Prefix) && x.Email.EndsWith("@" + Domeniu)).Select(x => x.Id);
        var o = db.Set<Organizatie>().Where(x => u.Contains(x.AdminId)).Select(x => x.Id);
        var p = db.Set<Parcurs>().Where(x => (x.AutorId != null && u.Contains(x.AutorId.Value)) || (x.OrganizatieId != null && o.Contains(x.OrganizatieId.Value))).Select(x => x.Id);
        var s = db.CamereTutoring.Where(x => u.Contains(x.MentorId)).Select(x => x.Id);
        var r = db.Rezervari.Where(x => s.Contains(x.CameraId) || u.Contains(x.ElevId)).Select(x => x.Id);
        var ses = db.Set<Sesiune>().Where(x => (x.RezervareId != null && r.Contains(x.RezervareId.Value)) || u.Contains(x.UtilizatorId)).Select(x => x.Id);

        var n = new Dictionary<string, int>
        {
            ["mesaj"] = await db.Set<Mesaj>().Where(x => ses.Contains(x.SesiuneId)).ExecuteDeleteAsync(),
            ["eveniment_recompensa"] = await db.Set<EvenimentRecompensa>().Where(x => (x.SesiuneId != null && ses.Contains(x.SesiuneId.Value)) || u.Contains(x.UtilizatorId)).ExecuteDeleteAsync(),
            ["sesiune"] = await db.Set<Sesiune>().Where(x => ses.Contains(x.Id)).ExecuteDeleteAsync(),
            ["plata"] = await db.Set<Plata>().Where(x => r.Contains(x.RezervareId)).ExecuteDeleteAsync(),
            ["comision_mentor"] = await db.Set<ComisionMentor>().Where(x => r.Contains(x.RezervareId) || u.Contains(x.MentorId)).ExecuteDeleteAsync(),
            ["rezervare"] = await db.Rezervari.Where(x => r.Contains(x.Id)).ExecuteDeleteAsync(),
            ["decontare"] = await db.Set<Decontare>().Where(x => u.Contains(x.MentorId)).ExecuteDeleteAsync(),
            ["camera_tutoring"] = await db.CamereTutoring.Where(x => s.Contains(x.Id)).ExecuteDeleteAsync(),
            ["parcurs"] = await db.Set<Parcurs>().Where(x => p.Contains(x.Id)).ExecuteDeleteAsync(), // chapters, questions, files, progress cascade
            ["organizatie"] = await db.Set<Organizatie>().Where(x => o.Contains(x.Id)).ExecuteDeleteAsync(),
            ["profil_mentor"] = await db.ProfiluriMentor.Where(x => u.Contains(x.UtilizatorId)).ExecuteDeleteAsync(),
            ["utilizator"] = await db.Utilizatori.Where(x => u.Contains(x.Id)).ExecuteDeleteAsync(),
        };
        // The subjects this command adds, when nothing else uses them any more (the ones seed or date-test made stay).
        var coduri = Materii.Select(m => m.Cod).ToList();
        n["materie"] = await db.Materii.Where(m => coduri.Contains(m.Cod)
            && !db.Set<Parcurs>().Any(x => x.MaterieId == m.Id) && !db.Set<Intrebare>().Any(x => x.MaterieId == m.Id) && !db.Curs.Any(x => x.MaterieId == m.Id)
            && !db.Set<MentorMaterie>().Any(x => x.MaterieId == m.Id) && !db.CamereTutoring.Any(x => x.MaterieId == m.Id)
            && !db.Set<EvenimentRecompensa>().Any(x => x.MaterieId == m.Id)).ExecuteDeleteAsync();
        await tx.CommitAsync();
        Console.WriteLine("Volume data removed: " + string.Join(", ", n.Where(x => x.Value > 0).Select(x => $"{x.Value} {x.Key}")) + ".");
    }
}

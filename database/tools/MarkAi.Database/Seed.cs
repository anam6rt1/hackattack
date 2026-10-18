using System.Text.Json;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using MarkAi.Data.Trivia;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Idempotent reference data and starter content: roles, XP levels, the fixed tutoring price, themes,
/// and the Romanian literature course imported from the frontend's JSON files. Safe to run repeatedly.
/// </summary>
internal static class Seed
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    // Island (chapter) names for the level ids used by the frontend map: "harap-1" -> "Harap-Alb".
    private static readonly Dictionary<string, string> Capitole = new()
    {
        ["harap"] = "Harap-Alb", ["moara"] = "Moara cu noroc", ["luceafarul"] = "Luceafărul",
    };

    private static readonly string[] Anotimpuri = ["primavara", "vara", "toamna", "iarna"];
    private static readonly string[] Momente = ["zi", "noapte"];

    internal static async Task Run(string connection, string dataDir)
    {
        await using var db = DatabaseTools.Open(connection);
        await using var tx = await db.Database.BeginTransactionAsync();

        foreach (var rol in new[] { "elev", "profesor", "admin" })
            if (!await db.Roluri.AnyAsync(r => r.Nume == rol)) db.Roluri.Add(new Rol { Nume = rol });

        (int nivel, int xp, string titlu)[] niveluri =
            [(0, 0, "Începător"), (1, 100, "Ucenic"), (2, 300, "Cititor"), (3, 600, "Cunoscător"), (4, 1000, "Critic literar"), (5, 1500, "Maestru al bacului")];
        foreach (var (nivel, xp, titlu) in niveluri)
            if (!await db.NiveluriXp.AnyAsync(n => n.Nivel == nivel))
                db.NiveluriXp.Add(new NivelXp { Nivel = nivel, XpNecesar = xp, Titlu = titlu });

        if (!await db.TarifeFixe.AnyAsync(t => t.Activ))
            db.TarifeFixe.Add(new TarifFix { Suma = 80, Moneda = "RON", DurataMinute = 60, Activ = true });

        var interfata = await db.Interfata.SingleOrDefaultAsync(i => i.Cod == "insule");
        if (interfata is null)
            db.Interfata.Add(interfata = new Interfata { Cod = "insule", Nume = "Insule", Descriere = "Harta cu insule a bacului", Activa = true });
        foreach (var anotimp in Anotimpuri)
            foreach (var moment in Momente)
            {
                var cod = $"{anotimp}-{moment}";
                if (await db.Backgrounds.AnyAsync(b => b.Cod == cod)) continue;
                var fundal = new Background { Cod = cod, Nume = NumeTema(anotimp, moment), AssetUrl = $"tema:{cod}", Activ = true, SelectabilStatic = true };
                db.Backgrounds.Add(fundal);
                db.ReguliBackground.Add(new RegulaBackground { Background = fundal, Interfata = interfata, Anotimp = anotimp, PerioadaZilei = moment, Activa = true });
            }
        await db.SaveChangesAsync();

        await ImportaRomana(db, dataDir);
        if (File.Exists(Path.Combine(dataDir, "continut-suplimentar.json"))) await ImportaSuplimentar(db, dataDir);
        await CreeazaAdmin(db);
        await tx.CommitAsync();
        Console.WriteLine("Seed applied.");
    }

    private static string NumeTema(string anotimp, string moment) =>
        $"{anotimp switch { "primavara" => "Primăvară", "vara" => "Vară", "toamna" => "Toamnă", _ => "Iarnă" }} · {(moment == "zi" ? "zi" : "noapte")}";

    private static async Task ImportaRomana(MarkAiDbContext db, string dataDir)
    {
        var materie = await db.Materii.SingleOrDefaultAsync(m => m.Cod == "romana");
        if (materie is null)
        {
            db.Materii.Add(materie = new Materie
            {
                Cod = "romana", Denumire = "Limba și literatura română", Ordine = 1,
                Descriere = "Operele de bac, de la Harap-Alb la Moromeții.",
            });
            await db.SaveChangesAsync();
        }

        var niveluri = Citeste<Dictionary<string, NivelJson>>(dataDir, "niveluri.json");
        var intrebari = Citeste<Dictionary<string, List<IntrebareJson>>>(dataDir, "intrebari.json");
        var ordine = 0;
        foreach (var (id, nivel) in niveluri)
        {
            ordine++;
            var titlu = nivel.Video?.Titlu ?? nivel.Tema;
            var curs = await db.Curs.SingleOrDefaultAsync(c => c.MaterieId == materie.Id && c.Titlu == titlu);
            if (curs is null)
            {
                db.Curs.Add(curs = new Curs
                {
                    MaterieId = materie.Id, Titlu = titlu, Descriere = nivel.Tema, Ordine = ordine,
                    Capitol = Capitole.GetValueOrDefault(id.Split('-')[0], id),
                    VideoUrl = string.IsNullOrEmpty(nivel.Video?.YoutubeId) ? null : $"https://www.youtube.com/watch?v={nivel.Video.YoutubeId}",
                    Materiale = nivel.Materiale.Select(m => new SectiuneMaterial { Titlu = m.Titlu, Text = m.Text }).ToList(),
                });
                await db.SaveChangesAsync();
            }
            foreach (var q in intrebari.GetValueOrDefault(id) ?? [])
                await AdaugaGrila(db, materie.Id, curs.Id, q.Text, q.Variante, q.Corect, q.Explicatie);
        }

        var triviador = Citeste<TriviadorJson>(dataDir, "intrebari-triviador.json");
        foreach (var q in triviador.Grila) await AdaugaGrila(db, materie.Id, null, q.Text, q.Variante, q.Corect, null);
        foreach (var q in triviador.Estimari)
            if (!await db.Set<TriviaQuestion>().AnyAsync(x => x.SubjectId == materie.Id && x.Text == q.Text))
                db.Add(new TriviaQuestion { SubjectId = materie.Id, Text = q.Text, Kind = "estimare", NumericAnswer = q.Raspuns });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Additional subjects, lessons and questions. Existing subjects are matched by code or name and existing
    /// lessons by title; content already in the database (possibly edited by a teacher) is never overwritten,
    /// only missing note sections are appended.
    /// </summary>
    private static async Task ImportaSuplimentar(MarkAiDbContext db, string dataDir)
    {
        var continut = Citeste<SuplimentarJson>(dataDir, "continut-suplimentar.json");
        foreach (var mj in continut.Materii)
        {
            var denumire = mj.Denumire.ToLower();
            var materie = await db.Materii.FirstOrDefaultAsync(m => m.Cod == mj.Cod || m.Denumire.ToLower() == denumire);
            if (materie is null)
            {
                db.Materii.Add(materie = new Materie { Cod = mj.Cod, Denumire = mj.Denumire, Ordine = await db.Materii.CountAsync() + 1 });
                await db.SaveChangesAsync();
            }

            foreach (var cj in mj.Cursuri)
            {
                var curs = await db.Curs.SingleOrDefaultAsync(c => c.MaterieId == materie.Id && c.Titlu == cj.Titlu);
                if (curs is null)
                {
                    // "dupa" keeps a new lesson next to its island's existing lessons in the unlock order.
                    var vecin = cj.Dupa is null ? null : await db.Curs.FirstOrDefaultAsync(c => c.MaterieId == materie.Id && c.Titlu == cj.Dupa);
                    var ordine = vecin?.Ordine ?? (await db.Curs.Where(c => c.MaterieId == materie.Id).MaxAsync(c => (int?)c.Ordine) ?? 0) + 1;
                    db.Curs.Add(curs = new Curs
                    {
                        MaterieId = materie.Id, Titlu = cj.Titlu, Descriere = cj.Descriere, Capitol = cj.Capitol, Ordine = ordine,
                        Materiale = cj.Materiale.Select(m => new SectiuneMaterial { Titlu = m.Titlu, Text = m.Text }).ToList(),
                    });
                    await db.SaveChangesAsync();
                }
                foreach (var q in cj.Intrebari) await AdaugaGrila(db, materie.Id, curs.Id, q.Text, q.Variante, q.Corect, q.Explicatie);
            }

            foreach (var completare in mj.Completari ?? [])
            {
                var curs = await db.Curs.SingleOrDefaultAsync(c => c.MaterieId == materie.Id && c.Titlu == completare.Titlu);
                if (curs is null) continue;
                var lipsa = completare.Materiale.Where(m => curs.Materiale.All(x => x.Titlu != m.Titlu)).ToList();
                if (lipsa.Count == 0) continue;
                // Reassign the list so EF sees the JSON column as changed.
                curs.Materiale = [.. curs.Materiale, .. lipsa.Select(m => new SectiuneMaterial { Titlu = m.Titlu, Text = m.Text })];
                await db.SaveChangesAsync();
            }

            foreach (var e in mj.Estimari ?? [])
                if (!await db.Set<TriviaQuestion>().AnyAsync(x => x.SubjectId == materie.Id && x.Text == e.Text))
                    db.Add(new TriviaQuestion { SubjectId = materie.Id, Text = e.Text, Kind = "estimare", NumericAnswer = e.Raspuns });
            await db.SaveChangesAsync();
        }
    }

    private static async Task AdaugaGrila(MarkAiDbContext db, int materieId, int? cursId, string text, List<string> variante, int corect, string? explicatie)
    {
        if (await db.Set<TriviaQuestion>().AnyAsync(x => x.SubjectId == materieId && x.CourseId == cursId && x.Text == text)) return;
        var intrebare = new TriviaQuestion { SubjectId = materieId, CourseId = cursId, Text = text, Explanation = explicatie };
        db.Add(intrebare);
        await db.SaveChangesAsync();
        db.AddRange(variante.Select((v, i) => new TriviaOption { QuestionId = intrebare.Id, Text = v, IsCorrect = i == corect }));
        await db.SaveChangesAsync();
    }

    private static async Task CreeazaAdmin(MarkAiDbContext db)
    {
        var email = Environment.GetEnvironmentVariable("MARKAI_ADMIN_EMAIL");
        var parola = Environment.GetEnvironmentVariable("MARKAI_ADMIN_PAROLA");
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(parola))
        {
            Console.WriteLine("MARKAI_ADMIN_EMAIL/MARKAI_ADMIN_PAROLA not set; no admin account created.");
            return;
        }
        if (await db.Utilizatori.AnyAsync(u => u.Email.ToLower() == email.ToLower())) return;
        var admin = new Utilizator { Nume = "Administrator", Username = "admin", Email = email };
        admin.ParolaHash = Parole.Hash(admin, parola);
        admin.Roluri.Add(new UtilizatorRol { Rol = await db.Roluri.SingleAsync(r => r.Nume == "admin") });
        db.Utilizatori.Add(admin);
        await db.SaveChangesAsync();
        Console.WriteLine($"Admin account created for {email}.");
    }

    private static T Citeste<T>(string dataDir, string fisier) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(dataDir, fisier)), Json)
        ?? throw new InvalidOperationException($"{fisier} is empty.");

    private sealed record NivelJson(string Tema, VideoJson? Video, List<MaterialJson> Materiale);
    private sealed record VideoJson(string? YoutubeId, string? Titlu);
    private sealed record MaterialJson(string Titlu, string Text);
    private sealed record IntrebareJson(string Text, List<string> Variante, int Corect, string? Explicatie);
    private sealed record TriviadorJson(List<GrilaJson> Grila, List<EstimareJson> Estimari);
    private sealed record GrilaJson(string Text, List<string> Variante, int Corect);
    private sealed record EstimareJson(string Text, int Raspuns);
    private sealed record SuplimentarJson(List<MaterieJson> Materii);
    private sealed record MaterieJson(string Cod, string Denumire, List<CursJson> Cursuri, List<CompletareJson>? Completari, List<EstimareJson>? Estimari);
    private sealed record CursJson(string Titlu, string? Capitol, string? Descriere, string? Dupa, List<MaterialJson> Materiale, List<IntrebareJson> Intrebari);
    private sealed record CompletareJson(string Titlu, List<MaterialJson> Materiale);
}

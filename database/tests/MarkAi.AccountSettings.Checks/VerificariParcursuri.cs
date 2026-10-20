using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MarkAi.Api.Endpoints;
using MarkAi.Api.Servicii;
using MarkAi.Api.Servicii.Triviador;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using MarkAi.Data.Trivia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

/// <summary>
/// Lecții (paths) and chapters: ownership (author, home organisation admin, platform admin), visibility on every read path
/// (including the phone tutor), view-only shares, the organisation's publishing switch, grades, chapter order, hard deletion,
/// the Triviador grade filter, and the migration that groups existing chapters into platform Lecții.
/// </summary>
static class VerificariParcursuri
{
    public static readonly Dictionary<string, string?> Setari = new()
    {
        ["Limite:AutentificariPeMinut"] = "200", ["Limite:PauzaQuizSecunde"] = "0",
    };

    public static async Task Ruleaza(Harness h)
    {
        await VerificaMigrarea(h);
        var (liceu, centru) = await Populeaza(h);

        using var admin = await h.Login("admin");
        using var irina = await h.Login("irina");     // professor in Liceul de Test and Centrul Alba
        using var petra = await h.Login("petra");     // another professor in Liceul de Test
        using var director = await h.Login("director"); // admin of Liceul de Test
        using var ovidiu = await h.Login("ovidiu");   // admin of Centrul Alba
        using var george = await h.Login("george");   // student in no organisation
        using var elena = await h.Login("elena");     // student in Liceul de Test
        using var radu = await h.Login("radu");       // student in Centrul Alba

        // Subjects are the platform's.
        h.Check((await irina.PostAsJsonAsync("/materii", new { denumire = "Filosofie" })).StatusCode == HttpStatusCode.Forbidden, "professors cannot create subjects");
        h.Check((await admin.PostAsJsonAsync("/materii", new { denumire = "Filosofie" })).StatusCode == HttpStatusCode.Created, "platform admins create subjects");

        // Creating a Lecție: a home organisation of the author, a subject they teach, a valid grade.
        h.Check((await irina.PostAsJsonAsync("/parcursuri", Lectie("Fără organizație", null))).StatusCode == HttpStatusCode.BadRequest, "a professor must pick a home organisation");
        h.Check((await petra.PostAsJsonAsync("/parcursuri", Lectie("Alt centru", centru))).StatusCode == HttpStatusCode.BadRequest, "only an organisation the author belongs to");
        h.Check((await irina.PostAsJsonAsync("/parcursuri", Lectie("Mate", liceu, materieId: 2))).StatusCode == HttpStatusCode.BadRequest, "only a subject the professor teaches");
        h.Check((await irina.PostAsJsonAsync("/parcursuri", Lectie("Clasă greșită", liceu, clasa: "VIII"))).StatusCode == HttpStatusCode.BadRequest, "grade must be IX–XII or Bac");
        h.Check((await admin.PostAsJsonAsync("/parcursuri", Lectie("Doar org fără org", null, vizibilitate: "organizatie"))).StatusCode == HttpStatusCode.BadRequest,
            "organisation-only needs an organisation");
        var platforma = await Id(await admin.PostAsJsonAsync("/parcursuri", Lectie("Platformă Bac", null)));
        await Capitol(admin, platforma, "Capitol platformă");

        // An organisation-only Lecție of Irina's, home Liceul de Test, grade XI, with two chapters.
        var privata = await Id(await irina.PostAsJsonAsync("/parcursuri", Lectie("Ion — organizație", liceu, clasa: "XI", vizibilitate: "organizatie")));
        var cap1 = await Capitol(irina, privata, "Ion: capitolul 1", "Glasul pământului și satul lui Ion");
        var cap2 = await Capitol(irina, privata, "Ion: capitolul 2", "Glasul iubirii și finalul romanului");
        await IntrebariDeTest(h, cap2);

        // Visibility on every read path.
        h.Check((await george.GetAsync($"/parcursuri/{privata}")).StatusCode == HttpStatusCode.NotFound, "outsider: organisation-only Lecție is 404");
        h.Check((await george.GetAsync($"/cursuri/{cap1}")).StatusCode == HttpStatusCode.NotFound, "outsider: its chapter is 404");
        h.Check((await george.PostAsync($"/cursuri/{cap1}/quiz", null)).StatusCode == HttpStatusCode.NotFound, "outsider: its quiz is 404");
        h.Check((await george.PostAsJsonAsync($"/cursuri/{cap1}/progres", new { activitate = "video" })).StatusCode == HttpStatusCode.NotFound, "outsider: no progress on it");
        h.Check(!(await Lista(george, "/materii/1/parcursuri")).Contains(privata) && (await Lista(george, "/materii/1/parcursuri")).Contains(platforma),
            "outsider: listed Lecții are only public ones");
        h.Check(await NumarCursuri(george, 1) == 1 && await NumarCursuri(elena, 1) == 3, "subject counts follow visibility (1 for an outsider, 3 for a member)");
        h.Check(await NumarCursuri(h.Anonim(), 1) == 1, "anonymous counts show only public content");
        h.Check((await elena.GetAsync($"/parcursuri/{privata}")).IsSuccessStatusCode && (await elena.GetAsync($"/cursuri/{cap2}")).IsSuccessStatusCode,
            "a student of the home organisation sees it");
        h.Check((await radu.GetAsync($"/parcursuri/{privata}")).StatusCode == HttpStatusCode.NotFound, "a student of another organisation does not");

        // Chapters open in any order: the second one first.
        h.Check((await elena.PostAsJsonAsync($"/cursuri/{cap2}/progres", new { activitate = "video" })).IsSuccessStatusCode, "chapter 2 can be started before chapter 1");
        h.Check((await elena.PostAsync($"/cursuri/{cap2}/quiz", null)).IsSuccessStatusCode, "its quiz can be taken before chapter 1");

        // Progress page: progress on a chapter the student can no longer see is hidden.
        var georgeId = await h.Db(db => db.Utilizatori.Where(u => u.Username == "george").Select(u => u.Id).SingleAsync());
        await h.Db(async db =>
        {
            db.Add(new ProgresCurs { UtilizatorId = georgeId, CursId = cap1, QuizPromovat = true, CompletatLa = DateTime.UtcNow, UltimaAccesare = DateTime.UtcNow });
            return await db.SaveChangesAsync();
        });
        var progres = await george.GetFromJsonAsync<JsonElement>("/progres");
        h.Check(progres.GetProperty("cursuriCompletate").EnumerateArray().All(c => c.GetProperty("cursId").GetInt32() != cap1)
            && progres.GetProperty("ultimulCurs").ValueKind == JsonValueKind.Null
            && progres.GetProperty("materii").EnumerateArray().Single(m => m.GetProperty("materieId").GetInt32() == 1).GetProperty("totalCursuri").GetInt32() == 1,
            "/progres hides chapters of organisations the student is not in");

        // The phone tutor: an outsider's prompt and speech hints never contain the organisation's chapters.
        await VerificaTutorul(h, georgeId, "george", cap1, vede: false);
        await VerificaTutorul(h, await h.Db(db => db.Utilizatori.Where(u => u.Username == "elena").Select(u => u.Id).SingleAsync()), "elena", cap1, vede: true);

        // Editing rights.
        var editare = Lectie("Ion — organizație (editat)", liceu, clasa: "XI", vizibilitate: "organizatie");
        h.Check((await petra.PutAsJsonAsync($"/parcursuri/{privata}", editare)).StatusCode == HttpStatusCode.Forbidden, "another professor cannot edit the Lecție (403)");
        h.Check((await petra.PostAsJsonAsync("/cursuri", new { parcursId = privata, titlu = "Intrus" })).StatusCode == HttpStatusCode.Forbidden, "nor add chapters to it");
        h.Check((await petra.PutAsJsonAsync($"/cursuri/{cap1}", new { parcursId = privata, titlu = "Intrus" })).StatusCode == HttpStatusCode.Forbidden, "nor edit its chapters");
        h.Check((await petra.DeleteAsync($"/cursuri/{cap1}")).StatusCode == HttpStatusCode.Forbidden, "nor delete them");
        h.Check((await petra.GetAsync($"/continut/parcursuri/{privata}")).StatusCode == HttpStatusCode.Forbidden, "nor open it in the editor");
        h.Check((await director.PutAsJsonAsync($"/parcursuri/{privata}", editare)).IsSuccessStatusCode, "the home organisation's admin can edit it");
        h.Check((await Lista(director, "/continut/parcursuri")).Contains(privata) && !(await Lista(petra, "/continut/parcursuri")).Contains(privata),
            "editor lists show only what each one may edit");
        h.Check((await director.PutAsJsonAsync($"/parcursuri/{privata}", Lectie("Mutată", centru, clasa: "XI"))).StatusCode == HttpStatusCode.BadRequest,
            "only the author moves a Lecție to another organisation");
        h.Check((await petra.PutAsJsonAsync($"/parcursuri/{platforma}", Lectie("x", liceu))).StatusCode == HttpStatusCode.Forbidden, "professors cannot edit platform Lecții");

        // View-only shares, to the author's other organisations only.
        h.Check((await irina.PutAsJsonAsync($"/parcursuri/{privata}", Lectie("Ion — organizație", liceu, clasa: "XI", vizibilitate: "organizatie", partajari: [centru])))
            .IsSuccessStatusCode, "the author shares it with her other organisation");
        h.Check((await radu.GetAsync($"/parcursuri/{privata}")).IsSuccessStatusCode && (await radu.GetAsync($"/cursuri/{cap1}")).IsSuccessStatusCode,
            "students of the shared organisation now see it");
        h.Check((await ovidiu.PutAsJsonAsync($"/parcursuri/{privata}", editare)).StatusCode == HttpStatusCode.Forbidden, "the shared organisation's admin cannot edit it");
        var petraPublica = await Id(await petra.PostAsJsonAsync("/parcursuri", Lectie("Lecția Petrei", liceu)));
        h.Check((await petra.PutAsJsonAsync($"/parcursuri/{petraPublica}", Lectie("Lecția Petrei", liceu, partajari: [centru]))).StatusCode == HttpStatusCode.BadRequest,
            "sharing only with organisations the author is in");

        // The publishing switch: off for Irina in Liceul de Test → her Lecții there become organisation-only.
        var publica = await Id(await irina.PostAsJsonAsync("/parcursuri", Lectie("Luceafărul pentru toți", liceu, clasa: "XII")));
        var capPublic = await Capitol(irina, publica, "Luceafărul: capitolul 1");
        var inCentru = await Id(await irina.PostAsJsonAsync("/parcursuri", Lectie("Lecție în Centrul Alba", centru, clasa: "XII")));
        h.Check((await george.GetAsync($"/cursuri/{capPublic}")).IsSuccessStatusCode, "a public Lecție is open to everyone");
        var irinaId = await h.Db(db => db.Utilizatori.Where(u => u.Username == "irina").Select(u => u.Id).SingleAsync());
        h.Check((await director.PutAsJsonAsync($"/organizatia-mea/membri/{irinaId}/publicare", new { poatePublicaPublic = false })).IsSuccessStatusCode,
            "the organisation admin switches public publishing off for Irina");
        h.Check((await george.GetAsync($"/cursuri/{capPublic}")).StatusCode == HttpStatusCode.NotFound
            && (await Lectia(irina, publica)).GetProperty("vizibilitate").GetString() == "organizatie", "her Lecție there is now organisation-only");
        h.Check((await irina.PutAsJsonAsync($"/parcursuri/{publica}", Lectie("Luceafărul pentru toți", liceu, clasa: "XII"))).StatusCode == HttpStatusCode.Conflict,
            "it cannot be made public again there");
        h.Check((await irina.PostAsJsonAsync("/parcursuri", Lectie("Nouă publică", liceu))).StatusCode == HttpStatusCode.Conflict, "nor can a new public one be created there");
        h.Check((await Lectia(irina, publica)).GetProperty("poatePublicaPublic").GetBoolean() == false, "the editor is told why (public greyed out)");
        h.Check((await Lectia(irina, inCentru)).GetProperty("vizibilitate").GetString() == "public"
            && (await irina.PostAsJsonAsync("/parcursuri", Lectie("Altă publică în Centru", centru))).StatusCode == HttpStatusCode.Created,
            "her Lecții in her other organisation are unaffected");
        h.Check((await Lectia(petra, petraPublica)).GetProperty("vizibilitate").GetString() == "public", "other professors of the organisation are unaffected");

        // Grade filter.
        var xi = await Lista(elena, "/materii/1/parcursuri?clasa=XI");
        var xiiBac = await Lista(elena, "/materii/1/parcursuri?clasa=XII,Bac");
        h.Check(xi.Contains(privata) && !xi.Contains(platforma) && xiiBac.Contains(platforma) && xiiBac.Contains(publica) && !xiiBac.Contains(privata),
            "grade filter (one grade, or several)");
        h.Check((await elena.GetAsync("/materii/1/parcursuri?clasa=XIII")).StatusCode == HttpStatusCode.BadRequest, "an unknown grade is rejected");
        h.Check((await Lista(elena, "/materii/1/parcursuri?filtru=organizatia-mea")).All(id => id != platforma)
            && (await Lista(elena, "/materii/1/parcursuri?filtru=public")).All(id => id != privata), "organisation/public filters");

        // Drafts: an unpublished Lecție is visible to its editors only.
        h.Check((await irina.PutAsJsonAsync($"/parcursuri/{inCentru}", Lectie("Lecție în Centrul Alba", centru, clasa: "XII", publicat: false))).IsSuccessStatusCode
            && (await radu.GetAsync($"/parcursuri/{inCentru}")).StatusCode == HttpStatusCode.NotFound && (await irina.GetAsync($"/parcursuri/{inCentru}")).IsSuccessStatusCode,
            "an unpublished Lecție is visible only to its editors");

        // Recommended order.
        var cap3 = await Capitol(irina, privata, "Ion: capitolul 3");
        h.Check((await irina.PutAsJsonAsync($"/parcursuri/{privata}/ordine", new { cursIds = new[] { cap3, cap1 } })).StatusCode == HttpStatusCode.BadRequest,
            "reordering needs every chapter");
        h.Check((await irina.PutAsJsonAsync($"/parcursuri/{privata}/ordine", new { cursIds = new[] { cap3, cap1, cap2 } })).StatusCode == HttpStatusCode.NoContent
            && (await Lectia(elena, privata, editor: false)).GetProperty("capitole").EnumerateArray().Select(c => c.GetProperty("id").GetInt32()).SequenceEqual([cap3, cap1, cap2]),
            "chapters are reordered");
        h.Check((await irina.PutAsJsonAsync($"/cursuri/{cap3}", new { parcursId = platforma, titlu = "Mutat" })).StatusCode == HttpStatusCode.Forbidden,
            "a chapter cannot move into a Lecție the user may not edit");

        // Triviador: the grade filter picks the islands (visibility is ignored on purpose).
        var motor = h.Serviciu<MotorTriviador>();
        var cod = await motor.CreeazaAsync(georgeId, new SetariCamera([1], 9, 2, "XI"), CancellationToken.None);
        var camera = motor.Descriere(cod)!.Value;
        h.Check(camera.Insule.Contains("Ion — organizație") && !camera.Insule.Contains("Platformă Bac") && !camera.Insule.Contains("Luceafărul pentru toți")
            && camera.Materie.EndsWith("clasa a XI-a"), "Triviador with grade XI uses only grade XI Lecții");
        h.Check(await Esueaza(() => motor.CreeazaAsync(georgeId, new SetariCamera([1], 9, 2, "XIII"), CancellationToken.None)), "Triviador rejects an unknown grade");

        await VerificaStergerea(h, irina, elena, privata, cap1, cap2, cap3);

        // Deleting the author's account: the Lecție stays, the home organisation's admin still edits it.
        var petraId = await h.Db(db => db.Utilizatori.Where(u => u.Username == "petra").Select(u => u.Id).SingleAsync());
        await using (var scope = h.App.Services.CreateAsyncScope())
            h.Check(await scope.ServiceProvider.GetRequiredService<StergereCont>().StergeAsync(petraId, null, CancellationToken.None), "the author's account is deleted");
        h.Check(await h.Db(db => db.Set<Parcurs>().AnyAsync(p => p.Id == petraPublica && p.AutorId == null)), "a deleted author's Lecție stays, without an author");
        h.Check((await director.PutAsJsonAsync($"/parcursuri/{petraPublica}", Lectie("Lecția Petrei", liceu))).IsSuccessStatusCode,
            "the home organisation's admin keeps editing it");
    }

    // Two valid multiple-choice questions on a chapter, so its quiz can start.
    static Task<int> IntrebariDeTest(Harness h, int cursId) => h.Db(async db =>
    {
        foreach (var text in new[] { "Cine e Ana?", "Cine e Florica?" })
        {
            var q = new TriviaQuestion { SubjectId = 1, CourseId = cursId, Text = text };
            db.Add(q);
            await db.SaveChangesAsync();
            db.AddRange(new TriviaOption { QuestionId = q.Id, Text = "Răspunsul bun", IsCorrect = true }, new TriviaOption { QuestionId = q.Id, Text = "Alt răspuns" });
        }
        return await db.SaveChangesAsync();
    });

    static async Task VerificaStergerea(Harness h, HttpClient irina, HttpClient elena, int privata, int cap1, int cap2, int cap3)
    {
        var elenaId = await h.Db(db => db.Utilizatori.Where(u => u.Username == "elena").Select(u => u.Id).SingleAsync());
        var apel = await h.Db(async db =>
        {
            var s = new Sesiune { UtilizatorId = elenaId, CursId = cap2, Canal = LiniaAiEndpoints.CanalTelefon, Status = "finalizat", InceputLa = DateTime.UtcNow };
            db.Add(s);
            await db.SaveChangesAsync();
            return s.Id;
        });
        await using (var scope = h.App.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ProgresService>().MarcheazaAsync(elenaId, cap2, ActivitateCurs.Quiz);
        var xpInainte = await h.Db(db => db.EvenimenteRecompensa.Where(e => e.UtilizatorId == elenaId).SumAsync(e => e.XpAcordat));

        h.Check((await irina.DeleteAsync($"/cursuri/{cap2}")).StatusCode == HttpStatusCode.NoContent, "the author deletes a chapter with student activity");
        var ramase = await h.Db(async db => new
        {
            Curs = await db.Curs.CountAsync(c => c.Id == cap2),
            Progres = await db.Set<ProgresCurs>().CountAsync(p => p.CursId == cap2),
            Incercari = await db.Set<IncercareQuiz>().CountAsync(i => i.CursId == cap2),
            Intrebari = await db.Set<TriviaQuestion>().CountAsync(q => q.CourseId == cap2),
            Variante = await db.Set<TriviaOption>().CountAsync(o => db.Set<TriviaQuestion>().Any(q => q.Id == o.QuestionId && q.CourseId == cap2)),
            Sesiune = await db.Sesiuni.Where(s => s.Id == apel).Select(s => new { s.CursId }).SingleOrDefaultAsync(),
            Xp = await db.EvenimenteRecompensa.Where(e => e.UtilizatorId == elenaId).SumAsync(e => e.XpAcordat),
        });
        h.Check(ramase.Curs == 0 && ramase.Progres == 0 && ramase.Incercari == 0 && ramase.Intrebari == 0 && ramase.Variante == 0,
            "the chapter, its progress, attempts, questions and options are gone");
        h.Check(ramase.Sesiune is { CursId: null }, "a tutor call about it stays, without the link");
        h.Check(xpInainte >= ProgresService.XpCurs && ramase.Xp == xpInainte, "XP already awarded stays");

        h.Check((await irina.DeleteAsync($"/parcursuri/{privata}")).StatusCode == HttpStatusCode.NoContent, "the author deletes the whole Lecție");
        h.Check(await h.Db(async db => !await db.Curs.AnyAsync(c => c.Id == cap1 || c.Id == cap3) && !await db.Set<ParcursOrganizatie>().AnyAsync(s => s.ParcursId == privata)
            && !await db.Set<Parcurs>().AnyAsync(p => p.Id == privata)), "its chapters and shares are gone");
        h.Check((await elena.GetAsync($"/parcursuri/{privata}")).StatusCode == HttpStatusCode.NotFound, "a deleted Lecție is 404");
    }

    static async Task VerificaTutorul(Harness h, int elevId, string username, int cursId, bool vede)
    {
        var titlu = await h.Db(db => db.Curs.Where(c => c.Id == cursId).Select(c => c.Titlu).SingleAsync());
        await using var scope = h.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MarkAiDbContext>();
        var tutor = scope.ServiceProvider.GetRequiredService<TutorTelefon>();
        var sesiune = new Sesiune { UtilizatorId = elevId, Canal = LiniaAiEndpoints.CanalTelefon, Status = LiniaAiEndpoints.StareInCurs, InceputLa = DateTime.UtcNow };
        db.Add(sesiune);
        await db.SaveChangesAsync();
        db.Add(new Mesaj { SesiuneId = sesiune.Id, Expeditor = TutorTelefon.ExpeditorElev, Tip = TutorTelefon.TipVoce, Continut = "Vreau să vorbim despre Ion, glasul pământului.", CreatLa = DateTime.UtcNow });
        await db.SaveChangesAsync();
        h.Ai.Raspunde = (_, _, _) => """{"raspuns":"Bine.","final":false,"voce":{"voce":"ioana","viteza":100,"ton":0}}""";
        while (h.Ai.Apeluri.TryDequeue(out _)) { }
        await tutor.RaspundeAsync(sesiune, VociTutor.Implicit, ultimaReplica: false, CancellationToken.None);
        var sistem = h.Ai.Apeluri.Last().Sistem;
        h.Check(sistem.Contains(titlu) == vede, vede ? $"{username}: the tutor uses the organisation's chapter" : $"{username}: the tutor never sees the organisation's chapter");
        h.Check((await tutor.IndiciiVorbire(elevId, CancellationToken.None)).Contains("Ion — organizație") == vede, $"{username}: speech hints follow visibility");
    }

    // A database at the Organizatii migration with hackathon-style chapters, then migrated: chapters grouped per subject and label.
    static async Task VerificaMigrarea(Harness h)
    {
        var adminConexiune = Environment.GetEnvironmentVariable("MARKAI_TEST_CONNECTION")!;
        var nume = "markai_checks_migrare_" + Guid.NewGuid().ToString("N");
        await using (var c = new NpgsqlConnection(adminConexiune)) { await c.OpenAsync(); await new NpgsqlCommand($"CREATE DATABASE \"{nume}\"", c).ExecuteNonQueryAsync(); }
        try
        {
            var conexiune = new NpgsqlConnectionStringBuilder(adminConexiune) { Database = nume }.ConnectionString;
            await using var db = new MarkAiDbContext(new DbContextOptionsBuilder<MarkAiDbContext>()
                .UseNpgsql(conexiune, x => x.MigrationsHistoryTable("__EFMigrationsHistory", "mark_ai")).Options);
            await db.GetService<IMigrator>().MigrateAsync("20261020020614_Organizatii");
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO mark_ai.rol (nume) VALUES ('admin') ON CONFLICT DO NOTHING;
                INSERT INTO mark_ai.utilizator (nume, username, email, parola_hash) VALUES ('Admin', 'admin', 'admin@example.invalid', 'x');
                INSERT INTO mark_ai.utilizator_rol (utilizator_id, rol_id) SELECT u.id, r.id FROM mark_ai.utilizator u, mark_ai.rol r WHERE r.nume = 'admin';
                INSERT INTO mark_ai.materie (denumire) VALUES ('Română'), ('Matematică');
                INSERT INTO mark_ai.curs (materie_id, titlu, capitol, ordine, publicat) VALUES
                    (1, 'Harap-Alb 1', 'Harap-Alb', 1, true), (1, 'Harap-Alb 2', 'Harap-Alb', 2, true), (1, 'Moara 1', 'Moara cu noroc', 3, true),
                    (1, 'Fără capitol', NULL, 4, false), (2, 'Funcții 1', 'Funcții', 1, true), (2, 'Harap-Alb la mate', 'Harap-Alb', 2, true);
                """);
            await db.Database.MigrateAsync();
            var parcursuri = await db.Set<Parcurs>().AsNoTracking().Include(p => p.Capitole).ToListAsync();
            var adminId = await db.Utilizatori.Where(u => u.Username == "admin").Select(u => u.Id).SingleAsync();
            h.Check(await db.Curs.CountAsync() == 6 && parcursuri.Sum(p => p.Capitole.Count) == 6, "migration keeps every chapter, each in a Lecție");
            h.Check(parcursuri.Count == 5 && parcursuri.Single(p => p.MaterieId == 1 && p.Titlu == "Harap-Alb").Capitole.Count == 2
                && parcursuri.Any(p => p.MaterieId == 2 && p.Titlu == "Harap-Alb") && parcursuri.Any(p => p.MaterieId == 1 && p.Titlu == "Lecții"),
                "one platform Lecție per subject and chapter label");
            h.Check(parcursuri.All(p => p.Clasa == "Bac" && p.Vizibilitate == Parcurs.Public && p.OrganizatieId == null && p.AutorId == adminId && p.Publicat),
                "migrated Lecții are public platform content, grade Bac, by the platform admin");
            h.Check(parcursuri.All(p => p.Capitole.All(c => c.MaterieId == p.MaterieId)), "no chapter is in another subject's Lecție");
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var c = new NpgsqlConnection(adminConexiune);
            await c.OpenAsync();
            await new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{nume}\" WITH (FORCE)", c).ExecuteNonQueryAsync();
        }
    }

    // Fixtures on top of the harness ones: a second organisation (Centrul Alba, admin ovidiu) where irina also teaches,
    // professor petra in Liceul de Test, students elena (Liceul de Test) and radu (Centrul Alba).
    static async Task<(int Liceu, int Centru)> Populeaza(Harness h) => await h.Db(async db =>
    {
        var roluri = await db.Roluri.ToDictionaryAsync(r => r.Nume, r => r.Id);
        Utilizator Cont(string username, string nume, params string[] rol)
        {
            var u = new Utilizator { Nume = nume, Username = username, Email = username + "@example.invalid", EmailConfirmatLa = DateTime.UtcNow };
            u.ParolaHash = Parole.Hash(u, Harness.ParolaTest);
            foreach (var r in rol) u.Roluri.Add(new UtilizatorRol { RolId = roluri[r] });
            if (rol.Contains("profesor"))
                u.ProfilMentor = new ProfilMentor { StareVerificare = "aprobat", ActivatLa = DateTime.UtcNow, Descriere = "Profesor de test.",
                    Materii = new List<MentorMaterie> { new() { MaterieId = 1 } } };
            db.Add(u);
            return u;
        }
        var ovidiu = Cont("ovidiu", "Ovidiu Organizator", "organizatie", "profesor");
        var petra = Cont("petra", "Petra Pop", "profesor");
        var elena = Cont("elena", "Elena Elev", "elev");
        var radu = Cont("radu", "Radu Elev", "elev");
        await db.SaveChangesAsync();
        var centru = new Organizatie { Nume = "Centrul Alba", Adresa = "Str. Unirii 2", JudetId = 1, Stare = Organizatie.Aprobata, AdminId = ovidiu.Id, CreatLa = DateTime.UtcNow };
        db.Add(centru);
        await db.SaveChangesAsync();
        var irina = await db.Utilizatori.SingleAsync(u => u.Username == "irina");
        MembruOrganizatie Membru(int org, int uid, string rol) => new() { OrganizatieId = org, UtilizatorId = uid, Rol = rol, Stare = MembruOrganizatie.Aprobat, CerutLa = DateTime.UtcNow };
        db.AddRange(Membru(centru.Id, irina.Id, MembruOrganizatie.RolProfesor), Membru(centru.Id, ovidiu.Id, MembruOrganizatie.RolProfesor),
            Membru(Harness.IdLiceu, petra.Id, MembruOrganizatie.RolProfesor), Membru(Harness.IdLiceu, elena.Id, MembruOrganizatie.RolElev),
            Membru(centru.Id, radu.Id, MembruOrganizatie.RolElev));
        await db.SaveChangesAsync();
        return (Harness.IdLiceu, centru.Id);
    });

    static object Lectie(string titlu, int? organizatieId, string clasa = "Bac", string vizibilitate = "public", int materieId = 1, int[]? partajari = null, bool publicat = true) =>
        new { materieId, titlu, clasa, vizibilitate, organizatieId, partajari = partajari ?? [], publicat };

    static async Task<int> Id(HttpResponseMessage r)
    {
        if (!r.IsSuccessStatusCode) throw new Exception($"FAIL: request returned {(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}");
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    static async Task<int> Capitol(HttpClient editor, int parcursId, string titlu, string text = "Material de test.") =>
        await Id(await editor.PostAsJsonAsync("/cursuri", new { parcursId, titlu, materiale = new[] { new { titlu = "Notițe", text } } }));

    static async Task<List<int>> Lista(HttpClient c, string url) =>
        (await c.GetFromJsonAsync<JsonElement>(url)).GetProperty("elemente").EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).ToList();

    static async Task<int> NumarCursuri(HttpClient c, int materieId) =>
        (await c.GetFromJsonAsync<JsonElement>("/materii")).GetProperty("elemente").EnumerateArray()
            .Single(m => m.GetProperty("id").GetInt32() == materieId).GetProperty("numarCursuri").GetInt32();

    static async Task<JsonElement> Lectia(HttpClient c, int id, bool editor = true) =>
        await c.GetFromJsonAsync<JsonElement>(editor ? $"/continut/parcursuri/{id}" : $"/parcursuri/{id}");

    static async Task<bool> Esueaza(Func<Task> f)
    {
        try { await f(); return false; } catch (MarkAi.Api.Infrastructura.CerereInvalida) { return true; }
    }
}

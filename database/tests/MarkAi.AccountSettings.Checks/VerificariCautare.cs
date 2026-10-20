using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Search and filters: diacritic-insensitive search (comma and cedilla ș/ț), visibility of search results, the "local"
/// filter for Lecții and professors (county, own locality first), "my organisation", grades; the professor's teaching place
/// (own or an organisation, falling back when they leave it); online / in-person / hybrid sessions, their place, the
/// note only booked students see, the type filter and how a student attends a hybrid session.
/// </summary>
static class VerificariCautare
{
    public static readonly Dictionary<string, string?> Setari = new()
    {
        ["Limite:AutentificariPeMinut"] = "200", ["Limite:CereriPeMinut"] = "2000",
    };

    const int Bucuresti = 40, Sector1 = 179141, Sector2 = 179150, Alba = 1, AlbaIulia = 1026, Barabant = 1035;

    public static async Task Ruleaza(Harness h)
    {
        var (centru, barabant) = await Populeaza(h);
        using var admin = await h.Login("admin");
        using var irina = await h.Login("irina");   // professor in Liceul de Test (Sectorul 1, București)
        using var ovidiu = await h.Login("ovidiu"); // admin of Centrul Alba (Alba Iulia), professor at Școala Bărăbanț
        using var george = await h.Login("george"); // student, no organisation, no place set
        using var elena = await h.Login("elena");   // student in Liceul de Test, lives in Sectorul 2
        using var radu = await h.Login("radu");     // student in Centrul Alba, lives in Alba Iulia

        // Search ignores diacritics: comma-below and cedilla letters alike, upper case, "-" as a space.
        var sarpe = await Id(await admin.PostAsJsonAsync("/parcursuri", Lectie("Șarpele și ţara", null, descriere: "Basme cu zmei")));
        var capSarpe = await Capitol(admin, sarpe, "Capitol cu Ştefan", "Harap-Alb");
        h.Check((await Cauta(george, "sarpe")).Lectii.Contains(sarpe), "\"sarpe\" finds \"Șarpele\"");
        h.Check((await Cauta(george, "SARPELE TARA")).Lectii.Contains(sarpe), "upper case and the cedilla ţ (\"tara\") match too");
        h.Check((await Cauta(george, "stefan")).Capitole.Contains(capSarpe), "a chapter is found by its title (cedilla Ş)");
        h.Check((await Cauta(george, "harap alb")).Capitole.Contains(capSarpe) && (await Cauta(george, "harap-alb")).Capitole.Contains(capSarpe),
            "the chapter label is searched; \"-\" and space are the same");
        h.Check((await Cauta(george, "zmei")).Lectii.Contains(sarpe), "the description is searched");
        h.Check((await Cauta(george, "sarpe inexistent")).Lectii.Count == 0, "every word must match");
        h.Check((await george.GetAsync("/cautare?q=a")).StatusCode == HttpStatusCode.BadRequest, "a one-letter search is refused");
        var procente = await Cauta(george, "%%");
        h.Check(procente.Lectii.Count == 0 && procente.Capitole.Count == 0, "% is searched literally, not as a wildcard");

        // Results never include what the user may not see.
        var ascunsa = await Id(await irina.PostAsJsonAsync("/parcursuri", Lectie("Șarpe ascuns", Harness.IdLiceu, vizibilitate: "organizatie", clasa: "XI")));
        var capAscuns = await Capitol(irina, ascunsa, "Șarpe secret");
        var deGeorge = await Cauta(george, "sarpe");
        h.Check(!deGeorge.Lectii.Contains(ascunsa) && !deGeorge.Capitole.Contains(capAscuns), "an outsider never gets organisation-only results");
        var deElena = await Cauta(elena, "sarpe");
        h.Check(deElena.Lectii.Contains(ascunsa) && deElena.Capitole.Contains(capAscuns), "a member of the organisation does");
        h.Check(!(await Cauta(h.Anonim(), "sarpe", raspunsOk: false)).Ok, "anonymous visitors cannot search");
        var nepublicata = await Id(await irina.PostAsJsonAsync("/parcursuri", Lectie("Șarpe nepublicat", Harness.IdLiceu, publicat: false)));
        h.Check(!(await Cauta(elena, "sarpe")).Lectii.Contains(nepublicata) && (await Cauta(irina, "sarpe")).Lectii.Contains(nepublicata),
            "an unpublished Lecție is found only by its editors");

        // Grade and subject filters apply to search too.
        h.Check((await Cauta(elena, "sarpe", "&clasa=XI")).Lectii.SequenceEqual([ascunsa]), "the grade filter narrows the search");
        h.Check((await Cauta(elena, "sarpe", "&materieId=2")).Lectii.Count == 0, "the subject filter narrows the search");
        h.Check((await Cauta(elena, "sarpe", "&filtru=public")).Lectii.All(id => id != ascunsa), "\"public\" leaves out organisation-only Lecții");
        h.Check((await Cauta(elena, "sarpe", "&filtru=organizatia-mea")).Lectii.Contains(ascunsa)
            && !(await Cauta(elena, "sarpe", "&filtru=organizatia-mea")).Lectii.Contains(sarpe), "\"my organisation\" leaves out platform Lecții");

        // Local: the home organisation's county; the user's own locality first; platform Lecții have no place.
        var dinAlbaIulia = await Id(await ovidiu.PostAsJsonAsync("/parcursuri", Lectie("Lecție din Alba Iulia", centru)));
        var dinBarabant = await Id(await ovidiu.PostAsJsonAsync("/parcursuri", Lectie("Lecție din Bărăbanț", barabant)));
        var localElena = await Lectii(elena, "/materii/1/parcursuri?filtru=local");
        h.Check(localElena.Select(l => l.Id).Contains(ascunsa) && !localElena.Any(l => l.Id is var i && (i == sarpe || i == dinAlbaIulia || i == dinBarabant)),
            "local (București): the Liceul's Lecții only, not platform or Alba ones");
        var localRadu = await Lectii(radu, "/materii/1/parcursuri?filtru=local");
        h.Check(localRadu.Select(l => l.Id).OrderBy(i => i).SequenceEqual(new[] { dinAlbaIulia, dinBarabant }.OrderBy(i => i)),
            "local (Alba): only Alba Lecții");
        h.Check(localRadu[0].Id == dinAlbaIulia && localRadu[0].InLocalitateaTa && !localRadu[1].InLocalitateaTa,
            "the student's own locality comes first and is marked");
        h.Check(localRadu[0].Loc == "Alba Iulia, Alba", "a Lecție shows its organisation's locality and county");
        h.Check((await george.GetAsync("/materii/1/parcursuri?filtru=local")).StatusCode == HttpStatusCode.BadRequest,
            "without a county set, \"local\" asks to set it in Settings");
        h.Check((await Cauta(radu, "lectie din", "&filtru=local")).Lectii.Count == 2, "search has the local filter too");
        h.Check((await george.GetAsync("/materii/1/parcursuri?filtru=altceva")).StatusCode == HttpStatusCode.BadRequest, "an unknown filter is refused");

        // The professor's teaching place: own or one of their organisations.
        var loc = await irina.GetFromJsonAsync<JsonElement>("/profesori/me/loc");
        h.Check(loc.GetProperty("optiuni").GetArrayLength() == 2 && loc.GetProperty("organizatieId").ValueKind == JsonValueKind.Null,
            "a professor can pick their own place or one of their organisations (own by default)");
        h.Check((await irina.PutAsJsonAsync("/profesori/me/loc", new { organizatieId = centru })).StatusCode == HttpStatusCode.BadRequest,
            "not an organisation they do not belong to");
        h.Check((await irina.PutAsJsonAsync("/profesori/me/loc", new { organizatieId = Harness.IdLiceu })).IsSuccessStatusCode, "Irina teaches at Liceul de Test");
        h.Check((await ovidiu.PutAsJsonAsync("/profesori/me/loc", new { organizatieId = centru })).IsSuccessStatusCode, "Ovidiu teaches at Centrul Alba");
        var irinaId = await IdUtilizator(h, "irina");
        var ovidiuId = await IdUtilizator(h, "ovidiu");
        var profElena = await Profesori(elena, "?filtru=local");
        h.Check(profElena.Any(p => p.Id == irinaId && p.Loc == "Liceul de Test · Sectorul 1, București") && profElena.All(p => p.Id != ovidiuId),
            "local professors (București): Irina, shown at her organisation");
        var profRadu = await Profesori(radu, "?filtru=local");
        h.Check(profRadu.Any(p => p.Id == ovidiuId && p.InLocalitateaTa) && profRadu.All(p => p.Id != irinaId), "local professors (Alba): Ovidiu, in Radu's locality");
        h.Check((await Profesori(elena, "?filtru=organizatia-mea")).Select(p => p.Id).Contains(irinaId)
            && !(await Profesori(elena, "?filtru=organizatia-mea")).Select(p => p.Id).Contains(ovidiuId), "\"my organisation\" professors");
        h.Check((await george.GetAsync("/profesori?filtru=local")).StatusCode == HttpStatusCode.BadRequest, "local professors need the student's county");

        // Sessions: online, in person (at an organisation or the professor's own place), hybrid.
        var maine = DateTime.UtcNow.AddDays(2);
        var online = await SlotId(await irina.PostAsJsonAsync("/profesori/me/sloturi", Slot("Online", maine)));
        h.Check(await TipSlot(h, online) == "online", "a session is online by default");
        h.Check((await irina.PostAsJsonAsync("/profesori/me/sloturi", Slot("Online cu notă", maine, notaLoc: "Sala 1"))).StatusCode == HttpStatusCode.BadRequest,
            "an online session has no room note");
        h.Check((await irina.PostAsJsonAsync("/profesori/me/sloturi", Slot("Sală cu link", maine, tip: "fata_in_fata", organizatieId: Harness.IdLiceu,
            link: "https://meet.example.invalid/x"))).StatusCode == HttpStatusCode.BadRequest, "an in-person session has no link");
        h.Check((await irina.PostAsJsonAsync("/profesori/me/sloturi", Slot("Acasă", maine, tip: "fata_in_fata"))).StatusCode == HttpStatusCode.BadRequest,
            "in person at their own place needs the professor's county");
        h.Check((await irina.PostAsJsonAsync("/profesori/me/sloturi", Slot("Alba", maine, tip: "fata_in_fata", organizatieId: centru))).StatusCode == HttpStatusCode.BadRequest,
            "not at an organisation they do not belong to");
        h.Check((await irina.PostAsJsonAsync("/profesori/me/sloturi", Slot("Zbor", maine, tip: "zbor"))).StatusCode == HttpStatusCode.BadRequest, "an unknown type is refused");
        var sala = await SlotId(await irina.PostAsJsonAsync("/profesori/me/sloturi", Slot("La liceu", maine, tip: "fata_in_fata", organizatieId: Harness.IdLiceu,
            notaLoc: "Sala 12, etajul 2")));
        var hibrid = await SlotId(await irina.PostAsJsonAsync("/profesori/me/sloturi", Slot("Hibrid", maine.AddHours(3), tip: "hibrid", organizatieId: Harness.IdLiceu,
            link: "https://meet.example.invalid/hibrid", notaLoc: "Sala 3")));
        await h.Db(async db => { var u = await db.Utilizatori.SingleAsync(x => x.Id == irinaId); (u.JudetId, u.LocalitateId) = (Bucuresti, Sector2); return await db.SaveChangesAsync(); });
        var acasa = await SlotId(await irina.PostAsJsonAsync("/profesori/me/sloturi", Slot("Acasă", maine.AddHours(6), tip: "fata_in_fata")));
        h.Check(await h.Db(db => db.CamereTutoring.Where(s => s.Id == acasa).Select(s => s.LocLocalitateId == Sector2 && s.LocOrganizatieId == null).SingleAsync()),
            "in person at their own place copies the professor's locality");
        await SlotId(await ovidiu.PostAsJsonAsync("/profesori/me/sloturi", Slot("Doar online", maine)));

        var inSala = await Profesori(george, "?tip=fata_in_fata");
        var sloturiIrina = inSala.Single(p => p.Id == irinaId).Sloturi;
        h.Check(sloturiIrina.Select(s => s.Id).OrderBy(i => i).SequenceEqual(new[] { sala, hibrid, acasa }.OrderBy(i => i)), "\"in person\" lists in-person and hybrid sessions");
        h.Check(inSala.All(p => p.Id != ovidiuId), "a professor with only online sessions is left out of \"in person\"");
        var peOnline = (await Profesori(george, "?tip=online")).Single(p => p.Id == irinaId).Sloturi.Select(s => s.Id).OrderBy(i => i);
        h.Check(peOnline.SequenceEqual(new[] { online, hibrid }.OrderBy(i => i)), "\"online\" lists online and hybrid sessions");
        h.Check((await george.GetAsync("/profesori?tip=zbor")).StatusCode == HttpStatusCode.BadRequest, "an unknown type filter is refused");
        var salaGeorge = sloturiIrina.Single(s => s.Id == sala);
        h.Check(salaGeorge.Loc == "Liceul de Test · Sectorul 1, București" && salaGeorge.NotaLoc is null, "everyone sees the place, not the room note");

        // Booking: in person / online decided by the session; a hybrid one asks.
        var rSala = await elena.PostAsJsonAsync("/rezervari", new { slotId = sala });
        h.Check(rSala.StatusCode == HttpStatusCode.Created && (await rSala.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("participare").GetString() == "la_sala",
            "booking an in-person session: attends in the room");
        var dupa = (await elena.GetFromJsonAsync<JsonElement>("/rezervari")).GetProperty("elemente").EnumerateArray().Single().GetProperty("slot");
        h.Check(dupa.GetProperty("notaLoc").GetString() == "Sala 12, etajul 2", "a booked student sees the room note");
        h.Check((await radu.PostAsJsonAsync("/rezervari", new { slotId = hibrid })).StatusCode == HttpStatusCode.Conflict, "a hybrid session asks how the student attends");
        h.Check((await radu.PostAsJsonAsync("/rezervari", new { slotId = hibrid, participare = "teleportare" })).StatusCode == HttpStatusCode.Conflict,
            "an unknown way of attending is refused");
        h.Check((await radu.PostAsJsonAsync("/rezervari", new { slotId = hibrid, participare = "online" })).IsSuccessStatusCode, "Radu attends the hybrid session online");
        h.Check((await elena.PostAsJsonAsync("/rezervari", new { slotId = hibrid, participare = "la_sala" })).IsSuccessStatusCode, "Elena attends it in the room");
        var hibridRadu = (await radu.GetFromJsonAsync<JsonElement>("/rezervari")).GetProperty("elemente").EnumerateArray().Single().GetProperty("slot");
        h.Check(hibridRadu.GetProperty("linkIntalnire").GetString() == "https://meet.example.invalid/hibrid" && hibridRadu.GetProperty("notaLoc").GetString() == "Sala 3",
            "a hybrid booking gets both the link and the room note");
        var panou = await irina.GetFromJsonAsync<JsonElement>("/profesori/me");
        var elevi = panou.GetProperty("sloturi").EnumerateArray().Single(s => s.GetProperty("slot").GetProperty("id").GetInt32() == hibrid).GetProperty("elevi")
            .EnumerateArray().ToDictionary(e => e.GetProperty("nume").GetString()!, e => e.GetProperty("participare").GetString());
        h.Check(elevi["Radu Elev"] == "online" && elevi["Elena Elev"] == "la_sala", "the professor sees how each student attends");
        h.Check((await irina.PutAsJsonAsync($"/profesori/me/sloturi/{sala}/link", new { linkIntalnire = "https://meet.example.invalid/y" })).StatusCode == HttpStatusCode.BadRequest,
            "an in-person session cannot get a link later");
        h.Check((await irina.PutAsJsonAsync($"/profesori/me/sloturi/{online}/nota", new { notaLoc = "Sala" })).StatusCode == HttpStatusCode.BadRequest,
            "an online session cannot get a room note later");
        h.Check((await irina.PutAsJsonAsync($"/profesori/me/sloturi/{sala}/nota", new { notaLoc = "Sala 14" })).IsSuccessStatusCode, "the room note can be changed");

        // A chosen organisation the professor no longer belongs to stops counting: their own place applies.
        await h.Db(async db =>
        {
            var m = await db.Set<MembruOrganizatie>().SingleAsync(x => x.OrganizatieId == Harness.IdLiceu && x.UtilizatorId == irinaId);
            m.Stare = MembruOrganizatie.Expirat;
            return await db.SaveChangesAsync();
        });
        var dupaPlecare = (await Profesori(elena, "?filtru=local")).Single(p => p.Id == irinaId);
        h.Check(dupaPlecare.Loc == "Sectorul 2, București" && dupaPlecare.InLocalitateaTa, "after leaving the organisation, the professor's own place is used");
        h.Check((await irina.GetFromJsonAsync<JsonElement>("/profesori/me/loc")).GetProperty("organizatieId").ValueKind == JsonValueKind.Null,
            "and their settings show their own place again");

        // Existing rows from before the migration: online sessions, online bookings (column defaults).
        await h.Db(db => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO mark_ai.camera_tutoring (mentor_id, materie_id, titlu, capacitate, pro_bono, status) VALUES ({0}, 1, 'Veche', 5, true, 'deschisa')", irinaId));
        h.Check(await h.Db(db => db.CamereTutoring.Where(s => s.Titlu == "Veche").Select(s => s.Tip).SingleAsync()) == "online", "a session without a type is online");
    }

    sealed record Gasite(bool Ok, List<int> Lectii, List<int> Capitole);
    sealed record LectieGasita(int Id, string? Loc, bool InLocalitateaTa);
    sealed record SlotGasit(int Id, string Tip, string? Loc, string? NotaLoc);
    sealed record ProfesorGasit(int Id, string? Loc, bool InLocalitateaTa, List<SlotGasit> Sloturi);

    static string? Loc(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null
        : string.Join(" · ", new[] { e.GetProperty("organizatie").GetString(),
            string.Join(", ", new[] { e.GetProperty("localitate").GetString(), e.GetProperty("judet").GetString() }.Where(x => x is not null)) }.Where(x => !string.IsNullOrEmpty(x)));

    static async Task<Gasite> Cauta(HttpClient c, string q, string extra = "", bool raspunsOk = true)
    {
        var r = await c.GetAsync($"/cautare?q={Uri.EscapeDataString(q)}{extra}");
        if (!r.IsSuccessStatusCode)
        {
            if (raspunsOk) throw new Exception($"FAIL: search returned {(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}");
            return new(false, [], []);
        }
        var e = await r.Content.ReadFromJsonAsync<JsonElement>();
        return new(true, e.GetProperty("lectii").EnumerateArray().Select(x => x.GetProperty("id").GetInt32()).ToList(),
            e.GetProperty("capitole").EnumerateArray().Select(x => x.GetProperty("id").GetInt32()).ToList());
    }

    static async Task<List<LectieGasita>> Lectii(HttpClient c, string url) =>
        (await c.GetFromJsonAsync<JsonElement>(url)).GetProperty("elemente").EnumerateArray()
            .Select(e => new LectieGasita(e.GetProperty("id").GetInt32(), e.GetProperty("loc").GetString(), e.GetProperty("inLocalitateaTa").GetBoolean())).ToList();

    static async Task<List<ProfesorGasit>> Profesori(HttpClient c, string query) =>
        (await c.GetFromJsonAsync<JsonElement>("/profesori" + query)).GetProperty("elemente").EnumerateArray()
            .Select(p => new ProfesorGasit(p.GetProperty("id").GetInt32(), Loc(p.GetProperty("loc")), p.GetProperty("inLocalitateaTa").GetBoolean(),
                p.GetProperty("sloturi").EnumerateArray().Select(s => new SlotGasit(s.GetProperty("id").GetInt32(), s.GetProperty("tip").GetString()!,
                    Loc(s.GetProperty("loc")), s.GetProperty("notaLoc").GetString())).ToList())).ToList();

    static object Lectie(string titlu, int? organizatieId, string clasa = "Bac", string vizibilitate = "public", bool publicat = true, string? descriere = null) =>
        new { materieId = 1, titlu, descriere, clasa, vizibilitate, organizatieId, publicat };

    static object Slot(string titlu, DateTime inceputLa, string? tip = null, int? organizatieId = null, string? notaLoc = null, string? link = null) =>
        new { materieId = 1, titlu, inceputLa, capacitate = 5, proBono = true, linkIntalnire = link, tip, locOrganizatieId = organizatieId, notaLoc };

    static async Task<int> Id(HttpResponseMessage r)
    {
        if (!r.IsSuccessStatusCode) throw new Exception($"FAIL: request returned {(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}");
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    // Creating a session returns the professor's dashboard; the new session is the one with that title.
    static async Task<int> SlotId(HttpResponseMessage r)
    {
        if (!r.IsSuccessStatusCode) throw new Exception($"FAIL: request returned {(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}");
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sloturi").EnumerateArray().Select(s => s.GetProperty("slot").GetProperty("id").GetInt32()).Max();
    }

    static Task<string> TipSlot(Harness h, int id) => h.Db(db => db.CamereTutoring.Where(s => s.Id == id).Select(s => s.Tip).SingleAsync());

    static Task<int> IdUtilizator(Harness h, string username) => h.Db(db => db.Utilizatori.Where(u => u.Username == username).Select(u => u.Id).SingleAsync());

    static async Task<int> Capitol(HttpClient editor, int parcursId, string titlu, string? capitol = null) =>
        await Id(await editor.PostAsJsonAsync("/cursuri", new { parcursId, titlu, capitol, materiale = new[] { new { titlu = "Notițe", text = "Material de test." } } }));

    static async Task<(int Centru, int Barabant)> Populeaza(Harness h) => await h.Db(async db =>
    {
        var roluri = await db.Roluri.ToDictionaryAsync(r => r.Nume, r => r.Id);
        Utilizator Cont(string username, string nume, int? judet, int? localitate, params string[] rol)
        {
            var u = new Utilizator { Nume = nume, Username = username, Email = username + "@example.invalid", EmailConfirmatLa = DateTime.UtcNow,
                JudetId = judet, LocalitateId = localitate };
            u.ParolaHash = Parole.Hash(u, Harness.ParolaTest);
            foreach (var r in rol) u.Roluri.Add(new UtilizatorRol { RolId = roluri[r] });
            if (rol.Contains("profesor"))
                u.ProfilMentor = new ProfilMentor { StareVerificare = "aprobat", ActivatLa = DateTime.UtcNow, Descriere = "Profesor de test.",
                    Materii = new List<MentorMaterie> { new() { MaterieId = 1 } } };
            db.Add(u);
            return u;
        }
        var ovidiu = Cont("ovidiu", "Ovidiu Organizator", null, null, "organizatie", "profesor");
        var elena = Cont("elena", "Elena Elev", Bucuresti, Sector2, "elev");
        var radu = Cont("radu", "Radu Elev", Alba, AlbaIulia, "elev");
        var bianca = Cont("bianca", "Bianca Directoare", Alba, Barabant, "organizatie", "profesor");
        await db.SaveChangesAsync();
        var centru = new Organizatie { Nume = "Centrul Alba", Adresa = "Str. Unirii 2", JudetId = Alba, LocalitateId = AlbaIulia, Stare = Organizatie.Aprobata,
            AdminId = ovidiu.Id, CreatLa = DateTime.UtcNow };
        var barabant = new Organizatie { Nume = "Școala Bărăbanț", Adresa = "Str. Școlii 3", JudetId = Alba, LocalitateId = Barabant, Stare = Organizatie.Aprobata,
            AdminId = bianca.Id, CreatLa = DateTime.UtcNow };
        db.AddRange(centru, barabant);
        await db.SaveChangesAsync();
        MembruOrganizatie Membru(int org, int uid, string rol) => new() { OrganizatieId = org, UtilizatorId = uid, Rol = rol, Stare = MembruOrganizatie.Aprobat, CerutLa = DateTime.UtcNow };
        db.AddRange(Membru(Harness.IdLiceu, elena.Id, MembruOrganizatie.RolElev), Membru(centru.Id, radu.Id, MembruOrganizatie.RolElev),
            Membru(barabant.Id, ovidiu.Id, MembruOrganizatie.RolProfesor));
        await db.SaveChangesAsync();
        return (centru.Id, barabant.Id);
    });
}

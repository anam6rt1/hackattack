using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using MarkAi.Data.Entities;
using MarkAi.Tests.Suport;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Continut;

/// <summary>Downloaded Lecții in the desktop/mobile apps: the encrypted answer key and the batch that brings offline work back.</summary>
public class OfflineTests : TestCuHarness
{
    private HttpClient elev = null!;
    private int lectie;
    private Dictionary<int, int> bune = [], gresite = [];

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync(); elev = await h.Login("george"); using var admin = await h.Login("admin");
        lectie = await Harness.LectiePlatforma(admin);
        await h.Db(async db => {
            for (var i = 0; i < 5; i++) db.Add(new Intrebare { ParcursId = lectie, MaterieId = 1, Text = $"Întrebarea {i}", Explicatie = "Explicația secretă",
                Variante = [new() { Text = "Bun", Corecta = true }, new() { Text = "Rău" }] });
            db.Add(new Intrebare { ParcursId = lectie, MaterieId = 1, Text = "Anul?", Tip = Intrebare.Estimare, RaspunsNumeric = 1883 });
            await db.SaveChangesAsync();
            var v = await db.Set<VariantaIntrebare>().ToListAsync();
            bune = v.Where(x => x.Corecta).ToDictionary(x => x.IntrebareId, x => x.Id);
            gresite = v.Where(x => !x.Corecta).ToDictionary(x => x.IntrebareId, x => x.Id);
        });
    }

    private Task<JsonElement> Sincronizeaza(params object[] operatii) => elev.Post("/sincronizare", new { operatii }).Json();
    private static object Op(string tip, object date, Guid? id = null) => new { idClient = id ?? Guid.NewGuid(), tip, date };

    [Fact]
    public async Task PachetulNuArataRaspunsurileIarCheiaLeDecripteaza()
    {
        var pachet = await elev.GetJson($"/parcursuri/{lectie}/offline");
        var text = pachet.GetRawText();
        text.ShouldNotContain("Explicația secretă"); text.ShouldNotContain("1883"); text.ShouldNotContain("corecta");
        pachet.GetProperty("intrebari").GetArrayLength().ShouldBe(6);

        var cheie = Convert.FromBase64String((await elev.GetJson("/utilizatori/me/cheie-offline")).Str("cheie"));
        var date = Convert.FromBase64String(pachet.Str("cheie"));
        var clar = new byte[date.Length - 28];
        using (var aes = new AesGcm(cheie, 16)) aes.Decrypt(date[..12], date[12..^16], date[^16..], clar);
        var chei = JsonSerializer.Deserialize<JsonElement>(clar).EnumerateArray().ToDictionary(x => x.Int("id"));
        foreach (var (q, v) in bune) chei[q].Int("corecta").ShouldBe(v);
        chei.Values.Single(x => x.GetProperty("numeric").ValueKind == JsonValueKind.Number).Int("numeric").ShouldBe(1883);
    }

    [Fact]
    public async Task TestulOfflineEsteRenotatSiPrimesteXpOSinguraData()
    {
        var ids = bune.Keys.ToArray(); var id = Guid.NewGuid();
        var op = Op("test", new { parcursId = lectie, intrebari = ids, raspunsuri = ids.Select(q => bune[q]).ToArray(), inceputLa = DateTime.UtcNow.AddMinutes(-5) }, id);
        var r = (await Sincronizeaza(op)).GetProperty("rezultate")[0];
        r.Str("stare").ShouldBe("aplicata");
        r.GetProperty("rezultat").Bool("promovat").ShouldBeTrue();
        var xp = r.GetProperty("rezultat").Int("xpCastigat"); xp.ShouldBeGreaterThan(0);

        var repetat = (await Sincronizeaza(op)).GetProperty("rezultate")[0];
        repetat.Str("stare").ShouldBe("duplicat");
        repetat.GetProperty("rezultat").Int("xpCastigat").ShouldBe(xp);
        (await h.Db(db => db.Set<IncercareQuiz>().CountAsync(i => i.ParcursId == lectie))).ShouldBe(1);
        (await elev.GetJson($"/parcursuri/{lectie}")).GetProperty("progres").GetProperty("promovatLa").ValueKind.ShouldBe(JsonValueKind.String);
    }

    [Fact]
    public async Task RaspunsurileGresiteDevinGreseliDeRecapitulat()
    {
        var ids = bune.Keys.ToArray();
        var r = (await Sincronizeaza(Op("test", new { parcursId = lectie, intrebari = ids, raspunsuri = ids.Select(q => gresite[q]).ToArray() })))
            .GetProperty("rezultate")[0];
        r.GetProperty("rezultat").Bool("promovat").ShouldBeFalse();
        (await h.Db(db => db.Set<Greseala>().CountAsync())).ShouldBe(5);
    }

    [Fact]
    public async Task UnTestCuIntrebariStraineSauPreaPutineEsteRespins()
    {
        var ids = bune.Keys.ToArray();
        var straina = (await Sincronizeaza(Op("test", new { parcursId = lectie, intrebari = ids.Append(999_999).ToArray(), raspunsuri = ids.Select(q => bune[q]).Append(1).ToArray() })))
            .GetProperty("rezultate")[0];
        straina.Str("stare").ShouldBe("respinsa");
        var putine = (await Sincronizeaza(Op("test", new { parcursId = lectie, intrebari = ids[..2], raspunsuri = ids[..2].Select(q => bune[q]).ToArray() })))
            .GetProperty("rezultate")[0];
        putine.Str("stare").ShouldBe("respinsa");
        var altaVarianta = (await Sincronizeaza(Op("test", new { parcursId = lectie, intrebari = ids, raspunsuri = ids.Select(q => bune[ids[0]]).ToArray() })))
            .GetProperty("rezultate")[0];
        altaVarianta.Str("stare").ShouldBe("respinsa");
        (await h.Db(db => db.Set<IncercareQuiz>().CountAsync())).ShouldBe(0);
    }

    [Fact]
    public async Task CapitolulDeschisSiSimulareaOfflineAjungLaServer()
    {
        using var admin = await h.Login("admin");
        var capitol = (await admin.Post("/cursuri", new { parcursId = lectie, titlu = "Capitolul offline", continut = "Text", publicat = true }).Json(HttpStatusCode.Created)).Int("id");
        var toate = await h.Db(db => db.Set<Intrebare>().Where(q => q.ParcursId == lectie).Select(q => new { q.Id, q.Tip }).ToListAsync());
        var raspunsuri = toate.Select(q => q.Tip == Intrebare.Grila ? (object)new { intrebareId = q.Id, variantaId = bune[q.Id] } : new { intrebareId = q.Id, valoare = 1883 }).ToArray();
        var r = (await Sincronizeaza(Op("capitol_deschis", new { cursId = capitol }),
            Op("simulare", new { materieId = 1, tip = "bac", clasa = "Bac", raspunsuri, durataMinute = 30, inceputLa = DateTime.UtcNow.AddMinutes(-10) }))).GetProperty("rezultate");
        r[0].Str("stare").ShouldBe("aplicata"); r[1].Str("stare").ShouldBe("aplicata");
        r[1].GetProperty("rezultat").Int("corecte").ShouldBe(6);
        (await elev.GetJson($"/parcursuri/{lectie}")).GetProperty("progres").Int("ultimulCursId").ShouldBe(capitol);
        var istoric = await elev.GetJson("/simulari");
        istoric.Int("total").ShouldBe(1);
    }

    [Fact]
    public async Task FaraAccesNuSeDescarcaNimic()
    {
        using var altul = await h.InregistreazaElev("strainoffline");
        await h.Db(async db => { var p = await db.Set<Parcurs>().FindAsync(lectie); p!.Publicat = false; await db.SaveChangesAsync(); });
        await altul.GetAsync($"/parcursuri/{lectie}/offline", Ct).Status(HttpStatusCode.NotFound);
        using var profesor = await h.Login("irina");
        await profesor.GetAsync("/utilizatori/me/cheie-offline", Ct).Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnLotPreaMareEsteRefuzat()
    {
        var multe = Enumerable.Range(0, 201).Select(_ => Op("capitol_deschis", new { cursId = 1 })).ToArray();
        await elev.Post("/sincronizare", new { operatii = multe }).Status(HttpStatusCode.BadRequest);
    }
}

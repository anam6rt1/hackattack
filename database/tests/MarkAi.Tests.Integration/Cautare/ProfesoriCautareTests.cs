using System.Net;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using MarkAi.Tests.Suport;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Cautare;

/// <summary>
/// The Profesori list's search (q: words in the name or the subjects, without diacritics, with every filter) and the rule that
/// professors without an upcoming session are not listed. Professors: irina (Română), ovidiu (Română), ștefan (Matematică and
/// Biologie, in Liceul de Test, added here).
/// </summary>
public sealed class ProfesoriCautareTests : TestCautare
{
    private int irinaId, ovidiuId, stefanId;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        stefanId = await h.Db(async db =>
        {
            var u = new Utilizator { Nume = "Ștefan Țurcanu-Ionescu", Username = "stefan", Email = "stefan@example.invalid", EmailConfirmatLa = DateTime.UtcNow };
            u.ParolaHash = Parole.Hash(u, Harness.ParolaTest);
            u.Roluri.Add(new UtilizatorRol { RolId = await db.Roluri.Where(r => r.Nume == "profesor").Select(r => r.Id).SingleAsync() });
            u.ProfilMentor = new ProfilMentor { StareVerificare = "aprobat", ActivatLa = DateTime.UtcNow, Descriere = "Profesor de istorie a artei.",
                Materii = new List<MentorMaterie> { new() { MaterieId = 2 }, new() { MaterieId = 3 } } };
            db.Add(u);
            await db.SaveChangesAsync();
            db.Add(new MembruOrganizatie { OrganizatieId = Liceu, UtilizatorId = u.Id, Rol = MembruOrganizatie.RolProfesor, Stare = MembruOrganizatie.Aprobat, CerutLa = DateTime.UtcNow });
            await db.SaveChangesAsync();
            return u.Id;
        });
        (irinaId, ovidiuId) = (await Id("irina"), await Id("ovidiu"));
        await CuSedinte("irina", "ovidiu", "stefan");
    }

    private async Task<List<int>> Gaseste(string q, string extra = "") =>
        (await Profesori(george, $"?q={Uri.EscapeDataString(q)}{extra}")).Select(p => p.Id).ToList();

    [Theory]
    [InlineData("Irina")]
    [InlineData("irina marin")]
    [InlineData("MARIN")]
    [InlineData("  irina  ")]
    public async Task DupaNume(string q) => (await Gaseste(q)).ShouldBe([irinaId]);

    [Theory]
    [InlineData("stefan")]
    [InlineData("Ștefan")]
    [InlineData("ŞTEFAN")] // cedilla
    [InlineData("turcanu")]
    [InlineData("turcanu ionescu")]
    [InlineData("țurcanu-ionescu")]
    public async Task FaraDiacriticeSiCuCratima(string q) => (await Gaseste(q)).ShouldBe([stefanId]);

    [Theory]
    [InlineData("matematica")]
    [InlineData("Matematică")]
    [InlineData("biolog")]
    public async Task DupaMaterie(string q) => (await Gaseste(q)).ShouldBe([stefanId]);

    [Fact]
    public async Task OMaterieGasesteTotiProfesoriiEi()
    {
        (await Gaseste("romana")).Order().ShouldBe(new[] { irinaId, ovidiuId }.Order());
    }

    [Fact]
    public async Task CuvinteleSeCombinaIntreNumeSiMaterie()
    {
        (await Gaseste("irina romana")).ShouldBe([irinaId]);
        (await Gaseste("stefan biologie")).ShouldBe([stefanId]);
        (await Gaseste("irina biologie")).ShouldBeEmpty();
    }

    [Fact]
    public async Task DescriereaNuEsteCautata()
    {
        (await Gaseste("istorie")).ShouldBeEmpty();
    }

    [Fact]
    public async Task NimicGasit()
    {
        (await Gaseste("zzz")).ShouldBeEmpty();
    }

    [Fact]
    public async Task FaraTextSuntTotiCuSedinte()
    {
        (await Gaseste("")).Order().ShouldBe(new[] { irinaId, ovidiuId, stefanId }.Order());
        (await Gaseste("   ")).Count.ShouldBe(3);
    }

    [Fact]
    public async Task CautareaSeCombinaCuFiltrele()
    {
        // Same-subject filter, organisation filter (elena is in Liceul de Test, like irina and ștefan), session type.
        (await Gaseste("romana", "&materieId=1")).Order().ShouldBe(new[] { irinaId, ovidiuId }.Order());
        (await Gaseste("romana", "&materieId=2")).ShouldBeEmpty();
        (await Profesori(elena, "?q=romana&filtru=organizatia-mea")).Select(p => p.Id).ShouldBe([irinaId]);
        (await Gaseste("stefan", "&tip=fata_in_fata")).ShouldBeEmpty();
        (await Gaseste("stefan", "&tip=online")).ShouldBe([stefanId]);
    }

    [Fact]
    public async Task CautareaMergeSiFaraCont()
    {
        (await Profesori(h.Anonim(), "?q=irina")).Select(p => p.Id).ShouldBe([irinaId]);
    }

    [Fact]
    public async Task TextulPreaLungEsteRefuzat()
    {
        await george.Get("/profesori?q=" + new string('a', 101)).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ProfesoriiFaraSedinteNuSuntListati()
    {
        await h.Db(db => db.CamereTutoring.Where(c => c.MentorId == ovidiuId).ExecuteDeleteAsync());
        (await Gaseste("")).ShouldNotContain(ovidiuId);
        (await Gaseste("ovidiu")).ShouldBeEmpty();
    }

    [Fact]
    public async Task OSedintaTrecutaSauAnulataNuTineProfesorulInLista()
    {
        await h.Db(db => db.CamereTutoring.Where(c => c.MentorId == irinaId).ExecuteUpdateAsync(s => s.SetProperty(c => c.InceputLa, DateTime.UtcNow.AddHours(-1))));
        await h.Db(db => db.CamereTutoring.Where(c => c.MentorId == ovidiuId).ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, "anulata")));
        (await Gaseste("")).ShouldBe([stefanId]);
    }

    [Fact]
    public async Task OSedintaPlinaTineProfesorulInLista()
    {
        await h.Db(db => db.CamereTutoring.Where(c => c.MentorId == stefanId).ExecuteUpdateAsync(s => s.SetProperty(c => c.Capacitate, 1)));
        var sedinta = await h.Db(db => db.CamereTutoring.Where(c => c.MentorId == stefanId).Select(c => c.Id).SingleAsync());
        await elena.Post("/rezervari", new { slotId = sedinta }).Status(HttpStatusCode.Created);
        var stefan = (await Profesori(george, "?q=stefan")).ShouldHaveSingleItem();
        stefan.Sloturi.ShouldHaveSingleItem();
    }
}

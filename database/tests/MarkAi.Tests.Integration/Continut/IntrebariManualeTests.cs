using System.Net;
using MarkAi.Data.Entities;
using MarkAi.Tests.Suport;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Continut;

/// <summary>Questions written or edited by hand: 2–6 options with exactly one correct, estimates with a number; only the Lecție's editors.</summary>
public sealed class IntrebariManualeTests : TestCuHarness
{
    private HttpClient admin = null!;
    private int lectie;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        admin = await h.Login("admin");
        lectie = await Harness.LectiePlatforma(admin);
    }

    private static object Grila(string text = "Cine a scris „Luceafărul”?", int variante = 4, int corecte = 1) => new
    {
        text, explicatie = "Eminescu.", activa = true,
        variante = Enumerable.Range(0, variante).Select(i => new { text = $"Varianta {i}", corecta = i < corecte }).ToArray(),
    };

    private Task<HttpResponseMessage> Adauga(object corp, HttpClient? c = null) => (c ?? admin).Post($"/parcursuri/{lectie}/intrebari", corp);

    [Fact]
    public async Task IntrebareaScrisaApareInLista()
    {
        var q = await Adauga(Grila()).Json(HttpStatusCode.Created);
        q.Str("sursa").ShouldBe("manual");
        q.Str("tip").ShouldBe("grila");
        (await admin.GetJson($"/continut/parcursuri/{lectie}/intrebari")).Lista().ShouldHaveSingleItem().Int("id").ShouldBe(q.Int("id"));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    public async Task DouaPanaLaSaseVarianteSuntAcceptate(int n)
    {
        await Adauga(Grila(variante: n)).Status(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(7, 1)]
    [InlineData(4, 0)]
    [InlineData(4, 2)]
    public async Task VarianteleGresiteSuntRefuzate(int variante, int corecte)
    {
        (await Adauga(Grila(variante: variante, corecte: corecte)).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("exact una corectă");
    }

    [Fact]
    public async Task GrilaFaraVarianteEsteRefuzata()
    {
        await Adauga(new { text = "Q", activa = true }).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TextulEsteObligatoriu()
    {
        await Adauga(Grila(text: "  ")).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task VariantaGoalaEsteRefuzata()
    {
        await Adauga(new { text = "Q", activa = true, variante = new[] { new { text = "", corecta = true }, new { text = "B", corecta = false } } })
            .Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EstimareaCereUnNumar()
    {
        await Adauga(new { text = "În ce an?", tip = "estimare", activa = true }).Status(HttpStatusCode.BadRequest);
        (await Adauga(new { text = "În ce an?", tip = "estimare", raspunsNumeric = 1883, activa = true }).Json(HttpStatusCode.Created)).Int("raspunsNumeric").ShouldBe(1883);
    }

    [Fact]
    public async Task TipulNecunoscutEsteRefuzat()
    {
        (await Adauga(new { text = "Q", tip = "eseu", activa = true }).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("grila");
    }

    [Fact]
    public async Task EditareaInlocuiesteVariantele()
    {
        var id = (await Adauga(Grila()).Json(HttpStatusCode.Created)).Int("id");
        var q = await admin.Put($"/intrebari/{id}", new { text = "Nouă", activa = true, variante = new[] { new { text = "X", corecta = false }, new { text = "Y", corecta = true } } }).Json();
        q.Str("text").ShouldBe("Nouă");
        q.Lista("variante").Select(v => v.Str("text")).ShouldBe(["X", "Y"]);
        (await h.Db(db => db.Set<VariantaIntrebare>().CountAsync(v => v.IntrebareId == id))).ShouldBe(2);
    }

    [Fact]
    public async Task EditareaFaraVariantePastreazaVariantele()
    {
        var id = (await Adauga(Grila()).Json(HttpStatusCode.Created)).Int("id");
        (await admin.Put($"/intrebari/{id}", new { text = "Doar textul", activa = true }).Json()).Lista("variante").Count.ShouldBe(4);
    }

    [Fact]
    public async Task IntrebareaAiEditataDevineManuala()
    {
        var id = await h.Db(async db =>
        {
            var q = new Intrebare { MaterieId = 1, ParcursId = lectie, Text = "De la AI", Sursa = Intrebare.SursaAi,
                Variante = [new() { Text = "A", Corecta = true }, new() { Text = "B" }] };
            db.Add(q); await db.SaveChangesAsync(); return q.Id;
        });
        (await admin.Put($"/intrebari/{id}", new { text = "Corectată", activa = true }).Json()).Str("sursa").ShouldBe("manual");
    }

    [Fact]
    public async Task DezactivareaPastreazaIntrebarea()
    {
        var id = (await Adauga(Grila()).Json(HttpStatusCode.Created)).Int("id");
        (await admin.Put($"/intrebari/{id}", new { text = "Q", activa = false }).Json()).Bool("activa").ShouldBeFalse();
        (await admin.GetJson($"/continut/parcursuri/{lectie}/intrebari")).Lista().Count.ShouldBe(1);
    }

    [Fact]
    public async Task StergereaScoateIntrebareaSiVariantele()
    {
        var id = (await Adauga(Grila()).Json(HttpStatusCode.Created)).Int("id");
        await admin.Delete($"/intrebari/{id}").Status(HttpStatusCode.NoContent);
        (await h.Db(db => db.Set<VariantaIntrebare>().CountAsync(v => v.IntrebareId == id))).ShouldBe(0);
        await admin.Delete($"/intrebari/{id}").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ElevulNuPoateScrieIntrebari()
    {
        using var george = await h.Login("george");
        await Adauga(Grila(), george).Status(HttpStatusCode.Forbidden);
        await george.Get($"/continut/parcursuri/{lectie}/intrebari").Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AltProfesorNuPoateEditaIntrebarile()
    {
        var id = (await Adauga(Grila()).Json(HttpStatusCode.Created)).Int("id");
        using var irina = await h.Login("irina");
        (await irina.Put($"/intrebari/{id}", new { text = "Furată", activa = true })).IsSuccessStatusCode.ShouldBeFalse();
        (await irina.Delete($"/intrebari/{id}")).IsSuccessStatusCode.ShouldBeFalse();
        (await Adauga(Grila(), irina)).IsSuccessStatusCode.ShouldBeFalse();
    }

    [Fact]
    public async Task AutorulIsiEditeazaIntrebarile()
    {
        using var irina = await h.Login("irina");
        var a = (await irina.Post("/parcursuri", new { materieId = 1, titlu = "A Irinei", clasa = "X", vizibilitate = "public", organizatieId = Harness.IdLiceu })
            .Json(HttpStatusCode.Created)).Int("id");
        var id = (await irina.Post($"/parcursuri/{a}/intrebari", Grila()).Json(HttpStatusCode.Created)).Int("id");
        await irina.Put($"/intrebari/{id}", new { text = "Editată", activa = true }).Status(HttpStatusCode.OK);
        await irina.Delete($"/intrebari/{id}").Status(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task IntrebareaMaterieiFaraLectieEsteDoarAAdminului()
    {
        var id = await h.Db(async db =>
        {
            var q = new Intrebare { MaterieId = 1, Text = "Doar Triviador", Variante = [new() { Text = "A", Corecta = true }, new() { Text = "B" }] };
            db.Add(q); await db.SaveChangesAsync(); return q.Id;
        });
        using var irina = await h.Login("irina");
        await irina.Put($"/intrebari/{id}", new { text = "x", activa = true }).Status(HttpStatusCode.Forbidden);
        await admin.Put($"/intrebari/{id}", new { text = "x", activa = true }).Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TextulPreaLungEsteRefuzat()
    {
        await Adauga(Grila(text: new string('a', 1001))).Status(HttpStatusCode.BadRequest);
    }
}

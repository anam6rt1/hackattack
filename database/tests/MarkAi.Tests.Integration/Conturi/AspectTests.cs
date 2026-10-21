using System.Net;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Conturi;

/// <summary>Appearance: the selectable backgrounds (season × time of day) and each account's preferences (mode, theme, style, avatar).</summary>
public sealed class AspectTests : TestCuHarness
{
    private static readonly string[] Anotimpuri = ["primavara", "vara", "toamna", "iarna"];
    private static readonly string[] Momente = ["zi", "noapte"];

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        // The same themes and styles the seed command creates, plus one inactive and one not selectable.
        await h.Db(async db =>
        {
            var insule = new Interfata { Cod = "insule", Nume = "Insule", Activa = true };
            db.AddRange(insule, new Interfata { Cod = "realist", Nume = "Realist", Activa = true }, new Interfata { Cod = "pixel", Nume = "Pixel", Activa = true },
                new Interfata { Cod = "retras", Nume = "Retras", Activa = false });
            foreach (var a in Anotimpuri)
                foreach (var m in Momente)
                {
                    var b = new Background { Cod = $"{a}-{m}", Nume = $"{a} {m}", AssetUrl = $"tema:{a}-{m}", Activ = true, SelectabilStatic = true };
                    db.Add(b);
                    db.Add(new RegulaBackground { Background = b, Interfata = insule, Anotimp = a, PerioadaZilei = m, Activa = true });
                }
            db.Add(new Background { Cod = "craciun", Nume = "Crăciun", AssetUrl = "tema:craciun", Activ = true, SelectabilStatic = false });
            db.Add(new Background { Cod = "vechi", Nume = "Vechi", AssetUrl = "tema:vechi", Activ = false, SelectabilStatic = true });
            await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task TemeleSelectabileSuntListateFaraCont()
    {
        var teme = (await h.Anonim().GetJson("/aspect/teme")).Lista();
        teme.Count.ShouldBe(8);
        teme.Select(t => t.Str("cod")).ShouldNotContain("craciun");
        teme.Select(t => t.Str("cod")).ShouldNotContain("vechi");
    }

    [Fact]
    public async Task TemaArataAnotimpulSiMomentul()
    {
        var t = (await h.Anonim().GetJson("/aspect/teme")).Lista().Single(x => x.Str("cod") == "iarna-noapte");
        (t.Str("anotimp"), t.Str("perioadaZilei")).ShouldBe(("iarna", "noapte"));
    }

    [Fact]
    public async Task FaraPreferinteModulEsteAuto()
    {
        using var george = await h.Login("george");
        var p = await george.GetJson("/utilizatori/me/preferinte");
        p.Str("mod").ShouldBe("auto");
        p.EsteNull("tema").ShouldBeTrue();
        p.EsteNull("interfata").ShouldBeTrue();
        p.EsteNull("avatar").ShouldBeTrue();
    }

    [Theory]
    [InlineData("primavara-zi")]
    [InlineData("vara-noapte")]
    [InlineData("toamna-zi")]
    [InlineData("iarna-noapte")]
    public async Task OTemaStaticaSeSalveaza(string tema)
    {
        using var george = await h.Login("george");
        var p = await george.Put("/utilizatori/me/preferinte", new { mod = "static", tema }).Json();
        (p.Str("mod"), p.Str("tema")).ShouldBe(("static", tema));
        (await george.GetJson("/utilizatori/me/preferinte")).Str("tema").ShouldBe(tema);
    }

    [Theory]
    [InlineData("craciun")]
    [InlineData("vechi")]
    [InlineData("nu-exista")]
    [InlineData(null)]
    public async Task TemaNeselectabilaEsteRefuzata(string? tema)
    {
        using var george = await h.Login("george");
        (await george.Put("/utilizatori/me/preferinte", new { mod = "static", tema }).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("Tema aleasă");
    }

    [Theory]
    [InlineData("manual")]
    [InlineData("STATIC")]
    [InlineData("")]
    public async Task ModulNecunoscutEsteRefuzat(string mod)
    {
        using var george = await h.Login("george");
        (await george.Put("/utilizatori/me/preferinte", new { mod }).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("„auto” sau „static”");
    }

    [Fact]
    public async Task InapoiLaAutoScoateTema()
    {
        using var george = await h.Login("george");
        await george.Put("/utilizatori/me/preferinte", new { mod = "static", tema = "vara-zi" });
        var p = await george.Put("/utilizatori/me/preferinte", new { mod = "auto" }).Json();
        p.Str("mod").ShouldBe("auto");
        p.EsteNull("tema").ShouldBeTrue();
    }

    [Theory]
    [InlineData("insule")]
    [InlineData("realist")]
    [InlineData("pixel")]
    public async Task StilulDesenelorSeSalveaza(string interfata)
    {
        using var irina = await h.Login("irina");
        (await irina.Put("/utilizatori/me/preferinte", new { interfata }).Json()).Str("interfata").ShouldBe(interfata);
    }

    [Theory]
    [InlineData("retras")]
    [InlineData("cubism")]
    public async Task StilulInactivSauNecunoscutEsteRefuzat(string interfata)
    {
        using var george = await h.Login("george");
        (await george.Put("/utilizatori/me/preferinte", new { interfata }).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("Stilul ales");
    }

    [Theory]
    [InlineData("vulpe")]
    [InlineData("pisica-portocalie")]
    [InlineData("a")]
    public async Task AvatarulValidSeSalveaza(string avatar)
    {
        using var george = await h.Login("george");
        (await george.Put("/utilizatori/me/preferinte", new { avatar }).Json()).Str("avatar").ShouldBe(avatar);
    }

    [Theory]
    [InlineData("Vulpe")]
    [InlineData("vulpe_2")]
    [InlineData("ursuleț")]
    [InlineData("<script>")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task AvatarulNevalidEsteRefuzat(string avatar)
    {
        using var george = await h.Login("george");
        (await george.Put("/utilizatori/me/preferinte", new { avatar }).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("Avatarul");
    }

    [Fact]
    public async Task AvatarulGolIlScoate()
    {
        using var george = await h.Login("george");
        await george.Put("/utilizatori/me/preferinte", new { avatar = "vulpe" });
        (await george.Put("/utilizatori/me/preferinte", new { avatar = "" }).Json()).EsteNull("avatar").ShouldBeTrue();
    }

    [Fact]
    public async Task CampurileLipsaRamanCumErau()
    {
        using var george = await h.Login("george");
        await george.Put("/utilizatori/me/preferinte", new { mod = "static", tema = "toamna-noapte", interfata = "pixel", avatar = "bufnita" });
        var p = await george.Put("/utilizatori/me/preferinte", new { interfata = "realist" }).Json();
        (p.Str("mod"), p.Str("tema"), p.Str("interfata"), p.Str("avatar")).ShouldBe(("static", "toamna-noapte", "realist", "bufnita"));
    }

    [Fact]
    public async Task PreferinteleSuntPeCont()
    {
        using var george = await h.Login("george");
        using var admin = await h.Login("admin");
        await george.Put("/utilizatori/me/preferinte", new { mod = "static", tema = "iarna-zi", avatar = "pinguin" });
        await admin.Put("/utilizatori/me/preferinte", new { mod = "static", tema = "vara-noapte" });
        (await george.GetJson("/utilizatori/me/preferinte")).Str("tema").ShouldBe("iarna-zi");
        var a = await admin.GetJson("/utilizatori/me/preferinte");
        a.Str("tema").ShouldBe("vara-noapte");
        a.EsteNull("avatar").ShouldBeTrue();
    }

    [Fact]
    public async Task PreferinteleCerCont()
    {
        await h.Anonim().Get("/utilizatori/me/preferinte").Status(HttpStatusCode.Unauthorized);
        await h.Anonim().Put("/utilizatori/me/preferinte", new { mod = "auto" }).Status(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OPreferintaRefuzataNuSchimbaNimic()
    {
        using var george = await h.Login("george");
        await george.Put("/utilizatori/me/preferinte", new { mod = "static", tema = "vara-zi" });
        await george.Put("/utilizatori/me/preferinte", new { mod = "static", tema = "vara-zi", interfata = "cubism" }).Status(HttpStatusCode.BadRequest);
        (await h.Db(db => db.PreferinteUtilizatori.CountAsync(p => p.Utilizator.Username == "george"))).ShouldBe(1);
        (await george.GetJson("/utilizatori/me/preferinte")).Str("tema").ShouldBe("vara-zi");
    }
}

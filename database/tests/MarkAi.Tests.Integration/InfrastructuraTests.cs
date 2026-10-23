using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MarkAi.Api.Servicii;
using MarkAi.Tests.Suport;

namespace MarkAi.Tests.Integration;

/// <summary>The harness itself: the fakes are the ones the app uses, and background jobs only run when a test runs them.</summary>
public sealed class InfrastructuraTests : TestCuHarness
{
    [Fact]
    public void AplicatiaFolosesteAiFals() => h.Serviciu<ModelAi>().Activ.ShouldBeOfType<FakeAi>();

    [Fact]
    public void AplicatiaFolosesteCeasulFals() => h.Serviciu<TimeProvider>().ShouldBeSameAs(h.Ceas);

    [Fact]
    public async Task CapitolNouNuGenereazaIntrebariSingur()
    {
        using var admin = await h.Login("admin");
        var lectie = await Harness.LectiePlatforma(admin);
        await Harness.Capitol(admin, lectie);
        var stare = await admin.GetFromJsonAsync<JsonElement>($"/parcursuri/{lectie}/generare", Ct);
        stare.GetProperty("stare").GetString().ShouldBe("necunoscuta");
        h.Ai.Apeluri.ShouldBeEmpty();
    }
}

using System.Text.Json;
using MarkAi.Api.Endpoints;
using MarkAi.Api.Infrastructura;
using MarkAi.Data.Servicii;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace MarkAi.Tests.Unit;

/// <summary>How exceptions become answers (<see cref="TratareErori"/>): Romanian messages for known cases, nothing internal otherwise.</summary>
public sealed class EroriTests
{
    private static async Task<(int Status, string Mesaj)> Raspuns(Exception ex)
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        await new TratareErori(_ => throw ex, NullLogger<TratareErori>.Instance).InvokeAsync(ctx);
        ctx.Response.Body.Position = 0;
        var corp = await JsonSerializer.DeserializeAsync<JsonElement>(ctx.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        corp.GetProperty("status").GetInt32().ShouldBe(ctx.Response.StatusCode);
        return (ctx.Response.StatusCode, corp.GetProperty("mesaj").GetString()!);
    }

    [Fact]
    public async Task CerereaInvalidaDa400CuMesajul() => (await Raspuns(new CerereInvalida("Ceva lipsește."))).ShouldBe((400, "Ceva lipsește."));

    [Fact]
    public async Task InexistentulDa404() => (await Raspuns(new KeyNotFoundException("Lecția nu există."))).ShouldBe((404, "Lecția nu există."));

    [Fact]
    public async Task FaraAccesDa403CuMesajGeneric() => (await Raspuns(new UnauthorizedAccessException("detaliu"))).ShouldBe((403, "Nu ai acces la această resursă."));

    [Fact]
    public async Task ConflictulDa409() => (await Raspuns(new Conflict("Ai deja o cerere."))).ShouldBe((409, "Ai deja o cerere."));

    [Fact]
    public async Task LocurileEpuizateSuntConflict() => (await Raspuns(new LocuriEpuizateException())).Status.ShouldBe(409);

    [Fact]
    public async Task UnicitateaDinBazaDa409()
    {
        var pg = new PostgresException("duplicate key", "ERROR", "ERROR", "23505");
        (await Raspuns(new DbUpdateException("x", pg))).ShouldBe((409, "Există deja o înregistrare cu aceste date."));
    }

    [Fact]
    public async Task AltaEroareDeBazaDa500()
    {
        var pg = new PostgresException("fk", "ERROR", "ERROR", "23503");
        (await Raspuns(new DbUpdateException("x", pg))).Status.ShouldBe(500);
    }

    [Fact]
    public async Task EroareaNeasteptataNuSpuneNimicIntern()
    {
        var (status, mesaj) = await Raspuns(new InvalidOperationException("Sequence contains no elements"));
        status.ShouldBe(500);
        mesaj.ShouldBe("A apărut o eroare. Încearcă din nou.");
    }

    [Fact]
    public async Task RaspunsulInceputNuMaiEsteSchimbat()
    {
        var ctx = new DefaultHttpContext();
        var corp = new MemoryStream();
        ctx.Response.Body = corp;
        var pornit = new PornitFeature();
        ctx.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpResponseFeature>(pornit);
        await Should.ThrowAsync<InvalidOperationException>(() =>
            new TratareErori(_ => throw new InvalidOperationException("x"), NullLogger<TratareErori>.Instance).InvokeAsync(ctx));
    }

    [Fact]
    public void PragulTestuluiEsteOptzeciLaSuta()
    {
        QuizEndpoints.Prag(10).ShouldBe(8);
        QuizEndpoints.Prag(5).ShouldBe(4);
        QuizEndpoints.Prag(3).ShouldBe(3);
        QuizEndpoints.Prag(1).ShouldBe(1);
        QuizEndpoints.Prag(7).ShouldBe(6);
    }

    [Theory]
    [InlineData(null, 10)]
    [InlineData("5", 5)]
    [InlineData("0", 1)]
    [InlineData("100", 50)]
    public void IntrebariPeTestSuntIntre1Si50(string? setare, int asteptat) =>
        QuizEndpoints.IntrebariPeIncercare(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Limite:IntrebariPeTest"] = setare }).Build()).ShouldBe(asteptat);

    private sealed class PornitFeature : Microsoft.AspNetCore.Http.Features.HttpResponseFeature
    {
        public override bool HasStarted => true;
    }
}

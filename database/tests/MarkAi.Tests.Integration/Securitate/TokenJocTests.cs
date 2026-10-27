using System.Net;
using System.Net.Http.Headers;
using MarkAi.Tests.Suport;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace MarkAi.Tests.Integration.Securitate;

/// <summary>The desktop and mobile apps open the game with a short token in the address: it opens /hub and nothing else.</summary>
public sealed class TokenJocTests : TestCuHarness
{
    private async Task<string> Token()
    {
        using var george = await h.Login("george");
        return (await george.Post("/utilizatori/me/token-joc").Json()).Str("token");
    }

    [Fact]
    public async Task TokenulDeschideJoculFaraCookie()
    {
        var token = await Token();
        await using var hub = new HubConnectionBuilder().WithUrl(h.Adresa + "/hub/joc", o =>
        {
            o.AccessTokenProvider = () => Task.FromResult<string?>(token);
            o.Transports = HttpTransportType.WebSockets;
            o.SkipNegotiation = true;
        }).Build();
        await hub.StartAsync(Ct);
        hub.State.ShouldBe(HubConnectionState.Connected);
    }

    [Fact]
    public async Task TokenulNuDeschideRestulApiului()
    {
        var token = await Token();
        using var c = h.Brut();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await c.GetAsync("/utilizatori/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnTokenInAdresaEsteIgnoratInAfaraJocului()
    {
        var (george, cookies) = await h.LoginCuCookie("george");
        george.Dispose();
        var sesiune = cookies.GetCookies(new Uri(h.Adresa))["markai_sesiune"]!.Value;
        using var c = h.Brut();
        (await c.GetAsync($"/utilizatori/me?access_token={sesiune}", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FaraContNuPrimesteToken()
    {
        await h.Anonim().PostAsync("/utilizatori/me/token-joc", null, Ct).Status(HttpStatusCode.Unauthorized);
    }
}

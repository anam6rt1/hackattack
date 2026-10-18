using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using MarkAi.Api.Infrastructura;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MarkAi.Api.Hubs;

public record JucatorPrezent(string Id, string Nume, bool Gazda, long Intrat);

/// <summary>
/// Relay for the Triviador island game, replacing the Supabase Realtime channel: room members broadcast
/// events to each other and see who is present. The game itself still runs in the host's browser;
/// the server only guarantees identities (from the login cookie) and room membership.
/// </summary>
[Authorize(Policy = Politici.Elev)]
public sealed class JocHub(Prezenta prezenta) : Hub
{
    private static string Grup(string cod) => "joc:" + cod;

    private static string Cod(string cod)
    {
        cod = (cod ?? "").Trim().ToUpperInvariant();
        if (cod.Length is < 4 or > 8 || !cod.All(char.IsAsciiLetterOrDigit)) throw new HubException("Cod de cameră invalid.");
        return cod;
    }

    public async Task<IReadOnlyList<JucatorPrezent>> Intra(string cod, bool gazda)
    {
        cod = Cod(cod);
        var jucator = new JucatorPrezent(Context.User!.Id().ToString(), Context.User!.FindFirstValue(ClaimTypes.Name) ?? "Jucător",
            gazda, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        prezenta.Intra(Context.ConnectionId, cod, jucator);
        await Groups.AddToGroupAsync(Context.ConnectionId, Grup(cod));
        var lista = prezenta.Lista(cod);
        await Clients.Group(Grup(cod)).SendAsync("prezenta", lista);
        await Clients.OthersInGroup(Grup(cod)).SendAsync("intrat", jucator);
        return lista;
    }

    /// <summary>Broadcasts to everyone in the room, the sender included (like Supabase's broadcast.self).</summary>
    public Task Trimite(string eveniment, JsonElement payload)
    {
        var cod = prezenta.CameraDe(Context.ConnectionId) ?? throw new HubException("Intră mai întâi într-o cameră.");
        if (eveniment is not ("stare" or "actiune")) throw new HubException("Eveniment necunoscut.");
        return Clients.Group(Grup(cod)).SendAsync(eveniment, payload);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var cod = prezenta.Iese(Context.ConnectionId);
        if (cod is not null) await Clients.Group(Grup(cod)).SendAsync("prezenta", prezenta.Lista(cod));
        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>In-memory room presence (single API instance). One entry per user; extra tabs reuse the first join time.</summary>
public sealed class Prezenta
{
    private readonly ConcurrentDictionary<string, (string Cod, JucatorPrezent Jucator)> _conexiuni = new();

    public void Intra(string conexiune, string cod, JucatorPrezent jucator)
    {
        var existent = _conexiuni.Values.FirstOrDefault(x => x.Cod == cod && x.Jucator.Id == jucator.Id).Jucator;
        _conexiuni[conexiune] = (cod, existent is null ? jucator : jucator with { Intrat = existent.Intrat, Gazda = existent.Gazda || jucator.Gazda });
    }

    public string? Iese(string conexiune) => _conexiuni.TryRemove(conexiune, out var x) ? x.Cod : null;

    public string? CameraDe(string conexiune) => _conexiuni.TryGetValue(conexiune, out var x) ? x.Cod : null;

    public IReadOnlyList<JucatorPrezent> Lista(string cod) => _conexiuni.Values.Where(x => x.Cod == cod)
        .GroupBy(x => x.Jucator.Id).Select(g => g.OrderBy(x => x.Jucator.Intrat).First().Jucator)
        .OrderBy(j => j.Intrat).ToList();
}

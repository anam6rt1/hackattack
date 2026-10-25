using System.Security.Claims;
using MarkAi.Api.Infrastructura;
using MarkAi.Api.Servicii.Triviador;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MarkAi.Api.Hubs;

/// <summary>
/// Triviador's real-time channel. The game runs on the server (<see cref="MotorTriviador"/>): clients only send intents,
/// identified by their login cookie, and receive the public game state ("stare") after every change.
/// Rooms are created over HTTP (POST /trivia/camere); the room of a connection is the one it joined, never a client value.
/// </summary>
[Authorize(Policy = Politici.Elev)]
public sealed class JocHub(MotorTriviador motor) : Hub
{
    private static readonly string InstantaServer = Guid.NewGuid().ToString("N");
    public string Instanta() => InstantaServer;
    public async Task<StareJoc> Intra(string cod)
    {
        // A connection follows one room: stop receiving the previous room's updates.
        if (motor.CodConexiune(Context.ConnectionId) is { } anterior)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, MotorTriviador.Grup(anterior));
        var stare = await motor.IntraAsync(Context.ConnectionId, cod, Context.User!.Id(), Context.User!.FindFirstValue(ClaimTypes.Name) ?? "Jucător");
        await Groups.AddToGroupAsync(Context.ConnectionId, MotorTriviador.Grup(stare.Cod));
        return stare;
    }

    public Task AdaugaBot(string dificultate) => motor.AdaugaBotAsync(Context.ConnectionId, dificultate);
    public Task ScoateBot(string botId) => motor.ScoateBotAsync(Context.ConnectionId, botId);
    public Task Porneste() => motor.PornesteAsync(Context.ConnectionId);
    public Task Ataca(string insula) => motor.AtacaAsync(Context.ConnectionId, insula);
    public Task Raspunde(int varianta) => motor.RaspundeAsync(Context.ConnectionId, varianta);
    public Task Estimeaza(int valoare) => motor.EstimeazaAsync(Context.ConnectionId, valoare);
    public Task JocNou() => motor.JocNouAsync(Context.ConnectionId);

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await motor.IeseAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using AixecAPI.Data;
using AixecAPI.Models;
using System.Security.Claims;

namespace AixecAPI.Hubs;

public class GameHub : Hub
{
    private readonly AppDbContext _db;
    
    // Diccionarios estáticos para mantener referencias si se necesitan en el futuro
    public static readonly Dictionary<int, GameState> GameStates = new();
    public static readonly Dictionary<string, (int GameId, int UserId)> ConnectionMap = new();

    public GameHub(AppDbContext db) => _db = db;

    private int GetUserId() => int.Parse(Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    // --- CONECTARSE: versión mínima ---
    public async Task JoinGame(int gameId)
    {
        var userId = GetUserId();
        if (userId == 0) return;

        // Guardamos el mapping conexión -> (gameId, userId)
        ConnectionMap[Context.ConnectionId] = (gameId, userId);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"game-{gameId}");

        // Notificamos al grupo que un jugador se ha unido (opcional)
        await Clients.Group($"game-{gameId}").SendAsync("PlayerJoined", new { UserId = userId });

        // Nota: no se reparte ni se ejecuta lógica de juego en el servidor.
    }

    // --- DESCONEXIÓN: limpiamos mappings ---
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (ConnectionMap.TryGetValue(Context.ConnectionId, out var info))
        {
            var (gameId, userId) = info;
            ConnectionMap.Remove(Context.ConnectionId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"game-{gameId}");
            await Clients.Group($"game-{gameId}").SendAsync("PlayerLeft", new { UserId = userId });
        }

        await base.OnDisconnectedAsync(exception);
    }

    // --- JUGAR CARTA---
    public async Task PlayCard(int gameId, int cardId, int slotIndex)
    {
        // Por ahora devolvemos un mensaje al cliente indicando que el servidor no ejecuta la jugada.
        await Clients.Caller.SendAsync("Error", "Server-side gameplay disabled. Execute game logic locally and report results at the end.");
    }

    // --- PASAR TURNO: stub seguro ---
    public async Task EndTurn(int gameId)
    {
        await Clients.Caller.SendAsync("Error", "Server-side gameplay disabled. Execute game logic locally and report results at the end.");
    }

    // --- Broadcast auxiliar---
    private async Task BroadcastGameStateIfExists(int gameId)
    {
        if (GameStates.TryGetValue(gameId, out var state))
        {
            await Clients.Group($"game-{gameId}").SendAsync("GameStateUpdated", state);
        }
    }
}

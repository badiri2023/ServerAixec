using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using AixecAPI.Data;
using AixecAPI.Models; // Aquí es donde vive el GameState ahora
using AixecAPI.Logic;
using System.Security.Claims;

namespace AixecAPI.Hubs;

public class GameHub : Hub
{
    private readonly AppDbContext _db;
    
    // Diccionarios estáticos para mantener las partidas en la RAM del servidor
    public static readonly Dictionary<int, GameState> GameStates = new();
    public static readonly Dictionary<string, (int GameId, int UserId)> ConnectionMap = new();

    public GameHub(AppDbContext db) => _db = db;

    private int GetUserId() => int.Parse(Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    // --- CONECTARSE ---
    public async Task JoinGame(int gameId)
    {
        var userId = GetUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return;

        if (!GameStates.TryGetValue(gameId, out var state))
        {
            state = new GameState { GameId = gameId };
            GameStates[gameId] = state;
        }

        if (state.Players.Any(p => p.UserId == userId)) return;

        var player = new PlayerGameState
        {
            UserId = userId,
            Username = user.Username,
            ConnectionId = Context.ConnectionId
        };
        state.Players.Add(player);
        
        ConnectionMap[Context.ConnectionId] = (gameId, userId);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"game-{gameId}");

        if (state.Players.Count == 1)
        {
            state.Status = "playing";
            state.CurrentTurnUserId = state.Players[0].UserId;

            foreach (var p in state.Players)
            {
                var deckCards = await _db.DeckCards
                    .Where(dc => dc.Deck.UserId == p.UserId)
                    .OrderBy(x => Guid.NewGuid())
                    .Take(5)
                    .Select(dc => dc.CardId)
                    .ToListAsync();
                
                p.HandCardIds.AddRange(deckCards);
            }
        }
        await BroadcastGameState(gameId);
    }

    // --- JUGAR CARTA ---
    public async Task PlayCard(int gameId, int cardId, int slotIndex)
    {
        if (!GameStates.TryGetValue(gameId, out var state)) return;

        var cardData = await _db.Cards.FindAsync(cardId);
        if (cardData == null) return;

        // Llamamos al GameEngine (AixecAPI.Logic)
        string? error = GameEngine.TryPlayCard(state, GetUserId(), cardData, slotIndex);

        if (error != null)
            await Clients.Caller.SendAsync("Error", error);
        else
            await BroadcastGameState(gameId);
    }

    // --- PASAR TURNO ---
    public async Task EndTurn(int gameId)
    {
        if (!GameStates.TryGetValue(gameId, out var state)) return;
        if (state.CurrentTurnUserId != GetUserId()) return;

        GameEngine.ProcessEndTurn(state);
        await BroadcastGameState(gameId);
    }

    private async Task BroadcastGameState(int gameId)
    {
        if (GameStates.TryGetValue(gameId, out var state))
        {
            await Clients.Group($"game-{gameId}").SendAsync("GameStateUpdated", state);
        }
    }
}
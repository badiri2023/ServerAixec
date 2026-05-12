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
                // 1. Buscamos todas las cartas del mazo del jugador en la DB
                var allDeckCards = await _db.DeckCards
                    .Where(dc => dc.Deck.UserId == p.UserId)
                    .Select(dc => dc.CardId)
                    .ToListAsync();
                
                // 2. Las barajamos aleatoriamente
                var shuffledDeck = allDeckCards.OrderBy(x => Guid.NewGuid()).ToList();

                // 3. Repartimos: 5 a la mano y el resto al mazo para robar después
                p.HandCardIds.AddRange(shuffledDeck.Take(5));
                p.DeckCardIds.AddRange(shuffledDeck.Skip(5));
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

        // 1. Terminas tu turno
        GameEngine.ProcessEndTurn(state);
        
        // Le avisamos a Godot que ahora es el turno del rival (el Bot)
        await BroadcastGameState(gameId); 

        // 2. ¿Es el turno del Bot (UserId 10)?
        if (state.CurrentTurnUserId == 10)
        {
            // Simulamos que el bot está "pensando" durante 1.5 segundos
            await Task.Delay(1500);

            // El bot ejecuta su jugada (Baja sus cartas)
            await GameEngine.PlayBotTurn(state, _db);
            
            // Avisamos a Godot para que dibuje la carta que bajó el bot
            await BroadcastGameState(gameId);

            // Esperamos 1.5 segundos para que veas la jugada
            await Task.Delay(1500);

            // El bot termina su turno y te lo devuelve
            GameEngine.ProcessEndTurn(state);
            await BroadcastGameState(gameId);
        }
    }

    private async Task BroadcastGameState(int gameId)
    {
        if (GameStates.TryGetValue(gameId, out var state))
        {
            await Clients.Group($"game-{gameId}").SendAsync("GameStateUpdated", state);
        }
    }
}

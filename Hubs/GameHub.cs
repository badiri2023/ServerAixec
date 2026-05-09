using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using AixecAPI.Data;
using AixecAPI.Models;

namespace AixecAPI.Hubs;

// Estado en memoria de una partida
public class GameState
{
    public int GameId { get; set; }
    public string Status { get; set; } = "waiting";
    public int CurrentTurnUserId { get; set; }
    public List<PlayerGameState> Players { get; set; } = new();
    public List<FieldCard> Field { get; set; } = new();
}

public class PlayerGameState
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
    public int CardsInHand { get; set; }
    public int Health { get; set; } = 20;
    public int Mana { get; set; } = 1;
}

public class FieldCard
{
    public int UserId { get; set; }
    public int CardId { get; set; }
    public string CardName { get; set; } = string.Empty;
    public int Attack { get; set; }
    public int Defense { get; set; }
    public bool HasAttacked { get; set; } = false;
}

public class GameHub : Hub
{
    private readonly AppDbContext _db;

    // Estado de todas las partidas activas en memoria
    private static readonly Dictionary<int, GameState> GameStates = new();

    // Mapa de connectionId → (gameId, userId) para gestionar desconexiones
    private static readonly Dictionary<string, (int gameId, int userId)> ConnectionMap = new();

    public GameHub(AppDbContext db)
    {
        _db = db;
    }

    // =============================================
    // CONEXIÓN A LA PARTIDA
    // =============================================

    public async Task JoinGame(int gameId, int userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"game-{gameId}");

        // Registrar la conexión
        ConnectionMap[Context.ConnectionId] = (gameId, userId);

        // Inicializar estado si no existe
        if (!GameStates.ContainsKey(gameId))
            GameStates[gameId] = new GameState { GameId = gameId };

        var state = GameStates[gameId];

        // Añadir jugador si no está ya
        if (!state.Players.Any(p => p.UserId == userId))
        {
            var user = await _db.Users.FindAsync(userId);
            state.Players.Add(new PlayerGameState
            {
                UserId = userId,
                Username = user?.Username ?? "Desconocido",
                ConnectionId = Context.ConnectionId,
                CardsInHand = 0,
                Health = 20,
                Mana = 1
            });
        }
        else
        {
            // Actualizar connectionId si reconecta
            var player = state.Players.First(p => p.UserId == userId);
            player.ConnectionId = Context.ConnectionId;
        }

        // Si ya hay 2 jugadores, empezar la partida
        if (state.Players.Count == 2 && state.Status == "waiting")
        {
            state.Status = "playing";
            state.CurrentTurnUserId = state.Players[0].UserId;
            await Clients.Group($"game-{gameId}")
                .SendAsync("GameStarted", state);
        }
        else
        {
            await Clients.Group($"game-{gameId}")
                .SendAsync("PlayerJoined", new { userId, state.Players.Count });
        }

        await BroadcastGameState(gameId);
    }

    // =============================================
    // CARTAS EN MANO
    // =============================================

    public async Task UpdateHandCount(int gameId, int userId, int cardCount)
    {
        if (!GameStates.ContainsKey(gameId)) return;

        var player = GameStates[gameId].Players
            .FirstOrDefault(p => p.UserId == userId);

        if (player != null)
        {
            player.CardsInHand = cardCount;
            await BroadcastGameState(gameId);
        }
    }

    // =============================================
    // JUGAR CARTA AL CAMPO
    // =============================================

    public async Task PlayCard(int gameId, int userId, int cardId)
    {
        if (!GameStates.ContainsKey(gameId)) return;

        var state = GameStates[gameId];

        // Verificar que es el turno del jugador
        if (state.CurrentTurnUserId != userId)
        {
            await Clients.Caller.SendAsync("Error", "No es tu turno");
            return;
        }

        var card = await _db.Cards.FindAsync(cardId);
        if (card == null)
        {
            await Clients.Caller.SendAsync("Error", "Carta no encontrada");
            return;
        }

        // Añadir carta al campo
        state.Field.Add(new FieldCard
        {
            UserId = userId,
            CardId = cardId,
            CardName = card.Name,
            Attack = card.Attack,
            Defense = card.Defense
        });

        // Reducir cartas en mano
        var player = state.Players.FirstOrDefault(p => p.UserId == userId);
        if (player != null && player.CardsInHand > 0)
            player.CardsInHand--;

        await Clients.Group($"game-{gameId}")
            .SendAsync("CardPlayed", new { userId, cardId, card.Name });

        await BroadcastGameState(gameId);
    }

    // =============================================
    // ATACAR
    // =============================================

    public async Task Attack(int gameId, int attackerUserId, int attackerCardId, int targetUserId, int? targetCardId)
    {
        if (!GameStates.ContainsKey(gameId)) return;

        var state = GameStates[gameId];

        if (state.CurrentTurnUserId != attackerUserId)
        {
            await Clients.Caller.SendAsync("Error", "No es tu turno");
            return;
        }

        var attackerCard = state.Field
            .FirstOrDefault(f => f.UserId == attackerUserId && f.CardId == attackerCardId);

        if (attackerCard == null || attackerCard.HasAttacked)
        {
            await Clients.Caller.SendAsync("Error", "Esta carta no puede atacar");
            return;
        }

        attackerCard.HasAttacked = true;

        // Atacar carta enemiga
        if (targetCardId.HasValue)
        {
            var targetCard = state.Field
                .FirstOrDefault(f => f.UserId == targetUserId && f.CardId == targetCardId.Value);

            if (targetCard != null)
            {
                targetCard.Defense -= attackerCard.Attack;
                attackerCard.Defense -= targetCard.Attack;

                // Eliminar cartas muertas del campo
                state.Field.RemoveAll(f => f.Defense <= 0);

                await Clients.Group($"game-{gameId}")
                    .SendAsync("CardsAttacked", new
                    {
                        attackerUserId,
                        attackerCardId,
                        targetUserId,
                        targetCardId,
                        attackerDefense = attackerCard.Defense,
                        targetDefense = targetCard.Defense
                    });
            }
        }
        else
        {
            // Atacar al jugador directamente
            var targetPlayer = state.Players.FirstOrDefault(p => p.UserId == targetUserId);
            if (targetPlayer != null)
            {
                targetPlayer.Health -= attackerCard.Attack;

                await Clients.Group($"game-{gameId}")
                    .SendAsync("PlayerAttacked", new
                    {
                        attackerUserId,
                        targetUserId,
                        targetPlayer.Health,
                        attackerCard.Attack
                    });

                // Comprobar si alguien ha ganado
                if (targetPlayer.Health <= 0)
                    await FinishGame(gameId, attackerUserId);
            }
        }

        await BroadcastGameState(gameId);
    }

    // =============================================
    // PASAR TURNO
    // =============================================

    public async Task EndTurn(int gameId, int userId)
    {
        if (!GameStates.ContainsKey(gameId)) return;

        var state = GameStates[gameId];

        if (state.CurrentTurnUserId != userId)
        {
            await Clients.Caller.SendAsync("Error", "No es tu turno");
            return;
        }

        // Resetear cartas que han atacado
        foreach (var card in state.Field.Where(f => f.UserId == userId))
            card.HasAttacked = false;

        // Pasar turno al siguiente jugador
        var nextPlayer = state.Players.FirstOrDefault(p => p.UserId != userId);
        if (nextPlayer != null)
        {
            state.CurrentTurnUserId = nextPlayer.UserId;

            // Aumentar maná del siguiente jugador
            nextPlayer.Mana = Math.Min(nextPlayer.Mana + 1, 10);

            await Clients.Group($"game-{gameId}")
                .SendAsync("TurnChanged", new
                {
                    nextUserId = nextPlayer.UserId,
                    nextPlayer.Mana
                });
        }

        await BroadcastGameState(gameId);
    }

    // =============================================
    // FIN DE PARTIDA
    // =============================================

    private async Task FinishGame(int gameId, int winnerUserId)
    {
        if (!GameStates.ContainsKey(gameId)) return;

        var state = GameStates[gameId];
        state.Status = "finished";

        await Clients.Group($"game-{gameId}")
            .SendAsync("GameFinished", new { winnerUserId });

        // Actualizar en base de datos
        var game = await _db.Games.FindAsync(gameId);
        if (game != null)
        {
            game.Status = "finished";
            await _db.SaveChangesAsync();
        }

        // Limpiar estado en memoria
        GameStates.Remove(gameId);
    }

    // =============================================
    // DESCONEXIÓN
    // =============================================

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (ConnectionMap.TryGetValue(Context.ConnectionId, out var info))
        {
            var (gameId, userId) = info;
            ConnectionMap.Remove(Context.ConnectionId);

            await Clients.Group($"game-{gameId}")
                .SendAsync("PlayerDisconnected", new { userId });

            // Si la partida estaba en curso, el otro jugador gana
            if (GameStates.TryGetValue(gameId, out var state) && state.Status == "playing")
            {
                var winner = state.Players.FirstOrDefault(p => p.UserId != userId);
                if (winner != null)
                    await FinishGame(gameId, winner.UserId);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    // =============================================
    // BROADCAST ESTADO COMPLETO
    // =============================================

    private async Task BroadcastGameState(int gameId)
    {
        if (!GameStates.ContainsKey(gameId)) return;

        await Clients.Group($"game-{gameId}")
            .SendAsync("GameStateUpdated", GameStates[gameId]);
    }
}
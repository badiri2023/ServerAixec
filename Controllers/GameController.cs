using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AixecAPI.Data;
using AixecAPI.Models;
using System.Security.Claims;

namespace AixecAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GameController : ControllerBase
{
    private readonly AppDbContext _db;

    public GameController(AppDbContext db)
    {
        _db = db;
    }

    // DTOs / Records
    public record StartGameResponse(int GameId, List<int> PlayerDeck, List<int> BotDeck);
    public record ReportResultDto(int GameId, int WinnerUserId, int LoserUserId);
    public record FinishGameDto(int WinnerUserId, int LoserUserId);

    // Crear una nueva sala
    [HttpPost("create")]
    public async Task<IActionResult> CreateGame()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var game = new Game();
        _db.Games.Add(game);
        await _db.SaveChangesAsync();

        var gamePlayer = new GamePlayer
        {
            GameId = game.Id,
            UserId = userId,
            IsCurrentTurn = true
        };
        _db.GamePlayers.Add(gamePlayer);
        await _db.SaveChangesAsync();

        return Ok(new { gameId = game.Id });
    }
    
    // Crear una nueva sala contra el BOT (mantengo por compatibilidad)
    [HttpPost("create-bot")]
    public async Task<IActionResult> CreateBotGame()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var game = new Game();
        _db.Games.Add(game);
        await _db.SaveChangesAsync();

        var player1 = new GamePlayer
        {
            GameId = game.Id,
            UserId = userId,
            IsCurrentTurn = true
        };
        _db.GamePlayers.Add(player1);

        var botPlayer = new GamePlayer
        {
            GameId = game.Id,
            UserId = 10, // ID del bot
            IsCurrentTurn = false
        };
        _db.GamePlayers.Add(botPlayer);

        await _db.SaveChangesAsync();

        return Ok(new { gameId = game.Id });
    }

    // Iniciar partida: crea partida y devuelve decks (player + bot)
    // mode: "bot_fixed" | "bot_random" (o cualquier otra cadena para random por defecto)
    [HttpPost("start/{mode}")]
    public async Task<IActionResult> StartGame(string mode)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        // 1) Crear partida simple en DB
        var game = new Game();
        _db.Games.Add(game);
        await _db.SaveChangesAsync();

        // 2) Asociar jugador a la partida
        _db.GamePlayers.Add(new GamePlayer { GameId = game.Id, UserId = userId, IsCurrentTurn = true });
        await _db.SaveChangesAsync();

        // 3) Obtener deck del jugador (IDs) desde sus decks guardados
        var playerDeck = await _db.Decks
            .Where(d => d.UserId == userId)
            .SelectMany(d => d.DeckCards.Select(dc => dc.CardId))
            .ToListAsync();

        // Si no tiene deck, fallback a un conjunto por defecto
        if (!playerDeck.Any())
            playerDeck = new List<int> { 1,2,3,4,5,11,12,13,14,15 };

        // 4) Generar deck del bot (fijo o aleatorio)
        List<int> botDeck;
        if (mode == "bot_fixed")
        {
            botDeck = new List<int> { 21,22,23,24,25,26,27,28,29,30,31,32,33,34,35,36,37,38,39,40 };
        }
        else
        {
            // Aleatorio: tomar 20 cartas distintas de la tabla Cards
            botDeck = await _db.Cards
                .OrderBy(x => Guid.NewGuid())
                .Take(20)
                .Select(c => c.Id)
                .ToListAsync();

            if (!botDeck.Any())
                botDeck = new List<int> { 21,22,23,24,25,26,27,28,29,30 };
        }

        // 5) Responder con gameId y decks (solo IDs)
        var response = new StartGameResponse(game.Id, playerDeck, botDeck);
        return Ok(response);
    }

    // Unirse a una sala
    [HttpPost("join/{gameId}")]
    public async Task<IActionResult> JoinGame(int gameId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var game = await _db.Games
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game == null) return NotFound("Partida no encontrada");
        if (game.Status != "waiting") return BadRequest("La partida ya ha comenzado");
        if (game.Players.Any(p => p.UserId == userId)) return BadRequest("Ya estás en esta partida");

        _db.GamePlayers.Add(new GamePlayer { GameId = gameId, UserId = userId });
        game.Status = "playing";
        await _db.SaveChangesAsync();

        return Ok(new { message = "Te has unido a la partida" });
    }

    // Obtener estado de una partida
    [HttpGet("{gameId}")]
    public async Task<IActionResult> GetGame(int gameId)
    {
        var game = await _db.Games
            .Include(g => g.Players)
            .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game == null) return NotFound();

        return Ok(new {
            game.Id,
            game.Status,
            game.CurrentTurn,
            Players = game.Players.Select(p => new {
                p.UserId,
                p.User.Username,
                p.Score,
                p.Level,
                p.IsCurrentTurn
            })
        });
    }

    // Pasar turno (mantengo por compatibilidad, aunque la lógica de juego está en cliente)
    [HttpPost("{gameId}/turn")]
    public async Task<IActionResult> NextTurn(int gameId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var game = await _db.Games
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game == null) return NotFound();

        var currentPlayer = game.Players.FirstOrDefault(p => p.UserId == userId);
        if (currentPlayer == null || !currentPlayer.IsCurrentTurn)
            return BadRequest("No es tu turno");

        currentPlayer.IsCurrentTurn = false;
        var players = game.Players.ToList();
        var nextIndex = (players.IndexOf(currentPlayer) + 1) % players.Count;
        players[nextIndex].IsCurrentTurn = true;
        game.CurrentTurn++;

        await _db.SaveChangesAsync();

        return Ok(new { nextUserId = players[nextIndex].UserId });
    }

    // Reportar resultado final (cliente envía gameId, winner y loser)
    [HttpPost("report-result")]
    public async Task<IActionResult> ReportResult([FromBody] ReportResultDto dto)
    {
        var game = await _db.Games.FindAsync(dto.GameId);
        if (game == null) return NotFound("Partida no encontrada");

        var winner = await _db.Users.FindAsync(dto.WinnerUserId);
        var loser = await _db.Users.FindAsync(dto.LoserUserId);
        if (winner == null || loser == null) return NotFound("Usuarios no encontrados");

        // Actualizar estadísticas básicas
        winner.WonMatches++;
        winner.PlayedMatches++;
        loser.PlayedMatches++;

        // Recompensas simples (ajusta valores si quieres)
        bool vsBot = dto.WinnerUserId == 10 || dto.LoserUserId == 10;
        if (vsBot)
        {
            var realWinner = dto.WinnerUserId == 10 ? loser : winner;
            var realLoser = dto.WinnerUserId == 10 ? winner : loser;

            realWinner.Money += 25;
            realLoser.Money += 10;
        }
        else
        {
            winner.Money += 50;
            loser.Money += 25;
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "Resultado registrado" });
    }

    // POST api/game/finish (mantengo para compatibilidad con lógica previa)
    [HttpPost("finish")]
    public async Task<IActionResult> FinishGame([FromBody] FinishGameDto dto)
    {
        var winner = await _db.Users.FindAsync(dto.WinnerUserId);
        var loser = await _db.Users.FindAsync(dto.LoserUserId);

        if (winner == null || loser == null)
            return NotFound("Uno o ambos usuarios no encontrados");

        bool vsBot = dto.WinnerUserId == 10 || dto.LoserUserId == 10;

        winner.WonMatches++;
        winner.PlayedMatches++;
        loser.PlayedMatches++;

        if (vsBot)
        {
            var realWinner = dto.WinnerUserId == 10 ? loser : winner;
            var realLoser = dto.WinnerUserId == 10 ? winner : loser;

            realWinner.Money += 25;
            realLoser.Money += 10;
        }
        else
        {
            winner.Money += 50;
            loser.Money += 25;
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            winner = new { winner.Id, winner.Username, winner.WonMatches, winner.PlayedMatches, winner.Money },
            loser = new { loser.Id, loser.Username, loser.PlayedMatches, loser.Money }
        });
    }
}


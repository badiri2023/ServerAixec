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
    
// Crear una nueva sala contra el BOT
    [HttpPost("create-bot")]
    public async Task<IActionResult> CreateBotGame()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        // Creamos la partida
        var game = new Game();
        _db.Games.Add(game);
        await _db.SaveChangesAsync();

        // Añadimos al jugador real
        var player1 = new GamePlayer
        {
            GameId = game.Id,
            UserId = userId,
            IsCurrentTurn = true // El jugador empieza
        };
        _db.GamePlayers.Add(player1);

        // Añadimos al Bot (poner el id del bot correctamente)
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

    // Pasar turno
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

        // Pasar turno al siguiente jugador
        currentPlayer.IsCurrentTurn = false;
        var players = game.Players.ToList();
        var nextIndex = (players.IndexOf(currentPlayer) + 1) % players.Count;
        players[nextIndex].IsCurrentTurn = true;
        game.CurrentTurn++;

        await _db.SaveChangesAsync();

        return Ok(new { nextUserId = players[nextIndex].UserId });
    }
    // POST api/game/finish
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
            // El ganador es el jugador real (no el bot)
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
    public record FinishGameDto(int WinnerUserId, int LoserUserId);
}
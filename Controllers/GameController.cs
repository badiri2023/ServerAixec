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
}
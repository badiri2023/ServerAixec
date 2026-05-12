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
    // 1. Obtener ID del usuario desde el Token
    var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
    if (userIdClaim == null) return Unauthorized();
    int userId = int.Parse(userIdClaim.Value);

    // 2. Crear partida en DB
    var game = new Game { Status = "playing", CurrentTurn = 1 };
    _db.Games.Add(game);
    await _db.SaveChangesAsync();

    // 3. Asociar jugador
    _db.GamePlayers.Add(new GamePlayer { GameId = game.Id, UserId = userId, IsCurrentTurn = true });
    await _db.SaveChangesAsync();

    // 4. OBTENER EL DECK (CORREGIDO: Solo el último mazo)
    var lastDeck = await _db.Decks
        .Where(d => d.UserId == userId)
        .OrderByDescending(d => d.Id) 
        .Include(d => d.DeckCards)
        .FirstOrDefaultAsync();

    // Forzamos explícitamente que sea una lista de Integers
    List<int> playerDeckIds = new List<int>();

    if (lastDeck != null && lastDeck.DeckCards.Any())
    {
        playerDeckIds = lastDeck.DeckCards.Select(dc => dc.CardId).ToList();
    }
    else
    {
        // Fallback: mazo básico si no tiene nada
        playerDeckIds = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
    }

    // 5. GENERAR MAZO DEL BOT
    List<int> botDeckIds;
    if (mode == "bot_fixed")
    {
        botDeckIds = new List<int> { 21, 22, 23, 24, 25, 26, 27, 28, 29, 30 };
    }
    else
    {
        botDeckIds = await _db.Cards
            .Where(c => c.Id != 50) 
            .OrderBy(x => Guid.NewGuid()) 
            .Take(20)
            .Select(c => c.Id)
            .ToListAsync();
    }

    // 6. Enviar respuesta
    return Ok(new StartGameResponse(game.Id, playerDeckIds, botDeckIds));
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

// Asegúrate de tener este record arriba de la clase o en un archivo de Models
public record BotResultDto(bool Win);

[HttpPost("report-bot-result")]
public async Task<IActionResult> ReportBotResult([FromBody] BotResultDto dto)
{
    // 1. Extraer ID del usuario del Token
    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
    if (userIdClaim == null) return Unauthorized("Token no válido");
    
    int userId = int.Parse(userIdClaim.Value);
    var user = await _db.Users.FindAsync(userId);
    
    if (user == null) return NotFound("Usuario no encontrado");

    // 2. Lógica de recompensas
    user.PlayedMatches++;
    if (dto.Win) // "Win" con Mayúscula porque C# suele mapear así los JSON
    {
        user.WonMatches++;
        user.Money += 25;
    }
    else
    {
        user.Money += 10;
    }

    await _db.SaveChangesAsync();
    
    // Devolvemos el nuevo saldo para que Godot lo confirme
    return Ok(new { message = "OK", nuevoSaldo = user.Money });
}

    // Reportar resultado final (cliente envía gameId, winner y loser)
[HttpPost("report-result")]
public async Task<IActionResult> ReportResult([FromBody] ReportResultDto dto)
{
    // 1. Validar que la partida existe (si esto falla, da 404)
    var game = await _db.Games.FindAsync(dto.GameId);
    if (game == null) return NotFound("Partida no encontrada");

    // 2. Buscar usuarios
    var winner = await _db.Users.FindAsync(dto.WinnerUserId);
    var loser = await _db.Users.FindAsync(dto.LoserUserId);
    if (winner == null || loser == null) return NotFound("Usuarios no encontrados");

    // 3. Marcar partida como terminada
    game.Status = "finished";

    // 4. Lógica de Recompensas y Estadísticas
    bool vsBot = dto.WinnerUserId == 10 || dto.LoserUserId == 10;

    if (vsBot)
    {
        // Si hay un bot, identificamos quién es el humano (el que no es el 10)
        var humano = dto.WinnerUserId == 10 ? loser : winner;
        bool ganoHumano = (dto.WinnerUserId != 10);

        humano.PlayedMatches++;
        if (ganoHumano)
        {
            humano.WonMatches++;
            humano.Money += 25; // Oro por ganar al bot
        }
        else
        {
            humano.Money += 10; // Oro de consolación por perder con bot
        }
    }
    else
    {
        // PvP Real (Entre dos humanos)
        winner.WonMatches++;
        winner.PlayedMatches++;
        winner.Money += 50;

        loser.PlayedMatches++;
        loser.Money += 25;
    }

    await _db.SaveChangesAsync();
    return Ok(new { message = "Resultado registrado y recompensas entregadas" });
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


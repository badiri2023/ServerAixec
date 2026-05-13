using Microsoft.AspNetCore.Authorization;
using AixecAPI.Data;
using AixecAPI.Models;
using AixecAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AixecAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtService _jwt;

    public AuthController(AppDbContext db, JwtService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    [HttpGet("ping")]
    public IActionResult Ping()
    {
        return Ok(new { mensaje = "¡La API está viva y conectada!" });
    }

[HttpPost("register")]
public async Task<IActionResult> Register([FromBody] RegisterDto dto)
{
    if (await _db.Users.AnyAsync(u => u.Email == dto.Email))
        return BadRequest("El email ya está en uso");

    using var tx = await _db.Database.BeginTransactionAsync();
    try
    {
        var user = new User
        {
            Username = dto.Username,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var nuevoMazo = new Deck
        {
            Name = "Mazo Inicial",
            UserId = user.Id
        };
        _db.Decks.Add(nuevoMazo);
        await _db.SaveChangesAsync();

        var idsCartasIniciales = new List<int> { 1, 2, 5, 6, 9, 15, 16, 21, 24, 37, 39, 36, 33, 4, 38, 25, 32, 17, 13, 27 };

        // Añadimos las relaciones DeckCard
        foreach (var cartaId in idsCartasIniciales)
        {
            _db.DeckCards.Add(new DeckCard
            {
                DeckId = nuevoMazo.Id,
                CardId = cartaId
            });
        }

        // Añadimos las cartas al inventario del usuario (PlayerCard)
        foreach (var cartaId in idsCartasIniciales)
        {
            var existing = await _db.PlayerCards.FirstOrDefaultAsync(pc => pc.UserId == user.Id && pc.CardId == cartaId);
            if (existing != null)
            {
                existing.Quantity += 1;
                _db.PlayerCards.Update(existing);
            }
            else
            {
                _db.PlayerCards.Add(new PlayerCard
                {
                    UserId = user.Id,
                    CardId = cartaId,
                    Quantity = 1
                });
            }
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new { token = _jwt.GenerateToken(user) });
    }
    catch (Exception ex)
    {
        await tx.RollbackAsync();
        // Loguea el error según tu sistema de logging
        return StatusCode(500, "Error al crear usuario: " + ex.Message);
    }
}




    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return Unauthorized("Credenciales incorrectas");

        return Ok(new { token = _jwt.GenerateToken(user),id = user.Id });
    }
    [HttpPost("loginprueba")]
    public async Task<IActionResult> Loginprueba([FromBody] LoginDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return Unauthorized(BCrypt.Net.BCrypt.HashPassword(dto.Password));

        return Ok(new { token = _jwt.GenerateToken(user) });
    }

// GET: api/auth/perfil
[HttpGet("perfil")]
//El authorize hace que se necesite token para que funcione
[Authorize]
public async Task<IActionResult> GetPerfil()
{
    // no fa falta que envie jugador, se puede coger con los claims
    var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (userIdString == null) return Unauthorized();
    int userId = int.Parse(userIdString);

    var user = await _db.Users.FindAsync(userId);
    if (user == null) return NotFound();

    return Ok(new {
        id = user.Id,
        username = user.Username,
        money = user.Money,
        level = user.Level
    });
}



}

public record RegisterDto(string Username, string Email, string Password);
public record LoginDto(string Email, string Password);
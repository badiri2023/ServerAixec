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

        var user = new User
        {
            Username = dto.Username,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Ok(new { token = _jwt.GenerateToken(user) });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return Unauthorized("Credenciales incorrectas");

        return Ok(new { token = _jwt.GenerateToken(user) });
    }
    [HttpPost("loginprueba")]
    public async Task<IActionResult> Loginprueba([FromBody] LoginDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return Unauthorized(BCrypt.Net.BCrypt.HashPassword(dto.Password));

        return Ok(new { token = _jwt.GenerateToken(user) });
    }
}

public record RegisterDto(string Username, string Email, string Password);
public record LoginDto(string Email, string Password);
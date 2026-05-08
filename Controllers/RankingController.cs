using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AixecAPI.Data;

namespace AixecAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RankingController : ControllerBase
{
    private readonly AppDbContext _db;

    public RankingController(AppDbContext db)
    {
        _db = db;
    }

[HttpGet]
    public async Task<IActionResult> GetRanking()
    {
        // Cogemos a todos los usuarios, los ordenamos por victorias de mayor a menor
        var ranking = await _db.Users
            .OrderByDescending(u => u.WonMatches)
            .Select(u => new {
                Username = u.Username,
                WonMatches = u.WonMatches,       // Victorias
                PlayedMatches = u.PlayedMatches, // Partidas totales jugadas
                Level = u.Level                  // Nivel actual del jugador
            })
            .ToListAsync();

        return Ok(ranking);
    }
}
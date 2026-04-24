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

    // Top 10 jugadores por puntuación
    [HttpGet]
    public async Task<IActionResult> GetRanking()
    {
        var ranking = await _db.GamePlayers
            .Include(gp => gp.User)
            .GroupBy(gp => gp.User.Username)
            .Select(g => new {
                Username = g.Key,
                TotalScore = g.Sum(gp => gp.Score),
                MaxLevel = g.Max(gp => gp.Level)
            })
            .OrderByDescending(r => r.TotalScore)
            .Take(10)
            .ToListAsync();

        return Ok(ranking);
    }
}
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
public class CardController : ControllerBase
{
    private readonly AppDbContext _db;

    public CardController(AppDbContext db)
    {
        _db = db;
    }

    // Ver mis cartas
    [HttpGet("my-cards")]
    public async Task<IActionResult> GetMyCards()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var cards = await _db.PlayerCards
            .Include(pc => pc.Card)
            .Where(pc => pc.UserId == userId)
            .Select(pc => new {
                pc.Card.Id,
                pc.Card.Name,
                pc.Card.Description,
                pc.Card.Type,
                pc.Card.Attack,
                pc.Card.Defense,
                pc.Card.Rarity,
                pc.Card.Ability,
                pc.Card.Expansion,
                pc.Quantity
            })
            .ToListAsync();

        return Ok(cards);
    }

    // Dar una carta a un jugador (para admins o lógica de juego)
    [HttpPost("give")]
    public async Task<IActionResult> GiveCard([FromBody] GiveCardDto dto)
    {
        var existing = await _db.PlayerCards
            .FirstOrDefaultAsync(pc => pc.UserId == dto.UserId && pc.CardId == dto.CardId);

        if (existing != null)
        {
            existing.Quantity++;
        }
        else
        {
            _db.PlayerCards.Add(new PlayerCard
            {
                UserId = dto.UserId,
                CardId = dto.CardId
            });
        }

        await _db.SaveChangesAsync();
        return Ok();
    }
}

public record GiveCardDto(int UserId, int CardId);
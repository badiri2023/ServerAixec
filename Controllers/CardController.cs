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

    // GET api/card/1 
    [AllowAnonymous]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetCard(int id)
    {
        var card = await _db.Cards
            .Include(c => c.Ability)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (card == null) return NotFound($"Carta con Id {id} no encontrada");

        return Ok(card);
    }

    // GET api/card
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetAllCards()
    {
        var cards = await _db.Cards
            .Include(c => c.Ability)
            .Where(c => c.Id != 50)
            .ToListAsync();

        return Ok(cards);
    }

    // Ver mis cartas (PROTEGIDO)
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
                pc.Card.Mana,
                pc.Card.Expansion,
                Ability = new { Name = pc.Card.Ability != null ? pc.Card.Ability.Name : "" },
                pc.Quantity
            })
            .ToListAsync();

        return Ok(cards);
    }

    // Dar una carta a un jugador (PROTEGIDO)
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

    // GET api/card/open/{expansion} — Abrir sobre (PROTEGIDO)
    [HttpGet("open/{expansion}")]
    public async Task<IActionResult> OpenPack(string expansion)
    {
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userIdString == null) return Unauthorized();
        int userId = int.Parse(userIdString);

        var user = await _db.Users.FindAsync(userId);
        int precioSobre = 100;

        if (user == null)
            return BadRequest("El usuario no existe.");

        if (user.Money < precioSobre)
            return BadRequest("No tienes suficiente dinero.");

        var cards = await _db.Cards.Include(c => c.Ability).Where(c => c.Expansion == expansion || c.Expansion == "Equipamiento" || c.Expansion == "Magias").ToListAsync();
        if (!cards.Any()) return NotFound("No hay cartas en esta expansión.");

        var random = new Random();
        var outputCards = new List<Card>();
        for (int i = 0; i < 3; i++)
        {
            double roll = random.NextDouble() * 100;
            double secondRoll = random.NextDouble() * 100;

            Card picked;
            if (roll < 10)
                if (secondRoll < 15)
                    picked = cards.Where(c => c.Rarity == 3).Where(c => c.Expansion == "Equipamiento" || c.Expansion == "Magias").OrderBy(x => Guid.NewGuid()).FirstOrDefault() ?? cards[0];
                else
                    picked = cards.Where(c => c.Rarity == 3).Where(c => c.Expansion == expansion).OrderBy(x => Guid.NewGuid()).FirstOrDefault() ?? cards[0];

            else if (roll < 40)
                if (secondRoll < 15)
                    picked = cards.Where(c => c.Rarity == 2).Where(c => c.Expansion == "Equipamiento" || c.Expansion == "Magias").OrderBy(x => Guid.NewGuid()).FirstOrDefault() ?? cards[0];
                else
                    picked = cards.Where(c => c.Rarity == 2).Where(c => c.Expansion == expansion).OrderBy(x => Guid.NewGuid()).FirstOrDefault() ?? cards[0];
            else
                if (secondRoll < 15)
                    picked = cards.Where(c => c.Rarity == 1).Where(c => c.Expansion == "Equipamiento" || c.Expansion == "Magias").OrderBy(x => Guid.NewGuid()).FirstOrDefault() ?? cards[0];
                else
                    picked = cards.Where(c => c.Rarity == 1).Where(c => c.Expansion == expansion).OrderBy(x => Guid.NewGuid()).FirstOrDefault() ?? cards[0];

            outputCards.Add(picked);
        }

        user.Money -= precioSobre;

        foreach (var c in outputCards)
        {
            var inventario = await _db.PlayerCards
                .FirstOrDefaultAsync(pc => pc.UserId == userId && pc.CardId == c.Id);

            if (inventario != null) inventario.Quantity++;
            else
            {
                _db.PlayerCards.Add(new PlayerCard { UserId = userId, CardId = c.Id, Quantity = 1 });
            }
        }

        await _db.SaveChangesAsync();
        return Ok(outputCards);
    }
}

public record GiveCardDto(int UserId, int CardId);

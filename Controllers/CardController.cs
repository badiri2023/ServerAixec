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
    [HttpGet]
    public async Task<IActionResult> GetAllCards()
    {
        var cards = await _db.Cards
            .Include(c => c.Ability)
            .ToListAsync();

        return Ok(cards);
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
                // hemos añadido el maná y la expansion añadiendo el nombre d ela hbilidad
                pc.Card.Mana,     
                pc.Card.Expansion,
                Ability = new {   
                    Name = pc.Card.Ability != null ? pc.Card.Ability.Name : "" 
                },
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
    // GET api/card/open/{expansion} — Abrir sobre
    [HttpGet("open/{expansion}")]
    public async Task<IActionResult> OpenPack(string expansion)
    {
        var cards = await _db.Cards
            .Where(c => c.Expansion == expansion)
            .ToListAsync();

        if (cards.Count == 0)
            return NotFound("No se encontraron cartas para esta expansión");

        var commons = cards.Where(c => c.Rarity == 1).ToList();
        var uncommons = cards.Where(c => c.Rarity == 2).ToList();
        var legendaries = cards.Where(c => c.Rarity == 3).ToList();

        var random = new Random();
        var result = new List<Card>();

        for (int i = 0; i < 3; i++)
        {
            var roll = random.NextDouble() * 100;

            List<Card> pool;
            if (roll < 10 && legendaries.Count > 0)
                pool = legendaries;
            else if (roll < 40 && uncommons.Count > 0)
                pool = uncommons;
            else if (commons.Count > 0)
                pool = commons;
            else
                pool = cards; // fallback si no hay cartas de esa rareza

            var card = pool[random.Next(pool.Count)];
            result.Add(card);
        }

        return Ok(result);
    }
}


public record GiveCardDto(int UserId, int CardId);
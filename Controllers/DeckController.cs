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
public class DeckController : ControllerBase
{
    private readonly AppDbContext _db;

    public DeckController(AppDbContext db)
    {
        _db = db;
    }

    // GET api/deck — Mis decks
    [HttpGet]
    public async Task<IActionResult> GetMyDecks()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var decks = await _db.Decks
            .Include(d => d.DeckCards)
                .ThenInclude(dc => dc.Card)
            .Where(d => d.UserId == userId)
            .Select(d => new {
                d.Id,
                d.Name,
                CardCount = d.DeckCards.Count,
                Cards = d.DeckCards.Select(dc => new {
                    dc.Card.Id,
                    dc.Card.Name,
                    dc.Card.Type,
                    dc.Card.Rarity
                })
            })
            .ToListAsync();

        return Ok(decks);
    }

    // GET api/deck/1 — Deck por Id
    [HttpGet("{id}")]
    public async Task<IActionResult> GetDeck(int id)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var deck = await _db.Decks
            .Include(d => d.DeckCards)
                .ThenInclude(dc => dc.Card)
                    .ThenInclude(c => c.Ability)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (deck == null) return NotFound("Deck no encontrado");
        if (deck.UserId != userId) return Forbid();

        return Ok(deck);
    }
    // GET api/firstDeck — Deck por Id
    [HttpGet("{firstDeck}")]
    public async Task<IActionResult> GetFirstDeck(int id)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var deck = await _db.Decks
            .Include(d => d.DeckCards)
                .ThenInclude(dc => dc.Card)
                    .ThenInclude(c => c.Ability)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (deck == null) return NotFound("Deck no encontrado");
        if (deck.UserId != userId) return Forbid();

        return Ok(deck);
    }


    // POST api/deck — Crear deck
    [HttpPost]
    public async Task<IActionResult> CreateDeck([FromBody] CreateDeckDto dto)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        if (dto.CardIds == null || dto.CardIds.Count == 0)
            return BadRequest("El deck debe tener al menos una carta");

        var deck = new Deck
        {
            UserId = userId,
            Name = dto.Name,
            DeckCards = dto.CardIds.Select(cardId => new DeckCard
            {
                CardId = cardId
            }).ToList()
        };

        _db.Decks.Add(deck);
        await _db.SaveChangesAsync();

        return Ok(new { deck.Id, deck.Name, CardCount = deck.DeckCards.Count });
    }

    // PUT api/deck/1 — Editar deck
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateDeck(int id, [FromBody] CreateDeckDto dto)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var deck = await _db.Decks
            .Include(d => d.DeckCards)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (deck == null) return NotFound("Deck no encontrado");
        if (deck.UserId != userId) return Forbid();

        // Actualizar nombre
        deck.Name = dto.Name;

        // Reemplazar cartas
        _db.DeckCards.RemoveRange(deck.DeckCards);
        deck.DeckCards = dto.CardIds.Select(cardId => new DeckCard
        {
            CardId = cardId,
            DeckId = id
        }).ToList();

        await _db.SaveChangesAsync();

        return Ok(new { deck.Id, deck.Name, CardCount = deck.DeckCards.Count });
    }

    // DELETE api/deck/1 — Eliminar deck
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDeck(int id)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var deck = await _db.Decks
            .Include(d => d.DeckCards)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (deck == null) return NotFound("Deck no encontrado");
        if (deck.UserId != userId) return Forbid();

        _db.Decks.Remove(deck);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Deck eliminado correctamente" });
    }
}

public record CreateDeckDto(string Name, List<int> CardIds);
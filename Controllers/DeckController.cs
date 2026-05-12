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
            .Where(d => d.Id == id)
            .Select(d => new {
                d.Id,
                d.Name,
                Cards = d.DeckCards.Select(dc => new {
                    dc.Card.Id,
                    dc.Card.Name,
                    dc.Card.Type,
                    dc.Card.Attack,
                    dc.Card.Defense,
                    dc.Card.Rarity,
                    dc.Card.Mana,
                    dc.Card.Expansion,
                    dc.Card.ImageUrl,
                    Ability = dc.Card.Ability == null ? null : new {
                        dc.Card.Ability.Id,
                        dc.Card.Ability.Name,
                        dc.Card.Ability.Description
                    }
                })
            })
            .FirstOrDefaultAsync();

        if (deck == null) return NotFound("Deck no encontrado");

        return Ok(deck);
    }
    // POST api/deck/generate — Generar mazo automático
    [HttpPost("generate")]
    public async Task<IActionResult> GenerateDeck([FromBody] GenerateDeckDto dto)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var fixedCardIds = new List<int> { 1, 2, 5, 6, 9, 15, 16, 21, 24, 37, 39, 36 };

        var allCards = await _db.Cards
            .Where(c => c.Id != 0 && !fixedCardIds.Contains(c.Id))
            .ToListAsync();

        var random = new Random();
        var randomCards = allCards
            .OrderBy(_ => random.Next())
            .Take(8)
            .Select(c => c.Id)
            .ToList();

        var allCardIds = fixedCardIds.Concat(randomCards).ToList();

        var deck = new Deck
        {
            UserId = userId,
            Name = dto.Name,
            DeckCards = allCardIds.Select(cardId => new DeckCard
            {
                CardId = cardId
            }).ToList()
        };
        

        _db.Decks.Add(deck);

        await _db.SaveChangesAsync();

        var result = await _db.Decks
            .Include(d => d.DeckCards)
                .ThenInclude(dc => dc.Card)
            .Where(d => d.Id == deck.Id)
            .Select(d => new {
                d.Id,
                d.Name,
                Cards = d.DeckCards.Select(dc => new {
                    dc.Card.Id,
                    dc.Card.Name,
                    dc.Card.Type,
                    dc.Card.Attack,
                    dc.Card.Defense,
                    dc.Card.Rarity,
                    dc.Card.Mana,
                    dc.Card.Expansion
                })
            })
            .FirstOrDefaultAsync();

        return Ok(result);
    }

    public record GenerateDeckDto(string Name);

    // GET api/deck/{id}/info — Info del mazo y si el jugador ha jugado partidas
    [HttpGet("{id}/info")]
    public async Task<IActionResult> GetDeckInfo(int id)
    {
        var deck = await _db.Decks
            .Include(d => d.DeckCards)
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (deck == null) return NotFound("Deck no encontrado");

        var cardIds = deck.DeckCards.Select(dc => dc.CardId).ToList();
        var hasPlayedBefore = deck.User.PlayedMatches > 0;

        return Ok(new
        {
            deckId = deck.Id,
            cardIds,
            hasPlayedBefore
        });
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

        deck.Name = dto.Name;
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
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
        //Obtener ID del usuario del Token
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userIdString == null) return Unauthorized();
        int userId = int.Parse(userIdString);

        //Buscar al usuario y verificar dinero
        var user = await _db.Users.FindAsync(userId);
        int precioSobre = 100; // Puedes cambiar esto según la expansión

        if (user == null || user.Money < precioSobre)
            return BadRequest("No tienes suficiente dinero o el usuario no existe.");

        // 3. Obtener cartas de esa expansión
        var cards = await _db.Cards.Where(c => c.Expansion == expansion).ToListAsync();
        if (!cards.Any()) return NotFound("No hay cartas en esta expansión.");

        // 4. Lógica de Gacha (Probabilidades)
        var random = new Random();
        var outputCards = new List<Card>();
        for (int i = 0; i < 3; i++)
        {
            double roll = random.NextDouble() * 100;
            Card picked;
            if (roll < 10) // 10% Legendaria (Rarity 3)
                picked = cards.Where(c => c.Rarity == 3).OrderBy(x => Guid.NewGuid()).FirstOrDefault() ?? cards[0];
            else if (roll < 40) 
                picked = cards.Where(c => c.Rarity == 2).OrderBy(x => Guid.NewGuid()).FirstOrDefault() ?? cards[0];
            else 
                picked = cards.Where(c => c.Rarity == 1).OrderBy(x => Guid.NewGuid()).FirstOrDefault() ?? cards[0];
            
            outputCards.Add(picked);
        }

        // PERSISTENCIA: Cobrar y Guardar en Inventario
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
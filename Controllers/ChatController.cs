using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AixecAPI.Data;
using AixecAPI.Models;

namespace AixecAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ChatController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory()
        {
            // se cogen los ultimos cien mensajes ordenados por fecha ascendente
            var mensajes = await _db.ChatMessages
                .OrderByDescending(m => m.CreatedAt)
                .Take(100)
                .OrderBy(m => m.CreatedAt) 
                .ToListAsync();

            return Ok(mensajes);
        }
    }
}
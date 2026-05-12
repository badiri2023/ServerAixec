using Microsoft.AspNetCore.SignalR;
using AixecAPI.Data;   // Necesario para acceder a AppDbContext
using AixecAPI.Models; // Necesario para acceder a ChatMessage

namespace AixecAPI.Hubs
{
    public class ChatHub : Hub
    {
        private readonly AppDbContext _db;

        // Inyectamos la base de datos en el constructor
        public ChatHub(AppDbContext db)
        {
            _db = db;
        }

        public async Task EnviarMensaje(string usuario, string texto)
        {
            try
            {
                var nuevoMensaje = new ChatMessage
                {
                    Username = usuario,
                    Text = texto,
                    CreatedAt = DateTime.UtcNow
                };

                _db.ChatMessages.Add(nuevoMensaje);
                await _db.SaveChangesAsync();

                await Clients.All.SendAsync("RecibirMensaje", usuario, texto);
            }
            catch (Exception ex)
            {
                // Esto imprimirá el error exacto en la consola de tu API
                Console.WriteLine($"🔥🔥 ERROR AL GUARDAR EL MENSAJE: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"🔥🔥 DETALLE INTERNO: {ex.InnerException.Message}");
                }
            }
        }
    }
}
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
            //CREAMOS EL REGISTRO PARA LA BASE DE DATOS
            var nuevoMensaje = new ChatMessage 
            { 
                Username = usuario, 
                Text = texto,
                CreatedAt = DateTime.UtcNow 
            };

            //LO GUARDAMOS (Esto es lo que da la PERSISTENCIA)
            _db.ChatMessages.Add(nuevoMensaje);
            await _db.SaveChangesAsync();

            //REENVIAMOS A TODOS
            await Clients.All.SendAsync("RecibirMensaje", usuario, texto);
        }
    }
}
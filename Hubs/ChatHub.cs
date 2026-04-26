using Microsoft.AspNetCore.SignalR;

namespace AixecAPI.Hubs
{
    public class ChatHub : Hub
    {
        // El cliente Flutter llamará a este método
        public async Task EnviarMensaje(string usuario, string texto)
        {
            // Reenviamos el mensaje a todos los conectados
            await Clients.All.SendAsync("RecibirMensaje", usuario, texto);
        }
    }
}
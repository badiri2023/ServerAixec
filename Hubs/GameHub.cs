using Microsoft.AspNetCore.SignalR;

namespace AixecAPI.Hubs;

public class GameHub : Hub
{
    // El cliente envía su posición y se la mandamos a todos
    public async Task SendPosition(string userId, float x, float y)
    {
        await Clients.Others.SendAsync("ReceivePosition", userId, x, y);
    }

    // Mensaje de chat o evento de juego
    public async Task SendGameEvent(string eventType, string data)
    {
        await Clients.All.SendAsync("ReceiveGameEvent", eventType, data);
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Others.SendAsync("PlayerConnected", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Clients.Others.SendAsync("PlayerDisconnected", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

// para el chat global
    public async Task SendChatMessage(object messageData)
    {
        // Retransmite el objeto tal cual a todos los conectados
        await Clients.All.SendAsync("ReceiveMessage", messageData);
    }


}
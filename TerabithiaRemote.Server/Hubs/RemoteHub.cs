using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using TerabithiaRemote.Shared.Dtos;
using TerabithiaRemote.Server.Input;

namespace TerabithiaRemote.Server.Hubs;

public class RemoteHub : Hub
{
    // Hangi ConnectionId hangi ID'ye sahip?
    private static readonly ConcurrentDictionary<string, string> _sessions = new();

    // Rastgele 6 haneli ID üretir
    private static readonly string _serverGuid = Random.Shared.Next(100000, 999999).ToString();

    public override async Task OnConnectedAsync()
    {
        // Bağlanan kişiye kendi numarasını söyle
        await Clients.Caller.SendAsync("ServerInfo", _serverGuid);
        await base.OnConnectedAsync();
    }

    public async Task JoinSession(string targetId)
    {
        if (targetId == _serverGuid)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, targetId);
            await Clients.Caller.SendAsync("JoinStatus", true);
        }
        else
        {
            await Clients.Caller.SendAsync("JoinStatus", false);
        }
    }

    // Input metodlarını güncelleyelim (Sadece ilgili gruba gitmesi için ileride lazım olacak)
    public Task SendMouseInput(MouseInputDto dto)
    {
        InputSimulator.ApplyMouse(dto);
        return Task.CompletedTask;
    }
}
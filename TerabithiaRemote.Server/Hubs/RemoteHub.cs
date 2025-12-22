using Microsoft.AspNetCore.SignalR;
using TerabithiaRemote.Shared.Dtos;
using TerabithiaRemote.Server.Input;
namespace TerabithiaRemote.Server.Hubs;

public class RemoteHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("ServerHello", "TerabithiaRemote.Server connected");
        await base.OnConnectedAsync();
    }
    public Task SendMouseInput(MouseInputDto dto)
    {
        InputSimulator.ApplyMouse(dto);
        return Task.CompletedTask;
    }

    public Task SendKeyboardInput(KeyboardInputDto dto)
    {
        InputSimulator.ApplyKeyboard(dto);
        return Task.CompletedTask;
    }
}

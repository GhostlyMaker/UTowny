using OpenMod.API.Eventing;using OpenMod.Unturned.Players.Connections.Events;using UTowny.Domain.Common;using UTowny.Services;
namespace UTowny.Events;
public sealed class PlayerConnectionListener:IEventListener<UnturnedPlayerConnectedEvent>,IEventListener<UnturnedPlayerDisconnectedEvent>
{
 private readonly IPlaytimeService m_Playtime;public PlayerConnectionListener(IPlaytimeService p)=>m_Playtime=p;
 public Task HandleEventAsync(object? sender,UnturnedPlayerConnectedEvent e)=>m_Playtime.PlayerConnectedAsync(new PlayerId(e.Player.SteamId.m_SteamID));
 public Task HandleEventAsync(object? sender,UnturnedPlayerDisconnectedEvent e)=>m_Playtime.PlayerDisconnectedAsync(new PlayerId(e.Player.SteamId.m_SteamID));
}

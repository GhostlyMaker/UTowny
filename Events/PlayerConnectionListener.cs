using OpenMod.API.Eventing;using OpenMod.Unturned.Players.Connections.Events;using UTowny.Domain.Common;using UTowny.Services;using UTowny.Protection;
namespace UTowny.Events;
public sealed class PlayerConnectionListener:IEventListener<UnturnedPlayerConnectedEvent>,IEventListener<UnturnedPlayerDisconnectedEvent>
{
 private readonly PlotSelectionService m_Selections;
 private readonly IPlaytimeService m_Playtime;private readonly ProtectionService m_Protection;private readonly BackgroundQueue m_Queue;
 public PlayerConnectionListener(IPlaytimeService playtime,ProtectionService protection,BackgroundQueue queue,PlotSelectionService selections){m_Selections=selections;m_Playtime=playtime;m_Protection=protection;m_Queue=queue;}
 public Task HandleEventAsync(object? sender,UnturnedPlayerConnectedEvent e)
 {var id=new PlayerId(e.Player.SteamId.m_SteamID);if(m_Protection.Ready)m_Queue.Enqueue(()=>m_Playtime.PlayerConnectedAsync(id));return Task.CompletedTask;}
 public Task HandleEventAsync(object? sender,UnturnedPlayerDisconnectedEvent e)
 {var id=new PlayerId(e.Player.SteamId.m_SteamID);m_Selections.Clear(id);m_Protection.SetBypass(id,false);if(m_Protection.Ready)m_Queue.Enqueue(()=>m_Playtime.PlayerDisconnectedAsync(id));return Task.CompletedTask;}
}


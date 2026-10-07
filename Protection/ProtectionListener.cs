using OpenMod.API.Eventing;
using OpenMod.Unturned.Building.Events;
using OpenMod.Unturned.Players.Life.Events;
using OpenMod.Unturned.Vehicles.Events;
using SDG.Unturned;
using UTowny.Domain.Common;
using UTowny.Services;
namespace UTowny.Protection;
public sealed class ProtectionListener :
 IEventListener<UnturnedBuildableDeployingEvent>,IEventListener<UnturnedBuildableDamagingEvent>,
 IEventListener<UnturnedBuildableSalvagingEvent>,IEventListener<UnturnedBuildableTransformingEvent>,
 IEventListener<UnturnedStorageOpeningEvent>,IEventListener<UnturnedPlantHarvestingEvent>,IEventListener<UnturnedSignModifyingEvent>,
 IEventListener<UnturnedVehicleDamagingEvent>,IEventListener<UnturnedPlayerEnteringVehicleEvent>,
 IEventListener<UnturnedVehicleSiphoningEvent>,IEventListener<UnturnedVehicleCarjackingEvent>,IEventListener<UnturnedVehicleLockpickingEvent>,IEventListener<UnturnedVehicleStealingBatteryEvent>,IEventListener<UnturnedPlayerDamagingEvent>
{
 private readonly TeleportService m_Teleport;
 private readonly IProtectionService m_Protection;private readonly IGridService m_Grid;
 public ProtectionListener(IProtectionService protection,IGridService grid,TeleportService teleport){m_Teleport=teleport;m_Protection=protection;m_Grid=grid;}
 private bool Allowed(ulong player,float x,float z,LandAction action)=>m_Protection.Can(new PlayerId(player),m_Grid.FromWorld(Level.info.name,x,z),action);
 public Task HandleEventAsync(object? sender,UnturnedBuildableDeployingEvent e){e.IsCancelled|=!Allowed(e.Owner,e.Point.x,e.Point.z,LandAction.Build);return Task.CompletedTask;}
 public Task HandleEventAsync(object? sender,UnturnedBuildableDamagingEvent e){var p=e.Buildable.Transform.Position;e.IsCancelled|=!Allowed(e.Instigator?.SteamId.m_SteamID??0,p.X,p.Z,LandAction.Damage);return Task.CompletedTask;}
 public Task HandleEventAsync(object? sender,UnturnedBuildableSalvagingEvent e){var p=e.Buildable.Transform.Position;e.IsCancelled|=!Allowed(e.Instigator?.SteamId.m_SteamID??0,p.X,p.Z,LandAction.Salvage);return Task.CompletedTask;}
 public Task HandleEventAsync(object? sender,UnturnedBuildableTransformingEvent e){var p=e.Buildable.Transform.Position;var id=e.Instigator?.SteamId.m_SteamID??0;e.IsCancelled|=!Allowed(id,p.X,p.Z,LandAction.Build)||!Allowed(id,e.Point.x,e.Point.z,LandAction.Build);return Task.CompletedTask;}
 public Task HandleEventAsync(object? sender,UnturnedStorageOpeningEvent e){var p=e.Buildable.Transform.Position;e.IsCancelled|=!Allowed(e.Instigator?.SteamId.m_SteamID??0,p.X,p.Z,LandAction.Interact);return Task.CompletedTask;}
 public Task HandleEventAsync(object? sender,UnturnedPlantHarvestingEvent e){var p=e.Buildable.Transform.Position;e.IsCancelled|=!Allowed(e.Instigator?.SteamId.m_SteamID??0,p.X,p.Z,LandAction.Interact);return Task.CompletedTask;}
 public Task HandleEventAsync(object? sender,UnturnedSignModifyingEvent e){var p=e.Buildable.Transform.Position;e.IsCancelled|=!Allowed(e.Instigator?.SteamId.m_SteamID??0,p.X,p.Z,LandAction.Interact);return Task.CompletedTask;}
 public Task HandleEventAsync(object? sender,UnturnedVehicleDamagingEvent e){var p=e.Vehicle.Transform.Position;e.IsCancelled|=!Allowed(e.Instigator?.m_SteamID??0,p.X,p.Z,LandAction.Damage);return Task.CompletedTask;}
 public Task HandleEventAsync(object? sender,UnturnedPlayerEnteringVehicleEvent e){var p=e.Vehicle.Transform.Position;e.IsCancelled|=!Allowed(e.Player.SteamId.m_SteamID,p.X,p.Z,LandAction.Vehicle);return Task.CompletedTask;}
 public Task HandleEventAsync(object? sender,UnturnedVehicleSiphoningEvent e){var p=e.Vehicle.Transform.Position;e.IsCancelled|=!Allowed(e.Instigator.SteamId.m_SteamID,p.X,p.Z,LandAction.Vehicle);return Task.CompletedTask;}
 public Task HandleEventAsync(object? sender,UnturnedVehicleCarjackingEvent e){var p=e.Vehicle.Transform.Position;e.IsCancelled|=!Allowed(e.Instigator.SteamId.m_SteamID,p.X,p.Z,LandAction.Vehicle);return Task.CompletedTask;}
 public Task HandleEventAsync(object? sender,UnturnedVehicleLockpickingEvent e){var p=e.Vehicle.Transform.Position;e.IsCancelled|=!Allowed(e.Instigator.SteamId.m_SteamID,p.X,p.Z,LandAction.Vehicle);return Task.CompletedTask;}
 public Task HandleEventAsync(object? sender,UnturnedVehicleStealingBatteryEvent e){var p=e.Vehicle.Transform.Position;e.IsCancelled|=!Allowed(e.Instigator.SteamId.m_SteamID,p.X,p.Z,LandAction.Vehicle);return Task.CompletedTask;}
 public Task HandleEventAsync(object? sender,UnturnedPlayerDamagingEvent e){if(!e.IsCancelled&&e.DamageAmount>0){m_Teleport.Damaged(e.Player.SteamId.m_SteamID);if(e.Killer.m_SteamID!=0)m_Teleport.Damaged(e.Killer.m_SteamID);}var p=e.Player.Transform.Position;e.IsCancelled|=!m_Protection.CanPvp(new(e.Killer.m_SteamID),new(e.Player.SteamId.m_SteamID),m_Grid.FromWorld(Level.info.name,p.X,p.Z));return Task.CompletedTask;}
}

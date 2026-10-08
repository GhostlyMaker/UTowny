using Autofac;
using OpenMod.API.Ioc;
using OpenMod.API.Plugins;
using Microsoft.Extensions.DependencyInjection;
using UTowny.Domain.Claims;using UTowny.Domain.Common;using UTowny.Domain.Towns;using UTowny.Plugin;using UTowny.Protection;using UTowny.Services;
namespace UTowny.Api;
// A global forwarding service resolves the current plugin instance at call time,
// rather than retaining a disposed plugin scope across reloads.
[ServiceImplementation(Lifetime=ServiceLifetime.Singleton)]
public sealed class GlobalTownyApi:IUTownyApi
{
 private readonly Lazy<IPluginAccessor<UTownyPlugin>> m_Accessor;
 public GlobalTownyApi(Lazy<IPluginAccessor<UTownyPlugin>> accessor)=>m_Accessor=accessor;
 private IUTownyApi Current
 {
  get
  {
   var plugin=m_Accessor.Value.Instance??throw new InvalidOperationException("UTowny is not loaded");
   if(!plugin.Ready)throw new InvalidOperationException("UTowny is not ready");
   return plugin.LifetimeScope.Resolve<UTownyApi>();
  }
 }
 public ITownService Towns=>Current.Towns;public IClaimService Claims=>Current.Claims;public IEconomyService Economy=>Current.Economy;public IAuthorizationService Authorization=>Current.Authorization;
 public INationService Nations=>Current.Nations;public IWarService Wars=>Current.Wars;public IProtectionService Protection=>Current.Protection;public ITownManagementService Management=>Current.Management;
 public Town? GetTown(PlayerId player)=>Current.GetTown(player);public Claim? GetClaimAt(GridCoord grid)=>Current.GetClaimAt(grid);public bool IsPlayerResident(PlayerId player,TownId town)=>Current.IsPlayerResident(player,town);public TownRole? GetPlayerRole(PlayerId player)=>Current.GetPlayerRole(player);
}

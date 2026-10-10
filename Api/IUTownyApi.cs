using UTowny.Domain.Claims;using UTowny.Domain.Common;using UTowny.Domain.Towns;using UTowny.Services;
namespace UTowny.Api;
[OpenMod.API.Ioc.Service]
public interface IUTownyApi
{
 IPlotService Plots {get;} IPlaytimeService Playtime {get;}
 INationService Nations {get;} IWarService Wars {get;} UTowny.Protection.IProtectionService Protection {get;} ITownManagementService Management {get;}
 ITownService Towns { get; } IClaimService Claims { get; } IEconomyService Economy { get; } IAuthorizationService Authorization { get; }
 Town? GetTown(PlayerId player); Claim? GetClaimAt(GridCoord grid); bool IsPlayerResident(PlayerId player,TownId town); TownRole? GetPlayerRole(PlayerId player);
}
public sealed class UTownyApi:IUTownyApi
{
 public IPlotService Plots {get;} public IPlaytimeService Playtime {get;}
 public INationService Nations {get;} public IWarService Wars {get;} public UTowny.Protection.IProtectionService Protection {get;} public ITownManagementService Management {get;}
 public ITownService Towns{get;}public IClaimService Claims{get;}public IEconomyService Economy{get;}public IAuthorizationService Authorization{get;}
 public UTownyApi(ITownService t,IClaimService c,IEconomyService e,IAuthorizationService a,INationService nations,IWarService wars,UTowny.Protection.IProtectionService protection,ITownManagementService management,IPlotService plots,IPlaytimeService playtime){Plots=plots;Playtime=playtime;Nations=nations;Wars=wars;Protection=protection;Management=management;Towns=t;Claims=c;Economy=e;Authorization=a;}
 public Town? GetTown(PlayerId p)=>Towns.GetTown(p);public Claim? GetClaimAt(GridCoord g)=>Claims.Get(g);public bool IsPlayerResident(PlayerId p,TownId t)=>Towns.GetTown(p)?.Id==t;public TownRole? GetPlayerRole(PlayerId p)=>Towns.GetRole(p);
}

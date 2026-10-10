using Microsoft.Extensions.Options;
using UTowny.Caching;using UTowny.Configuration;using UTowny.Domain.Common;using UTowny.Domain.Towns;
namespace UTowny.Services;
public enum TownAction { Invite,Kick,Promote,Demote,Claim,ManagePlot,SetSpawn,SetPvp,SetTax,Deposit,Disband,TransferMayor }
public interface IAuthorizationService { bool Can(PlayerId player,TownId town,TownAction action); }
public sealed class AuthorizationService:IAuthorizationService
{
 private readonly IWorldStateCache m_Cache; private readonly IOptions<UTownyOptions> m_Options; public AuthorizationService(IWorldStateCache c,IOptions<UTownyOptions> o){m_Cache=c;m_Options=o;}
 public bool Can(PlayerId p,TownId t,TownAction a){var m=m_Cache.GetMembership(p);if(m is null||m.TownId!=t)return false;if(a==TownAction.Deposit)return true;if(m.Role==TownRole.Mayor)return true;if(m.Role!=TownRole.CoMayor)return false;return a switch{TownAction.Disband=>m_Options.Value.Towns.CoMayorCanDisband,TownAction.TransferMayor=>m_Options.Value.Towns.CoMayorCanTransferMayor,_=>true};}
}

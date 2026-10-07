using OpenMod.API.Commands;using OpenMod.Core.Commands;using OpenMod.Unturned.Users;using UTowny.Domain.Common;using UTowny.Services;
namespace UTowny.Commands;
[Command("leave")][CommandParent(typeof(TownCommand))][CommandDescription("Leaves your current town.")][CommandActor(typeof(UnturnedUser))]
public sealed class TownLeaveCommand:Command
{
 private readonly ITownService m_Towns;public TownLeaveCommand(IServiceProvider sp,ITownService towns):base(sp)=>m_Towns=towns;
 protected override async Task OnExecuteAsync(){var u=(UnturnedUser)Context.Actor;var r=await m_Towns.LeaveAsync(new PlayerId(u.Player.SteamId.m_SteamID));if(!r.Success)throw new UserFriendlyException($"Leave failed: {r.Code}");await PrintAsync("You left your town.");}
}

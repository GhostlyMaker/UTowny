using OpenMod.API.Commands;using OpenMod.Core.Commands;using OpenMod.Unturned.Users;using UTowny.Domain.Common;using UTowny.Services;
namespace UTowny.Commands;
[Command("balance")][CommandAlias("bal")][CommandDescription("Shows your UTowny balance.")][CommandActor(typeof(UnturnedUser))]
public sealed class BalanceCommand:Command
{
 private readonly IEconomyService m_Economy;public BalanceCommand(IServiceProvider sp,IEconomyService e):base(sp)=>m_Economy=e;
 protected override async Task OnExecuteAsync(){var u=(UnturnedUser)Context.Actor;var b=await m_Economy.GetBalanceAsync(new PlayerId(u.Player.SteamId.m_SteamID));await PrintAsync($"UTowny balance: {b}");}
}

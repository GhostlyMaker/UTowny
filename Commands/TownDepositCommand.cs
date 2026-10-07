using OpenMod.API.Commands;using OpenMod.Core.Commands;using OpenMod.Unturned.Users;using UTowny.Domain.Common;using UTowny.Services;
namespace UTowny.Commands;
[Command("deposit")][CommandParent(typeof(TownCommand))][CommandSyntax("<amount>")][CommandDescription("Deposits virtual currency into your town treasury.")][CommandActor(typeof(UnturnedUser))]
public sealed class TownDepositCommand:Command
{
 private readonly ITownService m_Towns;public TownDepositCommand(IServiceProvider sp,ITownService towns):base(sp)=>m_Towns=towns;
 protected override async Task OnExecuteAsync(){var u=(UnturnedUser)Context.Actor;var amount=await Context.Parameters.GetAsync<long>(0);var r=await m_Towns.DepositAsync(new PlayerId(u.Player.SteamId.m_SteamID),amount);if(!r.Success)throw new UserFriendlyException($"Deposit failed: {r.Code}");await PrintAsync($"Town treasury: {r.Value}");}
}

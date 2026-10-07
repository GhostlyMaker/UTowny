using OpenMod.API.Commands;using OpenMod.Core.Commands;using OpenMod.Unturned.Users;using UTowny.Domain.Common;using UTowny.Services;
namespace UTowny.Commands;
[Command("create")][CommandParent(typeof(TownCommand))][CommandSyntax("<name>")][CommandDescription("Creates a town.")][CommandActor(typeof(UnturnedUser))]
public sealed class TownCreateCommand:Command
{
 private readonly ITownService m_Towns;public TownCreateCommand(IServiceProvider sp,ITownService towns):base(sp)=>m_Towns=towns;
 protected override async Task OnExecuteAsync(){var u=(UnturnedUser)Context.Actor;var name=await Context.Parameters.GetAsync<string>(0);var r=await m_Towns.CreateAsync(new PlayerId(u.Player.SteamId.m_SteamID),name);if(!r.Success)throw new UserFriendlyException($"Town creation failed: {r.Code}");await PrintAsync($"Town {r.Value!.Name} created.");}
}

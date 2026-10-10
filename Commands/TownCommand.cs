using OpenMod.API.Commands;using OpenMod.Core.Commands;
namespace UTowny.Commands;
[Command("town")][CommandAlias("t")][CommandAlias("towny")]
public sealed class TownCommand:Command
{
 private readonly CommandRouter m_Router;
 public TownCommand(IServiceProvider sp,CommandRouter router):base(sp)=>m_Router=router;
 protected override async Task OnExecuteAsync()=>await PrintAsync(await m_Router.ExecuteAsync(Context,"town"),UTowny.Utilities.UTownyChat.Color);
}

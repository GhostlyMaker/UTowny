using OpenMod.API.Commands;using OpenMod.Core.Commands;
namespace UTowny.Commands;
[Command("war")]
public sealed class WarCommand:Command
{
 private readonly CommandRouter m_Router;
 public WarCommand(IServiceProvider sp,CommandRouter router):base(sp)=>m_Router=router;
 protected override async Task OnExecuteAsync()=>await PrintAsync(await m_Router.ExecuteAsync(Context,"war"),UTowny.Utilities.UTownyChat.Color);
}

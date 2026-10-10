using OpenMod.API.Commands;using OpenMod.Core.Commands;
namespace UTowny.Commands;
[Command("utowny")]
public sealed class UtownyCommand:Command
{
 private readonly CommandRouter m_Router;
 public UtownyCommand(IServiceProvider sp,CommandRouter router):base(sp)=>m_Router=router;
 protected override async Task OnExecuteAsync()=>await PrintAsync(await m_Router.ExecuteAsync(Context,"utowny"),UTowny.Utilities.UTownyChat.Color);
}

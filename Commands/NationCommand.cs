using OpenMod.API.Commands;using OpenMod.Core.Commands;
namespace UTowny.Commands;
[Command("nation")][CommandAlias("n")]
public sealed class NationCommand:Command
{
 private readonly CommandRouter m_Router;
 public NationCommand(IServiceProvider sp,CommandRouter router):base(sp)=>m_Router=router;
 protected override async Task OnExecuteAsync()=>await PrintAsync(await m_Router.ExecuteAsync(Context,"nation"));
}

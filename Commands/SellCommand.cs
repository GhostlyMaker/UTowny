using OpenMod.API.Commands;using OpenMod.Core.Commands;
namespace UTowny.Commands;
[Command("sell")]
public sealed class SellCommand:Command
{
 private readonly CommandRouter m_Router;
 public SellCommand(IServiceProvider sp,CommandRouter router):base(sp)=>m_Router=router;
 protected override async Task OnExecuteAsync()=>await PrintAsync(await m_Router.ExecuteAsync(Context,"sell"));
}

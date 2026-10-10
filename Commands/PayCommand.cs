using OpenMod.API.Commands;using OpenMod.Core.Commands;
namespace UTowny.Commands;
[Command("pay")]
public sealed class PayCommand:Command
{
 private readonly CommandRouter m_Router;
 public PayCommand(IServiceProvider sp,CommandRouter router):base(sp)=>m_Router=router;
 protected override async Task OnExecuteAsync()=>await PrintAsync(await m_Router.ExecuteAsync(Context,"pay"),UTowny.Utilities.UTownyChat.Color);
}

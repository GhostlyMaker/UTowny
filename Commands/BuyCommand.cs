using OpenMod.API.Commands;using OpenMod.Core.Commands;
namespace UTowny.Commands;
[Command("buy")]
public sealed class BuyCommand:Command
{
 private readonly CommandRouter m_Router;
 public BuyCommand(IServiceProvider sp,CommandRouter router):base(sp)=>m_Router=router;
 protected override async Task OnExecuteAsync()=>await PrintAsync(await m_Router.ExecuteAsync(Context,"buy"),UTowny.Utilities.UTownyChat.Color);
}

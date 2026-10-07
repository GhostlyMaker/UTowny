using OpenMod.API.Commands;using OpenMod.Core.Commands;
namespace UTowny.Commands;
[Command("balance")][CommandAlias("bal")]
public sealed class BalanceCommand:Command
{
 private readonly CommandRouter m_Router;
 public BalanceCommand(IServiceProvider sp,CommandRouter router):base(sp)=>m_Router=router;
 protected override async Task OnExecuteAsync()=>await PrintAsync(await m_Router.ExecuteAsync(Context,"balance"));
}

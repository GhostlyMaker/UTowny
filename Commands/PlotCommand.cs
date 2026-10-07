using OpenMod.API.Commands;using OpenMod.Core.Commands;
namespace UTowny.Commands;
[Command("plot")]
public sealed class PlotCommand:Command
{
 private readonly CommandRouter m_Router;
 public PlotCommand(IServiceProvider sp,CommandRouter router):base(sp)=>m_Router=router;
 protected override async Task OnExecuteAsync()=>await PrintAsync(await m_Router.ExecuteAsync(Context,"plot"));
}

using OpenMod.API.Commands;using OpenMod.Core.Commands;
namespace UTowny.Commands;
[Command("town")][CommandAlias("t")][CommandDescription("UTowny town commands.")]
public sealed class TownCommand:Command{public TownCommand(IServiceProvider sp):base(sp){}protected override Task OnExecuteAsync()=>PrintAsync("Use /t create <name>, /t info, /t deposit <amount>, /t leave.");}

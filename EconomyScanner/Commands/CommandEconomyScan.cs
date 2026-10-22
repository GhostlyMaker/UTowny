using System;
using Cysharp.Threading.Tasks;
using OpenMod.Core.Commands;
using OpenMod.Unturned.Commands;

namespace EconomyScanner.Commands
{
    [Command("economyscan")]
    [CommandAlias("escan")]
    [CommandDescription("Starts a batched economy inventory/storage scan.")]
    public sealed class CommandEconomyScan : UnturnedCommand
    {
        public CommandEconomyScan(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        protected override async UniTask OnExecuteAsync()
        {
            EconomyScannerPlugin plugin = EconomyScannerPlugin.Instance;
            if (plugin == null)
            {
                await PrintAsync("EconomyScanner is not loaded.");
                return;
            }

            string message;
            plugin.TryStartManualScan(out message);
            await PrintAsync(message);
        }
    }
}

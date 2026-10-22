using System;
using Cysharp.Threading.Tasks;
using OpenMod.Core.Commands;
using OpenMod.Unturned.Commands;

namespace EconomyScanner.Commands
{
    [Command("economystatus")]
    [CommandAlias("estatus")]
    [CommandDescription("Shows EconomyScanner status and last-scan performance.")]
    public sealed class CommandEconomyStatus : UnturnedCommand
    {
        public CommandEconomyStatus(IServiceProvider serviceProvider) : base(serviceProvider)
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

            string state = plugin.IsScanning ? "Scanning now. " : "Idle. ";
            await PrintAsync(state + plugin.LastScanSummary);
        }
    }
}

using System.Collections.Generic;
using Rocket.API;
using Rocket.Unturned.Chat;

namespace EconomyScanner.Commands
{
    public sealed class CommandEconomyStatus : IRocketCommand
    {
        public AllowedCaller AllowedCaller { get { return AllowedCaller.Both; } }
        public string Name { get { return "economystatus"; } }
        public string Help { get { return "Shows EconomyScanner status and last-scan performance."; } }
        public string Syntax { get { return ""; } }
        public List<string> Aliases { get { return new List<string> { "estatus" }; } }
        public List<string> Permissions { get { return new List<string> { "economyscanner.status" }; } }

        public void Execute(IRocketPlayer caller, string[] command)
        {
            EconomyScannerPlugin plugin = EconomyScannerPlugin.Instance;
            if (plugin == null)
            {
                UnturnedChat.Say(caller, "EconomyScanner is not loaded.");
                return;
            }

            string state = plugin.IsScanning ? "Scanning now. " : "Idle. ";
            UnturnedChat.Say(caller, state + plugin.LastScanSummary);
        }
    }
}

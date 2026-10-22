using System.Collections.Generic;
using Rocket.API;
using Rocket.Unturned.Chat;

namespace EconomyScanner.Commands
{
    public sealed class CommandEconomyScan : IRocketCommand
    {
        public AllowedCaller AllowedCaller { get { return AllowedCaller.Both; } }
        public string Name { get { return "economyscan"; } }
        public string Help { get { return "Starts a batched economy inventory/storage scan."; } }
        public string Syntax { get { return ""; } }
        public List<string> Aliases { get { return new List<string> { "escan" }; } }
        public List<string> Permissions { get { return new List<string> { "economyscanner.scan" }; } }

        public void Execute(IRocketPlayer caller, string[] command)
        {
            EconomyScannerPlugin plugin = EconomyScannerPlugin.Instance;
            if (plugin == null)
            {
                UnturnedChat.Say(caller, "EconomyScanner is not loaded.");
                return;
            }

            string message;
            plugin.TryStartManualScan(out message);
            UnturnedChat.Say(caller, message);
        }
    }
}

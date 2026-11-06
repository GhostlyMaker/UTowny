using System;
using Cysharp.Threading.Tasks;
using EconomyScanner.Services;
using OpenMod.Core.Commands;
using OpenMod.Unturned.Commands;
using OpenMod.Unturned.Users;

namespace EconomyScanner.Commands
{
    [Command("econlink")]
    [CommandDescription("Links your in-game player to your website account using a one-time code.")]
    [CommandActor(typeof(UnturnedUser))]
    public sealed class CommandEconomyLink : UnturnedCommand
    {
        private static readonly PlayerLinkGate Gate = new PlayerLinkGate();
        public CommandEconomyLink(IServiceProvider serviceProvider) : base(serviceProvider) { }

        protected override async UniTask OnExecuteAsync()
        {
            if (Context.Parameters.Length != 1) { await PrintAsync("Usage: /econlink <12-character code>"); return; }
            var user = Context.Actor as UnturnedUser;
            if (user == null || user.Player == null || user.Player.Player == null)
            { await PrintAsync("This command can only be used by an in-game player."); return; }
            string code = (await Context.Parameters.GetAsync<string>(0))?.Trim().ToUpperInvariant();
            if (!PlayerLinkProtocol.IsCode(code))
            { await PrintAsync("Invalid link code. Copy the 12-character code from your website account."); return; }
            var plugin = EconomyScannerPlugin.Instance;
            if (plugin == null || plugin.Settings == null)
            { await PrintAsync("EconomyScanner is not ready yet."); return; }
            // Identity comes only from the authenticated in-game actor, never command arguments.
            ulong steamId = user.SteamId.m_SteamID;
            if (!Gate.TryEnter(steamId, DateTime.UtcNow))
            { await PrintAsync("A link request is pending or was just sent. Please wait a few seconds."); return; }
            try
            {
                await PrintAsync("Verifying your player link...");
                string result = await PlayerLinkPublisher.VerifyAsync(plugin.Settings, steamId, code);
                await UniTask.SwitchToMainThread();
                if (EconomyScannerPlugin.Instance == plugin) await PrintAsync(result);
            }
            finally { Gate.Exit(steamId); }
        }
    }
}

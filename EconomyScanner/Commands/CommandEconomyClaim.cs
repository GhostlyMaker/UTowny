using System;
using Cysharp.Threading.Tasks;
using EconomyScanner.Services;
using OpenMod.Core.Commands;
using OpenMod.Unturned.Commands;
using OpenMod.Unturned.Users;

namespace EconomyScanner.Commands
{
    [Command("econclaim")]
    [CommandDescription("Verifies website ownership of your current Unturned group using a one-time claim code.")]
    [CommandActor(typeof(UnturnedUser))]
    public sealed class CommandEconomyClaim : UnturnedCommand
    {
        public CommandEconomyClaim(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        protected override async UniTask OnExecuteAsync()
        {
            if (Context.Parameters.Length < 1)
            {
                await PrintAsync("Usage: /econclaim <code>");
                return;
            }

            UnturnedUser user = Context.Actor as UnturnedUser;
            if (user == null || user.Player == null || user.Player.Player == null)
            {
                await PrintAsync("This command can only be used by an in-game player.");
                return;
            }

            string code = (await Context.Parameters.GetAsync<string>(0))?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(code) || code.Length != 10)
            {
                await PrintAsync("Invalid claim code. Generate a fresh code on the website and try again.");
                return;
            }

            ulong steamId = user.SteamId.m_SteamID;
            ulong groupId = user.Player.Player.quests != null
                ? user.Player.Player.quests.groupID.m_SteamID
                : 0UL;

            if (groupId == 0)
            {
                await PrintAsync("You are not currently in an Unturned group. Join the group you want to claim first.");
                return;
            }

            EconomyScannerPlugin plugin = EconomyScannerPlugin.Instance;
            if (plugin == null || plugin.Settings == null)
            {
                await PrintAsync("EconomyScanner is not ready yet.");
                return;
            }

            await PrintAsync("Verifying your group claim...");
            string result = await GroupClaimPublisher.VerifyAsync(plugin.Settings, steamId, groupId, code);
            await PrintAsync(result);
        }
    }
}

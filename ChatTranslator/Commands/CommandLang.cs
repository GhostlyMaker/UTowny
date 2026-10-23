using System;
using Cysharp.Threading.Tasks;
using OpenMod.Core.Commands;
using OpenMod.Unturned.Commands;
using OpenMod.Unturned.Users;
using ChatTranslator.Services;

namespace ChatTranslator.Commands
{
    [Command("lang")]
    [CommandDescription("Enables chat translation for you, or turns it off.")]
    [CommandSyntax("<en|ko|off>")]
    [CommandActor(typeof(UnturnedUser))]
    public sealed class CommandLang : UnturnedCommand
    {
        private readonly IChatTranslatorService _translator;

        public CommandLang(
            IChatTranslatorService translator,
            IServiceProvider serviceProvider) : base(serviceProvider)
        {
            _translator = translator;
        }

        protected override async UniTask OnExecuteAsync()
        {
            string requested = await Context.Parameters.GetAsync<string>(0);
            string language = ChatTranslatorService.NormalizeLanguage(requested);
            var user = (UnturnedUser)Context.Actor;
            ulong steamId = user.SteamId.m_SteamID;

            if (language == "OFF")
            {
                await _translator.DisableAsync(steamId);
                await PrintAsync("Chat translation disabled.");
                return;
            }

            if (language != "EN" && language != "KO")
            {
                await PrintAsync("Supported options: en (English), ko (Korean), off");
                return;
            }

            await _translator.SetLanguageAsync(steamId, language);

            string displayName = language == "KO" ? "Korean" : "English";
            await PrintAsync($"Incoming chat will now be translated to {displayName} ({language}). Use /lang off to disable it.");
        }
    }
}

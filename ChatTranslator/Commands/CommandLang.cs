using System;
using Cysharp.Threading.Tasks;
using OpenMod.API.Commands;
using OpenMod.Unturned.Commands;
using OpenMod.Unturned.Users;
using ChatTranslator.Services;

namespace ChatTranslator.Commands
{
    [Command("lang")]
    [CommandDescription("Sets your automatic chat translation language.")]
    [CommandSyntax("<en|ko>")]
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

            if (language != "EN" && language != "KO")
            {
                await PrintAsync("Supported languages: en (English), ko (Korean)");
                return;
            }

            var user = (UnturnedUser)Context.Actor;
            await _translator.SetLanguageAsync(user.SteamId.m_SteamID, language);

            string displayName = language == "KO" ? "Korean" : "English";
            await PrintAsync($"Chat translation language set to {displayName} ({language}).");
        }
    }
}

using System;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenMod.API.Plugins;
using OpenMod.Unturned.Plugins;

[assembly: PluginMetadata("ChatTranslator", DisplayName = "Chat Translator")]

namespace ChatTranslator
{
    public sealed class ChatTranslatorPlugin : OpenModUnturnedPlugin
    {
        private readonly ILogger<ChatTranslatorPlugin> _logger;

        public ChatTranslatorPlugin(
            ILogger<ChatTranslatorPlugin> logger,
            IServiceProvider serviceProvider) : base(serviceProvider)
        {
            _logger = logger;
        }

        protected override UniTask OnLoadAsync()
        {
            _logger.LogInformation("Chat Translator v1.0.0 loaded. Players can use /lang en or /lang ko.");
            return UniTask.CompletedTask;
        }

        protected override UniTask OnUnloadAsync()
        {
            _logger.LogInformation("Chat Translator unloaded.");
            return UniTask.CompletedTask;
        }
    }
}

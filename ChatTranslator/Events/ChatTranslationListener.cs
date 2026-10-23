using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenMod.API.Eventing;
using OpenMod.Unturned.Players.Chat.Events;
using ChatTranslator.Services;

namespace ChatTranslator.Events
{
    public sealed class ChatTranslationListener : IEventListener<UnturnedServerSendingMessageEvent>
    {
        private readonly IChatTranslatorService _translator;
        private readonly ILogger<ChatTranslationListener> _logger;

        public ChatTranslationListener(
            IChatTranslatorService translator,
            ILogger<ChatTranslationListener> logger)
        {
            _translator = translator;
            _logger = logger;
        }

        public async Task HandleEventAsync(object? sender, UnturnedServerSendingMessageEvent @event)
        {
            string original = @event.Text;
            if (string.IsNullOrWhiteSpace(original))
            {
                return;
            }

            ulong senderId = @event.FromPlayer.SteamId.m_SteamID;
            ulong receiverId = @event.ToPlayer.SteamId.m_SteamID;

            string sourceLanguage = _translator.GetLanguage(senderId);
            string targetLanguage = _translator.GetLanguage(receiverId);

            if (string.Equals(sourceLanguage, targetLanguage, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                string translated = await _translator.TranslateAsync(original, sourceLanguage, targetLanguage);
                string text = translated;

                if (_translator.ShowOriginal)
                {
                    text += $" [{original}]";
                }

                if (!string.IsNullOrWhiteSpace(_translator.Prefix))
                {
                    text = _translator.Prefix + text;
                }

                @event.Text = text;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to translate chat from {SourceLanguage} to {TargetLanguage}; original message will be shown.",
                    sourceLanguage,
                    targetLanguage);
            }
        }
    }
}

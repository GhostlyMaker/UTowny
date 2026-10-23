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
        private DateTime _nextWarningUtc = DateTime.MinValue;

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

            // OpenMod also raises this event for some server/plugin messages where there is
            // no concrete player recipient. Those messages must be ignored by this plugin.
            if (@event.ToPlayer == null)
            {
                return;
            }

            ulong receiverId = @event.ToPlayer.SteamId.m_SteamID;
            string? targetLanguage = _translator.GetTargetLanguage(receiverId);

            // Translation is opt-in. If this recipient has not used /lang en or /lang ko,
            // do nothing and make no DeepL request.
            if (targetLanguage == null)
            {
                return;
            }

            try
            {
                // Source language is intentionally omitted so DeepL auto-detects it.
                string translated = await _translator.TranslateAsync(original, targetLanguage);
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
                // Avoid flooding the console if DeepL is unavailable or misconfigured.
                if (DateTime.UtcNow >= _nextWarningUtc)
                {
                    _nextWarningUtc = DateTime.UtcNow.AddMinutes(1);
                    _logger.LogWarning(
                        ex,
                        "Chat translation failed. Further translation errors will be suppressed for 1 minute; original chat is still shown.");
                }
            }
        }
    }
}

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

            if (@event.ToPlayer == null)
            {
                return;
            }

            ulong receiverId = @event.ToPlayer.SteamId.m_SteamID;
            string? targetLanguage = _translator.GetTargetLanguage(receiverId);

            // Translation is opt-in. Recipients without /lang en or /lang ko are untouched.
            if (targetLanguage == null)
            {
                return;
            }

            // This plugin is intentionally EN <-> KO only. Hangul means Korean;
            // otherwise treat the message as English. If the message already matches
            // the recipient's language, no API request is made.
            string sourceLanguage = ContainsHangul(original) ? "KO" : "EN";
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
                if (DateTime.UtcNow >= _nextWarningUtc)
                {
                    _nextWarningUtc = DateTime.UtcNow.AddMinutes(1);
                    _logger.LogWarning(
                        ex,
                        "Chat translation failed. Further translation errors will be suppressed for 1 minute; original chat is still shown.");
                }
            }
        }

        private static bool ContainsHangul(string text)
        {
            foreach (char c in text)
            {
                // Hangul Jamo, Compatibility Jamo, and Hangul Syllables.
                if ((c >= '\u1100' && c <= '\u11FF') ||
                    (c >= '\u3130' && c <= '\u318F') ||
                    (c >= '\uAC00' && c <= '\uD7AF'))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

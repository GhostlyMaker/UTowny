using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OpenMod.API.Ioc;
using OpenMod.API.Persistence;

namespace ChatTranslator.Services
{
    [Service]
    public interface IChatTranslatorService
    {
        string? GetTargetLanguage(ulong steamId);
        Task SetLanguageAsync(ulong steamId, string language);
        Task DisableAsync(ulong steamId);
        Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage);
        bool ShowOriginal { get; }
        string Prefix { get; }
    }

    [PluginServiceImplementation(Lifetime = ServiceLifetime.Singleton)]
    public sealed class ChatTranslatorService : IChatTranslatorService, IDisposable
    {
        private const string LanguageStoreKey = "chattranslator.languages";

        private readonly IDataStore _dataStore;
        private readonly ILogger<ChatTranslatorService> _logger;
        private readonly HttpClient _httpClient;
        private readonly ConcurrentDictionary<ulong, string> _languages = new ConcurrentDictionary<ulong, string>();
        private readonly ConcurrentDictionary<string, Lazy<Task<string>>> _translationCache = new ConcurrentDictionary<string, Lazy<Task<string>>>();
        private readonly string _apiKey;
        private readonly string _endpoint;
        private readonly string _defaultLanguage;

        public bool ShowOriginal { get; }
        public string Prefix { get; }

        public ChatTranslatorService(
            IConfiguration configuration,
            IDataStore dataStore,
            ILogger<ChatTranslatorService> logger)
        {
            _dataStore = dataStore;
            _logger = logger;

            _apiKey = (configuration["deepl:api_key"] ?? string.Empty).Trim();
            bool useFreeApi = !bool.TryParse(configuration["deepl:use_free_api"], out bool parsed) || parsed;
            _endpoint = useFreeApi
                ? "https://api-free.deepl.com/v2/translate"
                : "https://api.deepl.com/v2/translate";

            _defaultLanguage = NormalizeLanguage(configuration["translator:default_language"] ?? "EN");
            if (_defaultLanguage != "EN" && _defaultLanguage != "KO" && _defaultLanguage != "OFF")
            {
                _defaultLanguage = "EN";
            }

            ShowOriginal = bool.TryParse(configuration["translator:show_original"], out bool showOriginal) && showOriginal;
            Prefix = configuration["translator:prefix"] ?? string.Empty;

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(8)
            };

            LoadLanguagesAsync().GetAwaiter().GetResult();
        }

        public string? GetTargetLanguage(ulong steamId)
        {
            string language = _languages.TryGetValue(steamId, out string? savedLanguage)
                ? savedLanguage
                : _defaultLanguage;

            return language == "OFF" ? null : language;
        }

        public async Task SetLanguageAsync(ulong steamId, string language)
        {
            string normalized = NormalizeLanguage(language);
            _languages[steamId] = normalized;
            await SaveLanguagesAsync();
        }

        public async Task DisableAsync(ulong steamId)
        {
            // Store OFF explicitly so it overrides the server-wide default language.
            _languages[steamId] = "OFF";
            await SaveLanguagesAsync();
        }

        public Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
        {
            string source = NormalizeLanguage(sourceLanguage);
            string target = NormalizeLanguage(targetLanguage);

            if (string.IsNullOrWhiteSpace(text) || source == target)
            {
                return Task.FromResult(text);
            }

            string cacheKey = source + "\u001f" + target + "\u001f" + text;
            Lazy<Task<string>> lazy = _translationCache.GetOrAdd(
                cacheKey,
                _ => new Lazy<Task<string>>(
                    () => TranslateCoreAsync(text, source, target),
                    true));

            return AwaitAndCleanOnFailureAsync(cacheKey, lazy);
        }

        private async Task<string> AwaitAndCleanOnFailureAsync(string cacheKey, Lazy<Task<string>> lazy)
        {
            try
            {
                return await lazy.Value;
            }
            catch
            {
                _translationCache.TryRemove(cacheKey, out _);
                throw;
            }
        }

        private async Task<string> TranslateCoreAsync(string text, string sourceLanguage, string targetLanguage)
        {
            if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey == "PUT_YOUR_DEEPL_API_KEY_HERE")
            {
                throw new InvalidOperationException("DeepL API key is not configured in config.yaml.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("DeepL-Auth-Key", _apiKey);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["text"] = text,
                ["source_lang"] = ToDeepLSourceLanguage(sourceLanguage),
                ["target_lang"] = ToDeepLTargetLanguage(targetLanguage),
                ["preserve_formatting"] = "1"
            });

            using HttpResponseMessage response = await _httpClient.SendAsync(request);
            string json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"DeepL returned {(int)response.StatusCode}: {json}");
            }

            JObject payload = JObject.Parse(json);
            string? translated = payload["translations"]?[0]?["text"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(translated))
            {
                throw new InvalidOperationException("DeepL response did not contain translated text.");
            }

            return translated;
        }

        private async Task LoadLanguagesAsync()
        {
            try
            {
                Dictionary<string, string>? stored = await _dataStore.LoadAsync<Dictionary<string, string>>(LanguageStoreKey);
                if (stored == null)
                {
                    return;
                }

                foreach (KeyValuePair<string, string> pair in stored)
                {
                    if (!ulong.TryParse(pair.Key, out ulong steamId))
                    {
                        continue;
                    }

                    string language = NormalizeLanguage(pair.Value);
                    if (language == "EN" || language == "KO" || language == "OFF")
                    {
                        _languages[steamId] = language;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load Chat Translator language preferences. The configured default language will be used.");
            }
        }

        private async Task SaveLanguagesAsync()
        {
            try
            {
                var stored = new Dictionary<string, string>();
                foreach (KeyValuePair<ulong, string> pair in _languages)
                {
                    stored[pair.Key.ToString()] = pair.Value;
                }

                await _dataStore.SaveAsync(LanguageStoreKey, stored);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not save Chat Translator language preferences.");
            }
        }

        public static string NormalizeLanguage(string language)
        {
            string value = language.Trim().ToUpperInvariant();
            return value switch
            {
                "EN" => "EN",
                "ENGLISH" => "EN",
                "KO" => "KO",
                "KR" => "KO",
                "KOREAN" => "KO",
                "OFF" => "OFF",
                "NONE" => "OFF",
                "DISABLE" => "OFF",
                "DISABLED" => "OFF",
                _ => value
            };
        }

        private static string ToDeepLSourceLanguage(string language)
        {
            return language == "KO" ? "KO" : "EN";
        }

        private static string ToDeepLTargetLanguage(string language)
        {
            return language == "KO" ? "KO" : "EN-US";
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}

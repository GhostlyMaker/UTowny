using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace EconomyScanner.Services
{
    internal static class PlayerLinkPublisher
    {
        public static async Task<string> VerifyAsync(EconomyScannerConfiguration config, ulong steamId, string code)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.ApiKey))
                return "Player linking is not configured on this server.";
            string endpoint = PlayerLinkProtocol.ResolveEndpoint(config.PlayerLinkEndpoint, config.ApiEndpoint);
            if (endpoint == null) return "Player linking requires a configured HTTPS endpoint. Please contact an admin.";
            try
            {
                // Never forward the scanner key to a redirect target.
                using (var handler = new HttpClientHandler { AllowAutoRedirect = false })
                using (var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(Math.Max(1, Math.Min(120, config.ApiTimeoutSeconds))) })
                using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
                {
                    request.Headers.Add(string.IsNullOrWhiteSpace(config.ApiHeaderName) ? "X-Economy-Key" : config.ApiHeaderName, config.ApiKey);
                    request.Content = new ByteArrayContent(PlayerLinkProtocol.Payload(config.ServerId, steamId, code));
                    request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                    // Bound response size. Never log a key, code, or response body.
                    client.MaxResponseContentBufferSize = 8192;
                    using (var response = await client.SendAsync(request).ConfigureAwait(false))
                        return PlayerLinkProtocol.Message((int)response.StatusCode, await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                }
            }
            catch { return "Could not verify your player right now. Check your website account and try again shortly."; }
        }
    }
}

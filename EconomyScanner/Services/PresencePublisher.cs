using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace EconomyScanner.Services
{
    internal static class PresencePublisher
    {
        public static async Task<bool> PublishAsync(EconomyScannerConfiguration config, string[] steamIds, DateTime observedUtc, CancellationToken token)
        {
            string endpoint = PresenceProtocol.ResolveEndpoint(config.PresenceEndpoint, config.ApiEndpoint);
            if (endpoint == null || string.IsNullOrWhiteSpace(config.ApiKey)) return false;
            try
            {
                using (var handler = new HttpClientHandler { AllowAutoRedirect = false })
                using (var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(Math.Max(1, Math.Min(120, config.ApiTimeoutSeconds))), MaxResponseContentBufferSize = 8192 })
                using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
                {
                    request.Headers.Add(string.IsNullOrWhiteSpace(config.ApiHeaderName) ? "X-Economy-Key" : config.ApiHeaderName, config.ApiKey);
                    request.Content = new ByteArrayContent(PresenceProtocol.Payload(config.ServerId, steamIds, observedUtc));
                    request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                    using (var response = await client.SendAsync(request, token).ConfigureAwait(false))
                        return response.IsSuccessStatusCode;
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch { return false; }
        }
    }
}

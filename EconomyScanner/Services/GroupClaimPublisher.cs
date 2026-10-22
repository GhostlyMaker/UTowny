using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace EconomyScanner.Services
{
    internal static class GroupClaimPublisher
    {
        public static Task<string> VerifyAsync(
            EconomyScannerConfiguration config,
            ulong steamId,
            ulong groupId,
            string code)
        {
            return Task.Run(delegate
            {
                if (config == null)
                {
                    return "EconomyScanner configuration is unavailable.";
                }

                string endpoint = ResolveClaimEndpoint(config);
                if (string.IsNullOrWhiteSpace(endpoint))
                {
                    return "Group claiming is not configured on this server.";
                }

                if (string.IsNullOrWhiteSpace(config.ApiKey))
                {
                    return "Group claiming is not configured on this server.";
                }

                int timeoutMs = Math.Max(1, config.ApiTimeoutSeconds) * 1000;
                string payload = "{"
                    + "\"serverId\":\"" + JsonEscape(config.ServerId) + "\","
                    + "\"groupId\":\"" + groupId + "\","
                    + "\"steamId\":\"" + steamId + "\","
                    + "\"code\":\"" + JsonEscape(code.ToUpperInvariant()) + "\""
                    + "}";
                byte[] bytes = Encoding.UTF8.GetBytes(payload);

                try
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(endpoint);
                    request.Method = "POST";
                    request.ContentType = "application/json";
                    request.Accept = "application/json";
                    request.UserAgent = "Unturned-EconomyScanner-OpenMod/0.3.0";
                    request.Timeout = timeoutMs;
                    request.ReadWriteTimeout = timeoutMs;
                    request.ContentLength = bytes.Length;

                    string headerName = string.IsNullOrWhiteSpace(config.ApiHeaderName)
                        ? "X-Economy-Key"
                        : config.ApiHeaderName.Trim();
                    request.Headers[headerName] = config.ApiKey;

                    using (Stream stream = request.GetRequestStream())
                    {
                        stream.Write(bytes, 0, bytes.Length);
                    }

                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    {
                        if ((int)response.StatusCode >= 200 && (int)response.StatusCode < 300)
                        {
                            return "Group verified successfully. You can now edit its public name on the website.";
                        }

                        return "Group verification failed (HTTP " + (int)response.StatusCode + ").";
                    }
                }
                catch (WebException ex)
                {
                    HttpWebResponse response = ex.Response as HttpWebResponse;
                    if (response == null)
                    {
                        return "Could not contact the economy website. Try again shortly.";
                    }

                    int status = (int)response.StatusCode;
                    string body = ReadResponseBody(response);
                    response.Dispose();

                    if (status == 400 && body.IndexOf("invalid_or_expired_code", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return "That claim code is invalid or expired. Generate a new code on the website.";
                    }

                    if (status == 409 && body.IndexOf("group_already_claimed", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return "This group has already been claimed on the website.";
                    }

                    if (status == 401)
                    {
                        return "The server claim connection is not authorized. Please contact an admin.";
                    }

                    return "Group verification failed (HTTP " + status + ").";
                }
                catch
                {
                    return "Could not verify the group right now. Try again shortly.";
                }
            });
        }

        private static string ResolveClaimEndpoint(EconomyScannerConfiguration config)
        {
            if (!string.IsNullOrWhiteSpace(config.ClaimEndpoint))
            {
                return config.ClaimEndpoint.Trim();
            }

            string snapshotEndpoint = config.ApiEndpoint ?? string.Empty;
            const string snapshotSuffix = "/economy-snapshot";
            if (snapshotEndpoint.EndsWith(snapshotSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return snapshotEndpoint.Substring(0, snapshotEndpoint.Length - snapshotSuffix.Length) + "/economy-claim";
            }

            return string.Empty;
        }

        private static string ReadResponseBody(HttpWebResponse response)
        {
            try
            {
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = stream == null ? null : new StreamReader(stream))
                {
                    return reader == null ? string.Empty : reader.ReadToEnd();
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string JsonEscape(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}

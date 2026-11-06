using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace EconomyScanner.Services
{
    internal static class PlayerLinkProtocol
    {
        public static bool IsCode(string code)
        {
            if (code == null || code.Length != 12) return false;
            foreach (char c in code)
                if (!(c >= '0' && c <= '9') && !(c >= 'A' && c <= 'F')) return false;
            return true;
        }

        public static string ResolveEndpoint(string explicitEndpoint, string snapshotEndpoint)
        {
            string candidate = explicitEndpoint;
            if (string.IsNullOrWhiteSpace(candidate))
            {
                const string suffix = "/economy-snapshot";
                string snapshot = (snapshotEndpoint ?? "").Trim().TrimEnd('/');
                if (!snapshot.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return null;
                candidate = snapshot.Substring(0, snapshot.Length - suffix.Length) + "/economy-player-link";
            }
            Uri uri;
            if (!Uri.TryCreate(candidate.Trim(), UriKind.Absolute, out uri)
                || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo)
                || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) return null;
            return uri.AbsoluteUri;
        }

        public static byte[] Payload(string serverId, ulong steamId, string code)
        {
            if (!IsCode(code)) throw new ArgumentException("Invalid link code.");
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(LinkRequest)).WriteObject(stream, new LinkRequest {
                    ServerId = serverId, SteamId = steamId.ToString(System.Globalization.CultureInfo.InvariantCulture), Code = code
                });
                return stream.ToArray();
            }
        }

        public static string Message(int status, string body)
        {
            if (status == 200)
            {
                try
                {
                    using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(body ?? "")))
                    {
                        var result = (LinkResponse)new DataContractJsonSerializer(typeof(LinkResponse)).ReadObject(stream);
                        if (result != null && result.Ok) return "Player linked! Customize your avatar and profile on the website.";
                    }
                }
                catch { }
                return "The website did not confirm linking. Check your account page before trying again.";
            }
            if (status == 400) return "That code is invalid, expired, or belongs to a different player. Generate a new code for your player on the website.";
            if (status == 409) return "This player or website account is already linked. Check your account page or contact an admin.";
            if (status == 401 || status == 403) return "The server profile connection is not authorized. Please contact an admin.";
            if (status == 429) return "Too many linking attempts. Please wait before trying again.";
            return "Player linking is temporarily unavailable. Please try again shortly.";
        }

        [DataContract]
        private sealed class LinkRequest
        {
            [DataMember(Name = "server_id")] public string ServerId;
            [DataMember(Name = "steam_id")] public string SteamId;
            [DataMember(Name = "code")] public string Code;
        }
        [DataContract]
        private sealed class LinkResponse
        {
            [DataMember(Name = "ok")] public bool Ok;
        }
    }
}

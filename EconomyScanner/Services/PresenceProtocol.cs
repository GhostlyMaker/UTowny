using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Globalization;

namespace EconomyScanner.Services
{
    internal static class PresenceProtocol
    {
        public static string ResolveEndpoint(string explicitEndpoint, string snapshotEndpoint)
        {
            if (!string.IsNullOrWhiteSpace(explicitEndpoint))
                return PlayerLinkProtocol.ResolveEndpoint(explicitEndpoint, "");
            string link = PlayerLinkProtocol.ResolveEndpoint("", snapshotEndpoint);
            if (link == null) return null;
            return link.Substring(0, link.Length - "/economy-player-link".Length) + "/economy-presence";
        }

        public static byte[] Payload(string serverId, string[] steamIds, DateTime observedUtc)
        {
            if (steamIds == null || steamIds.Length > 1024) throw new ArgumentException("Invalid player list.");
            foreach (string id in steamIds)
            {
                if (id == null || id.Length < 17 || id.Length > 20) throw new ArgumentException("Invalid Steam ID.");
                foreach (char c in id) if (c < '0' || c > '9') throw new ArgumentException("Invalid Steam ID.");
            }
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(PresenceRequest)).WriteObject(stream, new PresenceRequest {
                    ServerId = serverId, SteamIds = steamIds,
                    ObservedAt = observedUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
                });
                return stream.ToArray();
            }
        }

        [DataContract]
        private sealed class PresenceRequest
        {
            [DataMember(Name = "server_id")] public string ServerId;
            [DataMember(Name = "steam_ids")] public string[] SteamIds;
            [DataMember(Name = "observed_at")] public string ObservedAt;
        }
    }
}

using System;
using System.Globalization;
using System.Text;
using EconomyScanner.Models;

namespace EconomyScanner.Services
{
    internal static class SnapshotJsonWriter
    {
        public static string Serialize(EconomySnapshot snapshot)
        {
            StringBuilder sb = new StringBuilder(64 * 1024);
            sb.Append('{');
            Property(sb, "schemaVersion", snapshot.SchemaVersion); sb.Append(',');
            Property(sb, "serverId", snapshot.ServerId); sb.Append(',');
            Property(sb, "generatedAtUtc", snapshot.GeneratedAtUtc); sb.Append(',');
            Property(sb, "trigger", snapshot.Trigger); sb.Append(',');
            Property(sb, "scopeNote", snapshot.ScopeNote); sb.Append(',');
            sb.Append("\"stats\":");
            WriteStats(sb, snapshot.Stats);
            sb.Append(',');
            sb.Append("\"groups\":[");
            for (int g = 0; g < snapshot.Groups.Count; g++)
            {
                if (g > 0) sb.Append(',');
                WriteGroup(sb, snapshot.Groups[g]);
            }
            sb.Append(']');
            sb.Append(',');
            sb.Append("\"players\":[");
            if (snapshot.Players != null)
            {
                for (int p = 0; p < snapshot.Players.Count; p++)
                {
                    if (p > 0) sb.Append(',');
                    WritePlayer(sb, snapshot.Players[p]);
                }
            }
            sb.Append(']');
            sb.Append('}');
            return sb.ToString();
        }

        private static void WriteStats(StringBuilder sb, ScanStats stats)
        {
            sb.Append('{');
            Property(sb, "barricadesVisited", stats.BarricadesVisited); sb.Append(',');
            Property(sb, "storageContainersVisited", stats.StorageContainersVisited); sb.Append(',');
            Property(sb, "onlinePlayersVisited", stats.OnlinePlayersVisited); sb.Append(',');
            Property(sb, "totalItemInstances", stats.TotalItemInstances); sb.Append(',');
            Property(sb, "playerItemInstances", stats.PlayerItemInstances); sb.Append(',');
            Property(sb, "storageItemInstances", stats.StorageItemInstances); sb.Append(',');
            Property(sb, "wallClockMilliseconds", stats.WallClockMilliseconds); sb.Append(',');
            Property(sb, "activeWorkMilliseconds", stats.ActiveWorkMilliseconds);
            sb.Append('}');
        }

        private static void WriteGroup(StringBuilder sb, GroupSnapshot group)
        {
            sb.Append('{');
            Property(sb, "groupId", group.GroupId.ToString(CultureInfo.InvariantCulture)); sb.Append(',');
            Property(sb, "groupName", group.GroupName); sb.Append(',');
            Property(sb, "onlinePlayers", group.OnlinePlayers); sb.Append(',');
            Property(sb, "storageContainers", group.StorageContainers); sb.Append(',');
            Property(sb, "playerItemInstances", group.PlayerItemInstances); sb.Append(',');
            Property(sb, "storageItemInstances", group.StorageItemInstances); sb.Append(',');
            sb.Append("\"items\":[");
            for (int i = 0; i < group.Items.Count; i++)
            {
                if (i > 0) sb.Append(',');
                WriteItem(sb, group.Items[i]);
            }
            sb.Append(']');
            sb.Append('}');
        }

        private static void WritePlayer(StringBuilder sb, PlayerSnapshot player)
        {
            sb.Append('{');
            Property(sb, "steamId", player.SteamId.ToString(CultureInfo.InvariantCulture)); sb.Append(',');
            Property(sb, "steamName", player.SteamName); sb.Append(',');
            Property(sb, "characterName", player.CharacterName); sb.Append(',');
            Property(sb, "nickName", player.NickName); sb.Append(',');
            Property(sb, "groupId", player.GroupId.ToString(CultureInfo.InvariantCulture)); sb.Append(',');
            Property(sb, "inventoryItemInstances", player.InventoryItemInstances); sb.Append(',');
            sb.Append("\"categories\":[");
            if (player.Categories != null)
            {
                for (int i = 0; i < player.Categories.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    WritePlayerCategory(sb, player.Categories[i]);
                }
            }
            sb.Append(']');
            sb.Append('}');
        }

        private static void WritePlayerCategory(StringBuilder sb, PlayerCategorySnapshot category)
        {
            sb.Append('{');
            Property(sb, "category", category.Category); sb.Append(',');
            Property(sb, "instances", category.Instances); sb.Append(',');
            Property(sb, "containedAmount", category.ContainedAmount);
            sb.Append('}');
        }

        private static void WriteItem(StringBuilder sb, ItemSnapshot item)
        {
            sb.Append('{');
            Property(sb, "itemId", item.ItemId); sb.Append(',');
            Property(sb, "guid", item.Guid); sb.Append(',');
            Property(sb, "name", item.Name); sb.Append(',');
            Property(sb, "itemType", item.ItemType); sb.Append(',');
            Property(sb, "instances", item.Instances); sb.Append(',');
            Property(sb, "containedAmount", item.ContainedAmount); sb.Append(',');
            Property(sb, "playerInstances", item.PlayerInstances); sb.Append(',');
            Property(sb, "storageInstances", item.StorageInstances);
            sb.Append('}');
        }

        private static void Property(StringBuilder sb, string name, string value)
        {
            WriteString(sb, name);
            sb.Append(':');
            WriteString(sb, value ?? string.Empty);
        }

        private static void Property(StringBuilder sb, string name, int value)
        {
            WriteString(sb, name);
            sb.Append(':').Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void Property(StringBuilder sb, string name, ushort value)
        {
            WriteString(sb, name);
            sb.Append(':').Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void Property(StringBuilder sb, string name, long value)
        {
            WriteString(sb, name);
            sb.Append(':').Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void Property(StringBuilder sb, string name, double value)
        {
            WriteString(sb, name);
            sb.Append(':').Append(value.ToString("0.###", CultureInfo.InvariantCulture));
        }

        private static void WriteString(StringBuilder sb, string value)
        {
            sb.Append('"');
            if (value != null)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\b': sb.Append("\\b"); break;
                        case '\f': sb.Append("\\f"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default:
                            if (c < 32)
                                sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            else
                                sb.Append(c);
                            break;
                    }
                }
            }
            sb.Append('"');
        }
    }
}

using System.Collections.Generic;

namespace EconomyScanner.Models
{
    public sealed class EconomySnapshot
    {
        public int SchemaVersion { get; set; }
        public string ServerId { get; set; }
        public string GeneratedAtUtc { get; set; }
        public string Trigger { get; set; }
        public string ScopeNote { get; set; }
        public ScanStats Stats { get; set; }
        public List<GroupSnapshot> Groups { get; set; }
        public List<PlayerSnapshot> Players { get; set; }
    }

    public sealed class ScanStats
    {
        public long BarricadesVisited { get; set; }
        public long StructuresVisited { get; set; }
        public long StorageContainersVisited { get; set; }
        public long OnlinePlayersVisited { get; set; }
        public long TotalItemInstances { get; set; }
        public long PlayerItemInstances { get; set; }
        public long StorageItemInstances { get; set; }
        public long PlacedAssetInstances { get; set; }
        public double WallClockMilliseconds { get; set; }
        public double ActiveWorkMilliseconds { get; set; }
    }

    public sealed class GroupSnapshot
    {
        public ulong GroupId { get; set; }
        public string GroupName { get; set; }
        public int OnlinePlayers { get; set; }
        public long StorageContainers { get; set; }
        public long PlayerItemInstances { get; set; }
        public long StorageItemInstances { get; set; }
        public long PlacedAssetInstances { get; set; }
        public List<ItemSnapshot> Items { get; set; }
    }

    public sealed class PlayerSnapshot
    {
        public ulong SteamId { get; set; }
        public string SteamName { get; set; }
        public string CharacterName { get; set; }
        public string NickName { get; set; }
        public ulong GroupId { get; set; }
        public long InventoryItemInstances { get; set; }
        public List<PlayerCategorySnapshot> Categories { get; set; }
    }

    public sealed class PlayerCategorySnapshot
    {
        public string Category { get; set; }
        public long Instances { get; set; }
        public long ContainedAmount { get; set; }
    }

    public sealed class ItemSnapshot
    {
        public ushort ItemId { get; set; }
        public string Guid { get; set; }
        public string Name { get; set; }
        public string ItemType { get; set; }
        public long Instances { get; set; }
        public long ContainedAmount { get; set; }
        public long PlayerInstances { get; set; }
        public long StorageInstances { get; set; }
        public long PlacedInstances { get; set; }
    }
}

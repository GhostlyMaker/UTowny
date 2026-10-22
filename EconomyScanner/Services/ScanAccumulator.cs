using System;
using System.Collections.Generic;
using System.Linq;
using EconomyScanner.Models;
using SDG.Unturned;
using Steamworks;

namespace EconomyScanner.Services
{
    internal enum EconomyItemSource
    {
        Player,
        Storage
    }

    internal sealed class ScanAccumulator
    {
        private readonly Dictionary<ulong, GroupAggregate> groups = new Dictionary<ulong, GroupAggregate>();
        private readonly Dictionary<ushort, ItemMetadata> itemMetadata = new Dictionary<ushort, ItemMetadata>();

        public long TotalItemInstances { get; private set; }
        public long PlayerItemInstances { get; private set; }
        public long StorageItemInstances { get; private set; }

        public GroupAggregate GetGroup(ulong groupId)
        {
            GroupAggregate group;
            if (!groups.TryGetValue(groupId, out group))
            {
                group = new GroupAggregate(groupId, ResolveGroupName(groupId));
                groups.Add(groupId, group);
            }

            return group;
        }

        public void AddItem(ulong groupId, Item item, EconomyItemSource source)
        {
            if (item == null)
            {
                return;
            }

            GroupAggregate group = GetGroup(groupId);
            ItemAggregate aggregate;
            if (!group.Items.TryGetValue(item.id, out aggregate))
            {
                ItemMetadata metadata = GetItemMetadata(item.id);
                aggregate = new ItemAggregate
                {
                    ItemId = item.id,
                    Guid = metadata.Guid,
                    Name = metadata.Name,
                    ItemType = metadata.ItemType
                };
                group.Items.Add(item.id, aggregate);
            }

            aggregate.Instances++;
            aggregate.ContainedAmount += item.amount;
            TotalItemInstances++;

            if (source == EconomyItemSource.Player)
            {
                aggregate.PlayerInstances++;
                group.PlayerItemInstances++;
                PlayerItemInstances++;
            }
            else
            {
                aggregate.StorageInstances++;
                group.StorageItemInstances++;
                StorageItemInstances++;
            }
        }

        public List<GroupSnapshot> ToSnapshots()
        {
            return groups.Values
                .OrderBy(g => g.GroupId)
                .Select(g => new GroupSnapshot
                {
                    GroupId = g.GroupId,
                    GroupName = g.GroupName,
                    OnlinePlayers = g.OnlinePlayers,
                    StorageContainers = g.StorageContainers,
                    PlayerItemInstances = g.PlayerItemInstances,
                    StorageItemInstances = g.StorageItemInstances,
                    Items = g.Items.Values
                        .OrderBy(i => i.ItemId)
                        .Select(i => new ItemSnapshot
                        {
                            ItemId = i.ItemId,
                            Guid = i.Guid,
                            Name = i.Name,
                            ItemType = i.ItemType,
                            Instances = i.Instances,
                            ContainedAmount = i.ContainedAmount,
                            PlayerInstances = i.PlayerInstances,
                            StorageInstances = i.StorageInstances
                        })
                        .ToList()
                })
                .ToList();
        }

        private static string ResolveGroupName(ulong groupId)
        {
            if (groupId == 0)
            {
                return "Ungrouped";
            }

            GroupInfo info = GroupManager.getGroupInfo(new CSteamID(groupId));
            return info == null || string.IsNullOrWhiteSpace(info.name) ? string.Empty : info.name;
        }

        private ItemMetadata GetItemMetadata(ushort itemId)
        {
            ItemMetadata metadata;
            if (itemMetadata.TryGetValue(itemId, out metadata))
            {
                return metadata;
            }

            ItemAsset asset = Assets.find(EAssetType.ITEM, itemId) as ItemAsset;
            if (asset == null)
            {
                metadata = new ItemMetadata
                {
                    Guid = "",
                    Name = "Unknown Item " + itemId,
                    ItemType = "Unknown"
                };
            }
            else
            {
                metadata = new ItemMetadata
                {
                    Guid = asset.GUID.ToString("N"),
                    Name = string.IsNullOrEmpty(asset.itemName) ? asset.name : asset.itemName,
                    ItemType = asset.type.ToString()
                };
            }

            itemMetadata.Add(itemId, metadata);
            return metadata;
        }
    }

    internal sealed class GroupAggregate
    {
        public GroupAggregate(ulong groupId, string groupName)
        {
            GroupId = groupId;
            GroupName = groupName ?? string.Empty;
            Items = new Dictionary<ushort, ItemAggregate>();
        }

        public ulong GroupId { get; private set; }
        public string GroupName { get; private set; }
        public int OnlinePlayers { get; set; }
        public long StorageContainers { get; set; }
        public long PlayerItemInstances { get; set; }
        public long StorageItemInstances { get; set; }
        public Dictionary<ushort, ItemAggregate> Items { get; private set; }
    }

    internal sealed class ItemAggregate
    {
        public ushort ItemId { get; set; }
        public string Guid { get; set; }
        public string Name { get; set; }
        public string ItemType { get; set; }
        public long Instances { get; set; }
        public long ContainedAmount { get; set; }
        public long PlayerInstances { get; set; }
        public long StorageInstances { get; set; }
    }

    internal sealed class ItemMetadata
    {
        public string Guid { get; set; }
        public string Name { get; set; }
        public string ItemType { get; set; }
    }
}

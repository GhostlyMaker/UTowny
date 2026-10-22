using System;
using System.Collections;
using System.Diagnostics;
using EconomyScanner.Models;
using EconomyScanner.Services;
using Rocket.Core.Plugins;
using SDG.Unturned;
using UnityEngine;
using RocketLogger = Rocket.Core.Logging.Logger;

namespace EconomyScanner
{
    public sealed class EconomyScannerPlugin : RocketPlugin<EconomyScannerConfiguration>
    {
        public static EconomyScannerPlugin Instance { get; private set; }
        private bool isScanning;
        private Coroutine periodicCoroutine;

        public bool IsScanning { get { return isScanning; } }
        public DateTime? LastScanUtc { get; private set; }
        public string LastScanSummary { get; private set; }

        protected override void Load()
        {
            Instance = this;
            System.IO.Directory.CreateDirectory(Directory);
            LastScanSummary = "No scan has completed yet.";
            periodicCoroutine = StartCoroutine(PeriodicScanLoop());
            RocketLogger.Log("[EconomyScanner] Loaded v0.1.1.");
        }

        protected override void Unload()
        {
            if (periodicCoroutine != null) StopCoroutine(periodicCoroutine);
            StopAllCoroutines();
            isScanning = false;
            Instance = null;
        }

        public bool TryStartManualScan(out string message)
        {
            if (isScanning)
            {
                message = "An economy scan is already running.";
                return false;
            }
            if (BarricadeManager.regions == null)
            {
                message = "The map is not fully loaded yet.";
                return false;
            }
            StartCoroutine(ScanCoroutine("manual"));
            message = "Economy scan started.";
            return true;
        }

        private IEnumerator PeriodicScanLoop()
        {
            if (Configuration.Instance.InitialDelaySeconds > 0)
                yield return new WaitForSecondsRealtime(Configuration.Instance.InitialDelaySeconds);

            while (true)
            {
                if (!isScanning && BarricadeManager.regions != null)
                    yield return StartCoroutine(ScanCoroutine("scheduled"));

                double minutes = Math.Max(0.25, Configuration.Instance.ScanIntervalMinutes);
                yield return new WaitForSecondsRealtime((float)(minutes * 60.0));
            }
        }

        private IEnumerator ScanCoroutine(string trigger)
        {
            if (isScanning) yield break;

            isScanning = true;
            DateTime generatedUtc = DateTime.UtcNow;
            EconomyScannerConfiguration config = Configuration.Instance;
            ScanAccumulator accumulator = new ScanAccumulator();
            ScanStats stats = new ScanStats();
            Stopwatch wallClock = Stopwatch.StartNew();
            Stopwatch activeWork = Stopwatch.StartNew();

            try
            {
                if (config.IncludeOnlinePlayerInventories)
                    ScanOnlinePlayers(accumulator, stats, config);

                int batchSize = Math.Max(1, config.BarricadesPerFrame);
                int batchCounter = 0;

                if (config.IncludeWorldStorage && BarricadeManager.regions != null)
                {
                    BarricadeRegion[,] regions = BarricadeManager.regions;
                    for (int x = 0; x < regions.GetLength(0); x++)
                    {
                        for (int y = 0; y < regions.GetLength(1); y++)
                        {
                            BarricadeRegion region = regions[x, y];
                            if (region == null || region.drops == null) continue;

                            for (int i = 0; i < region.drops.Count; i++)
                            {
                                stats.BarricadesVisited++;
                                ScanStorageDrop(region.drops[i], accumulator, stats, config);
                                if (++batchCounter >= batchSize)
                                {
                                    batchCounter = 0;
                                    activeWork.Stop();
                                    yield return null;
                                    activeWork.Start();
                                }
                            }
                        }
                    }
                }

                if (config.IncludeVehicleStorage && BarricadeManager.vehicleRegions != null)
                {
                    for (int r = 0; r < BarricadeManager.vehicleRegions.Count; r++)
                    {
                        VehicleBarricadeRegion region = BarricadeManager.vehicleRegions[r];
                        if (region == null || region.drops == null) continue;

                        for (int i = 0; i < region.drops.Count; i++)
                        {
                            stats.BarricadesVisited++;
                            ScanStorageDrop(region.drops[i], accumulator, stats, config);
                            if (++batchCounter >= batchSize)
                            {
                                batchCounter = 0;
                                activeWork.Stop();
                                yield return null;
                                activeWork.Start();
                            }
                        }
                    }
                }

                activeWork.Stop();
                wallClock.Stop();
                stats.TotalItemInstances = accumulator.TotalItemInstances;
                stats.PlayerItemInstances = accumulator.PlayerItemInstances;
                stats.StorageItemInstances = accumulator.StorageItemInstances;
                stats.WallClockMilliseconds = wallClock.Elapsed.TotalMilliseconds;
                stats.ActiveWorkMilliseconds = activeWork.Elapsed.TotalMilliseconds;

                EconomySnapshot snapshot = new EconomySnapshot
                {
                    SchemaVersion = 1,
                    ServerId = string.IsNullOrWhiteSpace(config.ServerId) ? "main" : config.ServerId.Trim(),
                    GeneratedAtUtc = generatedUtc.ToString("o"),
                    Trigger = trigger,
                    ScopeNote = "Storage is server-wide. Player inventories include online players only.",
                    Stats = stats,
                    Groups = accumulator.ToSnapshots()
                };

                string json = SnapshotJsonWriter.Serialize(snapshot);
                SnapshotPublisher.SaveFilesAsync(Directory, config, json, generatedUtc);
                SnapshotPublisher.UploadAsync(config, json);

                LastScanUtc = generatedUtc;
                LastScanSummary = "Groups/buckets: " + snapshot.Groups.Count
                    + ", storages: " + stats.StorageContainersVisited
                    + ", item instances: " + stats.TotalItemInstances
                    + ", active work: " + stats.ActiveWorkMilliseconds.ToString("0.0") + " ms"
                    + ", wall: " + stats.WallClockMilliseconds.ToString("0.0") + " ms.";
                RocketLogger.Log("[EconomyScanner] Scan complete. " + LastScanSummary);
            }
            finally
            {
                if (activeWork.IsRunning) activeWork.Stop();
                if (wallClock.IsRunning) wallClock.Stop();
                isScanning = false;
            }
        }

        private static void ScanOnlinePlayers(ScanAccumulator accumulator, ScanStats stats, EconomyScannerConfiguration config)
        {
            if (Provider.clients == null) return;

            for (int p = 0; p < Provider.clients.Count; p++)
            {
                SteamPlayer sp = Provider.clients[p];
                if (sp == null || sp.player == null || sp.player.inventory == null) continue;

                ulong groupId = sp.player.quests != null ? sp.player.quests.groupID.m_SteamID : 0UL;
                if (groupId == 0 && !config.IncludeUngrouped) continue;

                stats.OnlinePlayersVisited++;
                accumulator.GetGroup(groupId).OnlinePlayers++;
                PlayerInventory inventory = sp.player.inventory;

                for (byte page = 0; page < PlayerInventory.STORAGE; page++)
                {
                    byte count = inventory.getItemCount(page);
                    for (byte index = 0; index < count; index++)
                    {
                        ItemJar jar = inventory.getItem(page, index);
                        if (jar != null) accumulator.AddItem(groupId, jar.item, EconomyItemSource.Player);
                    }
                }
            }
        }

        private static void ScanStorageDrop(BarricadeDrop drop, ScanAccumulator accumulator, ScanStats stats, EconomyScannerConfiguration config)
        {
            if (drop == null) return;
            InteractableStorage storage = drop.interactable as InteractableStorage;
            if (storage == null) return;

            ulong groupId;
            BarricadeData data = drop.GetServersideData();
            if (data != null) groupId = data.group;
            else groupId = storage.group.m_SteamID;

            if (groupId == 0 && !config.IncludeUngrouped) return;

            stats.StorageContainersVisited++;
            accumulator.GetGroup(groupId).StorageContainers++;
            Items items = storage.items;
            if (items == null) return;

            byte count = items.getItemCount();
            for (byte i = 0; i < count; i++)
            {
                ItemJar jar = items.getItem(i);
                if (jar != null) accumulator.AddItem(groupId, jar.item, EconomyItemSource.Storage);
            }
        }
    }
}

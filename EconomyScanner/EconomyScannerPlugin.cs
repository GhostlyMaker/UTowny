using System;
using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;
using EconomyScanner.Models;
using EconomyScanner.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenMod.API.Plugins;
using OpenMod.Unturned.Plugins;
using SDG.Unturned;

[assembly: PluginMetadata("GhostlyMaker.EconomyScanner", DisplayName = "EconomyScanner", Author = "GhostlyMaker")]

namespace EconomyScanner
{
    public sealed class EconomyScannerPlugin : OpenModUnturnedPlugin
    {
        public static EconomyScannerPlugin Instance { get; private set; }

        private readonly IConfiguration m_Configuration;
        private readonly ILogger<EconomyScannerPlugin> m_Logger;
        private CancellationTokenSource m_Cancellation;
        private EconomyScannerConfiguration m_Settings;
        private bool m_IsScanning;

        public bool IsScanning => m_IsScanning;
        public DateTime? LastScanUtc { get; private set; }
        public string LastScanSummary { get; private set; } = "No scan has completed yet.";

        public EconomyScannerPlugin(
            IConfiguration configuration,
            ILogger<EconomyScannerPlugin> logger,
            IServiceProvider serviceProvider) : base(serviceProvider)
        {
            m_Configuration = configuration;
            m_Logger = logger;
        }

        protected override async UniTask OnLoadAsync()
        {
            await UniTask.SwitchToMainThread();

            Instance = this;
            m_Settings = EconomyScannerConfiguration.From(m_Configuration);
            m_Cancellation = new CancellationTokenSource();

            m_Logger.LogInformation(
                "EconomyScanner OpenMod v0.2.0 loaded. Scheduled every {Minutes} minute(s).",
                m_Settings.ScanIntervalMinutes);
            m_Logger.LogInformation("Commands: /economyscan and /economystatus");

            PeriodicScanLoopAsync(m_Cancellation.Token).Forget();
        }

        protected override async UniTask OnUnloadAsync()
        {
            await UniTask.SwitchToMainThread();

            if (m_Cancellation != null)
            {
                m_Cancellation.Cancel();
                m_Cancellation.Dispose();
                m_Cancellation = null;
            }

            m_IsScanning = false;
            Instance = null;
            m_Logger.LogInformation("EconomyScanner unloaded.");
        }

        public bool TryStartManualScan(out string message)
        {
            if (m_IsScanning)
            {
                message = "An economy scan is already running.";
                return false;
            }

            if (BarricadeManager.regions == null)
            {
                message = "The map is not fully loaded yet.";
                return false;
            }

            m_IsScanning = true;
            CancellationToken token = m_Cancellation?.Token ?? CancellationToken.None;
            RunScanAsync("manual", token, true).Forget();
            message = "Economy scan started. It is batched across frames to avoid a server hitch.";
            return true;
        }

        private async UniTask PeriodicScanLoopAsync(CancellationToken token)
        {
            try
            {
                if (m_Settings.InitialDelaySeconds > 0)
                {
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(m_Settings.InitialDelaySeconds),
                        cancellationToken: token);
                }

                while (!token.IsCancellationRequested)
                {
                    await UniTask.SwitchToMainThread();

                    if (!m_IsScanning && BarricadeManager.regions != null)
                    {
                        await RunScanAsync("scheduled", token, false);
                    }

                    double minutes = Math.Max(0.25, m_Settings.ScanIntervalMinutes);
                    await UniTask.Delay(
                        TimeSpan.FromMinutes(minutes),
                        cancellationToken: token);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal during plugin unload.
            }
            catch (Exception ex)
            {
                m_Logger.LogError(ex, "EconomyScanner periodic loop failed.");
            }
        }

        private async UniTask RunScanAsync(string trigger, CancellationToken token, bool alreadyClaimed)
        {
            await UniTask.SwitchToMainThread();

            if (!alreadyClaimed)
            {
                if (m_IsScanning)
                {
                    return;
                }

                m_IsScanning = true;
            }

            DateTime generatedUtc = DateTime.UtcNow;
            EconomyScannerConfiguration config = m_Settings;
            ScanAccumulator accumulator = new ScanAccumulator();
            ScanStats stats = new ScanStats();
            Stopwatch wallClock = Stopwatch.StartNew();
            Stopwatch activeWork = Stopwatch.StartNew();

            try
            {
                if (config.VerboseLogging)
                {
                    m_Logger.LogInformation("Starting {Trigger} economy scan...", trigger);
                }

                if (config.IncludeOnlinePlayerInventories)
                {
                    ScanOnlinePlayers(accumulator, stats, config);
                }

                int batchSize = Math.Max(1, config.BarricadesPerFrame);
                int batchCounter = 0;

                if (config.IncludeWorldStorage && BarricadeManager.regions != null)
                {
                    BarricadeRegion[,] regions = BarricadeManager.regions;
                    int width = regions.GetLength(0);
                    int height = regions.GetLength(1);

                    for (int x = 0; x < width; x++)
                    {
                        for (int y = 0; y < height; y++)
                        {
                            BarricadeRegion region = regions[x, y];
                            if (region == null || region.drops == null)
                            {
                                continue;
                            }

                            for (int i = 0; i < region.drops.Count; i++)
                            {
                                stats.BarricadesVisited++;
                                ScanStorageDrop(region.drops[i], accumulator, stats, config);

                                if (++batchCounter >= batchSize)
                                {
                                    batchCounter = 0;
                                    activeWork.Stop();
                                    await UniTask.Yield();
                                    token.ThrowIfCancellationRequested();
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
                        if (region == null || region.drops == null)
                        {
                            continue;
                        }

                        for (int i = 0; i < region.drops.Count; i++)
                        {
                            stats.BarricadesVisited++;
                            ScanStorageDrop(region.drops[i], accumulator, stats, config);

                            if (++batchCounter >= batchSize)
                            {
                                batchCounter = 0;
                                activeWork.Stop();
                                await UniTask.Yield();
                                token.ThrowIfCancellationRequested();
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
                    SchemaVersion = 2,
                    ServerId = string.IsNullOrWhiteSpace(config.ServerId) ? "main" : config.ServerId,
                    GeneratedAtUtc = generatedUtc.ToString("o"),
                    Trigger = trigger,
                    ScopeNote = "Storage is scanned server-wide. Player inventory data covers currently-online players only; active external storage pages are excluded to prevent double-counting.",
                    Stats = stats,
                    Groups = accumulator.ToSnapshots()
                };

                string json = SnapshotJsonWriter.Serialize(snapshot);
                SnapshotPublisher.SaveFilesAsync(WorkingDirectory, config, json, generatedUtc, m_Logger);
                SnapshotPublisher.UploadAsync(config, json, m_Logger);

                LastScanUtc = generatedUtc;
                LastScanSummary = "Groups/buckets: " + snapshot.Groups.Count
                    + ", storages: " + stats.StorageContainersVisited
                    + ", item instances: " + stats.TotalItemInstances
                    + ", active scan work: " + stats.ActiveWorkMilliseconds.ToString("0.0") + " ms"
                    + ", wall time: " + stats.WallClockMilliseconds.ToString("0.0") + " ms.";

                m_Logger.LogInformation("Economy scan complete. {Summary}", LastScanSummary);
            }
            catch (OperationCanceledException)
            {
                LastScanSummary = "Last scan cancelled because the plugin/server was stopping.";
            }
            catch (Exception ex)
            {
                if (activeWork.IsRunning) activeWork.Stop();
                if (wallClock.IsRunning) wallClock.Stop();
                LastScanSummary = "Last scan failed: " + ex.Message;
                m_Logger.LogError(ex, "Economy scan failed.");
            }
            finally
            {
                m_IsScanning = false;
            }
        }

        private static void ScanOnlinePlayers(ScanAccumulator accumulator, ScanStats stats, EconomyScannerConfiguration config)
        {
            if (Provider.clients == null)
            {
                return;
            }

            for (int p = 0; p < Provider.clients.Count; p++)
            {
                SteamPlayer steamPlayer = Provider.clients[p];
                if (steamPlayer == null || steamPlayer.player == null || steamPlayer.player.inventory == null)
                {
                    continue;
                }

                ulong groupId = steamPlayer.player.quests != null
                    ? steamPlayer.player.quests.groupID.m_SteamID
                    : 0UL;

                if (groupId == 0 && !config.IncludeUngrouped)
                {
                    continue;
                }

                stats.OnlinePlayersVisited++;
                GroupAggregate group = accumulator.GetGroup(groupId);
                group.OnlinePlayers++;

                PlayerInventory inventory = steamPlayer.player.inventory;
                for (byte page = 0; page < PlayerInventory.STORAGE; page++)
                {
                    byte count = inventory.getItemCount(page);
                    for (byte index = 0; index < count; index++)
                    {
                        ItemJar jar = inventory.getItem(page, index);
                        if (jar != null)
                        {
                            accumulator.AddItem(groupId, jar.item, EconomyItemSource.Player);
                        }
                    }
                }
            }
        }

        private static void ScanStorageDrop(
            BarricadeDrop drop,
            ScanAccumulator accumulator,
            ScanStats stats,
            EconomyScannerConfiguration config)
        {
            if (drop == null)
            {
                return;
            }

            InteractableStorage storage = drop.interactable as InteractableStorage;
            if (storage == null)
            {
                return;
            }

            BarricadeData serverData = drop.GetServersideData();
            ulong groupId = serverData != null ? serverData.group : storage.group.m_SteamID;

            if (groupId == 0 && !config.IncludeUngrouped)
            {
                return;
            }

            stats.StorageContainersVisited++;
            GroupAggregate group = accumulator.GetGroup(groupId);
            group.StorageContainers++;

            Items items = storage.items;
            if (items == null)
            {
                return;
            }

            byte count = items.getItemCount();
            for (byte index = 0; index < count; index++)
            {
                ItemJar jar = items.getItem(index);
                if (jar != null)
                {
                    accumulator.AddItem(groupId, jar.item, EconomyItemSource.Storage);
                }
            }
        }
    }
}

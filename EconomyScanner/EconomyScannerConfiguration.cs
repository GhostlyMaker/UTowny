using Rocket.API;

namespace EconomyScanner
{
    public sealed class EconomyScannerConfiguration : IRocketPluginConfiguration
    {
        public string ServerId { get; set; }
        public double ScanIntervalMinutes { get; set; }
        public float InitialDelaySeconds { get; set; }
        public int BarricadesPerFrame { get; set; }

        public bool IncludeOnlinePlayerInventories { get; set; }
        public bool IncludeWorldStorage { get; set; }
        public bool IncludeVehicleStorage { get; set; }
        public bool IncludeUngrouped { get; set; }

        public bool SaveSnapshotLocally { get; set; }
        public bool SaveHistoricalSnapshots { get; set; }
        public string SnapshotFileName { get; set; }

        public bool UploadEnabled { get; set; }
        public string ApiEndpoint { get; set; }
        public string ApiHeaderName { get; set; }
        public string ApiKey { get; set; }
        public int ApiTimeoutSeconds { get; set; }

        public bool VerboseLogging { get; set; }

        public void LoadDefaults()
        {
            ServerId = "main";
            ScanIntervalMinutes = 60.0;
            InitialDelaySeconds = 20.0f;
            BarricadesPerFrame = 200;

            IncludeOnlinePlayerInventories = true;
            IncludeWorldStorage = true;
            IncludeVehicleStorage = true;
            IncludeUngrouped = true;

            SaveSnapshotLocally = true;
            SaveHistoricalSnapshots = false;
            SnapshotFileName = "latest-snapshot.json";

            UploadEnabled = false;
            ApiEndpoint = "";
            ApiHeaderName = "X-Economy-Key";
            ApiKey = "";
            ApiTimeoutSeconds = 15;

            VerboseLogging = true;
        }
    }
}

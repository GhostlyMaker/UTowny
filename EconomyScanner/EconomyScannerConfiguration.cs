using System;
using Microsoft.Extensions.Configuration;

namespace EconomyScanner
{
    internal sealed class EconomyScannerConfiguration
    {
        public string ServerId { get; private set; }
        public double ScanIntervalMinutes { get; private set; }
        public float InitialDelaySeconds { get; private set; }
        public int BarricadesPerFrame { get; private set; }
        public bool IncludeOnlinePlayerInventories { get; private set; }
        public bool IncludeWorldStorage { get; private set; }
        public bool IncludeVehicleStorage { get; private set; }
        public bool IncludeUngrouped { get; private set; }
        public bool SaveSnapshotLocally { get; private set; }
        public bool SaveHistoricalSnapshots { get; private set; }
        public string SnapshotFileName { get; private set; }
        public bool UploadEnabled { get; private set; }
        public string ApiEndpoint { get; private set; }
        public string ApiHeaderName { get; private set; }
        public string ApiKey { get; private set; }
        public int ApiTimeoutSeconds { get; private set; }
        public bool VerboseLogging { get; private set; }

        public static EconomyScannerConfiguration From(IConfiguration configuration)
        {
            return new EconomyScannerConfiguration
            {
                ServerId = ReadString(configuration, "ServerId", "main"),
                ScanIntervalMinutes = ReadDouble(configuration, "ScanIntervalMinutes", 60.0),
                InitialDelaySeconds = (float)ReadDouble(configuration, "InitialDelaySeconds", 20.0),
                BarricadesPerFrame = ReadInt(configuration, "BarricadesPerFrame", 200),
                IncludeOnlinePlayerInventories = ReadBool(configuration, "IncludeOnlinePlayerInventories", true),
                IncludeWorldStorage = ReadBool(configuration, "IncludeWorldStorage", true),
                IncludeVehicleStorage = ReadBool(configuration, "IncludeVehicleStorage", true),
                IncludeUngrouped = ReadBool(configuration, "IncludeUngrouped", true),
                SaveSnapshotLocally = ReadBool(configuration, "SaveSnapshotLocally", true),
                SaveHistoricalSnapshots = ReadBool(configuration, "SaveHistoricalSnapshots", false),
                SnapshotFileName = ReadString(configuration, "SnapshotFileName", "latest-snapshot.json"),
                UploadEnabled = ReadBool(configuration, "UploadEnabled", false),
                ApiEndpoint = ReadString(configuration, "ApiEndpoint", string.Empty),
                ApiHeaderName = ReadString(configuration, "ApiHeaderName", "X-Economy-Key"),
                ApiKey = ReadString(configuration, "ApiKey", string.Empty),
                ApiTimeoutSeconds = ReadInt(configuration, "ApiTimeoutSeconds", 15),
                VerboseLogging = ReadBool(configuration, "VerboseLogging", true)
            };
        }

        private static string ReadString(IConfiguration configuration, string key, string fallback)
        {
            string value = configuration[key];
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static bool ReadBool(IConfiguration configuration, string key, bool fallback)
        {
            bool parsed;
            return bool.TryParse(configuration[key], out parsed) ? parsed : fallback;
        }

        private static int ReadInt(IConfiguration configuration, string key, int fallback)
        {
            int parsed;
            return int.TryParse(configuration[key], out parsed) ? parsed : fallback;
        }

        private static double ReadDouble(IConfiguration configuration, string key, double fallback)
        {
            double parsed;
            return double.TryParse(configuration[key], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : fallback;
        }
    }
}

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Rocket.Core.Logging;

namespace EconomyScanner.Services
{
    internal static class SnapshotPublisher
    {
        public static void SaveFilesAsync(string pluginDirectory, EconomyScannerConfiguration config, string json, DateTime generatedUtc)
        {
            if (!config.SaveSnapshotLocally)
            {
                return;
            }

            string latestPath = Path.Combine(pluginDirectory, config.SnapshotFileName);
            bool saveHistory = config.SaveHistoricalSnapshots;
            string historyPath = saveHistory
                ? Path.Combine(pluginDirectory, "snapshots", generatedUtc.ToString("yyyyMMdd-HHmmss") + ".json")
                : null;

            Task.Run(delegate
            {
                try
                {
                    string latestDirectory = Path.GetDirectoryName(latestPath);
                    if (!string.IsNullOrEmpty(latestDirectory))
                    {
                        Directory.CreateDirectory(latestDirectory);
                    }

                    File.WriteAllText(latestPath, json, new UTF8Encoding(false));

                    if (saveHistory)
                    {
                        string historyDirectory = Path.GetDirectoryName(historyPath);
                        if (!string.IsNullOrEmpty(historyDirectory))
                        {
                            Directory.CreateDirectory(historyDirectory);
                        }
                        File.WriteAllText(historyPath, json, new UTF8Encoding(false));
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("[EconomyScanner] Failed to save snapshot: " + ex.Message);
                }
            });
        }

        public static void UploadAsync(EconomyScannerConfiguration config, string json)
        {
            if (!config.UploadEnabled || string.IsNullOrWhiteSpace(config.ApiEndpoint))
            {
                return;
            }

            string endpoint = config.ApiEndpoint.Trim();
            string headerName = string.IsNullOrWhiteSpace(config.ApiHeaderName) ? "X-Economy-Key" : config.ApiHeaderName.Trim();
            string apiKey = config.ApiKey ?? string.Empty;
            int timeoutMs = Math.Max(1, config.ApiTimeoutSeconds) * 1000;
            byte[] payload = Encoding.UTF8.GetBytes(json);
            bool verbose = config.VerboseLogging;

            Task.Run(delegate
            {
                try
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(endpoint);
                    request.Method = "POST";
                    request.ContentType = "application/json";
                    request.Accept = "application/json";
                    request.UserAgent = "Unturned-EconomyScanner/0.1.0";
                    request.Timeout = timeoutMs;
                    request.ReadWriteTimeout = timeoutMs;
                    request.ContentLength = payload.Length;

                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        request.Headers[headerName] = apiKey;
                    }

                    using (Stream stream = request.GetRequestStream())
                    {
                        stream.Write(payload, 0, payload.Length);
                    }

                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    {
                        if (verbose)
                        {
                            Logger.Log("[EconomyScanner] Snapshot upload returned HTTP " + (int)response.StatusCode + ".");
                        }
                    }
                }
                catch (WebException ex)
                {
                    HttpWebResponse response = ex.Response as HttpWebResponse;
                    string status = response == null ? "no HTTP response" : "HTTP " + (int)response.StatusCode;
                    Logger.LogError("[EconomyScanner] Snapshot upload failed (" + status + "): " + ex.Message);
                    if (response != null)
                    {
                        response.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("[EconomyScanner] Snapshot upload failed: " + ex.Message);
                }
            });
        }
    }
}

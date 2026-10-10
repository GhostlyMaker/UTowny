using UTowny.Visualization;
using UTowny.Configuration;

internal static class RefreshRegression
{
    public static void Run()
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var refresh = new PreviewRefresh<int>(TimeSpan.FromSeconds(1));
        var lastSent = Enumerable.Repeat(start, 128).ToArray();
        for (int i = 0; i < lastSent.Length; i++) refresh.Add(i, start);
        refresh.Complete(start, TimeSpan.FromSeconds(8));
        for (int tick = 1; tick < 80; tick++)
        {
            var now = start.AddMilliseconds(tick * 100);
            foreach (var point in refresh.TakeDue(now, 512)) lastSent[point] = now;
            Check(lastSent.All(sent => now - sent < TimeSpan.FromSeconds(2)), "A two-second effect disappeared before the eight-second deadline");
            Check(refresh.Expires == start.AddSeconds(8), "Refresh extended the preview deadline");
        }
        Check(refresh.IsExpired(start.AddSeconds(8)) && refresh.TakeDue(start.AddSeconds(8), 512).Count == 0,
            "Effect refreshed at expiry");
        Check(refresh.TakeDue(start.AddSeconds(20), 512).Count == 0, "Expired preview restarted");

        var loading = new PreviewRefresh<int>(TimeSpan.FromSeconds(1));
        loading.Add(1, start);
        Check(loading.TakeDue(start.AddSeconds(1), 512).SequenceEqual(new[] { 1 }), "Early markers disappeared while later batches were drawing");
        loading.Add(2, start.AddSeconds(1));
        loading.Complete(start.AddSeconds(2), TimeSpan.FromSeconds(8));
        Check(loading.Expires == start.AddSeconds(10), "Duration must start after initial drawing completes");
        loading.Stop();
        Check(loading.TakeDue(start.AddSeconds(3), 512).Count == 0, "Stopped preview resurrected");
        var replacement = new PreviewRefresh<int>(TimeSpan.FromSeconds(1));
        replacement.Add(99, start.AddSeconds(3));
        Check(replacement.TakeDue(start.AddSeconds(4), 512).SequenceEqual(new[] { 99 }), "Replacement inherited old marker positions");

        var large = new PreviewRefresh<int>(TimeSpan.FromSeconds(1));
        for (int i = 0; i < 1200; i++) large.Add(i, start);
        large.Complete(start, TimeSpan.FromSeconds(8));
        var batch1 = large.TakeDue(start.AddSeconds(1), 512);
        var batch2 = large.TakeDue(start.AddSeconds(1.1), 512);
        var batch3 = large.TakeDue(start.AddSeconds(1.2), 512);
        Check(batch1.Count == 512 && batch2.Count == 512 && batch3.Count == 176,
            "Refresh burst budget or fair cursor failed");
        Check(batch1.Concat(batch2).Concat(batch3).Distinct().Count() == 1200, "Refresh starved some markers");
        Check(large.TakeDue(start.AddSeconds(1.3), 512).Count == 0, "Premature duplicate refresh");
        var options = new UTownyOptions();
        Check(options.Visualization.RefreshSeconds == 1, "Old configs must receive a one-second refresh default");
        options.Visualization.RefreshSeconds = float.NaN;
        Check(new ConfigValidator().Validate(null, options).Failed, "Invalid refresh interval accepted");
        Console.WriteLine("PASS: two-second effects refreshed through eight seconds; expiry, stop, replacement, incremental drawing and bounded fair batches verified.");
    }
}

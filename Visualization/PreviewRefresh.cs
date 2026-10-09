namespace UTowny.Visualization;

// Used only on the game thread. A deterministic clock keeps refresh independent
// of the effect asset's advertised lifetime (which can differ from its particles).
public sealed class PreviewRefresh<T>
{
    private readonly List<(T Point, DateTime Due)> m_Points = new();
    private readonly TimeSpan m_Interval;
    private int m_Cursor;
    public DateTime Expires { get; private set; } = DateTime.MaxValue;
    public PreviewRefresh(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval));
        m_Interval = interval;
    }
    public void Add(T point, DateTime sent) => m_Points.Add((point, sent + m_Interval));
    public void Complete(DateTime now, TimeSpan duration) => Expires = now + duration;
    public void Stop() => Expires = DateTime.MinValue;
    public bool IsExpired(DateTime now) => now >= Expires;
    public IReadOnlyList<T> TakeDue(DateTime now, int budget)
    {
        var result = new List<T>();
        if (IsExpired(now) || budget <= 0) return result;
        for (int visited = 0; visited < m_Points.Count && result.Count < budget; visited++)
        {
            int index = m_Cursor;
            m_Cursor = (m_Cursor + 1) % m_Points.Count;
            var point = m_Points[index];
            if (point.Due > now) continue;
            result.Add(point.Point);
            // No catch-up bursts after a stalled frame, and never extend expiry.
            m_Points[index] = (point.Point, now + m_Interval);
        }
        return result;
    }
}

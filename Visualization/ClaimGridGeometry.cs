using UTowny.Domain.Claims;

namespace UTowny.Visualization;

public readonly record struct GridEdge(int X, int Z, bool AlongX);
public readonly record struct GridMarker(float X, float Z, bool Outer);
public sealed record GridPreview(int CellCount, int OuterEdges, int InternalEdges, IReadOnlyList<GridMarker> Markers);

// Pure geometry: no Unity, terrain, network, database or ownership mutations.
public static class ClaimGridGeometry
{
    public static GridPreview Build(IEnumerable<GridCoord> claims, string mapId, int size, float spacing, int maxMarkers)
    {
        if (size < 1 || float.IsNaN(spacing) || float.IsInfinity(spacing) || spacing < 0.5f || maxMarkers < 1)
            throw new ArgumentOutOfRangeException(nameof(spacing));
        var edges = new Dictionary<GridEdge, int>();
        var cells = new HashSet<GridCoord>();
        void Edge(GridEdge e) { edges.TryGetValue(e, out var count); edges[e] = count + 1; }
        foreach (var cell in claims)
        {
            if (cell.MapId != mapId || !cells.Add(cell)) continue;
            // Every cell contributes four edges, including shared internal edges.
            Edge(new(cell.X, cell.Z, true));
            Edge(new(cell.X, checked(cell.Z + 1), true));
            Edge(new(cell.X, cell.Z, false));
            Edge(new(checked(cell.X + 1), cell.Z, false));
            if (cells.Count > maxMarkers) throw new PreviewTooLargeException();
        }
        var points = new Dictionary<(long X, long Z), bool>();
        int steps = Math.Max(1, (int)Math.Ceiling(size / (double)spacing));
        // Integer sample keys make adjacent corners identical even for non-divisible spacing.
        foreach (var entry in edges)
        {
            var edge = entry.Key;
            for (int i = 0; i <= steps; i++)
            {
                var key = ((long)edge.X * steps + (edge.AlongX ? i : 0),
                           (long)edge.Z * steps + (edge.AlongX ? 0 : i));
                points.TryGetValue(key, out var outer);
                points[key] = outer || entry.Value == 1;
                if (points.Count > maxMarkers) throw new PreviewTooLargeException();
            }
        }
        var markers = points.Select(p => new GridMarker((float)(p.Key.X * (double)size / steps),
            (float)(p.Key.Z * (double)size / steps), p.Value)).ToArray();
        return new(cells.Count, edges.Count(e => e.Value == 1), edges.Count(e => e.Value == 2), markers);
    }
}

public sealed class PreviewTooLargeException : Exception { }

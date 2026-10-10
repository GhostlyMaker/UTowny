using Microsoft.Extensions.Options;
using UTowny.Configuration;
using UTowny.Domain.Claims;
using UTowny.Services;
using UTowny.Visualization;

internal static class GridRegression
{
    public static void Run()
    {
        GridCoord C(int x, int z, string map = "PEI") => new(map, x, z);
        GridPreview Draw(params GridCoord[] cells) => ClaimGridGeometry.Build(cells, "PEI", 64, 2, 4096);
        void Check(bool value, string message) { if (!value) throw new Exception(message); }

        var one = Draw(C(0, 0));
        Check(one.CellCount == 1 && one.OuterEdges == 4 && one.InternalEdges == 0 && one.Markers.Count == 128,
            "Single cell must have a dense complete 64m perimeter, not sixteen isolated points");
        Check(one.Markers.All(p => p.X == 0 || p.X == 64 || p.Z == 0 || p.Z == 64), "Marker outside perimeter");
        var two = Draw(C(0, 0), C(1, 0));
        Check(two.OuterEdges == 6 && two.InternalEdges == 1 && two.Markers.Count == 223, "Shared edge missing or duplicated");
        Check(two.Markers.Count(p => p.X == 64 && p.Z > 0 && p.Z < 64 && !p.Outer) == 31, "Internal divider is incomplete");
        Check(two.Markers.Select(p => (p.X, p.Z)).Distinct().Count() == two.Markers.Count, "Duplicate marker positions");
        var four = Draw(C(0, 0), C(1, 0), C(0, 1), C(1, 1));
        Check(four.CellCount == 4 && four.OuterEdges == 8 && four.InternalEdges == 4 && four.Markers.Count == 381,
            "Four saleable claim cells must show the outer border plus all internal dividers");
        var l = Draw(C(0, 0), C(1, 0), C(0, 1));
        Check(l.OuterEdges == 8 && l.InternalEdges == 2, "L-shaped boundary has wrong edges");
        Check(!l.Markers.Any(p => p.X > 64 && p.Z > 64), "Unclaimed corner was outlined as claimed");
        var ring = Draw(Enumerable.Range(0, 3).SelectMany(x => Enumerable.Range(0, 3)
            .Where(z => x != 1 || z != 1).Select(z => C(x, z))).ToArray());
        Check(ring.OuterEdges == 16 && ring.InternalEdges == 8, "Hole perimeter must remain an outer boundary");
        Check(ring.Markers.Any(p => p.X == 64 && p.Z == 96 && p.Outer), "Hole border missing");
        var separate = Draw(C(0, 0), C(3, 0));
        Check(separate.OuterEdges == 8 && separate.InternalEdges == 0 && !separate.Markers.Any(p => p.X > 64 && p.X < 192),
            "Disconnected claims must not be connected across wilderness");
        var negative = Draw(C(-1, -1), C(-1, -1), C(5, 5, "Washington"));
        Check(negative.CellCount == 1 && negative.Markers.Count == 128 && negative.Markers.All(p => p.X >= -64 && p.X <= 0 && p.Z >= -64 && p.Z <= 0),
            "Negative coordinates, duplicate claims, or map filtering failed");
        var uneven = ClaimGridGeometry.Build(new[] { C(0, 0), C(1, 0) }, "PEI", 64, 3, 4096);
        Check(uneven.Markers.Select(p => (p.X, p.Z)).Distinct().Count() == uneven.Markers.Count, "Non-divisible spacing duplicated shared corners");
        Check(uneven.Markers.Any(p => p.X == 128 && p.Z == 64), "Far corner missing with non-divisible spacing");
        try { ClaimGridGeometry.Build(new[] { C(0, 0), C(1, 0) }, "PEI", 64, 2, 128); throw new Exception("Oversized preview silently truncated"); }
        catch (PreviewTooLargeException) { }
        var options = new UTownyOptions();
        var grid = new GridService(Options.Create(options));
        Check(grid.FromWorld("PEI", -0.1f, -64.1f) == C(-1, -2), "Preview does not match ownership grid");
        Check(grid.Bounds(C(-1, -2)) == (-64f, 0f, -128f, -64f), "Cell dimensions shifted");
        var validator = new ConfigValidator();
        Check(!validator.Validate(null, options).Failed, "Backward-compatible defaults rejected");
        options.Visualization.MarkerSpacingMeters = float.NaN;
        Check(validator.Validate(null, options).Failed, "NaN spacing accepted");
        options.Visualization.MarkerSpacingMeters = 2;
        options.Visualization.MaxMarkers = 100000;
        Check(validator.Validate(null, options).Failed, "Unbounded marker budget accepted");
        Console.WriteLine("PASS: town borders and internal grid cover single, adjacent, four-cell, irregular, holed and disconnected claims; negative coordinates, maps, spacing, budgets and config validated.");
    }
}

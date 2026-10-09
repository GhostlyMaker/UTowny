using UTowny.Domain.Claims;
using UTowny.Domain.Common;

namespace UTowny.Domain.Plots;

// Half-open horizontal bounds: adjoining plots share an edge, never an area.
// Height is deliberately absent: each plot includes everything above and below.
public readonly record struct PlotRect(string MapId, int MinX, int MinZ, int MaxX, int MaxZ)
{
    public int Width => MaxX - MinX;
    public int Depth => MaxZ - MinZ;
    public long Area => (long)Width * Depth;
    public bool Valid => !string.IsNullOrWhiteSpace(MapId) && MinX >= -1000000 && MinZ >= -1000000
        && MaxX <= 1000000 && MaxZ <= 1000000 && Width > 0 && Depth > 0 && Width <= 1024 && Depth <= 1024;
    public bool Contains(float x, float z) => x >= MinX && x < MaxX && z >= MinZ && z < MaxZ;
    public bool Overlaps(PlotRect other) => MapId == other.MapId && MinX < other.MaxX && MaxX > other.MinX && MinZ < other.MaxZ && MaxZ > other.MinZ;
    public static int Snap(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || Math.Abs(value) > 1000000) throw new ArgumentOutOfRangeException(nameof(value));
        return (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }
    public static PlotRect FromCorners(string map, int x1, int z1, int x2, int z2) => new(map, Math.Min(x1,x2), Math.Min(z1,z2), Math.Max(x1,x2), Math.Max(z1,z2));
    public static PlotRect Cell(GridCoord grid, int size) => new(grid.MapId, checked(grid.X*size), checked(grid.Z*size), checked((grid.X+1)*size), checked((grid.Z+1)*size));
    public IEnumerable<GridCoord> Cells(int size)
    {
        int x0=(int)Math.Floor(MinX/(double)size), z0=(int)Math.Floor(MinZ/(double)size);
        int x1=(int)Math.Floor((MaxX-1)/(double)size), z1=(int)Math.Floor((MaxZ-1)/(double)size);
        if (!Valid || size < 1 || (long)(x1-x0+1)*(z1-z0+1)>256) throw new ArgumentException("Invalid or oversized plot");
        for(int x=x0;x<=x1;x++) for(int z=z0;z<=z1;z++) yield return new(MapId,x,z);
    }
}

public sealed record RectPlot(long Id, TownId TownId, string Name, PlotRect Bounds, PlayerId? Owner, bool ForSale, long Price, long ProtectionFlags);

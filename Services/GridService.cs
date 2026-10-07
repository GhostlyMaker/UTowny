using Microsoft.Extensions.Options;using UTowny.Configuration;using UTowny.Domain.Claims;
namespace UTowny.Services;
public interface IGridService { GridCoord FromWorld(string mapId,float x,float z); (float MinX,float MaxX,float MinZ,float MaxZ) Bounds(GridCoord g); bool Touches(GridCoord a,GridCoord b); }
public sealed class GridService:IGridService
{
 private readonly IOptions<UTownyOptions> m_Options;public GridService(IOptions<UTownyOptions> o)=>m_Options=o;
 public GridCoord FromWorld(string mapId,float x,float z){var s=m_Options.Value.Claims.GridSizeMeters;return new GridCoord(mapId,(int)Math.Floor(x/s),(int)Math.Floor(z/s));}
 public (float,float,float,float) Bounds(GridCoord g){var s=m_Options.Value.Claims.GridSizeMeters;var minx=g.X*s;var minz=g.Z*s;return(minx,minx+s,minz,minz+s);}
 public bool Touches(GridCoord a,GridCoord b)=>a.MapId==b.MapId&&Math.Abs(a.X-b.X)+Math.Abs(a.Z-b.Z)==1;
}

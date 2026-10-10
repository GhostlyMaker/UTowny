using UTowny.Domain.Claims;
using UTowny.Domain.Plots;
namespace UTowny.Visualization;
public readonly record struct PlanningMarker(float X,float Z,float Lift);
public sealed record PlanningLayout(IReadOnlyList<PlanningMarker> Markers,int NearbyPlots,bool Limited);
public static class PlanningGeometry
{
    public const float Radius=96f;
    public static bool Nearby(PlotRect b,float x,float z)=>b.MinX<=x+Radius&&b.MaxX>=x-Radius&&b.MinZ<=z+Radius&&b.MaxZ>=z-Radius;
    public static PlanningLayout Build(IEnumerable<GridCoord> claims,IEnumerable<RectPlot> plots,string map,int size,float x,float z,float spacing,int budget,long? selected,PlotRect? selection)
    {
        var points=new HashSet<PlanningMarker>();var limited=false;
        bool Add(float px,float pz,float lift)
        {
            if(Math.Abs(px-x)>Radius||Math.Abs(pz-z)>Radius)return true;
            var point=new PlanningMarker(px,pz,lift);
            if(points.Contains(point))return true;
            if(points.Count>=budget){limited=true;return false;}
            points.Add(point);return true;
        }
        void Edge(float x1,float z1,float x2,float z2,float lift)
        {
            int steps=Math.Max(1,(int)Math.Ceiling((Math.Abs(x2-x1)+Math.Abs(z2-z1))/spacing));
            for(int i=0;i<=steps;i++)if(!Add(x1+(x2-x1)*i/steps,z1+(z2-z1)*i/steps,lift))return;
        }
        void Rect(PlotRect b,float lift)
        {
            Edge(b.MinX,b.MinZ,b.MaxX,b.MinZ,lift);Edge(b.MinX,b.MaxZ,b.MaxX,b.MaxZ,lift);
            Edge(b.MinX,b.MinZ,b.MinX,b.MaxZ,lift);Edge(b.MaxX,b.MinZ,b.MaxX,b.MaxZ,lift);
        }
        var nearby=plots.Where(p=>p.Bounds.MapId==map&&Nearby(p.Bounds,x,z)).OrderBy(p=>p.Id!=selected)
            .ThenBy(p=>Math.Pow((p.Bounds.MinX+p.Bounds.MaxX)/2f-x,2)+Math.Pow((p.Bounds.MinZ+p.Bounds.MaxZ)/2f-z,2)).ToArray();
        // A second raised outline distinguishes the focus with any configured effect.
        if(selection is { } candidate&&candidate.Valid&&candidate.MapId==map)Rect(candidate,0.7f);
        foreach(var plot in nearby){Rect(plot.Bounds,0);if(plot.Id==selected)Rect(plot.Bounds,0.7f);}
        var cells=new HashSet<GridCoord>(claims.Where(c=>c.MapId==map));
        foreach(var cell in cells.Where(c=>PlanningGeometry.Nearby(PlotRect.Cell(c,size),x,z)))
        {
            var b=PlotRect.Cell(cell,size);
            // Town perimeter only, so internal claim lines do not obscure plot layouts.
            if(!cells.Contains(cell with{Z=cell.Z-1}))Edge(b.MinX,b.MinZ,b.MaxX,b.MinZ,0);
            if(!cells.Contains(cell with{Z=cell.Z+1}))Edge(b.MinX,b.MaxZ,b.MaxX,b.MaxZ,0);
            if(!cells.Contains(cell with{X=cell.X-1}))Edge(b.MinX,b.MinZ,b.MinX,b.MaxZ,0);
            if(!cells.Contains(cell with{X=cell.X+1}))Edge(b.MaxX,b.MinZ,b.MaxX,b.MaxZ,0);
        }
        return new(points.ToArray(),nearby.Length,limited);
    }
}

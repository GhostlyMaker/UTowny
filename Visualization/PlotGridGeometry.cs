using UTowny.Domain.Plots;
namespace UTowny.Visualization;
public static class PlotGridGeometry
{
    public static GridPreview Build(PlotRect rect,float spacing,int maxMarkers)
    {
        if(!rect.Valid||float.IsNaN(spacing)||float.IsInfinity(spacing)||spacing<0.5f||maxMarkers<1)throw new ArgumentException("Invalid plot preview");
        var points=new HashSet<(float X,float Z)>();
        void Edge(int x1,int z1,int x2,int z2)
        {
            int steps=Math.Max(1,(int)Math.Ceiling((Math.Abs(x2-x1)+Math.Abs(z2-z1))/(double)spacing));
            for(int i=0;i<=steps;i++)
            {
                points.Add(((float)(x1+(x2-x1)*(double)i/steps),(float)(z1+(z2-z1)*(double)i/steps)));
                if(points.Count>maxMarkers)throw new PreviewTooLargeException();
            }
        }
        Edge(rect.MinX,rect.MinZ,rect.MaxX,rect.MinZ);Edge(rect.MaxX,rect.MinZ,rect.MaxX,rect.MaxZ);
        Edge(rect.MinX,rect.MaxZ,rect.MaxX,rect.MaxZ);Edge(rect.MinX,rect.MinZ,rect.MinX,rect.MaxZ);
        return new(1,4,0,points.Select(p=>new GridMarker(p.X,p.Z,true)).ToArray());
    }
}

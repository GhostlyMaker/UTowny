using System.Collections.Concurrent;
using UTowny.Domain.Common;
using UTowny.Domain.Plots;
namespace UTowny.Services;

public sealed record PlotSelection(TownId Town, string Map, (int X,int Z)? First, (int X,int Z)? Second, DateTime Updated)
{
    public PlotRect? Bounds => First is { } a && Second is { } b ? PlotRect.FromCorners(Map,a.X,a.Z,b.X,b.Z) : null;
}
public sealed class PlotSelectionService
{
    private readonly ConcurrentDictionary<ulong,PlotSelection> m_Selections=new();
    public PlotSelection Set(PlayerId player,TownId town,string map,bool first,float x,float z)
    {
        var point=(PlotRect.Snap(x),PlotRect.Snap(z));var now=DateTime.UtcNow;
        return m_Selections.AddOrUpdate(player.Value,
            _=>new(town,map,first?point:null,first?null:point,now),
            (_,s)=>s.Town!=town||s.Map!=map||s.Updated.AddMinutes(30)<now
                ?new(town,map,first?point:null,first?null:point,now)
                :s with{First=first?point:s.First,Second=first?s.Second:point,Updated=now});
    }
    public PlotSelection? Get(PlayerId player) => m_Selections.TryGetValue(player.Value,out var s)&&s.Updated.AddMinutes(30)>=DateTime.UtcNow?s:null;
    public void Clear(PlayerId player)=>m_Selections.TryRemove(player.Value,out _);
}

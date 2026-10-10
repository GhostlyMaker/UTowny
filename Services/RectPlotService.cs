using Microsoft.Extensions.Options;
using UTowny.Caching;
using UTowny.Configuration;
using UTowny.Domain.Claims;
using UTowny.Domain.Common;
using UTowny.Domain.Plots;
using UTowny.Persistence.Database;
using UTowny.Protection;

namespace UTowny.Services;
public sealed class RectPlotService
{
    private readonly IDatabaseConnectionFactory m_Db;
    private readonly IWorldStateCache m_Cache;
    private readonly IAuthorizationService m_Auth;
    private readonly MutationGate m_Gate;
    private readonly IOptions<UTownyOptions> m_Options;
    public RectPlotService(IDatabaseConnectionFactory db,IWorldStateCache cache,IAuthorizationService auth,MutationGate gate,IOptions<UTownyOptions> options)
    {m_Db=db;m_Cache=cache;m_Auth=auth;m_Gate=gate;m_Options=options;}

    public Result Validate(PlayerId actor,TownId town,PlotRect bounds,long? excludingId=null)
    {
        if(!m_Auth.Can(actor,town,TownAction.ManagePlot))return Result.Fail("insufficient_role");
        if(!bounds.Valid)return Result.Fail("rect_invalid");
        GridCoord[] cells;
        try {cells=bounds.Cells(m_Options.Value.Claims.GridSizeMeters).ToArray();}
        catch(ArgumentException){return Result.Fail("rect_invalid");}
        foreach(var cell in cells)
        {
            var claim=m_Cache.GetClaim(cell);
            if(claim==null||claim.TownId!=town)return Result.Fail("rect_outside");
            if(claim.PlotOwner!=null||claim.ForSale)return Result.Fail("rect_legacy_overlap");
        }
        if(m_Cache.GetTownPlots(town).Any(p=>p.Id!=excludingId&&p.Bounds.Overlaps(bounds)))return Result.Fail("rect_overlap");
        return Result.Ok();
    }
    public Task<Result<RectPlot>> CreateAsync(PlayerId actor,TownId town,PlotRect bounds,string name,long price)=>CreateCoreAsync(actor,town,bounds,name,price,false);
    public Task<Result<RectPlot>> CreateDraftAsync(PlayerId actor,TownId town,PlotRect bounds,string name,long price=0)=>CreateCoreAsync(actor,town,bounds,name,price,true);
    private Task<Result<RectPlot>> CreateCoreAsync(PlayerId actor,TownId town,PlotRect bounds,string name,long price,bool draft)=>m_Gate.RunAsync(async()=>
    {
        if(price<0)return Result<RectPlot>.Fail("invalid_amount");
        if(string.IsNullOrWhiteSpace(name)||name.Length>32||name.Any(c=>!((c>='a'&&c<='z')||(c>='A'&&c<='Z')||(c>='0'&&c<='9')||c=='_'||c=='-')))
            return Result<RectPlot>.Fail("rect_name_invalid");
        var valid=Validate(actor,town,bounds);if(!valid.Success)return Result<RectPlot>.Fail(valid.Code);
        if(m_Cache.GetTownPlots(town).Any(p=>string.Equals(p.Name,name,StringComparison.OrdinalIgnoreCase)))return Result<RectPlot>.Fail("rect_name_taken");
        using var db=await m_Db.OpenAsync();using var s=new SqlSession(db);
        s.Execute("INSERT INTO rect_plots(town_id,name,map_id,min_x,min_z,max_x,max_z,for_sale,price,draft) VALUES($0,$1,$2,$3,$4,$5,$6,$8,$7,$9)",town.Value,name,bounds.MapId,bounds.MinX,bounds.MinZ,bounds.MaxX,bounds.MaxZ,price,draft?0:1,draft?1:0);
        var id=s.Number("SELECT last_insert_rowid()");s.Commit();await m_Cache.RebuildAsync();
        return Result<RectPlot>.Ok(m_Cache.GetTownPlots(town).Single(p=>p.Id==id),"rect_created");
    },change:new UTowny.Api.Events.DomainOperation("plot.create",actor,town));

    public Task<Result> EditAsync(PlayerId actor,TownId town,string name,string action,PlotRect? bounds=null,string? newName=null,long price=0,int dx=0,int dz=0)=>m_Gate.RunAsync(async()=>
    {
        if(!m_Auth.Can(actor,town,TownAction.ManagePlot))return Result.Fail("insufficient_role");
        var plot=m_Cache.GetTownPlots(town).FirstOrDefault(p=>string.Equals(p.Name,name,StringComparison.OrdinalIgnoreCase));
        if(plot==null)return Result.Fail("plot_not_found");
        if(plot.Owner!=null)return Result.Fail("plan_owned");
        if(action!="publish"&&action!="unpublish"&&action!="delete"&&!plot.IsDraft)return Result.Fail("plan_not_draft");
        using var db=await m_Db.OpenAsync();using var s=new SqlSession(db);
        switch(action)
        {
            case "publish":
                if(price<0)return Result.Fail("invalid_amount");
                var valid=Validate(actor,town,plot.Bounds,plot.Id);if(!valid.Success)return valid;
                s.Execute("UPDATE rect_plots SET draft=0,for_sale=1,price=$0 WHERE id=$1",price,plot.Id);break;
            case "unpublish":
                s.Execute("UPDATE rect_plots SET draft=1,for_sale=0 WHERE id=$0",plot.Id);break;
            case "price":
                if(price<0)return Result.Fail("invalid_amount");
                s.Execute("UPDATE rect_plots SET price=$0 WHERE id=$1",price,plot.Id);break;
            case "rename":
                if(string.IsNullOrWhiteSpace(newName)||newName.Length>32||newName.Any(c=>!((c>='a'&&c<='z')||(c>='A'&&c<='Z')||(c>='0'&&c<='9')||c=='_'||c=='-')))return Result.Fail("rect_name_invalid");
                if(m_Cache.GetTownPlots(town).Any(p=>p.Id!=plot.Id&&string.Equals(p.Name,newName,StringComparison.OrdinalIgnoreCase)))return Result.Fail("rect_name_taken");
                s.Execute("UPDATE rect_plots SET name=$0 WHERE id=$1",newName,plot.Id);break;
            case "move":
            case "resize":
                var target=action=="move"?plot.Bounds with{MinX=checked(plot.Bounds.MinX+dx),MaxX=checked(plot.Bounds.MaxX+dx),MinZ=checked(plot.Bounds.MinZ+dz),MaxZ=checked(plot.Bounds.MaxZ+dz)}:bounds;
                if(target is not { } rect||rect.MapId!=plot.Bounds.MapId)return Result.Fail("rect_invalid");
                var validation=Validate(actor,town,rect,plot.Id);if(!validation.Success)return validation;
                s.Execute("UPDATE rect_plots SET min_x=$0,min_z=$1,max_x=$2,max_z=$3 WHERE id=$4",rect.MinX,rect.MinZ,rect.MaxX,rect.MaxZ,plot.Id);break;
            case "delete":s.Execute("DELETE FROM rect_plots WHERE id=$0",plot.Id);break;
            default:return Result.Fail("syntax");
        }
        s.Commit();await m_Cache.RebuildAsync();return Result.Ok("plan_"+action);
    },change:new UTowny.Api.Events.DomainOperation("plot."+action,actor,town));

    // Resolve the target again inside the serialized mutation, never trust a pre-await selection.
    public Task<Result> ChangeAsync(PlayerId actor,GridCoord grid,float x,float z,string action,long price=0,LandAction permission=LandAction.Build,bool allow=false)=>m_Gate.RunAsync(async()=>
    {
        var plot=m_Cache.GetPlotAt(grid,x,z);if(plot==null)return Result.Fail("plot_not_found");
        var leader=m_Auth.Can(actor,plot.TownId,TownAction.ManagePlot);
        using var db=await m_Db.OpenAsync();using var s=new SqlSession(db);
        string code;
        switch(action)
        {
            case "buy":
                if(plot.Owner!=null)return Result.Fail("plot_already_owned");
                if(plot.IsDraft)return Result.Fail("plan_draft_buy");
                if(!plot.ForSale)return Result.Fail("plot_not_for_sale");
                if(!m_Options.Value.Plots.OutsidersCanBuy&&m_Cache.GetMembership(actor)?.TownId!=plot.TownId)return Result.Fail("not_in_town");
                if(s.Execute("UPDATE players SET balance=balance-$0 WHERE steam64=$1 AND balance>=$0",plot.Price,unchecked((long)actor.Value))!=1)return Result.Fail("insufficient_balance");
                if(s.Execute("UPDATE towns SET bank_balance=bank_balance+$0 WHERE id=$1 AND bank_balance<=9223372036854775807-$0",plot.Price,plot.TownId.Value)!=1)return Result.Fail("rect_bank_full");
                if(s.Execute("UPDATE rect_plots SET owner_steam64=$0,for_sale=0,price=0 WHERE id=$1 AND owner_steam64 IS NULL AND for_sale=1",unchecked((long)actor.Value),plot.Id)!=1)return Result.Fail("plot_state_changed");
                code="rect_bought";break;
            case "forsale":
                if(!leader)return Result.Fail("insufficient_role");
                if(plot.Owner!=null)return Result.Fail("plot_already_owned");
                if(price<0)return Result.Fail("invalid_amount");
                s.Execute("UPDATE rect_plots SET draft=0,for_sale=1,price=$0 WHERE id=$1",price,plot.Id);code="rect_listed";break;
            case "notforsale":
                if(!leader)return Result.Fail("insufficient_role");
                s.Execute("UPDATE rect_plots SET for_sale=0,price=0 WHERE id=$0",plot.Id);code="rect_unlisted";break;
            case "permissions":
                if(!leader&&plot.Owner!=actor)return Result.Fail("insufficient_role");
                if(!Enum.IsDefined(typeof(LandAction),permission))return Result.Fail("invalid_target");
                var flags=allow?plot.ProtectionFlags|(long)permission:plot.ProtectionFlags&~(long)permission;
                s.Execute("UPDATE rect_plots SET protection_flags=$0 WHERE id=$1",flags,plot.Id);code="rect_permissions";break;
            case "release":
                if(plot.Owner!=actor)return Result.Fail("rect_not_owner");
                s.Execute("UPDATE rect_plots SET owner_steam64=NULL,for_sale=0,price=0,protection_flags=0 WHERE id=$0",plot.Id);code="rect_released";break;
            case "delete":
                if(!leader)return Result.Fail("insufficient_role");
                if(plot.Owner!=null)return Result.Fail("rect_owned_delete");
                s.Execute("DELETE FROM rect_plots WHERE id=$0",plot.Id);code="rect_deleted";break;
            default:return Result.Fail("syntax");
        }
        s.Commit();await m_Cache.RebuildAsync();return Result.Ok(code);
    },change:new UTowny.Api.Events.DomainOperation("plot."+action,actor));
}

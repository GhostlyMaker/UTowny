using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenMod.API.Eventing;
using OpenMod.API.Plugins;
using UTowny.Caching;
using UTowny.Configuration;
using UTowny.Domain.Claims;
using UTowny.Domain.Common;
using UTowny.Domain.Plots;
using UTowny.Persistence.Database;
using UTowny.Persistence.Repositories;
using UTowny.Services;
using UTowny.Visualization;

internal static class PlanningRegression
{
    public static async Task RunAsync()
    {
        void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        var directory=Path.Combine(Path.GetTempPath(),"utowny-planning-"+Guid.NewGuid());
        try
        {
            var options=Options.Create(new UTownyOptions());var db=new DatabaseConnectionFactory(directory,options);
            await new SchemaMigrator(db,NullLogger<SchemaMigrator>.Instance).MigrateAsync();
            var schema=new ExtendedSchema(db,options);await schema.InitializeAsync();
            var cache=new WorldStateCache(new WorldRepository(db));await cache.RebuildAsync();
            var events=new DomainEventPublisher(DispatchProxy.Create<IEventBus,NoopEvents>(),NullLogger<DomainEventPublisher>.Instance);
            events.Initialize(DispatchProxy.Create<IOpenModPlugin,NoopEvents>());
            var gate=new MutationGate(cache,events);var players=new PlayerRepository(db);var playtime=new PlaytimeService(players,options,gate);
            var towns=new TownService(db,cache,playtime,players,options,NullLogger<TownService>.Instance,gate);
            var mayor=new PlayerId(1);var buyer=new PlayerId(2);
            var town=(await towns.CreateAsAdminAsync(mayor,"PlanTown")).Value!;
            await players.EnsureAsync(buyer,1000);
            using(var c=await db.OpenAsync())using(var s=new SqlSession(c))
            {
                s.Execute("INSERT INTO town_members VALUES($0,2,0,$1,0)",town.Id.Value,DateTime.UtcNow.ToString("O"));
                s.Execute("INSERT INTO claims(town_id,map_id,grid_x,grid_z) VALUES($0,'PEI',0,0),($0,'PEI',1,0)",town.Id.Value);
                // A real v4 row is kept listed, not silently changed into a draft on upgrade.
                s.Execute("ALTER TABLE rect_plots DROP COLUMN draft; UPDATE schema_version SET version=4;");
                s.Execute("INSERT INTO rect_plots(town_id,name,map_id,min_x,min_z,max_x,max_z,for_sale,price) VALUES($0,'Existing','PEI',80,0,90,10,1,50)",town.Id.Value);
                s.Commit();
            }
            await schema.InitializeAsync();await schema.InitializeAsync();await cache.RebuildAsync();
            Check(cache.GetTownPlots(town.Id).Single().ForSale&&!cache.GetTownPlots(town.Id).Single().IsDraft,"Upgrade changed a live listing");
            var service=new RectPlotService(db,cache,new AuthorizationService(cache,options),gate,options);
            PlotRect Rect(int x1,int z1,int x2,int z2)=>new("PEI",x1,z1,x2,z2);
            var a=await service.CreateDraftAsync(mayor,town.Id,Rect(0,0,16,16),"HouseA",100);
            var b=await service.CreateDraftAsync(mayor,town.Id,Rect(20,0,36,16),"HouseB",150);
            Check(a.Success&&b.Success&&a.Value!.IsDraft&&!a.Value.ForSale&&a.Value.Price==100,"New draft was not safely staged");
            Check((await service.ChangeAsync(buyer,new("PEI",0,0),5,5,"buy")).Code=="plan_draft_buy","Draft could be bought");
            Check((await players.GetAsync(buyer))!.Balance==1000,"Draft purchase changed balance");
            Check((await service.EditAsync(buyer,town.Id,"HouseA","publish",price:100)).Code=="insufficient_role","Resident published a draft");
            Check((await service.EditAsync(mayor,town.Id,"HouseA","move",dx:10)).Code=="rect_overlap","Overlapping move accepted");
            Check(cache.GetTownPlots(town.Id).Single(p=>p.Name=="HouseA").Bounds==Rect(0,0,16,16),"Rejected move changed plot");
            Check((await service.EditAsync(mayor,town.Id,"HouseA","move",dx:-1)).Code=="rect_outside","Move outside claims accepted");
            Check((await service.EditAsync(mayor,town.Id,"HouseA","move",dz:20)).Success,"Valid move failed");
            Check((await service.EditAsync(mayor,town.Id,"HouseA","resize",bounds:Rect(0,20,18,38))).Success,"Valid resize failed");
            Check((await service.EditAsync(mayor,town.Id,"HouseA","rename",newName:"HOUSEB")).Code=="rect_name_taken","Rename duplicated name");
            Check((await service.EditAsync(mayor,town.Id,"HouseA","rename",newName:"GardenHome")).Success,"Rename failed");
            Check((await service.EditAsync(mayor,town.Id,"GardenHome","price",price:175)).Success,"Draft price failed");
            await cache.RebuildAsync();var saved=cache.GetTownPlots(town.Id).Single(p=>p.Name=="GardenHome");
            Check(saved.IsDraft&&saved.Bounds==Rect(0,20,18,38)&&saved.Price==175&&!saved.ForSale,"Draft changes did not survive reload");
            Check((await service.EditAsync(mayor,town.Id,"GardenHome","publish",price:175)).Success,"Publish failed");
            Check((await service.EditAsync(mayor,town.Id,"GardenHome","move",dx:1)).Code=="plan_not_draft","Live listing moved without unpublishing");
            Check((await service.EditAsync(mayor,town.Id,"GardenHome","unpublish")).Success,"Unpublish failed");
            Check((await service.ChangeAsync(buyer,new("PEI",0,0),5,25,"buy")).Code=="plan_draft_buy","Unpublished plot still buyable");
            Check((await service.EditAsync(mayor,town.Id,"GardenHome","publish",price:175)).Success,"Republish failed");
            Check((await service.ChangeAsync(buyer,new("PEI",0,0),5,25,"buy")).Success,"Published draft purchase failed");
            foreach(var action in new[]{"move","resize","rename","price","publish","unpublish","delete"})
                Check((await service.EditAsync(mayor,town.Id,"GardenHome",action,bounds:Rect(1,20,19,38),newName:"Renamed",price:1,dx:1)).Code=="plan_owned","Planning changed an owned plot: "+action);
            Check((await players.GetAsync(buyer))!.Balance==825&&cache.GetTown(town.Id)!.BankBalance==175,"Published sale accounting incorrect");
            Check((await service.EditAsync(mayor,town.Id,"HouseB","delete")).Success,"Draft deletion failed");
            Check((await service.CreateDraftAsync(mayor,town.Id,Rect(20,0,36,16),"NewDraft")).Success,"Deleted draft still reserves space");
            try
            {
                using var c=await db.OpenAsync();using var s=new SqlSession(c);
                s.Execute("UPDATE rect_plots SET draft=1 WHERE name='GardenHome'");
                throw new Exception("Database allowed owned draft");
            }
            catch(SqliteException ex)when(ex.SqliteErrorCode==19){}
            var focus=new PlotSelectionService();focus.Focus(mayor,town.Id,saved.Id);focus.Clear(mayor);
            Check(focus.Focused(mayor,town.Id)==saved.Id,"Clearing corners unexpectedly cleared named focus");
            focus.ClearFocus(mayor);Check(focus.Focused(mayor,town.Id)==null,"Focus did not clear");
            var plots=cache.GetTownPlots(town.Id);
            var layout=PlanningGeometry.Build(new[]{new GridCoord("PEI",0,0),new("PEI",1,0)},plots,"PEI",64,32,32,2,2048,saved.Id,null);
            Check(layout.NearbyPlots==3&&!layout.Limited,"Planning did not show all nearby draft/live/owned plots");
            Check(layout.Markers.Any(p=>p.Lift>0&&p.X==0&&p.Z==20),"Selected plot not emphasised");
            Check(layout.Markers.Any(p=>p.X==20&&p.Z==0)&&layout.Markers.Any(p=>p.X==80&&p.Z==0),"Other nearby outlines missing");
            Check(!layout.Markers.Any(p=>p.X==64&&p.Z==32),"Internal claim divider obscured planning view");
            Check(layout.Markers.All(p=>Math.Abs(p.X-32)<=96&&Math.Abs(p.Z-32)<=96),"Nearby view exceeded radius");
            var clipped=PlanningGeometry.Build(Array.Empty<GridCoord>(),plots,"PEI",64,32,32,0.5f,10,saved.Id,null);
            Check(clipped.Limited&&clipped.Markers.Count==10,"Budget should be bounded and truncation reported");
            var wrongMap=PlanningGeometry.Build(new[]{new GridCoord("PEI",0,0)},plots,"Washington",64,32,32,2,2048,null,null);
            Check(wrongMap.Markers.Count==0,"Planning leaked another map");
            Console.WriteLine("PASS: planning migration, saved drafts, blocked purchases, move/resize/rename/price validation, publish/unpublish, owned-plot safety, focus and bounded nearby multi-plot geometry.");
        }
        finally{SqliteConnection.ClearAllPools();if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}

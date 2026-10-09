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
using UTowny.Protection;
using UTowny.Services;
using UTowny.Visualization;

internal static class PlotRegression
{
    public static async Task RunAsync()
    {
        void Check(bool value,string message){if(!value)throw new Exception(message);}
        var directory=Path.Combine(Path.GetTempPath(),"utowny-plots-"+Guid.NewGuid());
        try
        {
            var options=Options.Create(new UTownyOptions());
            var db=new DatabaseConnectionFactory(directory,options);
            await new SchemaMigrator(db,NullLogger<SchemaMigrator>.Instance).MigrateAsync();
            var schema=new ExtendedSchema(db,options);await schema.InitializeAsync();
            var cache=new WorldStateCache(new WorldRepository(db));await cache.RebuildAsync();
            var events=new DomainEventPublisher(DispatchProxy.Create<IEventBus,NoopEvents>(),NullLogger<DomainEventPublisher>.Instance);
            events.Initialize(DispatchProxy.Create<IOpenModPlugin,NoopEvents>());
            var gate=new MutationGate(cache,events);
            var players=new PlayerRepository(db);
            var playtime=new PlaytimeService(players,options,gate);
            var towns=new TownService(db,cache,playtime,players,options,NullLogger<TownService>.Instance,gate);
            var mayor=new PlayerId(1);var resident=new PlayerId(2);var other=new PlayerId(3);var outsider=new PlayerId(4);
            var town=(await towns.CreateAsAdminAsync(mayor,"PlotsTown")).Value!;
            var foreign=(await towns.CreateAsAdminAsync(new PlayerId(5),"Foreign")).Value!;
            await players.EnsureAsync(resident,1000);await players.EnsureAsync(other,1000);await players.EnsureAsync(outsider,1000);
            // Recreate a genuine v3 state containing both owned and listed legacy cells.
            using(var connection=await db.OpenAsync())using(var s=new SqlSession(connection))
            {
                s.Execute("DROP TRIGGER rect_plots_member_removed; DROP TABLE rect_plots; UPDATE schema_version SET version=3;");
                s.Execute("INSERT INTO town_members VALUES($0,$1,0,$2,0),($0,$3,0,$2,0)",town.Id.Value,(long)resident.Value,DateTime.UtcNow.ToString("O"),(long)other.Value);
                foreach(var g in new[]{new GridCoord("PEI",0,0),new("PEI",1,0),new("PEI",-1,0),new("PEI",0,1),new("PEI",1,1)})
                    s.Execute("INSERT INTO claims(town_id,map_id,grid_x,grid_z) VALUES($0,$1,$2,$3)",town.Id.Value,g.MapId,g.X,g.Z);
                s.Execute("INSERT INTO claims(town_id,map_id,grid_x,grid_z) VALUES($0,'PEI',2,0)",foreign.Id.Value);
                s.Execute("UPDATE claims SET plot_owner_steam64=$0,protection_flags=8 WHERE grid_x=1 AND grid_z=1",(long)resident.Value);
                s.Execute("UPDATE claims SET for_sale=1,price=500 WHERE grid_x=0 AND grid_z=1");
                s.Commit();
            }
            await schema.InitializeAsync();await schema.InitializeAsync();await cache.RebuildAsync();
            Check(cache.GetClaim(new("PEI",1,1))?.PlotOwner==resident&&cache.GetClaim(new("PEI",1,1))?.ProtectionFlags==8,"Migration changed legacy owner or flags");
            Check(cache.GetClaim(new("PEI",0,1))?.Price==500&&cache.GetClaim(new("PEI",0,1))?.ForSale==true,"Migration changed legacy listing");
            var auth=new AuthorizationService(cache,options);var grid=new GridService(options);
            var service=new RectPlotService(db,cache,auth,gate,options);
            var legacy=new PlotService(db,cache,auth,options,gate);
            var land=new LandManagementService(db,cache,auth,gate,grid);
            var wars=new WarService(db,cache,gate,options);
            var protection=new ProtectionService(cache,wars,gate){Ready=true};
            var management=new TownManagementService(db,cache,auth,gate,grid,options);
            var admin=new AdminService(db,cache,gate,wars,NullLogger<AdminService>.Instance,grid);
            PlotRect Rect(int x1,int z1,int x2,int z2,string map="PEI")=>new(map,x1,z1,x2,z2);
            Check((await service.CreateAsync(resident,town.Id,Rect(0,0,32,32),"Denied",100)).Code=="insufficient_role","Resident created plot");
            Check((await service.CreateAsync(mayor,town.Id,Rect(32,0,32,32),"Empty",100)).Code=="rect_invalid","Zero area accepted");
            Check((await service.CreateAsync(mayor,town.Id,Rect(0,0,32,32),"Bad name",100)).Code=="rect_name_invalid","Unsafe name accepted");
            Check((await service.CreateAsync(mayor,town.Id,Rect(0,0,32,32),"BadPrice",-1)).Code=="invalid_amount","Negative price accepted");
            Check((await service.CreateAsync(mayor,town.Id,Rect(120,0,136,8),"Foreign",100)).Code=="rect_outside","Foreign claim included");
            Check((await service.CreateAsync(mayor,town.Id,Rect(0,-8,8,8),"Wilderness",100)).Code=="rect_outside","Wilderness included");
            Check((await service.CreateAsync(mayor,town.Id,Rect(0,0,8,8,"Washington"),"WrongMap",100)).Code=="rect_outside","Wrong map included");
            Check((await service.CreateAsync(mayor,town.Id,Rect(0,64,32,96),"ListedLegacy",100)).Code=="rect_legacy_overlap","Legacy listing overlapped");
            Check((await service.CreateAsync(mayor,town.Id,Rect(64,64,96,96),"OwnedLegacy",100)).Code=="rect_legacy_overlap","Legacy owner overlapped");
            var a=await service.CreateAsync(mayor,town.Id,Rect(0,0,32,32),"A",100);Check(a.Success,"First quadrant failed");
            Check((await service.CreateAsync(mayor,town.Id,Rect(16,16,48,48),"Overlap",100)).Code=="rect_overlap","Overlap accepted");
            Check((await service.CreateAsync(mayor,town.Id,Rect(32,0,64,32),"a",100)).Code=="rect_name_taken","Case-insensitive name duplicated");
            foreach(var (name,bounds) in new[]{("B",Rect(32,0,64,32)),("C",Rect(0,32,32,64)),("D",Rect(32,32,64,64))})
                Check((await service.CreateAsync(mayor,town.Id,bounds,name,100)).Success,"Adjacent quadrant failed: "+name);
            Check(cache.GetTownPlots(town.Id).Count==4,"Four quadrant plots not stored");
            Check(cache.GetPlotAt(new("PEI",0,0),32,5)?.Name=="B","Shared edge belongs to wrong plot");
            Check(cache.GetPlotAt(new("PEI",0,0),32,32)?.Name=="D","Shared corner belongs to wrong plot");
            var arbitrary=await service.CreateAsync(mayor,town.Id,Rect(70,3,83,20),"OddSize",2000);Check(arbitrary.Success,"Arbitrary metre-sized rectangle failed");
            var negative=await service.CreateAsync(mayor,town.Id,Rect(-16,0,0,32),"Negative",100);Check(negative.Success,"Negative coordinates failed");
            Check((await legacy.SetForSaleAsync(mayor,new("PEI",0,0),100)).Code=="rect_cell_in_use","Legacy API sold subdivided cell");
            Check((await legacy.BuyAsync(resident,new("PEI",0,0))).Code=="rect_cell_in_use","Legacy buy ignored rectangles");
            Check((await land.PermissionAsync(mayor,new("PEI",0,0),LandAction.Build,true)).Code=="rect_cell_in_use","Legacy permission changed subdivided cell");
            Check((await land.UnclaimAsync(mayor,new("PEI",0,0))).Code=="rect_cell_in_use","Town unclaimed under plot");
            Check((await admin.ExecuteAsync("console","unclaim","","",new("PEI",0,0))).Code=="rect_cell_in_use","Admin unclaimed under plot");
            Check((await service.ChangeAsync(outsider,new("PEI",0,0),5,5,"buy")).Code=="not_in_town","Outsider bought with outsiders disabled");
            Check((await service.ChangeAsync(resident,new("PEI",1,0),75,5,"buy")).Code=="insufficient_balance","Overdraft purchase accepted");
            Check((await players.GetAsync(resident))?.Balance==1000,"Failed buy debited player");
            var purchases=await Task.WhenAll(service.ChangeAsync(resident,new("PEI",0,0),5,5,"buy"),service.ChangeAsync(other,new("PEI",0,0),5,5,"buy"));
            Check(purchases.Count(p=>p.Success)==1,"Concurrent purchase assigned two owners");
            var owner=cache.GetPlotAt(new("PEI",0,0),5,5)!.Owner!.Value;var loser=owner==resident?other:resident;
            Check((await players.GetAsync(owner))?.Balance==900&&(await players.GetAsync(loser))?.Balance==1000&&cache.GetTown(town.Id)!.BankBalance==100,"Purchase did not transfer exactly one price");
            foreach(var action in new[]{LandAction.Build,LandAction.Damage,LandAction.Salvage,LandAction.Interact,LandAction.Vehicle})
            {
                Check(protection.CanAt(owner,new("PEI",0,0),5,5,action),"Owner denied in own full-height plot");
                Check(!protection.CanAt(loser,new("PEI",0,0),5,5,action),"Non-owner allowed inside protected rectangle");
                Check(!protection.CanAt(owner,new("PEI",0,0),33,5,action),"Owner authority leaked into neighbouring plot");
                Check(protection.CanAt(mayor,new("PEI",0,0),5,5,action),"Mayor denied");
            }
            Check(!protection.Can(owner,new("PEI",0,0),LandAction.Build),"Grid-only API silently bypassed rectangle protection");
            Check((await service.ChangeAsync(loser,new("PEI",0,0),5,5,"permissions",permission:LandAction.Interact,allow:true)).Code=="insufficient_role","Non-owner changed flags");
            Check((await service.ChangeAsync(owner,new("PEI",0,0),5,5,"permissions",permission:LandAction.Interact,allow:true)).Success,"Owner flag update failed");
            Check(protection.CanAt(loser,new("PEI",0,0),5,5,LandAction.Interact)&&!protection.CanAt(loser,new("PEI",0,0),5,5,LandAction.Build),"Public flags incorrect");
            Check((await service.ChangeAsync(mayor,new("PEI",0,0),5,5,"delete")).Code=="rect_owned_delete","Mayor deleted purchased plot");
            // Verify rollback if town cannot receive the price.
            using(var c=await db.OpenAsync())using(var s=new SqlSession(c)){s.Execute("UPDATE towns SET bank_balance=$0 WHERE id=$1",long.MaxValue,town.Id.Value);s.Commit();}
            await cache.RebuildAsync();
            Check((await service.ChangeAsync(loser,new("PEI",0,0),40,5,"buy")).Code=="rect_bank_full","Treasury overflow accepted");
            Check((await players.GetAsync(loser))?.Balance==1000&&cache.GetPlotAt(new("PEI",0,0),40,5)!.Owner==null,"Overflow did not roll back debit and ownership");
            using(var c=await db.OpenAsync())using(var s=new SqlSession(c)){s.Execute("UPDATE towns SET bank_balance=100 WHERE id=$0",town.Id.Value);s.Commit();}await cache.RebuildAsync();
            options.Value.Plots.OutsidersCanBuy=true;
            Check((await service.ChangeAsync(outsider,new("PEI",0,0),40,5,"buy")).Success,"Configured outsider purchase failed");
            await cache.RebuildAsync();
            Check(cache.GetPlotAt(new("PEI",0,0),5,5)!.Owner==owner,"Reload lost ownership");
            Check((await service.ChangeAsync(owner,new("PEI",0,0),5,5,"release")).Success,"Owner release failed");
            Check((await players.GetAsync(owner))?.Balance==900&&!cache.GetPlotAt(new("PEI",0,0),5,5)!.ForSale,"Release refunded or relisted unexpectedly");
            Check((await service.ChangeAsync(mayor,new("PEI",0,0),5,5,"delete")).Success,"Unowned deletion failed");
            Check((await service.CreateAsync(mayor,town.Id,Rect(0,0,32,32),"A2",0)).Success,"Released space cannot be reused");
            Check((await service.ChangeAsync(resident,new("PEI",0,0),5,5,"buy")).Success,"Zero-price plot failed");
            Check((await towns.LeaveAsync(resident)).Success,"Leave failed");
            Check(cache.GetPlotAt(new("PEI",0,0),5,5)!.Owner==null,"Leave did not release rectangle");
            Check(cache.GetPlotAt(new("PEI",0,0),40,5)!.Owner==outsider,"Leave released another owner's plot");
            // Legacy plots remain usable, and still obey their original permissions.
            Check((await legacy.BuyAsync(other,new("PEI",0,1))).Success,"Legacy listing no longer purchasable");
            Check(protection.CanAt(other,new("PEI",0,1),5,70,LandAction.Build),"Legacy owner lost build rights");
            Check(!protection.CanAt(outsider,new("PEI",0,1),5,70,LandAction.Build),"Legacy protection bypassed");
            var selection=new PlotSelectionService();
            Check(selection.Set(mayor,town.Id,"PEI",true,-1.6f,0.4f).First==(-2,0),"Corner snap failed");
            Check(selection.Set(mayor,town.Id,"PEI",false,10.4f,20.4f).Bounds==Rect(-2,0,10,20),"Two-corner normalization failed");
            Check(selection.Set(mayor,town.Id,"Washington",true,0,0).Second==null,"Cross-map corner retained");
            selection.Clear(mayor);Check(selection.Get(mayor)==null,"Selection clear failed");
            var preview=PlotGridGeometry.Build(Rect(0,0,32,32),2,4096);
            Check(preview.Markers.Count==64&&preview.Markers.All(p=>p.X==0||p.X==32||p.Z==0||p.Z==32),"Preview differs from stored rectangle");
            // Database constraint independently rejects overlaps.
            try
            {
                using var c=await db.OpenAsync();using var s=new SqlSession(c);
                s.Execute("INSERT INTO rect_plots(town_id,name,map_id,min_x,min_z,max_x,max_z) VALUES($0,'SqlOverlap','PEI',10,10,40,40)",town.Id.Value);
                throw new Exception("Database allowed overlapping rectangles");
            }
            catch(SqliteException ex)when(ex.SqliteErrorCode==19){}
            Check((await management.DisbandAsync(mayor)).Success,"Town deletion failed");
            using(var c=await db.OpenAsync())using(var s=new SqlSession(c))Check(s.Number("SELECT COUNT(*) FROM rect_plots WHERE town_id=$0",town.Id.Value)==0,"Town deletion left plots");
            Console.WriteLine("PASS: v3 migration preserves legacy plots; rectangular selection, containment, overlap, four subdivisions, purchases, rollback, concurrency, precise protection, ownership cleanup and previews verified.");
        }
        finally{SqliteConnection.ClearAllPools();if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}

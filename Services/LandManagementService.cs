using UTowny.Caching;using UTowny.Domain.Claims;using UTowny.Domain.Common;using UTowny.Persistence.Database;using UTowny.Protection;
namespace UTowny.Services;
public sealed class LandManagementService
{
 private readonly IDatabaseConnectionFactory m_Db;private readonly IWorldStateCache m_Cache;private readonly IAuthorizationService m_Auth;private readonly MutationGate m_Gate;private readonly IGridService m_Grid;
 public LandManagementService(IDatabaseConnectionFactory db,IWorldStateCache cache,IAuthorizationService auth,MutationGate gate,IGridService grid){m_Db=db;m_Cache=cache;m_Auth=auth;m_Gate=gate;m_Grid=grid;}
 public Task<Result> UnclaimAsync(PlayerId actor,GridCoord grid)=>m_Gate.RunAsync(async()=>
 {
  var claim=m_Cache.GetClaim(grid);if(claim==null)return Result.Fail("plot_not_found");if(!m_Auth.Can(actor,claim.TownId,TownAction.Claim))return Result.Fail("insufficient_role");
  using var db=await m_Db.OpenAsync();using var s=new SqlSession(db);s.Execute("DELETE FROM claims WHERE id=$0",claim.Id.Value);
  var town=m_Cache.GetTown(claim.TownId)!;if(town.Spawn!=null&&town.SpawnMap==grid.MapId&&m_Grid.FromWorld(grid.MapId,town.Spawn.X,town.Spawn.Z)==grid)s.Execute("UPDATE towns SET spawn_x=NULL,spawn_y=NULL,spawn_z=NULL,spawn_yaw=NULL,spawn_map=NULL WHERE id=$0",town.Id.Value);
  s.Commit();await m_Cache.RebuildAsync();return Result.Ok();
 });
 public Task<Result> PermissionAsync(PlayerId actor,GridCoord grid,LandAction action,bool allow)=>m_Gate.RunAsync(async()=>
 {
  var claim=m_Cache.GetClaim(grid);if(claim==null)return Result.Fail("plot_not_found");if(claim.PlotOwner!=actor&&!m_Auth.Can(actor,claim.TownId,TownAction.ManagePlot))return Result.Fail("insufficient_role");
  var flags=allow?claim.ProtectionFlags|(long)action:claim.ProtectionFlags&~(long)action;
  using var db=await m_Db.OpenAsync();using var s=new SqlSession(db);s.Execute("UPDATE claims SET protection_flags=$0 WHERE id=$1",flags,claim.Id.Value);s.Commit();await m_Cache.RebuildAsync();return Result.Ok();
 });
}

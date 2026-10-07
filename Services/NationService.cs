using Microsoft.Extensions.Options;
using UTowny.Caching;
using UTowny.Configuration;
using UTowny.Domain.Common;
using UTowny.Domain.Towns;
using UTowny.Persistence.Database;
namespace UTowny.Services;
public sealed record Nation(NationId Id, string Name, TownId Capital, IReadOnlyList<TownId> Members, IReadOnlyList<NationId> Allies);
public interface INationService
{
 Task<Nation?> GetAsync(TownId town);
 Task<Result> ExecuteAsync(PlayerId actor, string action, string target);
}
public sealed class NationService : INationService
{
 private readonly IDatabaseConnectionFactory m_Db; private readonly IWorldStateCache m_Cache;
 private readonly MutationGate m_Gate; private readonly IOptions<UTownyOptions> m_Options;
 public NationService(IDatabaseConnectionFactory db, IWorldStateCache cache, MutationGate gate, IOptions<UTownyOptions> options)
 { m_Db=db; m_Cache=cache; m_Gate=gate; m_Options=options; }
 public Task<Nation?> GetAsync(TownId town) => m_Gate.RunAsync(async () =>
 {
  using var db=await m_Db.OpenAsync(); using var s=new SqlSession(db);
  var rows=s.Rows("SELECT n.id,n.name,n.capital_town_id FROM nations n JOIN nation_members m ON m.nation_id=n.id WHERE m.town_id=$0",town.Value);
  if(rows.Count==0)return null;
  var id=Convert.ToInt64(rows[0][0]);
  return new Nation(new(id),(string)rows[0][1]!,new(Convert.ToInt64(rows[0][2])),s.Rows("SELECT town_id FROM nation_members WHERE nation_id=$0",id).Select(x=>new TownId(Convert.ToInt64(x[0]))).ToArray(),s.Rows("SELECT CASE WHEN a=$0 THEN b ELSE a END FROM alliances WHERE (a=$0 OR b=$0) AND status=1",id).Select(x=>new NationId(Convert.ToInt64(x[0]))).ToArray());
 });
 public Task<Result> ExecuteAsync(PlayerId actor,string action,string target) => m_Gate.RunAsync(async () =>
 {
  var member=m_Cache.GetMembership(actor); if(member==null)return Result.Fail("not_in_town");
  if(member.Role!=TownRole.Mayor)return Result.Fail("not_mayor");
  using var db=await m_Db.OpenAsync(); using var s=new SqlSession(db);
  var town=member.TownId.Value; var nation=s.Number("SELECT nation_id FROM nation_members WHERE town_id=$0",town);
  var now=DateTime.UtcNow.ToString("O");
  if(action=="create")
  {
   if(nation!=0)return Result.Fail("already_in_nation");
   if(target.Length<3||target.Length>32||target.Any(c=>!char.IsLetterOrDigit(c)&&c!='_'&&c!='-'))return Result.Fail("invalid_name");
   if(s.Number("SELECT COUNT(*) FROM nations WHERE name=$0",target)!=0)return Result.Fail("name_taken");
   if(s.Execute("UPDATE towns SET bank_balance=bank_balance-$0 WHERE id=$1 AND bank_balance>=$0",m_Options.Value.Nations.CreationPrice,town)!=1)return Result.Fail("insufficient_town_balance");
   s.Execute("INSERT INTO nations(name,capital_town_id,created_utc) VALUES($0,$1,$2)",target,town,now);
   nation=s.Number("SELECT last_insert_rowid()");s.Execute("INSERT INTO nation_members VALUES($0,$1)",town,nation);
  }
  else if(action=="accept")
  {
   if(nation!=0)return Result.Fail("already_in_nation");
   nation=s.Number("SELECT id FROM nations WHERE name=$0",target);
   if(s.Number("SELECT COUNT(*) FROM nation_invites WHERE nation_id=$0 AND town_id=$1 AND expires_utc>$2",nation,town,now)==0)return Result.Fail("invite_missing");
   s.Execute("INSERT INTO nation_members VALUES($0,$1)",town,nation);s.Execute("DELETE FROM nation_invites WHERE town_id=$0",town);
  }
  else
  {
   if(nation==0)return Result.Fail("nation_not_found");
   var capital=s.Number("SELECT capital_town_id FROM nations WHERE id=$0",nation);
   if(action=="leave")
   {
    if(capital==town)return Result.Fail("capital_cannot_leave");
    s.Execute("DELETE FROM nation_members WHERE town_id=$0",town);
   }
   else
   {
    if(capital!=town)return Result.Fail("not_nation_leader");
    if(action=="disband")s.Execute("DELETE FROM nations WHERE id=$0",nation);
    else if(action=="invite"||action=="kick")
    {
     var other=m_Cache.GetTownByName(target);if(other==null)return Result.Fail("town_not_found");
     if(other.Id.Value==town)return Result.Fail("invalid_target");
     if(action=="invite")
     {
      if(s.Number("SELECT COUNT(*) FROM nation_members WHERE town_id=$0",other.Id.Value)!=0)return Result.Fail("already_in_nation");
      s.Execute("INSERT INTO nation_invites VALUES($0,$1,$2) ON CONFLICT(nation_id,town_id) DO UPDATE SET expires_utc=excluded.expires_utc",nation,other.Id.Value,DateTime.UtcNow.AddHours(24).ToString("O"));
     }
     else if(s.Execute("DELETE FROM nation_members WHERE town_id=$0 AND nation_id=$1",other.Id.Value,nation)!=1)return Result.Fail("invalid_target");
    }
    else
    {
     var other=s.Number("SELECT id FROM nations WHERE name=$0",target); if(other==0||other==nation)return Result.Fail("invalid_target");
     var a=Math.Min(nation,other);var b=Math.Max(nation,other);
     if(action=="ally")
     {
      if(s.Number("SELECT COUNT(*) FROM alliances WHERE a=$0 AND b=$1",a,b)!=0)return Result.Fail("request_exists");
      s.Execute("INSERT INTO alliances VALUES($0,$1,$2,0,$3)",a,b,nation,now);
     }
     else if(action=="allyaccept")
     { if(s.Execute("UPDATE alliances SET status=1 WHERE a=$0 AND b=$1 AND requested_by=$2 AND status=0",a,b,other)!=1)return Result.Fail("invite_missing"); }
     else if(action=="allydecline")
     { if(s.Execute("DELETE FROM alliances WHERE a=$0 AND b=$1 AND requested_by=$2 AND status=0",a,b,other)!=1)return Result.Fail("invite_missing"); }
     else if(action=="unally")s.Execute("DELETE FROM alliances WHERE a=$0 AND b=$1",a,b);
     else return Result.Fail("syntax");
    }
   }
  }
  s.Commit();await m_Cache.RebuildAsync();return Result.Ok();
 });
}

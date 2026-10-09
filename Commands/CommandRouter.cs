using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OpenMod.API.Commands;
using OpenMod.API.Permissions;
using OpenMod.Core.Commands;
using OpenMod.Unturned.Users;
using SDG.Unturned;
using UTowny.Caching;using UTowny.Domain.Claims;using UTowny.Domain.Common;using UTowny.Domain.Towns;using UTowny.Protection;using UTowny.Services;
namespace UTowny.Commands;
public sealed class CommandRouter
{
 private readonly IUnturnedUserDirectory m_Users;
 private readonly ConfigurationService m_Configuration;
 private readonly UTowny.Visualization.ClaimToolService m_Tool;
 private readonly IShopService m_Shop;private readonly TeleportService m_Teleport;
 private readonly ITownService m_Towns;private readonly ITownManagementService m_Manage;private readonly IEconomyService m_Economy;private readonly IClaimService m_Claims;private readonly IPlotService m_Plots;private readonly IGridService m_Grid;private readonly IWorldStateCache m_Cache;private readonly INationService m_Nations;private readonly IWarService m_Wars;private readonly LandManagementService m_Land;private readonly AdminService m_Admin;private readonly ProtectionService m_Protection;private readonly IPermissionChecker m_Permissions;private readonly IStringLocalizer m_Text;private readonly ILogger<CommandRouter> m_Log;
 public CommandRouter(ITownService towns,ITownManagementService manage,IEconomyService economy,IClaimService claims,IPlotService plots,IGridService grid,IWorldStateCache cache,INationService nations,IWarService wars,LandManagementService land,AdminService admin,ProtectionService protection,IPermissionChecker permissions,IStringLocalizer text,ILogger<CommandRouter> log,IShopService shop,TeleportService teleport,UTowny.Visualization.ClaimToolService tool,ConfigurationService configuration,IUnturnedUserDirectory users)
 {m_Users=users;m_Configuration=configuration;m_Tool=tool;m_Shop=shop;m_Teleport=teleport;m_Towns=towns;m_Manage=manage;m_Economy=economy;m_Claims=claims;m_Plots=plots;m_Grid=grid;m_Cache=cache;m_Nations=nations;m_Wars=wars;m_Land=land;m_Admin=admin;m_Protection=protection;m_Permissions=permissions;m_Text=text;m_Log=log;}
 public async Task<string> ExecuteAsync(ICommandContext context,string group)
 {
  try
  {
   var args=context.Parameters.ToArray();var op=CommandPermissions.NormalizeAction(group,args.Length==0?"info":args[0]);string Arg(int n)=>args.Length>n?args[n]:throw new ArgumentException();
   if(group=="balance")op="info";
   if(group=="buy"||group=="sell")op="trade";
   var permission=CommandPermissions.Resolve(group,op);
   if(permission==null)return Help(group);
   if(await m_Permissions.CheckPermissionAsync(context.Actor,permission)!=PermissionGrantResult.Grant)return m_Text["permission_denied"];
   var user=context.Actor as UnturnedUser;if(user==null&&group!="utowny")return m_Text["player_only"];
   var id=new PlayerId(user?.Player.SteamId.m_SteamID??0);
   if(op=="help"||(group=="town"&&args.Length==0&&m_Towns.GetTown(id)==null))return Help(group);
   if(group=="town"&&op=="create"&&args.Length!=2)return Message("town_create_usage","Usage: /t create <name> or /t new <name>. Example: /t create VazerTown");
   GridCoord grid=default;TownSpawn? position=null;
   await UniTask.SwitchToMainThread();
   if(user!=null){var p=user.Player.Player.transform.position;grid=m_Grid.FromWorld(Level.info.name,p.x,p.z);position=new(p.x,p.y,p.z,user.Player.Player.transform.eulerAngles.y);}
   PlayerId PlayerArg(string value)
   {
    if(ulong.TryParse(value,out var steam)&&steam!=0)return new(steam);
    var found=Provider.clients.Where(p=>string.Equals(p.playerID.characterName,value,StringComparison.OrdinalIgnoreCase)).ToArray();
    if(found.Length!=1)throw new ArgumentException();return new(found[0].playerID.steamID.m_SteamID);
   }
   var targetPlayer=(op=="invite"||op=="kick"||op=="promote"||op=="demote"||op=="mayor")&&group=="town"?PlayerArg(Arg(1)):default;
   Result result=Result.Fail("syntax");
   if(group=="balance")return m_Text["balance_value",new {Value=await m_Economy.GetBalanceAsync(id)}];
   if(group=="pay")
   {
    if(args.Length!=2)return Help(group);
    var recipient=PlayerArg(Arg(0));var amount=long.Parse(Arg(1));
    var transfer=await m_Economy.TransferAsync(id,recipient,amount);
    if(!transfer.Success)return Message(transfer.Code,transfer.Code=="pay_self"?"You cannot pay yourself.":m_Text[transfer.Code].Value);
    try
    {
     Task delivery=Task.CompletedTask;
     await UTowny.Utilities.UnityDispatch.RunAsync(()=>{var receiver=m_Users.FindUser(new Steamworks.CSteamID(recipient.Value));if(receiver!=null)delivery=UTowny.Utilities.UTownyChat.SendAsync(receiver,Message("pay_received",$"Received {amount} from {context.Actor.FullActorName}.",new {Amount=amount,Player=context.Actor.FullActorName}));});
     await delivery;
    }
    catch(Exception ex){m_Log.LogWarning(ex,"Payment committed but recipient notification failed");}
    return Message("pay_sent",$"Sent {amount} to {Arg(0)}. Your balance: {transfer.Value}.",new {Amount=amount,Player=Arg(0),Balance=transfer.Value});
   }
   if(group=="buy"||group=="sell"){var trade=await m_Shop.TradeAsync(user!,Arg(0),Arg(1),group=="buy");return m_Text[trade.Code];}
   if(group=="town")
   {
    switch(op)
    {
     case "create":
      var isAdmin=await m_Permissions.CheckPermissionAsync(context.Actor,"UTowny:admin")==PermissionGrantResult.Grant;
      var created=isAdmin?await m_Towns.CreateAsAdminAsync(id,Arg(1)):await m_Towns.CreateAsync(id,Arg(1));
      return created.Success?Message("town_created",$"Town {created.Value!.Name} created. You are its mayor.",new {Name=created.Value!.Name}):m_Text[created.Code];
     case "deposit":var deposit=await m_Towns.DepositAsync(id,long.Parse(Arg(1)));result=new(deposit.Success,deposit.Code);break;
     case "leave":result=await m_Towns.LeaveAsync(id);break;
     case "invite":result=await m_Manage.InviteAsync(id,targetPlayer);break;
     case "accept":result=await m_Manage.AcceptAsync(id,FindTown(Arg(1)).Id);break;
     case "kick":result=await m_Manage.RemoveAsync(id,targetPlayer);break;
     case "promote":result=await m_Manage.RoleAsync(id,targetPlayer,TownRole.CoMayor);break;
     case "demote":result=await m_Manage.RoleAsync(id,targetPlayer,TownRole.Resident);break;
     case "mayor":result=await m_Manage.RoleAsync(id,targetPlayer,TownRole.Mayor);break;
     case "disband":if(Arg(1)!="confirm")throw new ArgumentException();result=await m_Manage.DisbandAsync(id);break;
     case "show":return await m_Tool.ShowAsync(user!,args.Length>1?Arg(1):"town");
     case "claim":var claim=await m_Claims.ClaimAsync(id,grid);result=new(claim.Success,claim.Code);break;
     case "unclaim":result=await m_Land.UnclaimAsync(id,grid);break;
     case "pvp":case "public":result=await m_Manage.SetAsync(id,op,Toggle(Arg(1)));break;
     case "tax":result=Arg(1)=="set"?await m_Manage.SetAsync(id,"taxamount",long.Parse(Arg(2))):await m_Manage.SetAsync(id,"tax",Toggle(Arg(1)));break;
     case "spawn":result=await m_Teleport.SpawnAsync(user!,args.Length>1?Arg(1):null);break;
     case "setspawn":result=await m_Manage.SetSpawnAsync(id,position!,grid.MapId);break;
     case "protection":result=await m_Manage.SetAsync(id,"protection",long.Parse(Arg(1)));break;
     case "list":return m_Text["list_value",new {Value=string.Join(", ",m_Cache.GetTowns().Select(t=>t.Name))}];
     case "residents":var own=OwnTown(id);return m_Text["list_value",new {Value=string.Join(", ",m_Cache.GetMembers(own.Id).Select(m=>$"{m.PlayerId} ({m.Role})"))}];
     case "info":var town=args.Length>1?FindTown(Arg(1)):OwnTown(id);return m_Text["town_info",new {Name=town.Name,Balance=town.BankBalance,Mayor=town.MayorId.Value,Residents=m_Cache.GetResidentCount(town.Id),Claims=m_Cache.GetTownClaims(town.Id).Count,Limit=m_Claims.GetAllowance(town.Id),Pvp=town.PvpEnabled,Tax=town.TaxEnabled?town.TaxAmount:0,Upkeep=town.NextUpkeepUtc}];
    }
   }
   else if(group=="plot")
   {
    if(op=="buy"){var r=await m_Plots.BuyAsync(id,grid);result=new(r.Success,r.Code);}
    else if(op=="forsale"){var r=await m_Plots.SetForSaleAsync(id,grid,long.Parse(Arg(1)));result=new(r.Success,r.Code);}
    else if(op=="notforsale"){var r=await m_Plots.SetNotForSaleAsync(id,grid);result=new(r.Success,r.Code);}
    else if(op=="permissions")result=await m_Land.PermissionAsync(id,grid,(LandAction)Enum.Parse(typeof(LandAction),Arg(1),true),Toggle(Arg(2))==1);
    else if(op=="info")return m_Text["list_value",new {Value=m_Cache.GetClaim(grid)?.ToString()??m_Text["plot_not_found"].Value}];
   }
   else if(group=="nation")
   {
    if(op=="info"||op=="members"||op=="allies")
    {var nation=await m_Nations.GetAsync(OwnTown(id).Id);if(nation==null)return m_Text["nation_not_found"];return m_Text["nation_info",new {Name=nation.Name,Members=string.Join(", ",nation.Members.Select(t=>m_Cache.GetTown(t)?.Name)),Allies=string.Join(", ",nation.Allies.Select(n=>n.Value))}];}
    result=await m_Nations.ExecuteAsync(id,op,args.Length>1?Arg(1):"");
   }
   else if(group=="war")
   {
    if(op=="info"||op=="list"){var town=OwnTown(id);return m_Text["list_value",new {Value=string.Join(" | ",m_Wars.All.Where(w=>w.A==town.Id||w.B==town.Id).Select(w=>$"{w.Id}: {w.A.Value}/{w.B.Value} {w.Status} {w.StartUtc:O} - {w.EndUtc:O}"))}];}
    result=await m_Wars.ExecuteAsync(id,op,FindTown(Arg(1)).Id);
   }
   else if(group=="utowny")
   {
    if(op=="effect"){if(user==null)return m_Text["player_only"];if(args.Length!=2)return "Usage: /utowny effect <effect ID>. Example: /utowny effect 146";return await m_Tool.TestEffectAsync(user,ushort.Parse(Arg(1)));}
    if(op=="balance"||op=="addbalance"||op=="removebalance")
    {
     if(args.Length!=3)return Message("admin_balance_usage","Usage: /utowny addbalance <player name|Steam64> <amount>. For towns: /utowny addtownbalance <town> <amount>.");
     var target=PlayerArg(Arg(1));var changed=await m_Admin.ExecuteAsync(context.Actor.Id,op,target.Value.ToString(),Arg(2),grid);
     return changed.Success?Message("admin_balance_updated",$"Admin balance update completed: {op} {Arg(2)} for {Arg(1)}. Your own balance was not charged.",new {Action=op,Amount=Arg(2),Player=Arg(1)}):m_Text[changed.Code];
    }
    if(op=="bypass"){if(user==null)return m_Text["player_only"];if(await m_Permissions.CheckPermissionAsync(context.Actor,"UTowny:admin.bypass")!=PermissionGrantResult.Grant)return m_Text["permission_denied"];m_Protection.SetBypass(id,Toggle(Arg(1))==1);m_Log.LogWarning("Admin {Actor} changed protection bypass to {Mode}",context.Actor.Id,Arg(1));return m_Text["bypass_changed"];}
    if(op=="inspect"||op=="debug")return m_Text["list_value",new {Value=$"{grid}: {m_Cache.GetClaim(grid)}"}];
    if(op=="reload"){var reload=await m_Configuration.ReloadAsync();m_Log.LogWarning("Admin {Actor} reloaded configuration: {Result}",context.Actor.Id,reload.Code);return m_Text[reload.Code];}
    if(op=="trades")return m_Text["list_value",new {Value=await m_Admin.PendingAsync(Arg(1))}];
    if(op=="member")return m_Text["list_value",new {Value=m_Cache.GetMembership(new PlayerId(ulong.Parse(Arg(1))))?.ToString()??"-"}];
    if(op=="info")return m_Text["list_value",new {Value=FindTown(Arg(1)).ToString()}];
    if(op=="claim"||op=="unclaim")if(user==null)return m_Text["player_only"];
    result=await m_Admin.ExecuteAsync(context.Actor.Id,op,args.Length>1?Arg(1):"",args.Length>2?Arg(2):"",grid);
   }
   return m_Text[result.Code];
  }
  catch(UTowny.Api.Events.TownyActionCancelledException){return m_Text["action_cancelled"];}
  catch(CommandFeedbackException ex){return m_Text[ex.Key];}
  catch(ArgumentException){return Help(group);}
  catch(FormatException){return m_Text["invalid_amount"];}
  catch(OverflowException){return m_Text["invalid_amount"];}
  catch(Exception ex){m_Log.LogError(ex,"Command {Group} failed for {Actor}",group,context.Actor.Id);return m_Text["generic_error"];}
 }
 private string Message(string key,string fallback,params object[] args)
 {var value=m_Text[key,args];return value.ResourceNotFound?fallback:value.Value;}
 private string Help(string group)=>Message(group+"_help",group switch
 {
  "town"=>"Town commands: /t create <name> (or /t new <name>), /t info [name], /t list, /t invite <player>, /t accept <town>, /t deposit <amount>, /t claim, /t show [town|cell|off], /t setspawn, /t spawn. Example: /t create VazerTown",
  "nation"=>"Nation commands: /n create <name>, /n info, /n members, /n invite <town>, /n accept <nation>. You must belong to a town first.",
  "plot"=>"Plot commands: /plot info, /plot buy, /plot forsale <price>, /plot notforsale, /plot permissions <action> on|off.",
  "war"=>"War commands: /war request|accept|decline|cancel <town>, /war info, /war list.",
  "pay"=>"Usage: /pay <player name|Steam64> <amount>. This transfers money from your own balance.",
  "buy"=>"Usage: /buy <item> <amount>. Example: /buy scrap 1",
  "sell"=>"Usage: /sell <item> <amount|all>. Example: /sell scrap all",
  "utowny"=>"Admin commands: /utowny info <town>, /utowny addbalance <player|Steam64> <amount>, /utowny addtownbalance <town> <amount>, /utowny reload, /utowny bypass on|off. See COMMANDS.md for all admin actions.",
  _=>"Use /t help for town commands."
 });
 private sealed class CommandFeedbackException:Exception
 {public string Key {get;} public CommandFeedbackException(string key)=>Key=key;}
 private Town FindTown(string name)=>m_Towns.GetTown(name)??throw new CommandFeedbackException("town_not_found");
 private Town OwnTown(PlayerId id)=>m_Towns.GetTown(id)??throw new CommandFeedbackException("not_in_town");
 private static long Toggle(string value)=>value.ToLowerInvariant() switch {"on"=>1,"off"=>0,_=>throw new ArgumentException()};
}


using Cysharp.Threading.Tasks;using Microsoft.Extensions.Options;using OpenMod.Unturned.Users;using SDG.Unturned;using UTowny.Configuration;using UTowny.Domain.Common;using UTowny.Persistence.Database;
namespace UTowny.Services;
public interface IShopService { Task<Result> TradeAsync(UnturnedUser user,string key,string quantity,bool buy); }
public sealed class ShopService:IShopService
{
 private readonly IDatabaseConnectionFactory m_Db;private readonly MutationGate m_Gate;private readonly IOptions<UTownyOptions> m_Options;
 public ShopService(IDatabaseConnectionFactory db,MutationGate gate,IOptions<UTownyOptions> options){m_Db=db;m_Gate=gate;m_Options=options;}
 public Task<Result> TradeAsync(UnturnedUser user,string key,string quantity,bool buy)=>m_Gate.RunAsync(async()=>
 {
  if(!m_Options.Value.Shop.Items.TryGetValue(key,out var spec))return Result.Fail("shop_item_missing");
  var player=user.Player.Player;var id=user.Player.SteamId.m_SteamID;var trade=Guid.NewGuid().ToString("N");
  await UniTask.SwitchToMainThread();if(player==null||player.life.isDead)return Result.Fail("invalid_player");
  var inv=player.inventory;var owned=new List<(byte Page,Item Item)>();
  for(byte page=2;page<PlayerInventory.STORAGE;page++)for(byte index=0;index<inv.getItemCount(page);index++){var item=inv.getItem(page,index).item;if(item.id==spec.AssetId)owned.Add((page,item));}
  int count;if(quantity=="all"&&!buy)count=owned.Sum(x=>(int)x.Item.amount);else if(!int.TryParse(quantity,out count))return Result.Fail("invalid_amount");
  if(count<=0||count>m_Options.Value.Shop.MaxBatch)return Result.Fail("invalid_amount");
  if(!buy&&owned.Sum(x=>(int)x.Item.amount)<count)return Result.Fail("insufficient_items");
  var amount=checked(count*(buy?spec.BuyPrice:spec.SellPrice));
  var reserved=await Task.Run(async()=>
  {
   using var db=await m_Db.OpenAsync();using var s=new SqlSession(db);
   if(s.Number("SELECT COUNT(*) FROM shop_trades WHERE player=$0 AND status='pending'",(long)id)>0)return Result.Fail("trade_pending");
   if(buy&&s.Execute("UPDATE players SET balance=balance-$0 WHERE steam64=$1 AND balance>=$0",amount,(long)id)!=1)return Result.Fail("insufficient_balance");
   if(!buy&&s.Number("SELECT COUNT(*) FROM players WHERE steam64=$0 AND balance<=9223372036854775807-$1",(long)id,amount)!=1)return Result.Fail("invalid_amount");
   s.Execute("INSERT INTO shop_trades VALUES($0,$1,$2,$3,$4,$5,'pending',$6)",trade,(long)id,buy?"buy":"sell",amount,spec.AssetId,count,DateTime.UtcNow.ToString("O"));s.Commit();return Result.Ok();
  });
  if(!reserved.Success)return reserved;
  await UniTask.SwitchToMainThread();
  // No await between revalidation, inventory mutation and save: client requests cannot interleave.
  var success=player!=null&&!player.life.isDead;var added=new List<Item>();
  if(success&&buy)
  {
   if(Assets.find(EAssetType.ITEM,spec.AssetId) is not ItemAsset)success=false;
   else for(int i=0;i<count;i++){var item=new Item(spec.AssetId,(byte)1,(byte)100);if(!inv.tryAddItem(item,false)){success=false;break;}added.Add(item);}
   if(!success)for(byte page=2;page<PlayerInventory.STORAGE;page++)for(int index=inv.getItemCount(page)-1;index>=0;index--)if(added.Contains(inv.getItem(page,(byte)index).item))inv.removeItem(page,(byte)index);
  }
  else if(success)
  {
   var available=0;for(byte page=2;page<PlayerInventory.STORAGE;page++)for(byte index=0;index<inv.getItemCount(page);index++)if(inv.getItem(page,index).item.id==spec.AssetId)available+=inv.getItem(page,index).item.amount;
   if(available<count)success=false;
   else{var remaining=count;for(byte page=2;page<PlayerInventory.STORAGE&&remaining>0;page++)for(int index=inv.getItemCount(page)-1;index>=0&&remaining>0;index--){var item=inv.getItem(page,(byte)index).item;if(item.id!=spec.AssetId)continue;var take=Math.Min(remaining,item.amount);if(take==item.amount)inv.removeItem(page,(byte)index);else inv.updateAmount(page,(byte)index,(byte)(item.amount-take));remaining-=take;}}
  }
  if(player!=null)player.save();
  // If the process stops before this commit, leave a durable pending record for admin reconciliation.
  return await Task.Run(async()=>
  {
   using var db=await m_Db.OpenAsync();using var s=new SqlSession(db);
   if((!buy&&success)||(buy&&!success))if(s.Execute("UPDATE players SET balance=balance+$0 WHERE steam64=$1 AND balance<=9223372036854775807-$0",amount,(long)id)!=1)throw new OverflowException("Shop credit overflow");
   s.Execute("UPDATE shop_trades SET status=$0 WHERE id=$1",success?"completed":"cancelled",trade);s.Commit();return success?Result.Ok("trade_done"):Result.Fail("inventory_changed");
  });
 });
}

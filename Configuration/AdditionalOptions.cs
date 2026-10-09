namespace UTowny.Configuration;
public sealed class ShopOptions {public int MaxBatch {get;set;}=100;public Dictionary<string,ShopItem> Items {get;set;}=new(StringComparer.OrdinalIgnoreCase){["scrap"]=new()};}
public sealed class ShopItem {public ushort AssetId {get;set;}=67;public long BuyPrice {get;set;}=100;public long SellPrice {get;set;}=50;}
public sealed class TeleportOptions {public int WarmupSeconds {get;set;}=5;public int CooldownSeconds {get;set;}=60;public long Cost {get;set;}public bool CancelOnMovement {get;set;}=true;public bool CancelOnDamage {get;set;}=true;public int CombatLockSeconds {get;set;}=20;}
public sealed class VisualizationOptions {public float RefreshSeconds {get;set;}=1f;public float MarkerSpacingMeters {get;set;}=2f;public float MarkerHeightMeters {get;set;}=0.15f;public int MaxMarkers {get;set;}=4096;public ushort ClaimToolAssetId {get;set;}public ushort EffectAssetId {get;set;}public int DurationSeconds {get;set;}=8;public byte PreviewKey {get;set;}=0;public byte ClaimKey {get;set;}=1;}

public sealed class ProtectionOptions
{
 public bool TownBuild {get;set;}=true;public bool TownDamage {get;set;}=true;public bool TownSalvage {get;set;}=true;public bool TownInteract {get;set;}=true;public bool VehicleInteraction {get;set;}=true;
 public long DefaultFlags=>(TownBuild?0:1)|(TownDamage?0:2)|(TownSalvage?0:4)|(TownInteract?0:8)|(VehicleInteraction?0:16);
}


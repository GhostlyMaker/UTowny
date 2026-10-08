using Microsoft.Extensions.Configuration;using Microsoft.Extensions.Options;using UTowny.Configuration;using UTowny.Domain.Common;
namespace UTowny.Services;
public sealed class ConfigurationService
{
 private readonly IConfiguration m_Source;private readonly IOptions<UTownyOptions> m_Current;private readonly MutationGate m_Gate;
 public ConfigurationService(IConfiguration source,IOptions<UTownyOptions> current,MutationGate gate){m_Source=source;m_Current=current;m_Gate=gate;}
 public Task<Result> ReloadAsync()=>m_Gate.RunAsync(async()=>
 {
  if(m_Source is IConfigurationRoot root)root.Reload();
  var values=m_Source.AsEnumerable().Where(x=>x.Value!=null).ToDictionary(x=>x.Key.Replace("_",""),x=>x.Value);
  var next=new ConfigurationBuilder().AddInMemoryCollection(values).Build().Get<UTownyOptions>()??new();
  var validation=new ConfigValidator().Validate(null,next);if(validation.Failed)return Result.Fail("config_invalid");
  var old=m_Current.Value;
  if(old.Database.FileName!=next.Database.FileName||old.Claims.GridSizeMeters!=next.Claims.GridSizeMeters||old.Claims.MapIdOverride!=next.Claims.MapIdOverride||old.Visualization.EffectAssetId!=next.Visualization.EffectAssetId)return Result.Fail("config_restart_required");
  old.Towns=next.Towns;old.Claims=next.Claims;old.Plots=next.Plots;old.Economy=next.Economy;old.Shop=next.Shop;old.Taxes=next.Taxes;old.Upkeep=next.Upkeep;old.Nations=next.Nations;old.Wars=next.Wars;old.Teleportation=next.Teleportation;old.Visualization=next.Visualization;old.Protection=next.Protection;
  await Task.CompletedTask;return Result.Ok();
 });
}

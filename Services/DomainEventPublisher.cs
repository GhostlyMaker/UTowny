using Microsoft.Extensions.Logging;using OpenMod.API.Eventing;using OpenMod.API.Plugins;using UTowny.Api.Events;
namespace UTowny.Services;
public sealed class DomainEventPublisher
{
 private readonly IEventBus m_Bus;private IOpenModPlugin? m_Plugin;private readonly ILogger<DomainEventPublisher> m_Log;
 public DomainEventPublisher(IEventBus bus,ILogger<DomainEventPublisher> log){m_Bus=bus;m_Log=log;}
 public void Initialize(IOpenModPlugin plugin)=>m_Plugin=plugin;
 private IOpenModPlugin Owner=>m_Plugin??throw new InvalidOperationException("UTowny event publisher is not initialized");
 public async Task BeforeAsync(DomainOperation operation)
 {var e=new TownyChangingEvent(operation);await m_Bus.EmitAsync(Owner,this,e);if(e.IsCancelled)throw new TownyActionCancelledException();}
 public async Task AfterAsync(DomainOperation operation)
 {try{await m_Bus.EmitAsync(Owner,this,new TownyChangedEvent(operation));}catch(Exception ex){m_Log.LogError(ex,"Post-commit event listener failed for {Operation}",operation.Kind);}m_Log.LogInformation("UTowny operation {Kind} actor {Actor} town {Town} detail {Detail}",operation.Kind,operation.Actor.Value,operation.Town?.Value,operation.Detail);}
}

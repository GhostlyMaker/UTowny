using OpenMod.API.Eventing;using OpenMod.Core.Eventing;using UTowny.Domain.Common;
namespace UTowny.Api.Events;
public sealed record DomainOperation(string Kind,PlayerId Actor,TownId? Town=null,string? Detail=null);
public sealed class TownyChangingEvent:Event,ICancellableEvent
{
 public DomainOperation Operation {get;}
 public bool IsCancelled {get;set;}
 public TownyChangingEvent(DomainOperation operation)=>Operation=operation;
}
public sealed class TownyChangedEvent:Event
{
 public DomainOperation Operation {get;}
 public TownyChangedEvent(DomainOperation operation)=>Operation=operation;
}
public sealed class TownyActionCancelledException:Exception {}

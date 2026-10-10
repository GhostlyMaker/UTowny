using OpenMod.Core.Eventing;
using OpenMod.API.Eventing;using UTowny.Domain.Common;using UTowny.Domain.Towns;
namespace UTowny.Api.Events;
public sealed class TownCreatingEvent:Event,ICancellableEvent{public PlayerId Mayor{get;init;}public string TownName{get;init;}="";public bool IsCancelled{get;set;}}
public sealed class TownCreatedEvent:Event{public Town Town{get;init;}=null!;}
public sealed class PlayerJoinedTownEvent:Event{public TownId TownId{get;init;}public PlayerId PlayerId{get;init;}}
public sealed class PlayerLeftTownEvent:Event{public TownId TownId{get;init;}public PlayerId PlayerId{get;init;}}

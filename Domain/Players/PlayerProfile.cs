using UTowny.Domain.Common;
namespace UTowny.Domain.Players;
public sealed record PlayerProfile(PlayerId PlayerId, long Balance, long PlaytimeSeconds, DateTime LastSeenUtc);

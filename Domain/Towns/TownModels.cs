using UTowny.Domain.Common;

namespace UTowny.Domain.Towns;

public sealed record Town(
    TownId Id,
    string Name,
    PlayerId MayorId,
    long BankBalance,
    bool PvpEnabled,
    DateTime CreatedUtc,
    DateTime NextUpkeepUtc,
    bool TaxEnabled,
    long TaxAmount,
    DateTime NextTaxUtc,
    TownSpawn? Spawn,
    bool SpawnPublic, string? SpawnMap = null, long ProtectionFlags = 0);

public sealed record TownMember(TownId TownId, PlayerId PlayerId, TownRole Role, DateTime JoinedUtc, int MissedTaxCycles);
public sealed record TownSpawn(float X, float Y, float Z, float Yaw);

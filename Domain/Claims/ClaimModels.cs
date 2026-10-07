using UTowny.Domain.Common;
namespace UTowny.Domain.Claims;

public readonly record struct GridCoord(string MapId, int X, int Z);

public sealed record Claim(
    ClaimId Id,
    TownId TownId,
    GridCoord Grid,
    PlayerId? PlotOwner,
    bool ForSale,
    long Price,
    long ProtectionFlags);

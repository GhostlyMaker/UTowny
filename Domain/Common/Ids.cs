namespace UTowny.Domain.Common;

public readonly record struct PlayerId(ulong Value)
{
    public override string ToString() => Value.ToString();
}
public readonly record struct TownId(long Value);
public readonly record struct NationId(long Value);
public readonly record struct ClaimId(long Value);

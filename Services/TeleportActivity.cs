using System.Collections.Concurrent;
namespace UTowny.Services;

public sealed class TeleportActivity
{
    private readonly ConcurrentDictionary<ulong, DateTime> m_Combat = new();
    private readonly ConcurrentDictionary<ulong, DateTime> m_Damage = new();

    public void Damaged(ulong victim, ulong attacker, int amount, DateTime now)
    {
        if (amount <= 0) return;
        m_Damage[victim] = now;
        // Environment damage and suicide can cancel a warmup, but are not PvP.
        if (attacker == 0 || attacker == victim) return;
        m_Combat[victim] = now;
        m_Combat[attacker] = now;
    }

    public int RemainingSeconds(ulong player, int duration, DateTime now)
    {
        if (duration <= 0 || !m_Combat.TryGetValue(player, out var hit)) return 0;
        return (int)Math.Max(0, Math.Ceiling((hit.AddSeconds(duration) - now).TotalSeconds));
    }

    public bool DamagedSince(ulong player, DateTime started) => m_Damage.TryGetValue(player, out var hit) && hit >= started;

    public void ResetLife(ulong player, DateTime now)
    {
        m_Combat.TryRemove(player, out _);
        // Invalidate a warmup belonging to the old life, including a quick respawn.
        m_Damage[player] = now;
    }
}

using UTowny.Services;

internal static class CombatRegression
{
    public static void Run()
    {
        var state = new TeleportActivity();
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        state.Damaged(1, 0, 5, start);
        if (state.RemainingSeconds(1, 20, start) != 0 || !state.DamagedSince(1, start))
            throw new Exception("Environmental damage must cancel a warmup without combat tagging");
        state.Damaged(1, 1, 5, start);
        if (state.RemainingSeconds(1, 20, start) != 0) throw new Exception("Self-damage tagged PvP");
        state.Damaged(1, 2, 0, start);
        if (state.RemainingSeconds(2, 20, start) != 0) throw new Exception("Zero damage tagged PvP");
        state.Damaged(1, 2, 5, start);
        if (state.RemainingSeconds(1, 20, start) != 20 || state.RemainingSeconds(2, 20, start) != 20)
            throw new Exception("Both PvP participants must be tagged");
        state.Damaged(1, 0, 1, start.AddSeconds(19));
        if (state.RemainingSeconds(1, 20, start.AddSeconds(20)) != 0) throw new Exception("Environmental damage extended combat");
        state.Damaged(1, 2, 5, start.AddSeconds(21));
        state.ResetLife(1, start.AddSeconds(22));
        if (state.RemainingSeconds(1, 20, start.AddSeconds(22)) != 0 || !state.DamagedSince(1, start.AddSeconds(21)))
            throw new Exception("Death/respawn must clear combat and invalidate old warmups");
        if (state.RemainingSeconds(2, 20, start.AddSeconds(22)) != 19) throw new Exception("Victim respawn cleared attacker lock");
        if (state.RemainingSeconds(2, 0, start.AddSeconds(22)) != 0) throw new Exception("Disabled lock still active");
        Console.WriteLine("PASS: environmental/self/zero damage does not tag PvP; PvP tags both sides, expires correctly, and clears per player on death/respawn.");
    }
}

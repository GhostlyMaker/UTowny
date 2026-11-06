using System;
using System.Collections.Generic;
using System.Linq;

namespace EconomyScanner.Services
{
    internal sealed class PlayerLinkGate
    {
        private readonly Dictionary<ulong, DateTime> recent = new Dictionary<ulong, DateTime>();
        private readonly HashSet<ulong> active = new HashSet<ulong>();
        public bool TryEnter(ulong id, DateTime now)
        {
            lock (recent)
            {
                foreach (ulong key in recent.Where(p => p.Value <= now && !active.Contains(p.Key)).Select(p => p.Key).ToArray())
                    recent.Remove(key);
                if (active.Contains(id) || recent.ContainsKey(id) || active.Count >= 16) return false;
                recent[id] = now.AddSeconds(10);
                active.Add(id);
                return true;
            }
        }
        public void Exit(ulong id) { lock (recent) active.Remove(id); }
    }
}

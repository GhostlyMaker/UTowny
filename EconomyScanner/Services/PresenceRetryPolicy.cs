using System;

namespace EconomyScanner.Services
{
    internal sealed class PresenceUploadResult
    {
        public bool Success { get; private set; }
        public string Reason { get; private set; }
        public static PresenceUploadResult Ok() => new PresenceUploadResult { Success = true };
        public static PresenceUploadResult Failure(string reason) => new PresenceUploadResult { Reason = reason };
    }

    internal sealed class PresenceRetryPolicy
    {
        private int failures;
        private DateTime nextWarningUtc = DateTime.MinValue;
        public int RecordFailure()
        {
            failures = Math.Min(failures + 1, 5);
            return Math.Min(300, 30 * (1 << (failures - 1)));
        }
        public bool ShouldWarn(DateTime now)
        {
            if (now < nextWarningUtc) return false;
            nextWarningUtc = now.AddMinutes(5);
            return true;
        }
        public bool RecordSuccess()
        {
            bool recovered = failures > 0;
            failures = 0;
            nextWarningUtc = DateTime.MinValue;
            return recovered;
        }
    }
}

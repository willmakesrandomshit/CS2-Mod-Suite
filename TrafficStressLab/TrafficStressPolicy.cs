using System;

namespace TrafficStressTester
{
    internal static class TrafficStressPolicy
    {
        public static int ExtraDepartures(int multiplier, int remainingBudget)
        {
            if (multiplier != 2 && multiplier != 5 && multiplier != 10 && multiplier != 25) return 0;
            return Math.Min(multiplier - 1, Math.Max(0, remainingBudget));
        }

        public static int BatchBudget(int maximum, int ramp, int elapsedBatches)
        {
            maximum = Math.Max(1, Math.Min(512, maximum));
            int increment = ramp == 0 ? 1 : ramp == 2 ? 8 : 3;
            int steps = Math.Min(8, Math.Max(0, Math.Min(8, elapsedBatches)) * increment);
            return Math.Max(1, maximum * steps / 8);
        }
    }
}

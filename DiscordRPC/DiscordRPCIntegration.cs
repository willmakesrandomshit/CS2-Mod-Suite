using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace DiscordRPC
{
    /// <summary>Optional, reflection-friendly activity API for companion mods. No SDK dependency is required.</summary>
    public static class DiscordRPCIntegration
    {
        private sealed class Entry
        {
            public string Source;
            public string Details;
            public string State;
            public int Priority;
            public DateTime ExpiresUtc;
        }

        private static readonly ConcurrentDictionary<string, Entry> s_Entries = new ConcurrentDictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        public static bool PushActivity(string source, string details, string state = null, int priority = 50, double timeoutSeconds = 20)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(details)) return false;
            timeoutSeconds = Math.Max(2, Math.Min(timeoutSeconds, 3600));
            s_Entries[source.Trim()] = new Entry
            {
                Source = source.Trim(), Details = details.Trim(), State = state?.Trim(), Priority = priority,
                ExpiresUtc = DateTime.UtcNow.AddSeconds(timeoutSeconds)
            };
            DiscordRPCRuntime.RequestImmediateUpdate();
            return true;
        }

        public static void ClearActivity(string source)
        {
            if (!string.IsNullOrWhiteSpace(source)) s_Entries.TryRemove(source, out _);
        }

        public static void ClearAll() => s_Entries.Clear();

        internal static bool TryGetHighest(out string source, out string details, out string state)
        {
            var now = DateTime.UtcNow;
            foreach (KeyValuePair<string, Entry> pair in s_Entries)
                if (pair.Value.ExpiresUtc <= now) s_Entries.TryRemove(pair.Key, out _);

            Entry best = s_Entries.Values.OrderByDescending(x => x.Priority).ThenByDescending(x => x.ExpiresUtc).FirstOrDefault();
            source = best?.Source;
            details = best?.Details;
            state = best?.State;
            return best != null;
        }
    }

    internal static class DiscordRPCRuntime
    {
        private static int s_Immediate;
        public static void RequestImmediateUpdate() => System.Threading.Interlocked.Exchange(ref s_Immediate, 1);
        public static bool ConsumeImmediateUpdate() => System.Threading.Interlocked.Exchange(ref s_Immediate, 0) != 0;
    }
}

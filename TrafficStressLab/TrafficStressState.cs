using System.Threading;

namespace TrafficStressTester
{
    internal static class TrafficStressState
    {
        private static int s_Multiplier = 1;
        private static int s_StopRequested;
        private static int s_RequestsGenerated;

        public static int Multiplier => Volatile.Read(ref s_Multiplier);
        public static bool Enabled => Multiplier > 1;
        public static int RequestsGenerated => Volatile.Read(ref s_RequestsGenerated);

        public static void SetMultiplier(int value)
        {
            if (value != 1 && value != 2 && value != 5 && value != 10 && value != 25)
                value = 1;
            Interlocked.Exchange(ref s_Multiplier, value);
            if (value == 1) Interlocked.Exchange(ref s_StopRequested, 1);
        }

        public static void AddGenerated(int count) => Interlocked.Add(ref s_RequestsGenerated, count);

        public static bool ConsumeStopRequest() => Interlocked.Exchange(ref s_StopRequested, 0) != 0;

        public static void Reset()
        {
            Interlocked.Exchange(ref s_Multiplier, 1);
            Interlocked.Exchange(ref s_RequestsGenerated, 0);
            Interlocked.Exchange(ref s_StopRequested, 1);
        }

        public static void PrepareForNewCity(bool resetMultiplier)
        {
            Interlocked.Exchange(ref s_RequestsGenerated, 0);
            Interlocked.Exchange(ref s_StopRequested, 1);
            if (resetMultiplier) Interlocked.Exchange(ref s_Multiplier, 1);
        }
    }
}

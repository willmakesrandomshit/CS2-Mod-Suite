using System.Threading;

namespace TrafficStressTester
{
    internal static class TrafficStressState
    {
        private static int s_Multiplier = 1;
        private static int s_StopRequested;
        private static int s_RequestsGenerated;
        private static int s_ReadyRoutes;
        private static int s_BatchLimit;
        private static int s_Limited;
        private static int s_RoadVehicles;

        public static int Multiplier => Volatile.Read(ref s_Multiplier);
        public static bool Enabled => Multiplier > 1;
        public static int RequestsGenerated => Volatile.Read(ref s_RequestsGenerated);
        public static int ReadyRoutes => Volatile.Read(ref s_ReadyRoutes);
        public static int BatchLimit => Volatile.Read(ref s_BatchLimit);
        public static bool Limited => Volatile.Read(ref s_Limited) != 0;
        public static int RoadVehicles => Volatile.Read(ref s_RoadVehicles);

        public static void SetRoadVehicles(int count) => Interlocked.Exchange(ref s_RoadVehicles, count);
        public static void PublishBatch(int routes, int limit, bool limited)
        {
            Interlocked.Exchange(ref s_ReadyRoutes, routes);
            Interlocked.Exchange(ref s_BatchLimit, limit);
            Interlocked.Exchange(ref s_Limited, limited ? 1 : 0);
        }

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
            PublishBatch(0, 0, false);
        }

        public static void PrepareForNewCity(bool resetMultiplier)
        {
            Interlocked.Exchange(ref s_RequestsGenerated, 0);
            Interlocked.Exchange(ref s_StopRequested, 1);
            SetRoadVehicles(0);
            PublishBatch(0, 0, false);
            if (resetMultiplier) Interlocked.Exchange(ref s_Multiplier, 1);
        }
    }
}

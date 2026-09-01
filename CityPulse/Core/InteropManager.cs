using System;
using System.Collections.Generic;
using System.Reflection;
using Colossal.Logging;
using Unity.Entities;

namespace CityPulse
{
    public static class InteropManager
    {
        public static ILog Log = LogManager.GetLogger(nameof(CityPulse)).SetShowsErrorsInUI(false);

        private static readonly string[] kLegacyAssemblies = new[]
        {
            "TrafficPulse",
            "TransitPulse",
            "ParkingPulse",
            "ServiceDoctor",
            "DemandLens",
            "LaneDoctor"
        };

        public static bool CheckForLegacyMods(World world, out string warningMessage)
        {
            warningMessage = null;
            var detected = new List<string>();

            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    var name = assemblies[i].GetName().Name;
                    for (int l = 0; l < kLegacyAssemblies.Length; l++)
                    {
                        if (string.Equals(name, kLegacyAssemblies[l], StringComparison.OrdinalIgnoreCase))
                        {
                            if (!detected.Contains(kLegacyAssemblies[l]))
                            {
                                detected.Add(kLegacyAssemblies[l]);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn(ex, "Failed to scan AppDomain for legacy diagnostic assemblies.");
            }

            if (detected.Count > 0)
            {
                warningMessage = $"Legacy Standalone Mod(s) Detected: {string.Join(", ", detected)}. These have been merged into City Pulse 2.0.0. You may safely unsubscribe/disable their standalone versions.";
                return true;
            }

            return false;
        }
    }
}

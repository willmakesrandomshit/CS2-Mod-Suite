using System;
using System.Reflection;
using Colossal.Logging;

namespace Colossal.UtilitySuite.Interop
{
    public static class SuiteBridge
    {
        private static ILog s_Log = LogManager.GetLogger("SuiteBridge").SetShowsErrorsInUI(false);
        private static Type s_RegistryType;
        private static bool s_Initialized = false;

        private static Type GetRegistryType()
        {
            if (s_Initialized) return s_RegistryType;
            s_Initialized = true;

            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    var t = assemblies[i].GetType("Colossal.UtilitySuite.Interop.SuiteRegistry", false);
                    if (t != null)
                    {
                        s_RegistryType = t;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                s_Log.Warn(ex, "Failed to resolve SuiteRegistry type via reflection.");
            }

            return s_RegistryType;
        }

        public static void Register(string toolName, Action<int, object> launcher)
        {
            try
            {
                var reg = GetRegistryType();
                if (reg != null)
                {
                    var method = reg.GetMethod("RegisterToolLauncher", BindingFlags.Public | BindingFlags.Static);
                    method?.Invoke(null, new object[] { toolName, launcher });
                }
            }
            catch (Exception ex)
            {
                s_Log.Warn(ex, $"Failed to register tool launcher for {toolName}");
            }
        }

        public static void Unregister(string toolName)
        {
            try
            {
                var reg = GetRegistryType();
                if (reg != null)
                {
                    var method = reg.GetMethod("UnregisterToolLauncher", BindingFlags.Public | BindingFlags.Static);
                    method?.Invoke(null, new object[] { toolName });
                }
            }
            catch { }
        }

        public static bool Launch(string toolName, int entityIndex, object context = null)
        {
            try
            {
                var reg = GetRegistryType();
                if (reg != null)
                {
                    var method = reg.GetMethod("LaunchTool", BindingFlags.Public | BindingFlags.Static);
                    var res = method?.Invoke(null, new object[] { toolName, entityIndex, context });
                    return res is bool b && b;
                }
            }
            catch (Exception ex)
            {
                s_Log.Warn(ex, $"Failed to invoke LaunchTool for {toolName}");
            }
            return false;
        }

        public static bool IsAvailable(string toolName)
        {
            try
            {
                var reg = GetRegistryType();
                if (reg != null)
                {
                    var method = reg.GetMethod("IsToolAvailable", BindingFlags.Public | BindingFlags.Static);
                    var res = method?.Invoke(null, new object[] { toolName });
                    return res is bool b && b;
                }
            }
            catch { }
            return false;
        }
    }
}

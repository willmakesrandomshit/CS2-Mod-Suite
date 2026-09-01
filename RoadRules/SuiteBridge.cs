using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Colossal.Logging;
using UnityEngine;

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

    public static class SuiteLogger
    {
        private static readonly Dictionary<string, (DateTime lastTime, int count)> s_ErrorThrottle = new Dictionary<string, (DateTime, int)>();
        private static readonly List<string> s_RecentSanitizedErrors = new List<string>();
        private const int kMaxRecentErrors = 50;

        public static string Sanitize(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile)) input = input.Replace(userProfile, "%USERPROFILE%");
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(appData)) input = input.Replace(appData, "%APPDATA%");
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData)) input = input.Replace(localAppData, "%LOCALAPPDATA%");
            return input;
        }

        public static void LogInfo(ILog logger, string modName, string message)
        {
            logger?.Info($"[{modName}] {message}");
        }

        public static void LogWarn(ILog logger, string modName, string message)
        {
            logger?.Warn($"[{modName}] [WARN] {message}");
        }

        public static void LogError(ILog logger, string modName, Exception ex, string context = "")
        {
            if (logger == null || ex == null) return;

            string key = $"{ex.GetType().Name}:{ex.Message}:{context}";
            DateTime now = DateTime.UtcNow;

            lock (s_ErrorThrottle)
            {
                if (s_ErrorThrottle.TryGetValue(key, out var record))
                {
                    if ((now - record.lastTime).TotalSeconds < 5.0)
                    {
                        s_ErrorThrottle[key] = (record.lastTime, record.count + 1);
                        return; // Throttled to prevent Player.log spam
                    }
                    else if (record.count > 1)
                    {
                        logger.Warn($"[{modName}] (Previous error repeated {record.count} times in last {(int)(now - record.lastTime).TotalSeconds}s)");
                    }
                }
                s_ErrorThrottle[key] = (now, 1);

                string sanitized = Sanitize($"[{modName}] [ERROR] {context} - {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                if (s_RecentSanitizedErrors.Count >= kMaxRecentErrors) s_RecentSanitizedErrors.RemoveAt(0);
                s_RecentSanitizedErrors.Add($"[{now:HH:mm:ss}] {sanitized}");

                logger.Error(ex, $"[{modName}] [ERROR] {context}");
            }
        }

        public static IReadOnlyList<string> GetRecentErrors()
        {
            lock (s_ErrorThrottle)
            {
                return new List<string>(s_RecentSanitizedErrors);
            }
        }
    }

    public static class SupportReportGenerator
    {
        public static string ExportReport(string modName, string modVersion, Dictionary<string, string> stateSnapshot = null)
        {
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "ModsData", modName);
                Directory.CreateDirectory(dir);
                string fileName = $"{modName}-Support-Report-{DateTime.Now:yyyyMMdd-HHmmss}.txt";
                string fullPath = Path.Combine(dir, fileName);

                var sb = new StringBuilder();
                sb.AppendLine("================================================================================");
                sb.AppendLine($"   {modName.ToUpper()} SUPPORT & DIAGNOSTIC REPORT");
                sb.AppendLine("================================================================================");
                sb.AppendLine($"Generated:      {DateTime.UtcNow:u} (UTC)");
                sb.AppendLine($"Mod Version:    {modVersion}");
                sb.AppendLine($"Unity Version:  {Application.unityVersion}");
                sb.AppendLine($"Platform:       {Application.platform}");
                sb.AppendLine();

                sb.AppendLine("--- ACTIVE ENVIRONMENT & CONFIGURATION ---");
                if (stateSnapshot != null)
                {
                    foreach (var kvp in stateSnapshot)
                    {
                        sb.AppendLine($"{kvp.Key,-24}: {SuiteLogger.Sanitize(kvp.Value)}");
                    }
                }
                sb.AppendLine();

                sb.AppendLine("--- RECENT SANITIZED ERRORS & WARNINGS ---");
                var errors = SuiteLogger.GetRecentErrors();
                if (errors.Count == 0)
                {
                    sb.AppendLine("No recent errors logged. Session is clean.");
                }
                else
                {
                    foreach (var err in errors)
                    {
                        sb.AppendLine(err);
                    }
                }
                sb.AppendLine();

                sb.AppendLine("================================================================================");
                sb.AppendLine("Privacy Note: Local user paths have been sanitized to %USERPROFILE%.");
                sb.AppendLine("================================================================================");

                File.WriteAllText(fullPath, sb.ToString(), new UTF8Encoding(false));
                return fullPath;
            }
            catch (Exception ex)
            {
                return $"Failed to export support report: {ex.Message}";
            }
        }

        public static void OpenFolder(string modName)
        {
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "ModsData", modName);
                Directory.CreateDirectory(dir);
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
            }
            catch { }
        }
    }
}

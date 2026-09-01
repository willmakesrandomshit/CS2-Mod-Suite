using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace CrashLens
{
    internal sealed class Suspect
    {
        public string Name;
        public int Score;
        public int Errors;
        public int Warnings;
        public readonly HashSet<string> Reasons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    internal static class CrashLensRuntime
    {
        private static readonly Dictionary<string, long> Offsets = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Suspect> Suspects = new Dictionary<string, Suspect>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> Activity = new List<string>();
        private static readonly List<AssemblyName> ThirdParty = new List<AssemblyName>();
        private static readonly List<string> DuplicateAssemblies = new List<string>();
        private static bool s_FullScan;
        private static bool s_PreviousScanned;
        private static float s_NextScan;
        private static DateTime s_SessionStart;

        public static int ErrorCount { get; private set; }
        public static int WarningCount { get; private set; }
        public static int LoadedMods => ThirdParty.Count;
        public static int DuplicateCount => DuplicateAssemblies.Count;
        public static string Health { get; private set; } = "Scanning";
        public static string Summary { get; private set; } = "Building a session baseline";
        public static string SuspectsJson { get; private set; } = "[]";
        public static string ActivityJson { get; private set; } = "[]";
        public static string LastReportPath { get; private set; } = "";
        public static bool HighConfidenceIssue { get; private set; }

        public static void Initialize()
        {
            Offsets.Clear();
            Suspects.Clear();
            Activity.Clear();
            ThirdParty.Clear();
            DuplicateAssemblies.Clear();
            ErrorCount = 0;
            WarningCount = 0;
            HighConfidenceIssue = false;
            Health = "Scanning";
            Summary = "Building a session baseline";
            SuspectsJson = "[]";
            ActivityJson = "[]";
            LastReportPath = "";
            s_PreviousScanned = false;
            s_SessionStart = DateTime.Now;
            RefreshAssemblies();
            s_FullScan = true;
            s_NextScan = 0f;
        }

        public static void Dispose()
        {
            Offsets.Clear(); Suspects.Clear(); Activity.Clear(); ThirdParty.Clear(); DuplicateAssemblies.Clear();
            s_FullScan = false;
            s_PreviousScanned = false;
            HighConfidenceIssue = false;
        }

        public static void Update()
        {
            if (Mod.Settings != null && !Mod.Settings.Enabled) return;
            if (!s_FullScan && Time.realtimeSinceStartup < s_NextScan) return;
            s_NextScan = Time.realtimeSinceStartup + Math.Max(2, Mod.Settings?.ScanIntervalSeconds ?? 5);
            Scan(s_FullScan);
            s_FullScan = false;
        }

        public static void RequestFullScan() { s_FullScan = true; s_PreviousScanned = false; }

        private static void Scan(bool full)
        {
            try
            {
                if (full)
                {
                    Offsets.Clear(); Suspects.Clear(); Activity.Clear(); ErrorCount = 0; WarningCount = 0;
                    RefreshAssemblies();
                }

                string root = Application.persistentDataPath;
                if ((Mod.Settings?.AnalysePreviousSession ?? true) && !s_PreviousScanned)
                {
                    ScanFile(Path.Combine(root, "Player-prev.log"), true, "Previous session");
                    s_PreviousScanned = true;
                }
                ScanFile(Path.Combine(root, "Player.log"), full, "Game");
                string logs = Path.Combine(root, "Logs");
                if (Directory.Exists(logs))
                {
                    foreach (string path in Directory.GetFiles(logs, "*.log").OrderByDescending(File.GetLastWriteTimeUtc).Take(80))
                        ScanFile(path, full, Path.GetFileNameWithoutExtension(path));
                }
                RebuildView();
            }
            catch (Exception ex) { Mod.Log.Warn("Health scan skipped: " + ex.Message); }
        }

        private static void RefreshAssemblies()
        {
            ThirdParty.Clear(); DuplicateAssemblies.Clear();
            var loaded = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic).ToList();
            foreach (Assembly asm in loaded)
            {
                AssemblyName n = asm.GetName();
                string name = n.Name ?? "";
                if (!IsFramework(name)) ThirdParty.Add(n);
            }
            foreach (var group in loaded.GroupBy(a => a.GetName().Name, StringComparer.OrdinalIgnoreCase).Where(g => !IsFramework(g.Key) && g.Count() > 1))
            {
                var versions = group.Select(a => (a.GetName().Version?.ToString() ?? "?") + " @ " + SafeLocation(a)).Distinct().ToList();
                if (versions.Count > 1) DuplicateAssemblies.Add(group.Key + ": " + string.Join(" | ", versions));
            }
            foreach (string duplicate in DuplicateAssemblies)
            {
                string name = duplicate.Split(':')[0];
                AddEvidence(name, 12, true, "multiple loaded DLL versions");
            }
        }

        private static bool IsFramework(string n)
        {
            string[] prefixes = { "System", "Microsoft", "Unity", "Game", "Colossal", "netstandard", "mscorlib", "Mono", "Newtonsoft", "UI", "Ansel", "nunit", "Bee", "Burst" };
            return prefixes.Any(p => n.Equals(p, StringComparison.OrdinalIgnoreCase) || n.StartsWith(p + ".", StringComparison.OrdinalIgnoreCase));
        }

        private static string SafeLocation(Assembly a) { try { return Sanitize(a.Location); } catch { return "unknown"; } }

        private static void ScanFile(string path, bool tailOnly, string source)
        {
            if (!File.Exists(path)) return;
            const long maxInitial = 512 * 1024;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                long start;
                if (Offsets.TryGetValue(path, out long known) && known <= stream.Length) start = known;
                else start = tailOnly ? Math.Max(0, stream.Length - maxInitial) : 0;
                if (stream.Length - start > maxInitial) start = stream.Length - maxInitial;
                stream.Seek(start, SeekOrigin.Begin);
                using (var reader = new StreamReader(stream, Encoding.UTF8, true, 4096, true))
                {
                    string text = reader.ReadToEnd();
                    Offsets[path] = stream.Length;
                    AnalyseText(text, source);
                }
            }
        }

        private static void AnalyseText(string text, string source)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            bool perModLog = source != "Game" && source != "Previous session" && !IsFramework(source);
            foreach (string raw in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = raw.Trim();
                bool error = ContainsAny(line, "[ERROR]", " ERROR ", "Exception:", "Exception ", "NullReferenceException", "MissingMethodException", "TypeLoadException", "failed to load", "Crash!!!", "Unhandled Exception");
                bool warning = !error && (Mod.Settings?.IncludeWarnings ?? true) && ContainsAny(line, "[WARN", " WARNING ", "Warning:", "could not", "not compatible");
                if (!error && !warning) continue;
                if (error) ErrorCount++; else WarningCount++;
                string compact = line.Length > 180 ? line.Substring(0, 180) + "..." : line;
                AddActivity((error ? "Error" : "Warning") + " | " + source + " | " + compact);

                bool matched = false;
                foreach (AssemblyName asm in ThirdParty)
                {
                    if (line.IndexOf(asm.Name, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    AddEvidence(asm.Name, error ? 7 : 2, error, (error ? "error" : "warning") + " stack/log reference in " + source);
                    matched = true;
                }
                if (perModLog)
                    AddEvidence(source, error ? (matched ? 2 : 5) : 1, error, (error ? "error" : "warning") + " emitted by its log");

                // Signature: Stale UI system during world preload
                if (line.IndexOf("FastTrackUISystem", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    (line.IndexOf("CheckedState", StringComparison.OrdinalIgnoreCase) >= 0 || line.IndexOf("OnGamePreload", StringComparison.OrdinalIgnoreCase) >= 0 || line.IndexOf("already been destroyed", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    AddEvidence("FastTrack", 15, true, "STALE/DESTROYED UI ECS SYSTEM INSTANCE DURING WORLD PRELOAD: Likely system registration/lifecycle defect, not necessarily save corruption.");
                }
            }
        }

        private static bool ContainsAny(string value, params string[] needles) => needles.Any(n => value.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0);

        private static void AddEvidence(string name, int score, bool error, string reason)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Equals("CrashLens", StringComparison.OrdinalIgnoreCase)) return;
            if (!Suspects.TryGetValue(name, out Suspect s)) Suspects[name] = s = new Suspect { Name = name };
            s.Score += score; if (error) s.Errors++; else s.Warnings++; s.Reasons.Add(reason);
        }

        private static void AddActivity(string line)
        {
            Activity.Add(Sanitize(line));
            if (Activity.Count > 60) Activity.RemoveAt(0);
        }

        private static void RebuildView()
        {
            var ranked = Suspects.Values.OrderByDescending(s => s.Score).ThenByDescending(s => s.Errors).Take(10).ToList();
            SuspectsJson = "[" + string.Join(",", ranked.Select(s => "{\"name\":\"" + Json(s.Name) + "\",\"confidence\":\"" + Confidence(s.Score) + "\",\"score\":" + s.Score + ",\"errors\":" + s.Errors + ",\"warnings\":" + s.Warnings + ",\"reason\":\"" + Json(string.Join("; ", s.Reasons.Take(3))) + "\"}")) + "]";
            ActivityJson = "[" + string.Join(",", Activity.AsEnumerable().Reverse().Take(30).Select(x => "\"" + Json(x) + "\"")) + "]";
            int top = ranked.Count == 0 ? 0 : ranked[0].Score;
            HighConfidenceIssue = top >= 12;
            if (HighConfidenceIssue) { Health = "Action recommended"; Summary = ranked[0].Name + " has the strongest evidence"; }
            else if (ErrorCount > 0 || DuplicateCount > 0) { Health = "Needs attention"; Summary = "Errors found, but no single culprit is proven"; }
            else if (WarningCount > 0) { Health = "Watch"; Summary = "Warnings detected; the session is currently stable"; }
            else { Health = "No evidence found"; Summary = "No supported mod-error patterns were found in the scanned log sections"; }
        }

        private static string Confidence(int score) => score >= 12 ? "High" : score >= 6 ? "Medium" : "Low";
        private static string Json(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u" + ((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
        private static string Sanitize(string s)
        {
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(profile) || string.IsNullOrEmpty(s)) return s;

            var result = new StringBuilder(s.Length);
            int cursor = 0;
            while (cursor < s.Length)
            {
                int match = s.IndexOf(profile, cursor, StringComparison.OrdinalIgnoreCase);
                if (match < 0)
                {
                    result.Append(s, cursor, s.Length - cursor);
                    break;
                }
                result.Append(s, cursor, match - cursor);
                result.Append("%USERPROFILE%");
                cursor = match + profile.Length;
            }
            return result.ToString();
        }

        public static void ExportReport()
        {
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "ModsData", "CrashLens");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "CrashLens-Report-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
                var b = new StringBuilder();
                b.AppendLine("CrashLens Support Report").AppendLine("Generated: " + DateTime.Now.ToString("u")).AppendLine("Session start: " + s_SessionStart.ToString("u"));
                b.AppendLine("Health: " + Health).AppendLine("Summary: " + Summary).AppendLine("Errors: " + ErrorCount + " | Warnings: " + WarningCount);
                b.AppendLine("Third-party assemblies: " + LoadedMods + " | duplicate-version signals: " + DuplicateCount).AppendLine();
                b.AppendLine("Ranked evidence (a suspect is not proof):");
                foreach (Suspect s in Suspects.Values.OrderByDescending(x => x.Score).Take(15))
                    b.AppendLine("- " + s.Name + " | " + Confidence(s.Score) + " | score " + s.Score + " | " + string.Join("; ", s.Reasons));
                if (DuplicateAssemblies.Count > 0) { b.AppendLine().AppendLine("Duplicate assemblies:"); foreach (string d in DuplicateAssemblies) b.AppendLine("- " + Sanitize(d)); }
                if ((Mod.Settings?.ReportDetail ?? ReportDetail.Standard) != ReportDetail.Compact)
                {
                    b.AppendLine().AppendLine("Loaded third-party assemblies:");
                    foreach (AssemblyName a in ThirdParty.OrderBy(x => x.Name)) b.AppendLine("- " + a.Name + " " + a.Version);
                }
                int lines = (Mod.Settings?.ReportDetail ?? ReportDetail.Standard) == ReportDetail.Detailed ? 60 : 25;
                b.AppendLine().AppendLine("Recent evidence:"); foreach (string line in Activity.AsEnumerable().Reverse().Take(lines)) b.AppendLine("- " + line);
                b.AppendLine().AppendLine("Privacy: user-profile paths are replaced with %USERPROFILE%. CrashLens does not upload reports.");
                File.WriteAllText(path, b.ToString(), new UTF8Encoding(false));
                LastReportPath = path;
                AddActivity("Report exported | " + Sanitize(path));
            }
            catch (Exception ex) { Mod.Log.Error(ex, "Could not export CrashLens report"); }
        }

        public static void OpenReportFolder()
        {
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "ModsData", "CrashLens"); Directory.CreateDirectory(dir);
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + dir + "\"") { UseShellExecute = true });
            }
            catch (Exception ex) { Mod.Log.Warn("Could not open report folder: " + ex.Message); }
        }
    }
}

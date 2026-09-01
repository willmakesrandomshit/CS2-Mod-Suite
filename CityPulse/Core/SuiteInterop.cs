using System;
using System.Collections.Generic;
using System.Reflection;
using Colossal.Logging;
using Unity.Entities;
using Unity.Mathematics;

namespace Colossal.UtilitySuite.Interop
{
    public enum SuiteSeverity
    {
        Healthy,
        Advisory,
        Warning,
        Critical
    }

    public enum SuiteModule
    {
        CityPulse,
        MagicMarking,
        RoadRules,
        JunctionStudio,
        TrafficStressLab,
        CrashLens,
        ContourPlus,
        FastTrack,
        SaveGuard
    }

    public struct DiagnosticFinding
    {
        public string FindingId;
        public SuiteModule Source;
        public SuiteSeverity Severity;
        public string Title;
        public string MeasuredEvidence;
        public string InferredCause;
        public string Confidence; // "HIGH", "MED", "LOW"
        public int EntityIndex;
        public float3 Position;
        public string RecommendedTool; // "MagicMarking", "RoadRules", "JunctionStudio", "TrafficStressLab", "CrashLens", "ContourPlus"
        public string ActionLabel;
    }

    public struct BeforeAfterSnapshot
    {
        public string BaselineId;
        public DateTime Timestamp;
        public int EntityIndex;
        public float AverageSpeedKph;
        public float StoppedRatioPercent;
        public float QueuePressurePercent;
        public float CongestionIndex;
        public int SafetyIncidentCount;
    }

    public static class SuiteRegistry
    {
        public static readonly Dictionary<string, Action<int, object>> ToolLaunchers = new Dictionary<string, Action<int, object>>(StringComparer.OrdinalIgnoreCase);
        public static readonly List<DiagnosticFinding> SharedFindings = new List<DiagnosticFinding>();
        public static readonly Dictionary<int, BeforeAfterSnapshot> Baselines = new Dictionary<int, BeforeAfterSnapshot>();

        public static void RegisterToolLauncher(string toolName, Action<int, object> launcher)
        {
            if (string.IsNullOrEmpty(toolName) || launcher == null) return;
            ToolLaunchers[toolName] = launcher;
        }

        public static void UnregisterToolLauncher(string toolName)
        {
            if (string.IsNullOrEmpty(toolName)) return;
            ToolLaunchers.Remove(toolName);
        }

        public static bool IsToolAvailable(string toolName)
        {
            return !string.IsNullOrEmpty(toolName) && ToolLaunchers.ContainsKey(toolName);
        }

        public static bool LaunchTool(string toolName, int entityIndex, object context = null)
        {
            if (string.IsNullOrEmpty(toolName)) return false;
            if (ToolLaunchers.TryGetValue(toolName, out var launcher))
            {
                try
                {
                    launcher.Invoke(entityIndex, context);
                    return true;
                }
                catch (Exception ex)
                {
                    LogManager.GetLogger("SuiteInterop").Warn(ex, $"Failed to launch tool {toolName}");
                }
            }
            return false;
        }

        public static void PublishFinding(DiagnosticFinding finding)
        {
            SharedFindings.Add(finding);
            if (SharedFindings.Count > 100)
            {
                SharedFindings.RemoveAt(0); // Bounded memory
            }
        }

        public static void CaptureBaseline(int entityIndex, BeforeAfterSnapshot snapshot)
        {
            Baselines[entityIndex] = snapshot;
        }

        public static bool TryGetBaseline(int entityIndex, out BeforeAfterSnapshot baseline)
        {
            return Baselines.TryGetValue(entityIndex, out baseline);
        }

        public static void ClearAll()
        {
            ToolLaunchers.Clear();
            SharedFindings.Clear();
            Baselines.Clear();
        }
    }
}

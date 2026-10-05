using System;
using System.Linq;
using System.Reflection;
using Colossal.Logging;
using Colossal.UI.Binding;
using Game.UI;
using UnityEngine;

namespace DiscordRPC
{
    // Explicit requests only: no per-frame assembly enumeration or log scanning.
    internal sealed partial class PortfolioSupportUISystem : UISystemBase
    {
        private const string Group = "Portfolio.DiscordRPC";
        private ValueBinding<string> _compatibility;
        private ValueBinding<string> _copyResult;

        protected override void OnCreate()
        {
            base.OnCreate();
            var assembly = typeof(PortfolioSupportUISystem).Assembly;
            string version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString() ?? "Unknown";
            AddBinding(new ValueBinding<string>(Group, "runtimeVersion", version));
            AddBinding(new ValueBinding<string>(Group, "gameVersion", Application.version));
            _compatibility = new ValueBinding<string>(Group, "compatibility", "");
            _copyResult = new ValueBinding<string>(Group, "copyResult", "");
            AddBinding(_compatibility);
            AddBinding(_copyResult);
            AddBinding(new TriggerBinding(Group, "checkCompatibility", CheckCompatibility));
            AddBinding(new TriggerBinding<string>(Group, "copySnapshot", CopySnapshot));
        }

        private void CheckCompatibility()
        {
            _copyResult.Update("");
            var names = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name).ToArray();
            string own = typeof(PortfolioSupportUISystem).Assembly.GetName().Name;
            string message = names.Count(n => n == own) > 1
                ? "Multiple assemblies for this mod are loaded. Check for duplicate local and Paradox copies. " : "";
            if (names.Contains("TownRoadLane") && names.Contains("MarkingStudio"))
                message += "Town Road Lane and Magic Marking assemblies are both loaded. Enable only one marking implementation. ";
            if (own == "JunctionStudio" && !names.Contains("TownRoadLane"))
                message += "Town Road Lane is required for Junction Studio. Enable the dependency and restart. ";
            _compatibility.Update(message);
        }

        private void CopySnapshot(string report)
        {
            // The UI sends a numeric/boolean snapshot, never logs, paths or city names.
            if (string.IsNullOrEmpty(report) || report.Length > 4096)
            {
                _copyResult.Update("Snapshot is empty or too large. Select the text to copy manually.");
                return;
            }
            try
            {
                GUIUtility.systemCopyBuffer = report;
                _copyResult.Update("Copied");
            }
            catch (Exception ex)
            {
                LogManager.GetLogger(Group).Warn($"Clipboard copy failed: {ex.GetType().Name}: {ex.Message}");
                _copyResult.Update("Clipboard unavailable. Select the text and press Ctrl+C.");
            }
        }
    }
}

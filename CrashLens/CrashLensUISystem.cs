using Colossal.Serialization.Entities;
using Colossal.UI.Binding;
using Game;
using Game.UI;

namespace CrashLens
{
    public sealed partial class CrashLensUISystem : UISystemBase
    {
        private const string Group = "crashLens";
        private bool m_Open;
        private bool m_AutoOpened;
        private ValueBinding<bool> m_OpenBinding;
        private ValueBinding<bool> m_ShowButton;
        private ValueBinding<string> m_Health;
        private ValueBinding<string> m_Summary;
        private ValueBinding<int> m_Errors;
        private ValueBinding<int> m_Warnings;
        private ValueBinding<int> m_Mods;
        private ValueBinding<int> m_Duplicates;
        private ValueBinding<string> m_Suspects;
        private ValueBinding<string> m_Activity;
        private ValueBinding<string> m_ReportPath;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_OpenBinding = new ValueBinding<bool>(Group, "open", false, ValueWriters.Create<bool>());
            m_ShowButton = new ValueBinding<bool>(Group, "showButton", true, ValueWriters.Create<bool>());
            m_Health = new ValueBinding<string>(Group, "health", "Scanning", ValueWriters.Create<string>());
            m_Summary = new ValueBinding<string>(Group, "summary", "Building a session baseline", ValueWriters.Create<string>());
            m_Errors = new ValueBinding<int>(Group, "errors", 0, ValueWriters.Create<int>());
            m_Warnings = new ValueBinding<int>(Group, "warnings", 0, ValueWriters.Create<int>());
            m_Mods = new ValueBinding<int>(Group, "mods", 0, ValueWriters.Create<int>());
            m_Duplicates = new ValueBinding<int>(Group, "duplicates", 0, ValueWriters.Create<int>());
            m_Suspects = new ValueBinding<string>(Group, "suspects", "[]", ValueWriters.Create<string>());
            m_Activity = new ValueBinding<string>(Group, "activity", "[]", ValueWriters.Create<string>());
            m_ReportPath = new ValueBinding<string>(Group, "reportPath", "", ValueWriters.Create<string>());
            AddBinding(m_OpenBinding); AddBinding(m_ShowButton); AddBinding(m_Health); AddBinding(m_Summary); AddBinding(m_Errors); AddBinding(m_Warnings);
            AddBinding(m_Mods); AddBinding(m_Duplicates); AddBinding(m_Suspects); AddBinding(m_Activity); AddBinding(m_ReportPath);
            AddBinding(new TriggerBinding(Group, "toggle", () => m_Open = !m_Open));
            AddBinding(new TriggerBinding(Group, "close", () => m_Open = false));
            AddBinding(new TriggerBinding(Group, "rescan", CrashLensRuntime.RequestFullScan));
            AddBinding(new TriggerBinding(Group, "export", CrashLensRuntime.ExportReport));
            AddBinding(new TriggerBinding(Group, "openFolder", CrashLensRuntime.OpenReportFolder));
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            if (World == null || !World.IsCreated) return;
            base.OnGamePreload(purpose, mode);
            m_Open = false;
            m_AutoOpened = false;
        }

        protected override void OnUpdate()
        {
            if (World == null || !World.IsCreated) return;
            base.OnUpdate();
            if (!m_AutoOpened && CrashLensRuntime.HighConfidenceIssue && (Mod.Settings?.OpenOnHighConfidenceIssue ?? false)) { m_Open = true; m_AutoOpened = true; }
            m_OpenBinding.Update(m_Open); m_ShowButton.Update(Mod.Settings == null || Mod.Settings.ShowToolbarButton);
            m_Health.Update(CrashLensRuntime.Health); m_Summary.Update(CrashLensRuntime.Summary); m_Errors.Update(CrashLensRuntime.ErrorCount); m_Warnings.Update(CrashLensRuntime.WarningCount);
            m_Mods.Update(CrashLensRuntime.LoadedMods); m_Duplicates.Update(CrashLensRuntime.DuplicateCount); m_Suspects.Update(CrashLensRuntime.SuspectsJson);
            m_Activity.Update(CrashLensRuntime.ActivityJson); m_ReportPath.Update(CrashLensRuntime.LastReportPath);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
        }
    }
}

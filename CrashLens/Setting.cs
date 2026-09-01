using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using System.Collections.Generic;

namespace CrashLens
{
    [FileLocation(nameof(CrashLens))]
    [SettingsUIGroupOrder(kMonitor, kInterface, kReports)]
    [SettingsUIShowGroupName(kMonitor, kInterface, kReports)]
    public sealed class CrashLensSetting : ModSetting
    {
        public const string kMain = "Main";
        public const string kMonitor = "Monitoring";
        public const string kInterface = "Interface";
        public const string kReports = "Reports";

        public CrashLensSetting(IMod mod) : base(mod) => SetDefaults();

        [SettingsUISection(kMain, kMonitor)] public bool Enabled { get; set; }
        [SettingsUISection(kMain, kMonitor)] public bool AnalysePreviousSession { get; set; }
        [SettingsUISection(kMain, kMonitor)] public bool IncludeWarnings { get; set; }
        [SettingsUISlider(min = 2, max = 15, step = 1)]
        [SettingsUISection(kMain, kMonitor)] public int ScanIntervalSeconds { get; set; }
        [SettingsUISection(kMain, kInterface)] public bool ShowToolbarButton { get; set; }
        [SettingsUISection(kMain, kInterface)] public bool OpenOnHighConfidenceIssue { get; set; }
        [SettingsUISection(kMain, kReports)] public ReportDetail ReportDetail { get; set; }

        [SettingsUIButton]
        [SettingsUISection(kMain, kReports)]
        public bool RescanNow { set { CrashLensRuntime.RequestFullScan(); } }

        [SettingsUIButton]
        [SettingsUISection(kMain, kReports)]
        public bool ExportSupportReport { set { CrashLensRuntime.ExportReport(); } }

        public override void SetDefaults()
        {
            Enabled = true;
            AnalysePreviousSession = true;
            IncludeWarnings = true;
            ScanIntervalSeconds = 5;
            ShowToolbarButton = true;
            OpenOnHighConfidenceIssue = false;
            ReportDetail = ReportDetail.Standard;
        }
    }

    public enum ReportDetail { Compact, Standard, Detailed }

    public sealed class LocaleEN : IDictionarySource
    {
        private readonly CrashLensSetting s;
        public LocaleEN(CrashLensSetting setting) => s = setting;
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> counts) => new Dictionary<string, string>
        {
            { s.GetSettingsLocaleID(), "CrashLens" }, { s.GetOptionTabLocaleID(CrashLensSetting.kMain), "Main" },
            { s.GetOptionGroupLocaleID(CrashLensSetting.kMonitor), "Monitoring" }, { s.GetOptionGroupLocaleID(CrashLensSetting.kInterface), "Interface" }, { s.GetOptionGroupLocaleID(CrashLensSetting.kReports), "Reports" },
            { s.GetOptionLabelLocaleID(nameof(CrashLensSetting.Enabled)), "Enable live monitoring" }, { s.GetOptionDescLocaleID(nameof(CrashLensSetting.Enabled)), "Watches game and per-mod logs for new evidence. Read-only; it never changes mods or saves." },
            { s.GetOptionLabelLocaleID(nameof(CrashLensSetting.AnalysePreviousSession)), "Analyse previous session" }, { s.GetOptionDescLocaleID(nameof(CrashLensSetting.AnalysePreviousSession)), "Reads Player-prev.log at startup to explain crashes or errors from the last run." },
            { s.GetOptionLabelLocaleID(nameof(CrashLensSetting.IncludeWarnings)), "Include mod warnings" }, { s.GetOptionDescLocaleID(nameof(CrashLensSetting.IncludeWarnings)), "Surfaces warnings that often precede crashes, like missing prefabs or broken asset metadata." },
            { s.GetOptionLabelLocaleID(nameof(CrashLensSetting.ScanIntervalSeconds)), "Log scan interval" }, { s.GetOptionDescLocaleID(nameof(CrashLensSetting.ScanIntervalSeconds)), "How often in seconds CrashLens checks game logs for fresh stack traces." },
            { s.GetOptionLabelLocaleID(nameof(CrashLensSetting.ShowToolbarButton)), "Show CrashLens toolbar button" }, { s.GetOptionDescLocaleID(nameof(CrashLensSetting.ShowToolbarButton)), "Shows the CrashLens launcher in the top toolbar." },
            { s.GetOptionLabelLocaleID(nameof(CrashLensSetting.OpenOnHighConfidenceIssue)), "Open on strong log evidence" }, { s.GetOptionDescLocaleID(nameof(CrashLensSetting.OpenOnHighConfidenceIssue)), "Opens CrashLens when its evidence score crosses the alert threshold. A ranked lead is not proof of causation." },
            { s.GetOptionLabelLocaleID(nameof(CrashLensSetting.ReportDetail)), "Report detail level" }, { s.GetOptionDescLocaleID(nameof(CrashLensSetting.ReportDetail)), "How much stack trace context is copied to exported reports." },
            { s.GetOptionLabelLocaleID(nameof(CrashLensSetting.RescanNow)), "Rescan logs now" }, { s.GetOptionDescLocaleID(nameof(CrashLensSetting.RescanNow)), "Forces a full scan of current and previous logs right now." },
            { s.GetOptionLabelLocaleID(nameof(CrashLensSetting.ExportSupportReport)), "Export support report" }, { s.GetOptionDescLocaleID(nameof(CrashLensSetting.ExportSupportReport)), "Writes a summary to ModsData/CrashLens. Profile paths are redacted, but inspect the report for private information before sharing." },
            { s.GetEnumValueLocaleID(ReportDetail.Compact), "Compact" }, { s.GetEnumValueLocaleID(ReportDetail.Standard), "Standard" }, { s.GetEnumValueLocaleID(ReportDetail.Detailed), "Detailed" }
        };
        public void Unload() { }
    }
}

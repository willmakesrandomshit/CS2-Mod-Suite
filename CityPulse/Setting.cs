using System.Collections.Generic;
using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;

namespace CityPulse
{
    [FileLocation(nameof(CityPulse))]
    [SettingsUIGroupOrder(kGeneralGroup, kTrafficGroup, kTransitGroup, kParkingGroup, kServicesGroup, kBuildingsGroup, kNetworkGroup)]
    [SettingsUIShowGroupName(kGeneralGroup, kTrafficGroup, kTransitGroup, kParkingGroup, kServicesGroup, kBuildingsGroup, kNetworkGroup)]
    public class CityPulseSetting : ModSetting
    {
        public const string kSection = "Main";
        public const string kGeneralGroup = "General";
        public const string kTrafficGroup = "Traffic Diagnostics";
        public const string kTransitGroup = "Transit Intelligence";
        public const string kParkingGroup = "Parking Analytics";
        public const string kServicesGroup = "Service Diagnostics";
        public const string kBuildingsGroup = "Building Diagnostics";
        public const string kNetworkGroup = "Road Network Diagnostics";

        public CityPulseSetting(IMod mod) : base(mod)
        {
            SetDefaults();
        }

        // General
        [SettingsUISection(kSection, kGeneralGroup)]
        public bool ShowToolbarButton { get; set; } = true;

        [SettingsUISection(kSection, kGeneralGroup)]
        public bool EnableLegacyWarning { get; set; } = true;

        // Traffic
        [SettingsUISection(kSection, kTrafficGroup)]
        public bool EnableFlowTracing { get; set; } = true;

        // Transit
        [SettingsUISection(kSection, kTransitGroup)]
        public bool EnableBunchingDetector { get; set; } = true;

        // Parking
        [SettingsUISection(kSection, kParkingGroup)]
        public bool EnableOvercapacityAlerts { get; set; } = true;

        // Services
        [SettingsUISection(kSection, kServicesGroup)]
        public bool EnableCriticalServiceAlerts { get; set; } = true;

        // Buildings
        [SettingsUISection(kSection, kBuildingsGroup)]
        public bool AutoInspectSelectedBuilding { get; set; } = true;

        // Network
        [SettingsUISection(kSection, kNetworkGroup)]
        public bool EnableNetworkDefectScanner { get; set; } = true;

        public override void SetDefaults()
        {
            ShowToolbarButton = true;
            EnableLegacyWarning = true;
            EnableFlowTracing = true;
            EnableBunchingDetector = true;
            EnableOvercapacityAlerts = true;
            EnableCriticalServiceAlerts = true;
            AutoInspectSelectedBuilding = true;
            EnableNetworkDefectScanner = true;
        }
    }

    public class LocaleEN : IDictionarySource
    {
        private readonly CityPulseSetting m_Setting;
        public LocaleEN(CityPulseSetting setting) { m_Setting = setting; }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "City Pulse — Complete City Diagnostics" },
                { m_Setting.GetOptionLabelLocaleID(nameof(CityPulseSetting.ShowToolbarButton)), "Show Toolbar Launcher" },
                { m_Setting.GetOptionDescLocaleID(nameof(CityPulseSetting.ShowToolbarButton)), "Displays the City Pulse master diagnostics button in the top toolbar." },
                { m_Setting.GetOptionLabelLocaleID(nameof(CityPulseSetting.EnableLegacyWarning)), "Legacy Standalone Mod Warning" },
                { m_Setting.GetOptionDescLocaleID(nameof(CityPulseSetting.EnableLegacyWarning)), "Displays a friendly notice when superseded standalone diagnostic mods are still active." },
                { m_Setting.GetOptionLabelLocaleID(nameof(CityPulseSetting.EnableFlowTracing)), "Enable Upstream/Downstream Flow Tracing" },
                { m_Setting.GetOptionDescLocaleID(nameof(CityPulseSetting.EnableFlowTracing)), "Traces traffic propagation lines upstream and downstream from severe bottlenecks." },
                { m_Setting.GetOptionLabelLocaleID(nameof(CityPulseSetting.EnableBunchingDetector)), "Transit Bunching Detector" },
                { m_Setting.GetOptionDescLocaleID(nameof(CityPulseSetting.EnableBunchingDetector)), "Monitors vehicle spacing along transit routes to identify vehicle bunching." },
                { m_Setting.GetOptionLabelLocaleID(nameof(CityPulseSetting.EnableOvercapacityAlerts)), "Parking Overcapacity Alerts" },
                { m_Setting.GetOptionDescLocaleID(nameof(CityPulseSetting.EnableOvercapacityAlerts)), "Highlights off-street parking facilities operating above 90% utilization." },
                { m_Setting.GetOptionLabelLocaleID(nameof(CityPulseSetting.EnableCriticalServiceAlerts)), "Critical Service Alerts" },
                { m_Setting.GetOptionDescLocaleID(nameof(CityPulseSetting.EnableCriticalServiceAlerts)), "Flags emergency and municipal service buildings suffering low operating efficiency." },
                { m_Setting.GetOptionLabelLocaleID(nameof(CityPulseSetting.AutoInspectSelectedBuilding)), "Auto-Inspect Selected Building" },
                { m_Setting.GetOptionDescLocaleID(nameof(CityPulseSetting.AutoInspectSelectedBuilding)), "Presents live workforce and efficiency diagnostics when selecting a building in-game." },
                { m_Setting.GetOptionLabelLocaleID(nameof(CityPulseSetting.EnableNetworkDefectScanner)), "Road Network Defect Scanner" },
                { m_Setting.GetOptionDescLocaleID(nameof(CityPulseSetting.EnableNetworkDefectScanner)), "Scans network geometry for sub-2m micro-segments and broken lane connections." }
            };
        }

        public void Unload() { }
    }
}

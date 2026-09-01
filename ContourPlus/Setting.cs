using System;
using System.Collections.Generic;
using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;
using Game.UI.Widgets;

namespace ContourPlus
{
    [FileLocation(nameof(ContourPlus))]
    [SettingsUIGroupOrder(kMainGroup, kContourGroup)]
    [SettingsUIShowGroupName(kMainGroup, kContourGroup)]
    public class ContourPlusSetting : ModSetting
    {
        public const string kSection = "Main";
        public const string kMainGroup = "General";
        public const string kContourGroup = "Contour & Elevation";

        public enum ContourInterval
        {
            OneMeter = 1,
            TwoMeters = 2,
            FiveMeters = 5,
            TenMeters = 10,
            TwentyMeters = 20,
            TwentyFiveMeters = 25,
            FiftyMeters = 50
        }

        public enum SlopeUnit
        {
            Percentage = 0,
            Degrees = 1
        }

        public ContourPlusSetting(IMod mod) : base(mod)
        {
            SetDefaults();
        }

        [SettingsUISection(kSection, kMainGroup)]
        public bool EnableElevationReadout { get; set; } = true;

        [SettingsUISection(kSection, kMainGroup)]
        public SlopeUnit Unit { get; set; } = SlopeUnit.Percentage;

        [SettingsUISection(kSection, kContourGroup)]
        public ContourInterval Interval { get; set; } = ContourInterval.FiveMeters;

        [SettingsUISection(kSection, kContourGroup)]
        public bool ContoursEnabled { get; set; }

        [SettingsUISection(kSection, kContourGroup)]
        public bool HighVisibility { get; set; }

        [SettingsUISection(kSection, kContourGroup)]
        public bool ShowRoadGradeInspector { get; set; } = true;

        public override void SetDefaults()
        {
            EnableElevationReadout = true;
            Unit = SlopeUnit.Percentage;
            Interval = ContourInterval.FiveMeters;
            ContoursEnabled = false;
            HighVisibility = false;
            ShowRoadGradeInspector = true;
        }
    }

    public class LocaleEN : IDictionarySource
    {
        private readonly ContourPlusSetting m_Setting;
        public LocaleEN(ContourPlusSetting setting) { m_Setting = setting; }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "Contour Plus" },
                { m_Setting.GetOptionTabLocaleID(ContourPlusSetting.kSection), "Main" },
                { m_Setting.GetOptionGroupLocaleID(ContourPlusSetting.kMainGroup), "General" },
                { m_Setting.GetOptionGroupLocaleID(ContourPlusSetting.kContourGroup), "Contour & Elevation" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ContourPlusSetting.EnableElevationReadout)), "Enable Elevation Readout" },
                { m_Setting.GetOptionDescLocaleID(nameof(ContourPlusSetting.EnableElevationReadout)), "Displays live cursor elevation and terrain slope." },
                { m_Setting.GetOptionLabelLocaleID(nameof(ContourPlusSetting.Unit)), "Slope Unit" },
                { m_Setting.GetOptionDescLocaleID(nameof(ContourPlusSetting.Unit)), "Format for slope readouts (percentage grade or degrees)." },
                { m_Setting.GetOptionLabelLocaleID(nameof(ContourPlusSetting.Interval)), "Contour Line Interval" },
                { m_Setting.GetOptionDescLocaleID(nameof(ContourPlusSetting.Interval)), "Elevation step between contour overlay lines." },
                { m_Setting.GetOptionLabelLocaleID(nameof(ContourPlusSetting.ContoursEnabled)), "Show Contour Lines" },
                { m_Setting.GetOptionDescLocaleID(nameof(ContourPlusSetting.ContoursEnabled)), "Shows terrain contour overlays. Disabled by default to avoid background rendering work." },
                { m_Setting.GetOptionLabelLocaleID(nameof(ContourPlusSetting.HighVisibility)), "High-Visibility Lines" },
                { m_Setting.GetOptionDescLocaleID(nameof(ContourPlusSetting.HighVisibility)), "Uses thicker, brighter contour lines." },
                { m_Setting.GetOptionLabelLocaleID(nameof(ContourPlusSetting.ShowRoadGradeInspector)), "Show Road Grade Inspector" },
                { m_Setting.GetOptionDescLocaleID(nameof(ContourPlusSetting.ShowRoadGradeInspector)), "Analyzes longitudinal slope profile and earthwork cut/fill for hovered roads." },
                { m_Setting.GetEnumValueLocaleID(ContourPlusSetting.SlopeUnit.Percentage), "Percentage (%)" },
                { m_Setting.GetEnumValueLocaleID(ContourPlusSetting.SlopeUnit.Degrees), "Degrees (°)" },
                { m_Setting.GetEnumValueLocaleID(ContourPlusSetting.ContourInterval.OneMeter), "1 Meter" },
                { m_Setting.GetEnumValueLocaleID(ContourPlusSetting.ContourInterval.TwoMeters), "2 Meters" },
                { m_Setting.GetEnumValueLocaleID(ContourPlusSetting.ContourInterval.FiveMeters), "5 Meters" },
                { m_Setting.GetEnumValueLocaleID(ContourPlusSetting.ContourInterval.TenMeters), "10 Meters" },
                { m_Setting.GetEnumValueLocaleID(ContourPlusSetting.ContourInterval.TwentyMeters), "20 Meters" },
                { m_Setting.GetEnumValueLocaleID(ContourPlusSetting.ContourInterval.TwentyFiveMeters), "25 Meters" },
                { m_Setting.GetEnumValueLocaleID(ContourPlusSetting.ContourInterval.FiftyMeters), "50 Meters" }
            };
        }

        public void Unload() { }
    }
}

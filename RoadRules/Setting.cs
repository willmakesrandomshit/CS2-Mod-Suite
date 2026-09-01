using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;

namespace RoadRules
{
    [FileLocation(nameof(RoadRules))]
    [SettingsUIGroupOrder(kMainGroup)]
    [SettingsUIShowGroupName(kMainGroup)]
    public class RoadRulesSetting : ModSetting
    {
        public const string kSection = "Main";
        public const string kMainGroup = "Options";

        public RoadRulesSetting(IMod mod) : base(mod)
        {
            SetDefaults();
        }

        [SettingsUISection(kSection, kMainGroup)]
        public bool ShowOverlays { get; set; } = true;

        // Retained only so older settings files deserialize safely. CS2 does not expose an
        // emergency-only override for a hard-closed lane, so this must not be presented as real.
        [SettingsUIHidden]
        public bool AllowEmergencyOverride { get; set; } = true;

        public override void SetDefaults()
        {
            ShowOverlays = true;
            AllowEmergencyOverride = true;
        }
    }
}

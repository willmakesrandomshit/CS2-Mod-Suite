using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;

namespace EventEngine
{
    [FileLocation("ModsSettings/" + nameof(EventEngine) + "/" + nameof(EventEngine))]
    [SettingsUIGroupOrder(GRP_GENERAL)]
    [SettingsUIShowGroupName(GRP_GENERAL)]
    public class EventEngineSetting : ModSetting
    {
        public const string GRP_GENERAL = "General";

        public EventEngineSetting(IMod mod) : base(mod)
        {
        }

        [SettingsUISection(GRP_GENERAL)]
        public bool EnableEventSimulation { get; set; } = true;

        [SettingsUISection(GRP_GENERAL)]
        public int MaxEventAttendance { get; set; } = 15000;

        public override void SetDefaults()
        {
            EnableEventSimulation = true;
            MaxEventAttendance = 15000;
        }
    }
}

using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;

namespace NetworkStudio
{
    [FileLocation("ModsSettings/" + nameof(NetworkStudio) + "/" + nameof(NetworkStudio))]
    [SettingsUIGroupOrder(GRP_GENERAL)]
    [SettingsUIShowGroupName(GRP_GENERAL)]
    public class NetworkStudioSetting : ModSetting
    {
        public const string GRP_GENERAL = "General";

        public NetworkStudioSetting(IMod mod) : base(mod)
        {
        }

        [SettingsUIHidden]
        public bool EnableFurnitureEditing { get; set; } = true;

        [SettingsUIHidden]
        public bool AutoVergeAdjustment { get; set; } = true;

        [SettingsUIHidden]
        public bool DebugOverlay { get; set; } = false;

        public override void SetDefaults()
        {
            EnableFurnitureEditing = true;
            AutoVergeAdjustment = true;
            DebugOverlay = false;
        }
    }
}

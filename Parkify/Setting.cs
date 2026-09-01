using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;

namespace Parkify
{
    [FileLocation("ModsSettings/" + nameof(Parkify) + "/" + nameof(Parkify))]
    [SettingsUIGroupOrder(GRP_GENERAL)]
    [SettingsUIShowGroupName(GRP_GENERAL)]
    public class ParkifySetting : ModSetting
    {
        public const string GRP_GENERAL = "General";

        public ParkifySetting(IMod mod) : base(mod)
        {
        }

        [SettingsUIHidden]
        public bool EnablePedestrianParkAccess { get; set; } = true;

        [SettingsUIHidden]
        public bool SuppressNoRoadWarningOnPaths { get; set; } = true;

        public override void SetDefaults()
        {
            EnablePedestrianParkAccess = true;
            SuppressNoRoadWarningOnPaths = true;
        }
    }
}

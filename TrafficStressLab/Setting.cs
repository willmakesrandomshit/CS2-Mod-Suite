using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using System.Collections.Generic;

namespace TrafficStressTester
{
    [FileLocation(nameof(TrafficStressTester))]
    [SettingsUIGroupOrder(kInterface, kSafety)]
    [SettingsUIShowGroupName(kInterface, kSafety)]
    public sealed class TrafficStressSetting : ModSetting
    {
        public const string kMain = "Main", kInterface = "Interface", kSafety = "Safety";
        public TrafficStressSetting(IMod mod) : base(mod) => SetDefaults();
        [SettingsUISection(kMain, kInterface)] public bool ShowToolbarButton { get; set; }
        [SettingsUISection(kMain, kSafety)] public bool ResetWhenLeavingCity { get; set; }
        [SettingsUISection(kMain, kSafety)] public RampSpeed TrafficRampSpeed { get; set; }
        [SettingsUISlider(min = 24, max = 192, step = 24)]
        [SettingsUISection(kMain, kSafety)] public int PerFrameRequestLimit { get; set; }
        [SettingsUIButton]
        [SettingsUISection(kMain, kSafety)] public bool StopAndReset { set { TrafficStressState.Reset(); } }
        public override void SetDefaults() { ShowToolbarButton = true; ResetWhenLeavingCity = true; TrafficRampSpeed = RampSpeed.Fast; PerFrameRequestLimit = 96; }
    }
    public enum RampSpeed { Gentle, Fast, Extreme }
    public sealed class LocaleEN : IDictionarySource
    {
        private readonly TrafficStressSetting s; public LocaleEN(TrafficStressSetting setting) => s = setting;
        public IEnumerable<KeyValuePair<string,string>> ReadEntries(IList<IDictionaryEntryError> e, Dictionary<string,int> c) => new Dictionary<string,string>
        {
            {s.GetSettingsLocaleID(),"Traffic Stress Lab"},{s.GetOptionTabLocaleID(TrafficStressSetting.kMain),"Main"},{s.GetOptionGroupLocaleID(TrafficStressSetting.kInterface),"Interface"},{s.GetOptionGroupLocaleID(TrafficStressSetting.kSafety),"Safety"},
            {s.GetOptionLabelLocaleID(nameof(TrafficStressSetting.ShowToolbarButton)),"Show TST toolbar button"},{s.GetOptionDescLocaleID(nameof(TrafficStressSetting.ShowToolbarButton)),"Shows the TST button in the top toolbar."},
            {s.GetOptionLabelLocaleID(nameof(TrafficStressSetting.ResetWhenLeavingCity)),"Reset when leaving city"},{s.GetOptionDescLocaleID(nameof(TrafficStressSetting.ResetWhenLeavingCity)),"Guarantees each city starts with vanilla traffic generation."},
            {s.GetOptionLabelLocaleID(nameof(TrafficStressSetting.TrafficRampSpeed)),"Traffic ramp speed"},{s.GetOptionDescLocaleID(nameof(TrafficStressSetting.TrafficRampSpeed)),"Controls how quickly sustained extra trips enter the network. The selected multiplier remains unchanged."},
            {s.GetOptionLabelLocaleID(nameof(TrafficStressSetting.PerFrameRequestLimit)),"Safety request limit"},{s.GetOptionDescLocaleID(nameof(TrafficStressSetting.PerFrameRequestLimit)),"Caps additional requests in a single frame to avoid extreme spikes on heavily modded cities."},
            {s.GetOptionLabelLocaleID(nameof(TrafficStressSetting.StopAndReset)),"Stop / Reset now"},{s.GetOptionDescLocaleID(nameof(TrafficStressSetting.StopAndReset)),"Stops generation immediately. Existing vehicles finish their normal trips."},
            {s.GetEnumValueLocaleID(RampSpeed.Gentle),"Gentle"},{s.GetEnumValueLocaleID(RampSpeed.Fast),"Fast"},{s.GetEnumValueLocaleID(RampSpeed.Extreme),"Extreme"}
        }; public void Unload(){}
    }
}

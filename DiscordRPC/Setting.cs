using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using System.Collections.Generic;

namespace DiscordRPC
{
    public enum PopulationPrivacy { Hidden, Rounded, Exact }
    public enum MoneyPrivacy { Hidden, Rounded, Exact }

    [FileLocation(nameof(DiscordRPC))]
    [SettingsUIGroupOrder(kConnection, kPrivacy, kContent, kTemplates, kInterface)]
    [SettingsUIShowGroupName(kConnection, kPrivacy, kContent, kTemplates, kInterface)]
    public sealed class DiscordRPCSetting : ModSetting
    {
        public const string kMain = "Main";
        public const string kConnection = "Connection";
        public const string kPrivacy = "Privacy";
        public const string kContent = "Presence Content";
        public const string kTemplates = "Templates";
        public const string kInterface = "Interface";

        public DiscordRPCSetting(IMod mod) : base(mod) => SetDefaults();

        [SettingsUISection(kMain, kConnection)] public bool Enabled { get; set; }
        [SettingsUISlider(min = 15, max = 60, step = 5)]
        [SettingsUISection(kMain, kConnection)] public int UpdateIntervalSeconds { get; set; }

        [SettingsUISection(kMain, kPrivacy)] public bool ShareCityName { get; set; }
        [SettingsUISection(kMain, kPrivacy)] public PopulationPrivacy Population { get; set; }
        [SettingsUISection(kMain, kPrivacy)] public MoneyPrivacy Money { get; set; }

        [SettingsUISection(kMain, kContent)] public bool ShowActivity { get; set; }
        [SettingsUISection(kMain, kContent)] public bool ShowDate { get; set; }
        [SettingsUISection(kMain, kContent)] public bool ShowSeason { get; set; }
        [SettingsUISection(kMain, kContent)] public bool ShowWeather { get; set; }
        [SettingsUISection(kMain, kContent)] public bool ShowTrafficFlow { get; set; }
        [SettingsUISection(kMain, kContent)] public bool ShowSessionTimer { get; set; }
        [SettingsUISection(kMain, kContent)] public bool DetectSuiteActivity { get; set; }

        [SettingsUITextInput]
        [SettingsUISection(kMain, kTemplates)] public string DetailsTemplate { get; set; }
        [SettingsUITextInput]
        [SettingsUISection(kMain, kTemplates)] public string StateTemplate { get; set; }

        [SettingsUISection(kMain, kInterface)] public bool ShowToolbarButton { get; set; }
        [SettingsUISection(kMain, kInterface)] public bool OpenPanelOnConnectionFailure { get; set; }

        [SettingsUIButton]
        [SettingsUISection(kMain, kConnection)]
        public bool RefreshNow { set => DiscordRPCRuntime.RequestImmediateUpdate(); }

        [SettingsUIButton]
        [SettingsUISection(kMain, kTemplates)]
        public bool RestoreRecommendedTemplates
        {
            set
            {
                DetailsTemplate = "{activity}";
                StateTemplate = "{population_line} · {date}";
                ApplyAndSave();
                DiscordRPCRuntime.RequestImmediateUpdate();
            }
        }

        public override void SetDefaults()
        {
            Enabled = true;
            UpdateIntervalSeconds = 15;
            ShareCityName = false;
            Population = PopulationPrivacy.Rounded;
            Money = MoneyPrivacy.Hidden;
            ShowActivity = true;
            ShowDate = true;
            ShowSeason = true;
            ShowWeather = true;
            ShowTrafficFlow = false;
            ShowSessionTimer = true;
            DetectSuiteActivity = true;
            DetailsTemplate = "{activity}";
            StateTemplate = "{population_line} · {date}";
            ShowToolbarButton = true;
            OpenPanelOnConnectionFailure = false;
        }
    }

    public sealed class LocaleEN : IDictionarySource
    {
        private readonly DiscordRPCSetting s;
        public LocaleEN(DiscordRPCSetting setting) => s = setting;

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> counts) => new Dictionary<string, string>
        {
            { s.GetSettingsLocaleID(), "Discord RPC" },
            { s.GetOptionTabLocaleID(DiscordRPCSetting.kMain), "Main" },
            { s.GetOptionGroupLocaleID(DiscordRPCSetting.kConnection), "Connection" },
            { s.GetOptionGroupLocaleID(DiscordRPCSetting.kPrivacy), "Privacy" },
            { s.GetOptionGroupLocaleID(DiscordRPCSetting.kContent), "Presence Content" },
            { s.GetOptionGroupLocaleID(DiscordRPCSetting.kTemplates), "Templates" },
            { s.GetOptionGroupLocaleID(DiscordRPCSetting.kInterface), "Interface" },
            { s.GetOptionLabelLocaleID(nameof(DiscordRPCSetting.Enabled)), "Enable enhanced Discord presence" },
            { s.GetOptionDescLocaleID(nameof(DiscordRPCSetting.Enabled)), "Uses the Discord Game SDK connection already owned by Cities: Skylines II. Disable to restore the vanilla updater." },
            { s.GetOptionLabelLocaleID(nameof(DiscordRPCSetting.UpdateIntervalSeconds)), "Update interval" },
            { s.GetOptionDescLocaleID(nameof(DiscordRPCSetting.UpdateIntervalSeconds)), "Seconds between Discord updates. The minimum is deliberately conservative." },
            { s.GetOptionLabelLocaleID(nameof(DiscordRPCSetting.ShareCityName)), "Share city name" },
            { s.GetOptionDescLocaleID(nameof(DiscordRPCSetting.ShareCityName)), "Off by default. No save paths or local account details are ever sent." },
            { s.GetOptionLabelLocaleID(nameof(DiscordRPCSetting.Population)), "Population precision" },
            { s.GetOptionDescLocaleID(nameof(DiscordRPCSetting.Population)), "Hide population, round it for privacy, or share the exact value." },
            { s.GetOptionLabelLocaleID(nameof(DiscordRPCSetting.Money)), "Treasury precision" },
            { s.GetOptionDescLocaleID(nameof(DiscordRPCSetting.Money)), "Treasury is hidden by default and is never required for presence." },
            { s.GetOptionLabelLocaleID(nameof(DiscordRPCSetting.ShowActivity)), "Show current activity" },
            { s.GetOptionLabelLocaleID(nameof(DiscordRPCSetting.ShowDate)), "Show in-game date" },
            { s.GetOptionLabelLocaleID(nameof(DiscordRPCSetting.ShowSeason)), "Show season" },
            { s.GetOptionLabelLocaleID(nameof(DiscordRPCSetting.ShowWeather)), "Show weather and temperature" },
            { s.GetOptionLabelLocaleID(nameof(DiscordRPCSetting.ShowTrafficFlow)), "Show traffic flow" },
            { s.GetOptionDescLocaleID(nameof(DiscordRPCSetting.ShowTrafficFlow)), "Samples aggregate road flow at a low frequency. Disabled by default." },
            { s.GetOptionLabelLocaleID(nameof(DiscordRPCSetting.ShowSessionTimer)), "Show session timer" },
            { s.GetOptionLabelLocaleID(nameof(DiscordRPCSetting.DetectSuiteActivity)), "Detect companion mod activity" },
            { s.GetOptionDescLocaleID(nameof(DiscordRPCSetting.DetectSuiteActivity)), "Shows active tools from Access Studio, Road Rules, Event Engine and Traffic Stress Lab when detectable." },
            { s.GetOptionLabelLocaleID(nameof(DiscordRPCSetting.DetailsTemplate)), "Details template" },
            { s.GetOptionDescLocaleID(nameof(DiscordRPCSetting.DetailsTemplate)), "Tokens: {activity}, {city}, {mode}, {population}, {money}, {date}, {season}, {weather}, {temperature}, {traffic}." },
            { s.GetOptionLabelLocaleID(nameof(DiscordRPCSetting.StateTemplate)), "State template" },
            { s.GetOptionDescLocaleID(nameof(DiscordRPCSetting.StateTemplate)), "Use the same tokens plus {population_line}, {money_line}, {weather_line} and {traffic_line}." },
            { s.GetOptionLabelLocaleID(nameof(DiscordRPCSetting.ShowToolbarButton)), "Show toolbar button" },
            { s.GetOptionLabelLocaleID(nameof(DiscordRPCSetting.OpenPanelOnConnectionFailure)), "Open panel if Discord disconnects" },
            { s.GetOptionLabelLocaleID(nameof(DiscordRPCSetting.RefreshNow)), "Refresh Discord now" },
            { s.GetOptionLabelLocaleID(nameof(DiscordRPCSetting.RestoreRecommendedTemplates)), "Restore recommended templates" },
            { s.GetEnumValueLocaleID(PopulationPrivacy.Hidden), "Hidden" },
            { s.GetEnumValueLocaleID(PopulationPrivacy.Rounded), "Rounded" },
            { s.GetEnumValueLocaleID(PopulationPrivacy.Exact), "Exact" },
            { s.GetEnumValueLocaleID(MoneyPrivacy.Hidden), "Hidden" },
            { s.GetEnumValueLocaleID(MoneyPrivacy.Rounded), "Rounded" },
            { s.GetEnumValueLocaleID(MoneyPrivacy.Exact), "Exact" }
        };

        public void Unload() { }
    }
}

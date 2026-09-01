using System;
using Colossal.Serialization.Entities;
using Colossal.UI.Binding;
using Game;
using Game.UI;

namespace DiscordRPC
{
    public sealed partial class DiscordRPCUISystem : UISystemBase
    {
        private ValueBinding<bool> m_Open;
        private ValueBinding<bool> m_ShowButton;
        private ValueBinding<bool> m_Enabled;
        private ValueBinding<string> m_Status;
        private ValueBinding<string> m_Message;
        private ValueBinding<string> m_Details;
        private ValueBinding<string> m_State;
        private ValueBinding<string> m_LastUpdate;
        private ValueBinding<string> m_Source;
        private ValueBinding<string> m_Mode;
        private ValueBinding<string> m_Privacy;
        private ValueBinding<int> m_Interval;
        private ValueBinding<int> m_Successes;
        private ValueBinding<int> m_Failures;
        private DateTime m_LastUiUpdate;

        protected override void OnCreate()
        {
            base.OnCreate();
            AddBinding(m_Open = new ValueBinding<bool>("discordRPC", "open", false));
            AddBinding(m_ShowButton = new ValueBinding<bool>("discordRPC", "showButton", true));
            AddBinding(m_Enabled = new ValueBinding<bool>("discordRPC", "enabled", true));
            AddBinding(m_Status = new ValueBinding<string>("discordRPC", "status", "Waiting"));
            AddBinding(m_Message = new ValueBinding<string>("discordRPC", "message", "Waiting for Discord"));
            AddBinding(m_Details = new ValueBinding<string>("discordRPC", "details", "In the Main Menu"));
            AddBinding(m_State = new ValueBinding<string>("discordRPC", "state", "Ready to build"));
            AddBinding(m_LastUpdate = new ValueBinding<string>("discordRPC", "lastUpdate", "Not sent yet"));
            AddBinding(m_Source = new ValueBinding<string>("discordRPC", "source", "Cities: Skylines II"));
            AddBinding(m_Mode = new ValueBinding<string>("discordRPC", "mode", "Main Menu"));
            AddBinding(m_Privacy = new ValueBinding<string>("discordRPC", "privacy", "City hidden · Population rounded · Treasury hidden"));
            AddBinding(m_Interval = new ValueBinding<int>("discordRPC", "interval", 15));
            // Colossal's UI serializer has no writer for Int64. An exception here is
            // particularly dangerous: UISystemBase has already subscribed its preload
            // callback, leaving a destroyed system in the delegate list.
            AddBinding(m_Successes = new ValueBinding<int>("discordRPC", "successes", 0));
            AddBinding(m_Failures = new ValueBinding<int>("discordRPC", "failures", 0));

            AddBinding(new TriggerBinding("discordRPC", "toggle", () => m_Open.Update(!m_Open.value)));
            AddBinding(new TriggerBinding("discordRPC", "close", () => m_Open.Update(false)));
            AddBinding(new TriggerBinding("discordRPC", "refresh", DiscordRPCRuntime.RequestImmediateUpdate));
            AddBinding(new TriggerBinding<bool>("discordRPC", "setEnabled", SetEnabled));
            AddBinding(new TriggerBinding<string>("discordRPC", "privacyPreset", SetPrivacyPreset));
            AddBinding(new TriggerBinding<string>("discordRPC", "templatePreset", SetTemplatePreset));
            Mod.Log.Info($"DRPC_LIFECYCLE event=created system={nameof(DiscordRPCUISystem)} world={DescribeWorld()}");
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            Mod.Log.Info($"DRPC_LIFECYCLE event=preload system={nameof(DiscordRPCUISystem)} world={DescribeWorld()} purpose={purpose} mode={mode}");
            base.OnGamePreload(purpose, mode);
            m_Open?.Update(false);
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            Mod.Log.Info($"DRPC_LIFECYCLE event=loading_complete system={nameof(DiscordRPCUISystem)} world={DescribeWorld()} purpose={purpose} mode={mode}");
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            if ((DateTime.UtcNow - m_LastUiUpdate).TotalSeconds < 0.5) return;
            m_LastUiUpdate = DateTime.UtcNow;
            DiscordRPCSetting s = Mod.Settings;
            if (s == null) return;

            m_ShowButton.Update(s.ShowToolbarButton);
            m_Enabled.Update(s.Enabled);
            m_Status.Update(DiscordRPCSystem.ConnectionStatus);
            m_Message.Update(DiscordRPCSystem.ConnectionMessage);
            m_Details.Update(DiscordRPCSystem.DetailsPreview);
            m_State.Update(DiscordRPCSystem.StatePreview);
            m_LastUpdate.Update(DiscordRPCSystem.LastUpdateText);
            m_Source.Update(DiscordRPCSystem.ActivitySource);
            m_Mode.Update(DiscordRPCSystem.CurrentMode);
            m_Interval.Update(Math.Max(15, s.UpdateIntervalSeconds));
            m_Successes.Update(ToBindingCount(DiscordRPCSystem.SuccessfulCallbacks));
            m_Failures.Update(ToBindingCount(DiscordRPCSystem.FailedCallbacks));
            m_Privacy.Update((s.ShareCityName ? "City shared" : "City hidden") + " · Population " + s.Population.ToString().ToLowerInvariant() + " · Treasury " + s.Money.ToString().ToLowerInvariant());

            if (s.OpenPanelOnConnectionFailure && s.Enabled && DiscordRPCSystem.ConnectionStatus == "Not connected" && !m_Open.value)
                m_Open.Update(true);
        }

        private static void SetEnabled(bool enabled)
        {
            if (Mod.Settings == null) return;
            Mod.Settings.Enabled = enabled;
            Mod.Settings.ApplyAndSave();
            DiscordRPCRuntime.RequestImmediateUpdate();
        }

        private static void SetPrivacyPreset(string preset)
        {
            if (Mod.Settings == null) return;
            switch ((preset ?? string.Empty).ToLowerInvariant())
            {
                case "private":
                    Mod.Settings.ShareCityName = false;
                    Mod.Settings.Population = PopulationPrivacy.Hidden;
                    Mod.Settings.Money = MoneyPrivacy.Hidden;
                    break;
                case "social":
                    Mod.Settings.ShareCityName = true;
                    Mod.Settings.Population = PopulationPrivacy.Rounded;
                    Mod.Settings.Money = MoneyPrivacy.Hidden;
                    break;
                case "detailed":
                    Mod.Settings.ShareCityName = true;
                    Mod.Settings.Population = PopulationPrivacy.Exact;
                    Mod.Settings.Money = MoneyPrivacy.Rounded;
                    break;
                default: return;
            }
            Mod.Settings.ApplyAndSave();
            DiscordRPCRuntime.RequestImmediateUpdate();
        }

        private static void SetTemplatePreset(string preset)
        {
            if (Mod.Settings == null) return;
            switch ((preset ?? string.Empty).ToLowerInvariant())
            {
                case "minimal":
                    Mod.Settings.DetailsTemplate = "{activity}";
                    Mod.Settings.StateTemplate = "{mode}";
                    break;
                case "city":
                    Mod.Settings.DetailsTemplate = "{activity}";
                    Mod.Settings.StateTemplate = "{population_line} · {date}";
                    break;
                case "weather":
                    Mod.Settings.DetailsTemplate = "{activity}";
                    Mod.Settings.StateTemplate = "{season} · {weather_line} · {population_line}";
                    break;
                default: return;
            }
            Mod.Settings.ApplyAndSave();
            DiscordRPCRuntime.RequestImmediateUpdate();
        }

        protected override void OnDestroy()
        {
            Mod.Log.Info($"DRPC_LIFECYCLE event=destroying system={nameof(DiscordRPCUISystem)} world={DescribeWorld()}");
            base.OnDestroy();
        }

        private static int ToBindingCount(long value)
        {
            if (value <= 0L) return 0;
            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }

        private string DescribeWorld()
        {
            return World == null ? "<null>" : $"{World.Name}#{World.GetHashCode():X8}:created={World.IsCreated}";
        }
    }
}

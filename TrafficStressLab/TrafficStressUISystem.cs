using Colossal.UI.Binding;
using Game.UI;

namespace TrafficStressTester
{
    public sealed partial class TrafficStressUISystem : UISystemBase
    {
        private const string Group = "trafficStressTester";
        private ValueBinding<int> m_MultiplierBinding;
        private ValueBinding<bool> m_EnabledBinding;
        private ValueBinding<bool> m_PanelOpenBinding;
        private ValueBinding<bool> m_ShowButtonBinding;
        private ValueBinding<int> m_GeneratedBinding;
        private ValueBinding<string> m_RampBinding;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_MultiplierBinding = new ValueBinding<int>(
                Group, "multiplier", 1, ValueWriters.Create<int>());
            m_EnabledBinding = new ValueBinding<bool>(
                Group, "enabled", false, ValueWriters.Create<bool>());
            m_PanelOpenBinding = new ValueBinding<bool>(
                Group, "panelOpen", false, ValueWriters.Create<bool>());
            m_ShowButtonBinding = new ValueBinding<bool>(Group, "showButton", true, ValueWriters.Create<bool>());
            m_GeneratedBinding = new ValueBinding<int>(Group, "generated", 0, ValueWriters.Create<int>());
            m_RampBinding = new ValueBinding<string>(Group, "ramp", "Fast", ValueWriters.Create<string>());

            AddBinding(m_MultiplierBinding);
            AddBinding(m_EnabledBinding);
            AddBinding(m_PanelOpenBinding);
            AddBinding(m_ShowButtonBinding);
            AddBinding(m_GeneratedBinding);
            AddBinding(m_RampBinding);
            AddBinding(new TriggerBinding<int>(
                Group, "setMultiplier", SetMultiplier, ValueReaders.Create<int>()));
            AddBinding(new TriggerBinding(Group, "reset", Reset));
            AddBinding(new TriggerBinding(Group, "togglePanel", TogglePanel));
            AddBinding(new TriggerBinding(Group, "closePanel", ClosePanel));
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            PublishState();
            m_ShowButtonBinding.Update(Mod.Settings == null || Mod.Settings.ShowToolbarButton);
            m_GeneratedBinding.Update(TrafficStressState.RequestsGenerated);
            m_RampBinding.Update(Mod.Settings == null ? "Fast" : Mod.Settings.TrafficRampSpeed.ToString());
        }

        private void SetMultiplier(int multiplier)
        {
            TrafficStressState.SetMultiplier(multiplier);
            PublishState();
        }

        private void Reset()
        {
            TrafficStressState.Reset();
            PublishState();
        }

        private void TogglePanel() => m_PanelOpenBinding.Update(!m_PanelOpenBinding.value);
        private void ClosePanel() => m_PanelOpenBinding.Update(false);

        private void PublishState()
        {
            m_MultiplierBinding.Update(TrafficStressState.Multiplier);
            m_EnabledBinding.Update(TrafficStressState.Enabled);
        }
    }
}

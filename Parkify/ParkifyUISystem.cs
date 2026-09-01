using System;
using Colossal.Logging;
using Colossal.UI.Binding;
using Game.UI;
using Unity.Entities;

namespace Parkify
{
    public partial class ParkifyUISystem : UISystemBase
    {
        private static new readonly ILog log = Mod.log;

        private ValueBinding<bool> m_IsOpenBinding;
        private ValueBinding<int> m_AccessibleParksBinding;
        private ValueBinding<int> m_PedestrianPathsBinding;
        private ValueBinding<bool> m_AutoBridgeBinding;

        private ParkifySystem m_ParkifySystem;
        private bool m_IsOpen = false;

        protected override void OnCreate()
        {
            base.OnCreate();
            log.Info("[ParkifyUISystem] OnCreate");

            m_ParkifySystem = World.GetOrCreateSystemManaged<ParkifySystem>();

            AddBinding(m_IsOpenBinding = new ValueBinding<bool>("Parkify", "isOpen", false));
            AddBinding(m_AccessibleParksBinding = new ValueBinding<int>("Parkify", "accessibleParks", 0));
            AddBinding(m_PedestrianPathsBinding = new ValueBinding<int>("Parkify", "pedestrianPaths", 0));
            AddBinding(m_AutoBridgeBinding = new ValueBinding<bool>("Parkify", "autoBridge", false));

            AddBinding(new TriggerBinding<bool>("Parkify", "setOpen", OnSetOpen));
            AddBinding(new TriggerBinding<bool>("Parkify", "toggleAutoBridge", OnToggleAutoBridge));
        }

        private void OnSetOpen(bool open)
        {
            m_IsOpen = open;
            m_IsOpenBinding?.Update(m_IsOpen);
        }

        private void OnToggleAutoBridge(bool enabled)
        {
            if (m_ParkifySystem != null)
            {
                m_ParkifySystem.AutoBridgeEnabled = enabled;
            }
            m_AutoBridgeBinding?.Update(enabled);
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            if (!m_IsOpen || m_ParkifySystem == null) return;

            m_AccessibleParksBinding?.Update(m_ParkifySystem.AccessibleParkCount);
            m_PedestrianPathsBinding?.Update(m_ParkifySystem.PedestrianPathCount);
            m_AutoBridgeBinding?.Update(m_ParkifySystem.AutoBridgeEnabled);
        }
    }
}

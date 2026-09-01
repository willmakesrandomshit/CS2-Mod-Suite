using Colossal.Logging;
using Game;
using Game.Common;
using Game.Net;
using Unity.Entities;

namespace Parkify
{
    public partial class ParkifySystem : GameSystemBase
    {
        private static ILog log = Mod.log;

        public int AccessibleParkCount { get; private set; } = 0;
        public int PedestrianPathCount { get; private set; } = 0;
        public bool AutoBridgeEnabled { get; set; } = false;

        private EntityQuery m_ParkQuery;
        private EntityQuery m_PedestrianPathQuery;

        protected override void OnCreate()
        {
            base.OnCreate();
            log.Info("[ParkifySystem] OnCreate");

            m_ParkQuery = GetEntityQuery(
                ComponentType.ReadOnly<Game.Buildings.Park>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Game.Tools.Temp>()
            );

            m_PedestrianPathQuery = GetEntityQuery(
                ComponentType.ReadOnly<Game.Net.PedestrianLane>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Game.Tools.Temp>()
            );
        }

        protected override void OnUpdate()
        {
            AccessibleParkCount = m_ParkQuery.CalculateEntityCount();
            PedestrianPathCount = m_PedestrianPathQuery.CalculateEntityCount();
        }
    }
}

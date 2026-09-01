using Colossal.Serialization.Entities;
using Game;

namespace CrashLens
{
    public sealed partial class HealthMonitorSystem : GameSystemBase
    {
        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            if (World == null || !World.IsCreated) return;
            base.OnGamePreload(purpose, mode);
        }

        protected override void OnUpdate()
        {
            if (World == null || !World.IsCreated) return;
            CrashLensRuntime.Update();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
        }
    }
}

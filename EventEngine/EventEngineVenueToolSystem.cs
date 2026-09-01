using Game.Buildings;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Unity.Entities;
using Unity.Jobs;

namespace EventEngine
{
    /// <summary>
    /// Temporary map tool used only while choosing an Event Engine venue.
    /// Every terminal path reports back to the UI system so the panel cannot be stranded hidden.
    /// </summary>
    public sealed partial class EventEngineVenueToolSystem : ToolBaseSystem
    {
        private DefaultToolSystem m_DefaultTool;
        private EventEngineSystem m_EventSystem;
        private EventEngineUISystem m_UISystem;

        public override string toolID => "EventEngineVenuePicker";
        public bool IsActive => m_ToolSystem != null && m_ToolSystem.activeTool == this;

        public override PrefabBase GetPrefab() => null;
        public override bool TrySetPrefab(PrefabBase prefab) => false;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_DefaultTool = World.GetOrCreateSystemManaged<DefaultToolSystem>();
            m_EventSystem = World.GetOrCreateSystemManaged<EventEngineSystem>();
            m_UISystem = World.GetOrCreateSystemManaged<EventEngineUISystem>();
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            applyAction.shouldBeEnabled = true;
            secondaryApplyAction.shouldBeEnabled = true;
            cancelAction.shouldBeEnabled = true;
            Mod.log.Info("[EventEngine] Venue picker tool activated.");
        }

        protected override void OnStopRunning()
        {
            applyAction.shouldBeEnabled = false;
            secondaryApplyAction.shouldBeEnabled = false;
            cancelAction.shouldBeEnabled = false;
            base.OnStopRunning();
        }

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();
            if (m_ToolRaycastSystem == null) return;
            m_ToolRaycastSystem.typeMask = TypeMask.StaticObjects;
            m_ToolRaycastSystem.raycastFlags = RaycastFlags.BuildingLots | RaycastFlags.SubElements;
            m_ToolRaycastSystem.collisionMask = CollisionMask.OnGround | CollisionMask.Overground;
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (applyAction.WasPressedThisFrame())
            {
                if (GetRaycastResult(out Entity hitEntity, out _))
                {
                    Entity building = ResolveBuilding(hitEntity);
                    if (building != Entity.Null && m_EventSystem.SelectVenueByEntity(building))
                    {
                        m_UISystem?.CompleteVenuePicking();
                    }
                    else
                    {
                        m_EventSystem.ShowToast("That building is not a compatible event venue");
                    }
                }
                else
                {
                    m_EventSystem.ShowToast("No venue found under the cursor");
                }
            }

            if (secondaryApplyAction.WasPressedThisFrame() || cancelAction.WasPressedThisFrame())
            {
                m_UISystem?.CancelVenuePicking();
            }

            return inputDeps;
        }

        public void Activate()
        {
            InitializeRaycast();
            if (m_ToolSystem != null) m_ToolSystem.activeTool = this;
        }

        public void ExitToDefaultTool()
        {
            if (m_ToolSystem != null && m_DefaultTool != null && m_ToolSystem.activeTool == this)
                m_ToolSystem.activeTool = m_DefaultTool;
        }

        private Entity ResolveBuilding(Entity entity)
        {
            Entity current = entity;
            for (int depth = 0; depth < 12; depth++)
            {
                if (current == Entity.Null || !EntityManager.Exists(current)) return Entity.Null;
                if (EntityManager.HasComponent<Temp>(current))
                {
                    Entity original = EntityManager.GetComponentData<Temp>(current).m_Original;
                    if (original == Entity.Null || original == current) return Entity.Null;
                    current = original;
                    continue;
                }
                if (EntityManager.HasComponent<Building>(current) && !EntityManager.HasComponent<Deleted>(current))
                    return current;
                if (!EntityManager.HasComponent<Owner>(current)) return Entity.Null;
                Entity owner = EntityManager.GetComponentData<Owner>(current).m_Owner;
                if (owner == Entity.Null || owner == current) return Entity.Null;
                current = owner;
            }
            return Entity.Null;
        }
    }
}

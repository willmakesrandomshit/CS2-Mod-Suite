using System;
using Colossal.Logging;
using Game;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace RoadRules
{
    public enum RoadRulesToolState
    {
        SelectRoad,
        EditRules
    }

    /// <summary>
    /// Dedicated ToolBaseSystem for Road Rules.
    /// Captures input, performs road network raycasts, resolves child lanes/sublanes
    /// to their owning road Edge, and suppresses vanilla road info panel takeovers.
    /// Fully instrumented with runtime diagnostics.
    /// </summary>
    public partial class RoadRulesToolSystem : ToolBaseSystem
    {
        public static readonly ILog log = Mod.log;
        public static RoadRulesToolSystem Instance { get; private set; }

        public override string toolID => "RoadRulesTool";

        private DefaultToolSystem m_DefaultTool;
        private RoadRulesSystem m_RulesSystem;
        private RoadRulesUISystem m_UISystem;

        public RoadRulesToolState State { get; private set; } = RoadRulesToolState.SelectRoad;
        public Entity HoveredEdge { get; private set; } = Entity.Null;
        public float3 CursorWorldPos { get; private set; } = float3.zero;

        // Telemetry diagnostics
        public int UpdateTickCount { get; private set; } = 0;
        public bool LastClickDetected { get; private set; } = false;
        public bool LastRaycastHit { get; private set; } = false;

        public override PrefabBase GetPrefab() => null;
        public override bool TrySetPrefab(PrefabBase prefab) => false;

        protected override void OnCreate()
        {
            base.OnCreate();
            Instance = this;
            m_DefaultTool = World.GetOrCreateSystemManaged<DefaultToolSystem>();
            m_RulesSystem = World.GetOrCreateSystemManaged<RoadRulesSystem>();
            m_UISystem = World.GetOrCreateSystemManaged<RoadRulesUISystem>();

            log.Info("[RoadRules] RoadRulesToolSystem created and registered with ToolSystem.");
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();

            applyAction.shouldBeEnabled = true;
            secondaryApplyAction.shouldBeEnabled = true;
            cancelAction.shouldBeEnabled = true;

            if (m_RulesSystem?.SelectedEdge != Entity.Null && EntityManager.Exists(m_RulesSystem.SelectedEdge))
            {
                State = RoadRulesToolState.EditRules;
            }
            else
            {
                State = RoadRulesToolState.SelectRoad;
            }

            // Ensure UI panel is open when tool is activated
            if (m_UISystem != null)
            {
                m_UISystem.SetPanelOpen(true);
            }

            log.Info("[RoadRules] TOOL ENABLED = True");
            log.Info($"[RoadRules] ACTIVE TOOL = {m_ToolSystem?.activeTool?.toolID ?? "NULL"}");
            log.Info($"[RoadRules] Tool State = {State}");
        }

        protected override void OnStopRunning()
        {
            applyAction.shouldBeEnabled = false;
            secondaryApplyAction.shouldBeEnabled = false;
            cancelAction.shouldBeEnabled = false;

            HoveredEdge = Entity.Null;
            log.Info("[RoadRules] TOOL ENABLED = False (Deactivated)");
            base.OnStopRunning();
        }

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();
            if (m_ToolRaycastSystem != null)
            {
                m_ToolRaycastSystem.typeMask = TypeMask.Net | TypeMask.Terrain;
                m_ToolRaycastSystem.netLayerMask = Layer.Road | Layer.PublicTransportRoad | Layer.TrainTrack | Layer.TramTrack;
                m_ToolRaycastSystem.raycastFlags = RaycastFlags.SubElements | RaycastFlags.Markers;
                m_ToolRaycastSystem.collisionMask = CollisionMask.OnGround | CollisionMask.Overground | CollisionMask.Underground;
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            UpdateTickCount++;

            // Periodically log liveness during active tool usage (every ~300 frames)
            if (UpdateTickCount % 300 == 1)
            {
                log.Info($"[RoadRules] TOOL ONUPDATE RUNNING (Tick #{UpdateTickCount}, ActiveTool: {m_ToolSystem?.activeTool?.toolID}, State: {State})");
            }

            // Verify selected edge validity
            if (m_RulesSystem != null && m_RulesSystem.SelectedEdge != Entity.Null)
            {
                if (!EntityManager.Exists(m_RulesSystem.SelectedEdge) || EntityManager.HasComponent<Deleted>(m_RulesSystem.SelectedEdge))
                {
                    log.Info("[RoadRules] Selected edge was deleted or destroyed — clearing selection.");
                    m_RulesSystem.ClearSelection();
                    State = RoadRulesToolState.SelectRoad;
                }
                else
                {
                    State = RoadRulesToolState.EditRules;
                }
            }
            else
            {
                State = RoadRulesToolState.SelectRoad;
            }

            // Perform raycast
            RaycastHit hit;
            bool hitSomething = GetRaycastResult(out Entity hitEntity, out hit);
            LastRaycastHit = hitSomething;
            CursorWorldPos = hitSomething ? hit.m_HitPosition : float3.zero;

            Entity resolvedEdge = Entity.Null;
            if (hitSomething && m_RulesSystem != null)
            {
                resolvedEdge = m_RulesSystem.ResolveToEdge(hitEntity, out _);
            }
            HoveredEdge = resolvedEdge;

            // Detect Left Click (applyAction)
            bool applyPressed = applyAction != null && applyAction.WasPressedThisFrame();

            if (applyPressed)
            {
                LastClickDetected = true;
                log.Info($"[RoadRules] LEFT CLICK RECEIVED (applyAction.WasPressedThisFrame()=true)");
                log.Info($"[RoadRules] RAYCAST START (hitSomething={hitSomething}, hitEntity=#{hitEntity.Index}:{hitEntity.Version}, hitPos={CursorWorldPos})");

                if (hitSomething && m_RulesSystem != null)
                {
                    log.Info($"[RoadRules] RESOLVE ENTITY starting for #{hitEntity.Index}:{hitEntity.Version}");
                    m_RulesSystem.InspectEntity(hitEntity, CursorWorldPos);

                    if (m_RulesSystem.SelectedEdge != Entity.Null)
                    {
                        State = RoadRulesToolState.EditRules;
                        if (m_UISystem != null)
                        {
                            m_UISystem.SetPanelOpen(true);
                        }
                    }
                    else
                    {
                        log.Warn($"[RoadRules] RESOLUTION FAILED: {m_RulesSystem.LastFailureReason}");
                        m_RulesSystem.SetStatus($"Couldn't select that road: {m_RulesSystem.LastFailureReason}");
                    }
                }

                else
                {
                    log.Info("[RoadRules] Click on empty space (no raycast hit).");
                    if (m_RulesSystem != null)
                    {
                        m_RulesSystem.SetStatus("No road found under cursor. Point directly at a road surface.");
                    }
                }
            }

            // Handle Right Click (Deselect / Cancel)
            bool secondaryPressed = secondaryApplyAction != null && secondaryApplyAction.WasPressedThisFrame();
            if (secondaryPressed)
            {
                if (m_RulesSystem != null && m_RulesSystem.SelectedEdge != Entity.Null)
                {
                    log.Info("[RoadRules] Deselected road on Right-Click.");
                    m_RulesSystem.ClearSelection();
                    State = RoadRulesToolState.SelectRoad;
                }
                else
                {
                    log.Info("[RoadRules] Exiting RoadRulesTool on Right-Click.");
                    ExitTool();
                }
            }

            // Handle Esc key
            if (cancelAction != null && cancelAction.WasPressedThisFrame())
            {
                if (m_RulesSystem != null && m_RulesSystem.SelectedEdge != Entity.Null)
                {
                    log.Info("[RoadRules] Deselected road on Esc key.");
                    m_RulesSystem.ClearSelection();
                    State = RoadRulesToolState.SelectRoad;
                }
                else
                {
                    log.Info("[RoadRules] Exiting RoadRulesTool on Esc key.");
                    ExitTool();
                }
            }

            return inputDeps;
        }

        public void ExitTool()
        {
            log.Info("[RoadRules] ExitTool called — restoring DefaultToolSystem.");
            if (m_ToolSystem != null && m_DefaultTool != null)
            {
                m_ToolSystem.activeTool = m_DefaultTool;
            }
            if (m_UISystem != null)
            {
                m_UISystem.SetPanelOpen(false);
            }
        }
    }
}

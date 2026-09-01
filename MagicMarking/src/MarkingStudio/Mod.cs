using Colossal.Logging;
using Game;
using Game.Modding;
using Game.Net;
using Game.SceneFlow;
using Colossal.IO.AssetDatabase;
using System;
using System.Linq;
using Unity.Entities;
using MarkingStudio.Diagnostics;

namespace MarkingStudio
{
    /// <summary>
    /// Entry point. Disables vanilla <see cref="SecondaryLaneSystem"/>, registers our drop-in copy
    /// <see cref="CustomSecondaryLaneSystem"/> (Layer 2) plus the per-entity <c>MarkingOverride</c>
    /// toggle (Layer 3). Layers 1 (clone systems) and 4 (node UI tool) are added in later phases —
    /// see IMPLEMENTATION_PLAN.md.
    /// </summary>
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{"MagicMarking"}.{nameof(Mod)}").SetShowsErrorsInUI(false);

        public static MarkingStudioSetting Settings { get; private set; }
        // Singleton handle to the live mod instance — needed by MarkingStudioUISystem so it can
        // resolve its own ExecutableAsset path to read the React bundle. ModManager indexes
        // assets by mod instance; using a fresh new Mod() would lose that mapping.
        public static Mod Instance { get; private set; }

        private SecondaryLaneSystem m_VanillaSecondaryLaneSystem;
        private bool m_VanillaSecondaryLaneWasEnabled;

        public void OnLoad(UpdateSystem updateSystem)
        {
            Instance = this;
            log.Info("Magic Marking v2.4.1-beta.1-BETA loaded | maintenance candidate");


            // Check if TownRoadLane is present in the domain
            bool trlPresent = AppDomain.CurrentDomain.GetAssemblies().Any(a =>
                    string.Equals(a.GetName().Name, "TownRoadLane", StringComparison.OrdinalIgnoreCase));
            if (trlPresent)
            {
                log.Info("Town Road Lane detected alongside Magic Marking. Magic Marking is operating as an independent engine.");
            }

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Magic Marking v2.4.1-beta.1 asset location: {asset.path}");

            // Register with SuiteBridge
            Colossal.UtilitySuite.Interop.SuiteBridge.Register("MagicMarking", (entityIndex, pos) =>
            {
                var tool = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<MarkingNodeToolSystem>();
                if (tool != null)
                {
                    var toolSys = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<Game.Tools.ToolSystem>();
                    if (toolSys != null)
                    {
                        toolSys.activeTool = tool;
                    }
                }
            });

            // The defaults instance must be created BEFORE the live one: every ModSetting ctor
            // registers itself in the static ModSetting.instances[id] map, so whichever is
            // constructed last is what the game resolves by id. Constructing the defaults inline
            // in the LoadSettings call used to leave the throwaway object as the registered one.
            var settingDefaults = new MarkingStudioSetting(this);
            Settings = new MarkingStudioSetting(this);
            // RegisterKeyBindings must run BEFORE GetAction() resolves anything. Without this call
            // the ProxyAction for ToggleMarkingTool never fires (silent — no warn). Traffic's
            // Mod.cs:56-57 does the same: RegisterKeyBindings before RegisterInOptionsUI.
            Settings.RegisterKeyBindings();
            Settings.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(Settings));
            GameManager.instance.localizationManager.AddSource("ru-RU", new LocaleRU(Settings));
            AssetDatabase.global.LoadSettings(nameof(MarkingStudio), Settings, settingDefaults);
            // Decode failures fall back to SetDefaults() silently (both toggles back to true) —
            // log what actually survived the load so user reports show the real state.
            log.Info($"settings loaded: edge={Settings.EdgeLineEnabled}/{Settings.EdgeLineStyle}, parking={Settings.ParkingMarkingsEnabled}/{Settings.ParkingLineStyle}/{Settings.ParkingEndStyle}, pins='{Settings.PinnedLineStylesCsv}'/'{Settings.PinnedAreaStylesCsv}'");

            // EXPERIMENT: vanilla grass surface as an area fill, registered on a live frame
            // (the EAI recipe — see VanillaSurfaceLateClone). Style slots 15/16.
            VanillaSurfaceLateClone.Register(updateSystem.World);

            // Development-only prefab/mesh probes intentionally stay unregistered in release
            // builds. Registering them here caused very large per-session log files and needless
            // prefab traversal for every player.
            // ParkingPairDumpSystem is kept in the tree for phase 4 endpoint-extraction debugging.
            // Re-register when needed: updateSystem.UpdateAt<ParkingPairDumpSystem>(SystemUpdatePhase.GameSimulation);

            // Layer 1: clone vanilla marking prefabs (one-shot per session, self-disables after first run).
            // Both must live in PrefabUpdate so PrefabSystem.UpdatePrefab fires NetInitializeSystem on the
            // same frame and the SecondaryNetLane buffers are baked before road geometry processes them.
            // See K2 / K4 in IMPLEMENTATION_PLAN.md.
            updateSystem.UpdateAt<EdgeLineCloneSystem>(SystemUpdatePhase.PrefabUpdate);
            updateSystem.UpdateAt<ParkingLineCloneSystem>(SystemUpdatePhase.PrefabUpdate);

            // Disable vanilla markings generator. Cars still drive normally — LaneSystem (primary lanes)
            // is untouched; only the secondary marking pass is replaced.
            m_VanillaSecondaryLaneSystem = updateSystem.World.GetOrCreateSystemManaged<SecondaryLaneSystem>();
            m_VanillaSecondaryLaneWasEnabled = m_VanillaSecondaryLaneSystem.Enabled;
            m_VanillaSecondaryLaneSystem.Enabled = false;
            log.Info($"vanilla SecondaryLaneSystem disabled (was Enabled={m_VanillaSecondaryLaneWasEnabled})");

            // Our replacement. Phase 0 is byte-for-byte equivalent to vanilla — success criterion is
            // "city looks identical after enabling the mod". MUST run on Modification4B (not 4) — that's
            // where AllowBarrier<ModificationBarrier4B> lives. Using Modification4 instead would put us
            // outside the barrier's allowed window and SafeCommandBufferSystem.CreateCommandBuffer
            // would throw "Trying to create EntityCommandBuffer when it's not allowed!".
            // See decomp/Game/Game.Common/SystemOrder.cs:184.
            updateSystem.UpdateAt<CustomSecondaryLaneSystem>(SystemUpdatePhase.Modification4B);
            log.Info($"CustomSecondaryLaneSystem registered at Modification4B");

            // No runtime "reapply" system: refreshing clone prefabs (UpdatePrefab) in a live
            // world leaves existing sublanes with stale PrefabRefs and the next SecondaryLane
            // rebuild crashes natively in a Burst job (three crashes on 2026-07-17, see
            // Setting.cs). Settings apply on the next save load via the clone systems'
            // regular PrefabUpdate pass.

            // Phase 4 tool: per-node marking customisation. ToolBaseSystem self-registers with
            // ToolSystem.tools in its OnCreate; we just need to instantiate it. Update phase per
            // vanilla tool convention (ToolBaseSystem.cs base wires its own ToolUpdate path).
            updateSystem.UpdateAt<MarkingNodeToolSystem>(SystemUpdatePhase.ToolUpdate);
            // Hotkey poller — flips activeTool when Ctrl+M fires. Cheap WasPerformedThisFrame check.
            updateSystem.UpdateAt<MarkingToolHotkeySystem>(SystemUpdatePhase.Modification1);
            // Overlay renderer for connector dots, drag-line, and confirmed pairs. Gated on
            // activeTool == MarkingNodeToolSystem; idle otherwise. Rendering phase is fine here
            // (we read tool state, write to vanilla OverlayRenderSystem.Buffer).
            updateSystem.UpdateAt<MarkingOverlaySystem>(SystemUpdatePhase.Rendering);

            // Phase 4 step 4 (B.1 revive): spawn vanilla SecondaryLane entities per user pair.
            // The earlier custom-mesh path (MarkingMeshRenderSystem on Graphics.DrawMesh /
            // GameObject+MeshRenderer) was exhaustively explored and proven incompatible with
            // HDRP's DBufferMesh pass — vanilla decal shaders depend on DOTS InstanceProperties
            // (colossal_CurveMatrix) only BRG can supply. See commits 35e504c..7d6a9f1 +
            // research/RESEARCH_decal_*.md for the full dead-end exploration.
            //
            // ECS path: spawn entity with edge-line clone prefab's archetype, vanilla BRG
            // pipeline picks it up via Game.Net.SecondaryLane tag (auto-included by archetype),
            // SecondaryLaneReferencesSystem registers it in node.SubLane at Modification5,
            // vanilla CurvedDecalShader renders with full quality. Same path EAI/RealVision use.
            // See research/RESEARCH_sublane_lifecycle.md for the full spec.
            //
            // Modification1 puts us BEFORE LaneSystem (4) / SecondaryLaneSystem (4B) /
            // SecondaryLaneReferencesSystem (5) — same phase the old commits used; proven safe.
            // Stage 5b migration: rewrite v2 MarkingPair buffers as v3 MarkingLine+MarkingSegment
            // on first sight of a node. Idempotent + cheap (empty query 99% of frames). Must run
            // before emission — [UpdateBefore] on the class handles ordering inside Modification1.
            updateSystem.UpdateAt<MarkingPairMigrationSystem>(SystemUpdatePhase.Modification1);
            // Stage 5b: pairwise Bezier intersection + segment buffer rewrite. Ordered between
            // migration and emission via [UpdateAfter]/[UpdateBefore] on the class itself.
            updateSystem.UpdateAt<MarkingTopologySystem>(SystemUpdatePhase.Modification1);
            // Magic Marking 2.1: Adaptive Road-Aware Remapping on geometry edits
            updateSystem.UpdateAt<AdaptiveRemappingSystem>(SystemUpdatePhase.Modification1);
            // Magic Marking Maintenance & Global Redraw System
            updateSystem.UpdateAt<MarkingMaintenanceSystem>(SystemUpdatePhase.Modification1);
            // Stage 5b emission: spawn one sublane per visible MarkingSegment (replaces the
            // Phase-4 MarkingPairEmissionSystem which keyed off MarkingPair). The old system is
            // intentionally not registered any more — its MSPairLink entities get GC'd by
            // MarkingSegmentEmissionSystem on first tick after migration.
            updateSystem.UpdateAt<MarkingSegmentEmissionSystem>(SystemUpdatePhase.Modification1);
            // Phase 6e: split areas at every line intersection. Must run AFTER MarkingTopologySystem
            // (line buffer up-to-date) and BEFORE MarkingAreaEmissionSystem (piece buffer must be
            // fresh when emission diffs). [UpdateAfter]/[UpdateBefore] on the class enforces this.
            updateSystem.UpdateAt<MarkingAreaTopologySystem>(SystemUpdatePhase.Modification1);
            // Phase 6c: per-node MarkingArea → vanilla Game.Areas.Area emitter. Same Modification1
            // phase as the line emitter (independent buffers; no ordering required between them).
            updateSystem.UpdateAt<MarkingAreaEmissionSystem>(SystemUpdatePhase.Modification1);
            // Phase 8: re-triangulate our fills after vanilla Game.Areas.GeometrySystem
            // (Modification2B, [UpdateAfter] on the class). Replaces its shrink-and-budget
            // ear-clip — the source of silently invisible fills — with a full triangulation
            // of the true ring.
            updateSystem.UpdateAt<MarkingAreaTriangulationSystem>(SystemUpdatePhase.Modification2B);

            // Stage 5d: React panel bridge. UISystemBase wants UIUpdate phase.
            updateSystem.UpdateAt<MarkingStudioUISystem>(SystemUpdatePhase.UIUpdate);

            // MarkingMeshRenderSystem (HDRP/Unlit + GameObject pipeline) kept as commented
            // fallback in case ECS path reveals an unknown blocker. Source file stays in tree.
            // updateSystem.UpdateAt<MarkingMeshRenderSystem>(SystemUpdatePhase.Rendering);
            // updateSystem.UpdateAt<UserPairEmissionDumpSystem>(SystemUpdatePhase.GameSimulation);
        }

        public void OnDispose()
        {
            log.Info("Magic Marking disposing.");
            VanillaSurfaceLateClone.Unregister();
            Instance = null;
            Colossal.UtilitySuite.Interop.SuiteBridge.Unregister("MagicMarking");
            if (m_VanillaSecondaryLaneSystem != null)
            {
                try
                {
                    m_VanillaSecondaryLaneSystem.Enabled = m_VanillaSecondaryLaneWasEnabled;
                    log.Info($"vanilla SecondaryLaneSystem restored (Enabled={m_VanillaSecondaryLaneWasEnabled})");
                }
                catch (Exception ex)
                {
                    // World teardown may already have disposed the system. Do not let mod
                    // disposal turn that harmless ordering difference into an exception storm.
                    log.Warn($"Could not restore SecondaryLaneSystem during world teardown: {ex.Message}");
                }
                m_VanillaSecondaryLaneSystem = null;
            }
            if (Settings != null)
            {
                Settings.UnregisterInOptionsUI();
                Settings = null;
            }
        }
    }
}

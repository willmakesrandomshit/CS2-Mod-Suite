using System;
using System.Collections.Generic;
using Game;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Zones;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace GrowableModulesPrototype
{
    /// <summary>
    /// Registers one low-density residential growable by cloning a
    /// vanilla building prefab and composing its mesh list with a second vanilla building.
    /// This is intentionally a one-shot technical preview, not a player-facing builder.
    /// </summary>
    public partial class GrowableModulePrototypeSystem : GameSystemBase
    {
        private const string PrototypeName = "Modular Growables Duplex 01";
        private const string LowResidentialToken = "Residential Low";
        private const int SearchIntervalFrames = 60;
        private const int LongWaitFrames = 3600;

        private PrefabSystem m_PrefabSystem;
        private EntityQuery m_PrefabQuery;
        private bool m_Complete;
        private int m_SearchFrames;
        private bool m_LongWaitLogged;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_PrefabQuery = GetEntityQuery(ComponentType.ReadOnly<PrefabData>());
            RequireForUpdate(m_PrefabQuery);
        }

        protected override void OnUpdate()
        {
            if (m_Complete) return;
            var gameManager = GameManager.instance;
            if (++m_SearchFrames % SearchIntervalFrames != 0) return;

            BuildingPrefab registeredClone = null;
            try
            {
                if (m_PrefabSystem.TryGetPrefab(new PrefabID(nameof(BuildingPrefab), PrototypeName), out _))
                {
                    m_Complete = true;
                    Mod.log.Info($"Growable variant '{PrototypeName}' is already registered");
                    Enabled = false;
                    return;
                }

                if (!TryFindSourcePair(out var baseBuilding, out var moduleBuilding, out var zoneName))
                {
                    bool cityReady = gameManager != null && gameManager.gameMode == GameMode.Game &&
                        gameManager.state != GameManager.State.Booting && gameManager.state != GameManager.State.Loading;
                    if (cityReady)
                    {
                        m_Complete = true;
                        Mod.log.Warn("No compatible vanilla low-density residential prefab pair was available by the end of city loading; module prototype was not registered");
                        Enabled = false;
                    }
                    else if (!m_LongWaitLogged && m_SearchFrames >= LongWaitFrames)
                    {
                        m_LongWaitLogged = true;
                        Mod.log.Warn("Still waiting for a compatible built-in low-density residential prefab pair");
                    }
                    return;
                }

                m_Complete = true;
                if (baseBuilding.m_LotWidth <= 0 || baseBuilding.m_LotDepth <= 0 ||
                    moduleBuilding.m_LotWidth != baseBuilding.m_LotWidth || moduleBuilding.m_LotDepth != baseBuilding.m_LotDepth)
                {
                    Mod.log.Warn($"Selected source footprints are incompatible: {baseBuilding.name} ({baseBuilding.m_LotWidth}x{baseBuilding.m_LotDepth}), {moduleBuilding.name} ({moduleBuilding.m_LotWidth}x{moduleBuilding.m_LotDepth})");
                    Enabled = false;
                    return;
                }

                registeredClone = m_PrefabSystem.DuplicatePrefab(baseBuilding, PrototypeName) as BuildingPrefab;
                if (registeredClone == null)
                {
                    Mod.log.Error("PrefabSystem.DuplicatePrefab did not return a BuildingPrefab");
                    Enabled = false;
                    return;
                }

                float halfModuleWidth = baseBuilding.m_LotWidth * ZoneUtils.CELL_SIZE * 0.5f;
                var composedMeshes = ComposeMeshes(baseBuilding, moduleBuilding, -halfModuleWidth, halfModuleWidth);
                if (composedMeshes == null)
                {
                    m_PrefabSystem.RemovePrefab(registeredClone);
                    registeredClone = null;
                    Mod.log.Warn("Selected source pair had no usable meshes; module prototype was removed");
                    Enabled = false;
                    return;
                }

                registeredClone.m_Meshes = composedMeshes;
                registeredClone.m_LotWidth = checked(baseBuilding.m_LotWidth + moduleBuilding.m_LotWidth);
                registeredClone.m_LotDepth = Math.Max(baseBuilding.m_LotDepth, moduleBuilding.m_LotDepth);
                registeredClone.isDirty = true;
                m_PrefabSystem.UpdatePrefab(registeredClone);

                Mod.log.Info($"Registered experimental growable '{PrototypeName}' from '{baseBuilding.name}' + '{moduleBuilding.name}': zone='{zoneName}', footprint={registeredClone.m_LotWidth}x{registeredClone.m_LotDepth}, meshes={composedMeshes.Length}");
                Enabled = false;
            }
            catch (Exception e)
            {
                m_Complete = true;
                if (registeredClone != null)
                {
                    try { m_PrefabSystem.RemovePrefab(registeredClone); }
                    catch (Exception cleanupError) { Mod.log.Error(cleanupError, "Failed to remove partially registered growable prototype"); }
                }
                Enabled = false;
                Mod.log.Error(e, "Modular Growables registration failed");
            }
        }

        private bool TryFindSourcePair(out BuildingPrefab baseBuilding, out BuildingPrefab moduleBuilding, out string zoneName)
        {
            baseBuilding = null;
            moduleBuilding = null;
            zoneName = null;

            var candidates = new List<(BuildingPrefab building, string zone)>();
            using (var entities = m_PrefabQuery.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    if (!m_PrefabSystem.TryGetPrefab<BuildingPrefab>(entities[i], out var building) || building == null || !building.isBuiltin)
                        continue;
                    if (building.m_Meshes == null || building.m_Meshes.Length == 0 ||
                        !building.TryGet<SpawnableBuilding>(out var spawnable) || spawnable == null || spawnable.m_ZoneType == null)
                        continue;

                    string zone = spawnable.m_ZoneType.name;
                    if (zone.IndexOf(LowResidentialToken, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    candidates.Add((building, zone));
                }
            }

            candidates.Sort((a, b) => string.CompareOrdinal(a.building.name, b.building.name));
            for (int i = 0; i < candidates.Count; i++)
            {
                var first = candidates[i];
                if (first.building.m_LotWidth <= 0 || first.building.m_LotDepth <= 0) continue;
                for (int j = i + 1; j < candidates.Count; j++)
                {
                    var second = candidates[j];
                    if (!string.Equals(first.zone, second.zone, StringComparison.Ordinal) ||
                        first.building.m_LotWidth != second.building.m_LotWidth ||
                        first.building.m_LotDepth != second.building.m_LotDepth)
                        continue;

                    baseBuilding = first.building;
                    moduleBuilding = second.building;
                    zoneName = first.zone;
                    return true;
                }
            }

            return false;
        }

        private static ObjectMeshInfo[] ComposeMeshes(BuildingPrefab first, BuildingPrefab second, float firstHorizontalOffset, float secondHorizontalOffset)
        {
            if (first.m_Meshes == null || second.m_Meshes == null || first.m_Meshes.Length == 0 || second.m_Meshes.Length == 0)
                return null;

            var result = new List<ObjectMeshInfo>(first.m_Meshes.Length + second.m_Meshes.Length);
            if (CopyMeshes(first.m_Meshes, result, firstHorizontalOffset) == 0 ||
                CopyMeshes(second.m_Meshes, result, secondHorizontalOffset) == 0)
                return null;

            return result.ToArray();
        }

        private static int CopyMeshes(ObjectMeshInfo[] source, List<ObjectMeshInfo> destination, float horizontalOffset)
        {
            int copied = 0;
            foreach (var original in source)
            {
                if (original == null || original.m_Mesh == null) continue;
                var copy = new ObjectMeshInfo
                {
                    m_Mesh = original.m_Mesh,
                    m_Position = original.m_Position + new float3(horizontalOffset, 0f, 0f),
                    m_Rotation = original.m_Rotation,
                    m_RequireState = original.m_RequireState,
                };
                destination.Add(copy);
                copied++;
            }

            return copied;
        }
    }
}

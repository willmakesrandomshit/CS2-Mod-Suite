using System;
using System.Collections.Generic;
using Colossal.Logging;
using Colossal.Mathematics;
using Game;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace NetworkStudio
{
    public partial class NetworkStudioSystem : GameSystemBase
    {
        private static ILog log = Mod.log;

        public Entity SelectedEdge { get; private set; } = Entity.Null;
        public Entity SelectedFurnitureEntity { get; private set; } = Entity.Null;
        public int SelectedFurnitureIndex { get; private set; } = -1;

        public List<FurnitureItemInfo> CurrentFurnitureList { get; private set; } = new List<FurnitureItemInfo>();
        public Dictionary<int, FurnitureOverrideRecord> ActiveOverrides { get; private set; } = new Dictionary<int, FurnitureOverrideRecord>();

        private EntityQuery m_SubObjectQuery;
        private PrefabSystem m_PrefabSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            log.Info("[NetworkStudioSystem] OnCreate");
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_SubObjectQuery = GetEntityQuery(ComponentType.ReadOnly<Game.Objects.SubObject>());
        }

        protected override void OnUpdate()
        {
            if (SelectedEdge == Entity.Null || !EntityManager.Exists(SelectedEdge))
                return;

            if (EntityManager.HasComponent<Deleted>(SelectedEdge))
            {
                ClearSelection();
                return;
            }
        }

        public void SelectEdge(Entity edge)
        {
            if (edge == SelectedEdge) return;

            SelectedEdge = edge;
            SelectedFurnitureEntity = Entity.Null;
            SelectedFurnitureIndex = -1;
            RefreshFurnitureList();
        }

        public void ClearSelection()
        {
            SelectedEdge = Entity.Null;
            SelectedFurnitureEntity = Entity.Null;
            SelectedFurnitureIndex = -1;
            CurrentFurnitureList.Clear();
        }

        public void RefreshFurnitureList()
        {
            CurrentFurnitureList.Clear();

            if (SelectedEdge == Entity.Null || !EntityManager.Exists(SelectedEdge))
                return;

            if (!EntityManager.HasBuffer<Game.Objects.SubObject>(SelectedEdge))
                return;

            var subObjects = EntityManager.GetBuffer<Game.Objects.SubObject>(SelectedEdge);
            for (int i = 0; i < subObjects.Length; i++)
            {
                Entity sub = subObjects[i].m_SubObject;
                if (!EntityManager.Exists(sub) || EntityManager.HasComponent<Deleted>(sub))
                    continue;

                string name = "Road Prop";
                FurnitureCategory category = FurnitureCategory.RoadSigns;

                if (EntityManager.HasComponent<PrefabRef>(sub))
                {
                    var prefabRef = EntityManager.GetComponentData<PrefabRef>(sub);
                    if (m_PrefabSystem != null && m_PrefabSystem.TryGetPrefab<PrefabBase>(prefabRef.m_Prefab, out var prefabBase))
                    {
                        name = prefabBase.name;
                        string lower = name.ToLowerInvariant();
                        if (lower.Contains("sign") || lower.Contains("speed") || lower.Contains("overhead"))
                            category = FurnitureCategory.RoadSigns;
                        else if (lower.Contains("light") || lower.Contains("lamp") || lower.Contains("street"))
                            category = FurnitureCategory.StreetLights;
                        else if (lower.Contains("traffic") || lower.Contains("signal"))
                            category = FurnitureCategory.TrafficSignals;
                        else if (lower.Contains("tree") || lower.Contains("bush") || lower.Contains("plant"))
                            category = FurnitureCategory.Trees;
                        else if (lower.Contains("guard") || lower.Contains("barrier") || lower.Contains("fence"))
                            category = FurnitureCategory.Barriers;
                    }
                }

                float3 pos = float3.zero;
                float latOffset = 0f;
                if (EntityManager.HasComponent<Game.Objects.Transform>(sub))
                {
                    pos = EntityManager.GetComponentData<Game.Objects.Transform>(sub).m_Position;
                    if (EntityManager.HasComponent<Curve>(SelectedEdge))
                    {
                        var curve = EntityManager.GetComponentData<Curve>(SelectedEdge);
                        float t = 0.5f;
                        if (EntityManager.HasComponent<Game.Objects.Attached>(sub))
                        {
                            t = math.clamp(EntityManager.GetComponentData<Game.Objects.Attached>(sub).m_CurvePosition, 0f, 1f);
                        }
                        float3 center = MathUtils.Position(curve.m_Bezier, t);
                        latOffset = math.distance(pos, center);
                    }
                }

                bool onAsphalt = latOffset < 2.0f;

                CurrentFurnitureList.Add(new FurnitureItemInfo
                {
                    Entity = sub,
                    Index = i,
                    Name = name,
                    Category = category,
                    Position = pos,
                    LateralOffset = latOffset,
                    IsOnAsphalt = onAsphalt
                });
            }

            log.Info($"[NetworkStudioSystem] Refreshed furniture list for edge {SelectedEdge.Index}: {CurrentFurnitureList.Count} items found.");
        }

        public void SelectFurniture(int index)
        {
            if (index >= 0 && index < CurrentFurnitureList.Count)
            {
                SelectedFurnitureIndex = index;
                SelectedFurnitureEntity = CurrentFurnitureList[index].Entity;
            }
            else
            {
                SelectedFurnitureIndex = -1;
                SelectedFurnitureEntity = Entity.Null;
            }
        }

        public bool MoveSignToVerge(int index, bool leftSide)
        {
            if (index < 0 || index >= CurrentFurnitureList.Count)
                return false;

            var item = CurrentFurnitureList[index];
            Entity sub = item.Entity;
            if (!EntityManager.Exists(sub))
                return false;

            float targetOffset = leftSide ? -4.2f : 4.2f;

            if (EntityManager.HasComponent<Game.Objects.Transform>(sub))
            {
                var transform = EntityManager.GetComponentData<Game.Objects.Transform>(sub);
                if (EntityManager.HasComponent<Curve>(SelectedEdge))
                {
                    var curve = EntityManager.GetComponentData<Curve>(SelectedEdge);
                    float t = 0.5f;
                    if (EntityManager.HasComponent<Game.Objects.Attached>(sub))
                    {
                        t = math.clamp(EntityManager.GetComponentData<Game.Objects.Attached>(sub).m_CurvePosition, 0f, 1f);
                    }
                    float3 tangent = math.normalize(MathUtils.Tangent(curve.m_Bezier, t));
                    float3 up = new float3(0, 1, 0);
                    float3 normal = math.normalize(math.cross(up, tangent));
                    float3 centerPos = MathUtils.Position(curve.m_Bezier, t);
                    transform.m_Position = centerPos + normal * targetOffset;
                    EntityManager.SetComponentData(sub, transform);
                }
            }

            // Force world transform refresh
            if (!EntityManager.HasComponent<Updated>(sub))
            {
                EntityManager.AddComponent<Updated>(sub);
            }
            if (!EntityManager.HasComponent<BatchesUpdated>(sub))
            {
                EntityManager.AddComponent<BatchesUpdated>(sub);
            }

            ActiveOverrides[index] = new FurnitureOverrideRecord
            {
                EdgeIndex = SelectedEdge.Index,
                SubObjectIndex = index,
                Category = FurnitureCategory.RoadSigns,
                Placement = leftSide ? SignPlacement.LeftVerge : SignPlacement.RightVerge,
                LateralOffset = targetOffset,
                IsHidden = false
            };

            RefreshFurnitureList();
            log.Info($"[NetworkStudioSystem] Moved sign #{index} to verge (Offset: {targetOffset:F1}m)");
            return true;
        }

        public bool SetCustomOffset(int index, float offset)
        {
            if (index < 0 || index >= CurrentFurnitureList.Count)
                return false;

            var item = CurrentFurnitureList[index];
            Entity sub = item.Entity;
            if (!EntityManager.Exists(sub))
                return false;

            if (EntityManager.HasComponent<Game.Objects.Transform>(sub) && EntityManager.HasComponent<Curve>(SelectedEdge))
            {
                var transform = EntityManager.GetComponentData<Game.Objects.Transform>(sub);
                var curve = EntityManager.GetComponentData<Curve>(SelectedEdge);
                float t = 0.5f;
                if (EntityManager.HasComponent<Game.Objects.Attached>(sub))
                {
                    t = math.clamp(EntityManager.GetComponentData<Game.Objects.Attached>(sub).m_CurvePosition, 0f, 1f);
                }
                float3 tangent = math.normalize(MathUtils.Tangent(curve.m_Bezier, t));
                float3 up = new float3(0, 1, 0);
                float3 normal = math.normalize(math.cross(up, tangent));
                float3 centerPos = MathUtils.Position(curve.m_Bezier, t);
                transform.m_Position = centerPos + normal * offset;
                EntityManager.SetComponentData(sub, transform);
            }

            if (!EntityManager.HasComponent<Updated>(sub))
            {
                EntityManager.AddComponent<Updated>(sub);
            }

            ActiveOverrides[index] = new FurnitureOverrideRecord
            {
                EdgeIndex = SelectedEdge.Index,
                SubObjectIndex = index,
                Category = item.Category,
                Placement = SignPlacement.CustomOffset,
                LateralOffset = offset,
                IsHidden = false
            };

            RefreshFurnitureList();
            return true;
        }

        public bool ResetFurniture(int index)
        {
            if (index < 0 || index >= CurrentFurnitureList.Count)
                return false;

            var item = CurrentFurnitureList[index];
            Entity sub = item.Entity;
            if (!EntityManager.Exists(sub))
                return false;

            if (ActiveOverrides.ContainsKey(index))
            {
                ActiveOverrides.Remove(index);
            }

            if (!EntityManager.HasComponent<Updated>(sub))
            {
                EntityManager.AddComponent<Updated>(sub);
            }

            RefreshFurnitureList();
            return true;
        }
    }
}

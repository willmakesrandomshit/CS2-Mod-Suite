using System;
using System.Collections.Generic;
using Colossal.UI.Binding;
using Game.Buildings;
using Game.Common;
using Game.Net;
using Game.Tools;
using Game.UI;
using Unity.Collections;
using Unity.Entities;

namespace BuildGuard
{
    // Read-only. The game's own edit tools build "Temp" entities as the preview before the player
    // commits; this reads them and summarizes what they say will change. It never creates,
    // modifies or deletes any entity and never touches the tool or the edit itself.
    public partial class BuildGuardSystem : UISystemBase
    {
        private EntityQuery m_TempQuery;
        private ValueBinding<bool> m_Active;
        private ValueBinding<bool> m_Attention;
        private ValueBinding<string> m_Text;
        private int m_Tick;
        private readonly List<PreviewItem> m_Items = new List<PreviewItem>(256);

        protected override void OnCreate()
        {
            base.OnCreate();
            m_TempQuery = GetEntityQuery(ComponentType.ReadOnly<Temp>(), ComponentType.Exclude<Deleted>());
            AddBinding(m_Active = new ValueBinding<bool>("buildGuard", "active", false));
            AddBinding(m_Attention = new ValueBinding<bool>("buildGuard", "attention", false));
            AddBinding(m_Text = new ValueBinding<string>("buildGuard", "text", ""));
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (++m_Tick % 15 != 0) return; // about 4 times a second; previews change slowly

            try
            {
                if (m_TempQuery.IsEmptyIgnoreFilter)
                {
                    if (m_Active.value) { m_Active.Update(false); m_Attention.Update(false); m_Text.Update(""); }
                    return;
                }

                m_Items.Clear();
                using (var entities = m_TempQuery.ToEntityArray(Allocator.Temp))
                using (var temps = m_TempQuery.ToComponentDataArray<Temp>(Allocator.Temp))
                {
                    // Bound the work: an enormous drag-built preview is summarized from its first items.
                    int n = Math.Min(entities.Length, 4000);
                    for (int i = 0; i < n; i++)
                    {
                        Entity e = entities[i];
                        Temp t = temps[i];
                        m_Items.Add(new PreviewItem
                        {
                            Change = Classify(t),
                            Object = EntityManager.HasComponent<Building>(e) ? ObjectKind.Building
                                   : EntityManager.HasComponent<Edge>(e) ? ObjectKind.RoadSegment
                                   : EntityManager.HasComponent<Node>(e) ? ObjectKind.Intersection
                                   : ObjectKind.Other,
                            Cost = t.m_Cost,
                        });
                    }
                    if (entities.Length > n) Mod.Log.Debug("BuildGuard summarized the first " + n + " of " + entities.Length + " preview objects");
                }

                ImpactSummary s = Impact.Summarize(m_Items);
                if (s.Empty) { if (m_Active.value) m_Active.Update(false); return; }
                m_Text.Update(Impact.Describe(s));
                m_Attention.Update(s.NeedsAttention);
                if (!m_Active.value) m_Active.Update(true);
            }
            catch (Exception ex)
            {
                Mod.Log.Warn("BuildGuard could not read the edit preview: " + ex.Message);
                if (m_Active.value) m_Active.Update(false);
            }
        }

        private static ChangeKind Classify(Temp t)
        {
            if ((t.m_Flags & TempFlags.Delete) != 0) return ChangeKind.Delete;
            if ((t.m_Flags & TempFlags.Replace) != 0) return ChangeKind.Replace;
            if ((t.m_Flags & TempFlags.Upgrade) != 0) return ChangeKind.Upgrade;
            if ((t.m_Flags & TempFlags.Modify) != 0) return ChangeKind.Modify;
            return ChangeKind.Create;
        }
    }
}

using System;
using Colossal.UI.Binding;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Tools;
using Game.UI;
using Unity.Entities;

namespace CitizenStories
{
    // Read-only: looks at the entity the player has selected and, when it is a citizen,
    // turns the game's own component data into a short story. It never writes to any entity.
    public partial class CitizenStoriesUISystem : UISystemBase
    {
        private ToolSystem m_ToolSystem;
        private ValueBinding<bool> m_HasStory;
        private ValueBinding<string> m_Headline;
        private ValueBinding<string> m_Story;
        private Entity m_LastEntity = Entity.Null;
        private int m_Tick;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            AddBinding(m_HasStory = new ValueBinding<bool>("citizenStories", "hasStory", false));
            AddBinding(m_Headline = new ValueBinding<string>("citizenStories", "headline", ""));
            AddBinding(m_Story = new ValueBinding<string>("citizenStories", "story", ""));
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            // Selection changes are rare; re-reading twice a second is plenty for the
            // "what are they doing now" sentence and costs nothing.
            if (++m_Tick % 30 != 0) return;

            Entity selected = m_ToolSystem != null ? m_ToolSystem.selected : Entity.Null;
            if (selected == Entity.Null || !EntityManager.Exists(selected) || !EntityManager.HasComponent<Citizen>(selected))
            {
                if (m_HasStory.value) { m_HasStory.Update(false); m_LastEntity = Entity.Null; }
                return;
            }

            try
            {
                CitizenFacts facts = ReadFacts(selected);
                string story = StoryEngine.Write(facts);
                if (selected != m_LastEntity || story != m_Story.value)
                {
                    m_Headline.Update(StoryEngine.Headline(facts));
                    m_Story.Update(story);
                }
                m_LastEntity = selected;
                if (!m_HasStory.value) m_HasStory.Update(true);
            }
            catch (Exception ex)
            {
                Mod.Log.Warn("Could not build a citizen story: " + ex.Message);
                if (m_HasStory.value) m_HasStory.Update(false);
            }
        }

        private CitizenFacts ReadFacts(Entity e)
        {
            Citizen c = EntityManager.GetComponentData<Citizen>(e);
            var f = new CitizenFacts
            {
                Seed = c.m_PseudoRandom,
                Male = (c.m_State & CitizenFlags.Male) != 0,
                Age = ReadAge(c.m_State),
                Education = ((c.m_State & CitizenFlags.EducationBit1) != 0 ? 1 : 0)
                          + ((c.m_State & CitizenFlags.EducationBit2) != 0 ? 2 : 0)
                          + ((c.m_State & CitizenFlags.EducationBit3) != 0 ? 4 : 0),
                Health = c.m_Health,
                WellBeing = c.m_WellBeing,
                Tourist = (c.m_State & CitizenFlags.Tourist) != 0,
                Commuter = (c.m_State & CitizenFlags.Commuter) != 0,
                Homeless = (c.m_State & CitizenFlags.Homeless) != 0,
                HouseholdSize = 1,
            };

            if (EntityManager.HasComponent<Game.Citizens.Student>(e)) f.IsStudent = true;
            if (EntityManager.HasComponent<Worker>(e))
            {
                f.HasJob = true;
                f.WorkCommuteMinutes = (int)(EntityManager.GetComponentData<Worker>(e).m_LastCommuteTime / 60f);
            }
            else if (f.Age == AgeGroup.Adult && !f.IsStudent && !f.Tourist)
            {
                f.Unemployed = true;
            }

            if (EntityManager.HasComponent<HealthProblem>(e))
                f.Sick = (EntityManager.GetComponentData<HealthProblem>(e).m_Flags & HealthProblemFlags.Sick) != 0;

            if (EntityManager.HasComponent<HouseholdMember>(e))
            {
                Entity hh = EntityManager.GetComponentData<HouseholdMember>(e).m_Household;
                if (hh != Entity.Null && EntityManager.Exists(hh))
                {
                    if (EntityManager.HasBuffer<HouseholdCitizen>(hh))
                        f.HouseholdSize = Math.Max(1, EntityManager.GetBuffer<HouseholdCitizen>(hh, true).Length);
                    if (EntityManager.HasComponent<PropertyRenter>(hh))
                        f.Rent = Math.Max(0, EntityManager.GetComponentData<PropertyRenter>(hh).m_Rent);
                }
            }

            if (EntityManager.HasComponent<TravelPurpose>(e))
                f.Doing = StoryEngine.DescribeActivity(EntityManager.GetComponentData<TravelPurpose>(e).m_Purpose.ToString());
            return f;
        }

        private static AgeGroup ReadAge(CitizenFlags s)
        {
            int v = ((s & CitizenFlags.AgeBit1) != 0 ? 1 : 0) + ((s & CitizenFlags.AgeBit2) != 0 ? 2 : 0);
            return (AgeGroup)v; // Child, Teen, Adult, Elderly in the game's own order
        }
    }
}

using System.Collections.Generic;
using Colossal;

namespace RoadRules
{
    public class LocaleEN : IDictionarySource
    {
        private readonly RoadRulesSetting m_Setting;

        public LocaleEN(RoadRulesSetting setting)
        {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "Road Rules" },
                { m_Setting.GetOptionTabLocaleID(RoadRulesSetting.kSection), "Main" },
                { m_Setting.GetOptionGroupLocaleID(RoadRulesSetting.kMainGroup), "Options" },
                { m_Setting.GetOptionLabelLocaleID(nameof(RoadRulesSetting.ShowOverlays)), "Show In-World Restriction Overlays" },
                { m_Setting.GetOptionDescLocaleID(nameof(RoadRulesSetting.ShowOverlays)), "Display vehicle prohibition symbols over restricted lanes." }
            };
        }

        public void Unload()
        {
        }
    }
}

using System;
using System.Collections.Generic;
using Colossal.Serialization.Entities;
using Colossal.UI.Binding;
using Game;
using Game.UI;

namespace SaveGuard
{
    public partial class SaveGuardUISystem : UISystemBase
    {
        private ValueBinding<string> m_StatusBinding;
        private ValueBinding<int> m_SaveCountBinding;

        // Selected Save Item Bindings
        private ValueBinding<string> m_SelFileNameBinding;
        private ValueBinding<string> m_SelCityNameBinding;
        private ValueBinding<string> m_SelFullPathBinding;
        private ValueBinding<float> m_SelSizeMbBinding;
        private ValueBinding<string> m_SelTimestampBinding;
        private ValueBinding<string> m_SelHealthBinding;
        private ValueBinding<string> m_SelTypeBinding;

        // Health Diagnostic Bindings
        private ValueBinding<string> m_DiagSummaryBinding;
        private ValueBinding<string> m_DiagCategoryBinding;
        private ValueBinding<string> m_DiagActionBinding;
        private ValueBinding<string> m_DiagIntegrityBinding;
        private ValueBinding<bool> m_DiagIsCorruptedBinding;
        private ValueBinding<string> m_TestRecoveryResultBinding;

        private RawValueBinding m_TimelineBinding;
        private bool m_IsPanelOpen;

        protected override void OnCreate()
        {
            base.OnCreate();

            AddBinding(m_StatusBinding = new ValueBinding<string>("SaveGuard", "status", "SaveGuard 1.2.4-beta.1 active"));
            AddBinding(m_SaveCountBinding = new ValueBinding<int>("SaveGuard", "saveCount", 0));

            AddBinding(m_SelFileNameBinding = new ValueBinding<string>("SaveGuard", "selFileName", "None"));
            AddBinding(m_SelCityNameBinding = new ValueBinding<string>("SaveGuard", "selCityName", "None"));
            AddBinding(m_SelFullPathBinding = new ValueBinding<string>("SaveGuard", "selFullPath", "-"));
            AddBinding(m_SelSizeMbBinding = new ValueBinding<float>("SaveGuard", "selSizeMb", 0f));
            AddBinding(m_SelTimestampBinding = new ValueBinding<string>("SaveGuard", "selTimestamp", "-"));
            AddBinding(m_SelHealthBinding = new ValueBinding<string>("SaveGuard", "selHealth", "-"));
            AddBinding(m_SelTypeBinding = new ValueBinding<string>("SaveGuard", "selType", "-"));

            AddBinding(m_DiagSummaryBinding = new ValueBinding<string>("SaveGuard", "diagSummary", "-"));
            AddBinding(m_DiagCategoryBinding = new ValueBinding<string>("SaveGuard", "diagCategory", "-"));
            AddBinding(m_DiagActionBinding = new ValueBinding<string>("SaveGuard", "diagAction", "-"));
            AddBinding(m_DiagIntegrityBinding = new ValueBinding<string>("SaveGuard", "diagIntegrity", "-"));
            AddBinding(m_DiagIsCorruptedBinding = new ValueBinding<bool>("SaveGuard", "diagIsCorrupted", false));
            AddBinding(m_TestRecoveryResultBinding = new ValueBinding<string>("SaveGuard", "testRecoveryResult", ""));

            AddBinding(m_TimelineBinding = new RawValueBinding("SaveGuard", "timeline", WriteTimeline));

            AddBinding(m_PanelOpenBinding = new ValueBinding<bool>("SaveGuard", "panelOpen", false));
            m_IsPanelOpen = false;
            AddBinding(new TriggerBinding("SaveGuard", "togglePanel", OnTogglePanel));
            AddBinding(new TriggerBinding("SaveGuard", "closePanel", OnClosePanel));

            AddBinding(new TriggerBinding<string>("SaveGuard", "selectSave", OnSelectSave));
            AddBinding(new TriggerBinding("SaveGuard", "createRestorePoint", OnCreateRestorePoint));
            AddBinding(new TriggerBinding("SaveGuard", "restoreAsCopy", OnRestoreAsCopy));
            AddBinding(new TriggerBinding("SaveGuard", "testRecovery", OnTestRecovery));
            AddBinding(new TriggerBinding("SaveGuard", "refresh", OnRefresh));
        }

        private ValueBinding<bool> m_PanelOpenBinding;

        private void OnTogglePanel()
        {
            if (m_PanelOpenBinding != null)
            {
                m_IsPanelOpen = !m_IsPanelOpen;
                m_PanelOpenBinding.Update(m_IsPanelOpen);
                Mod.Log.Info($"SaveGuard panel {(m_IsPanelOpen ? "opened" : "closed")}.");
            }
        }

        private void OnClosePanel()
        {
            if (m_PanelOpenBinding != null)
            {
                m_IsPanelOpen = false;
                m_PanelOpenBinding.Update(false);
            }
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            if (World == null || !World.IsCreated) return;
            base.OnGamePreload(purpose, mode);
            m_IsPanelOpen = false;
            m_PanelOpenBinding?.Update(false);
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            var sys = SaveGuardSystem.Instance;
            if (sys != null)
            {
                m_StatusBinding.Update(sys.LastStatus);
                m_SaveCountBinding.Update(sys.TimelineEntries.Count);
                m_TestRecoveryResultBinding.Update(sys.TestRecoveryResult);

                if (sys.SelectedEntry.HasValue)
                {
                    var e = sys.SelectedEntry.Value;
                    m_SelFileNameBinding.Update(e.FileName);
                    m_SelCityNameBinding.Update(e.CityName);
                    m_SelFullPathBinding.Update(e.FullPath);
                    m_SelSizeMbBinding.Update((float)Math.Round(e.SizeBytes / (1024f * 1024f), 1));
                    m_SelTimestampBinding.Update(e.Timestamp.ToString("g"));
                    m_SelHealthBinding.Update(e.Health.ToString().ToUpperInvariant());
                    m_SelTypeBinding.Update(e.SaveType);
                }
                else
                {
                    m_SelFileNameBinding.Update("No save selected");
                    m_SelCityNameBinding.Update("-");
                    m_SelFullPathBinding.Update("-");
                    m_SelSizeMbBinding.Update(0f);
                    m_SelTimestampBinding.Update("-");
                    m_SelHealthBinding.Update("-");
                    m_SelTypeBinding.Update("-");
                }

                var diag = sys.CurrentHealthReport;
                m_DiagSummaryBinding.Update(diag.DiagnosticSummary ?? "-");
                m_DiagCategoryBinding.Update(diag.FailureCategory ?? "None");
                m_DiagActionBinding.Update(diag.RecommendedAction ?? "-");
                m_DiagIntegrityBinding.Update(diag.IntegrityDetails ?? "-");
                m_DiagIsCorruptedBinding.Update(diag.IsSaveCorrupted);

                m_TimelineBinding.Update();
            }
        }

        private void WriteTimeline(IJsonWriter writer)
        {
            var list = SaveGuardSystem.Instance?.TimelineEntries ?? new List<SaveTimelineEntry>();
            writer.ArrayBegin(list.Count);
            foreach (var item in list)
            {
                writer.TypeBegin("SaveTimelineEntry");
                writer.PropertyName("fileName");
                writer.Write(item.FileName);
                writer.PropertyName("cityName");
                writer.Write(item.CityName);
                writer.PropertyName("fullPath");
                writer.Write(item.FullPath);
                writer.PropertyName("sizeMb");
                writer.Write((float)Math.Round(item.SizeBytes / (1024f * 1024f), 1));
                writer.PropertyName("timestamp");
                writer.Write(item.Timestamp.ToString("g"));
                writer.PropertyName("health");
                writer.Write(item.Health.ToString().ToUpperInvariant());
                writer.PropertyName("saveType");
                writer.Write(item.SaveType);
                writer.TypeEnd();
            }
            writer.ArrayEnd();
        }

        private void OnSelectSave(string fullPath) => SaveGuardSystem.Instance?.SelectEntry(fullPath);
        private void OnCreateRestorePoint() => SaveGuardSystem.Instance?.CreateRestorePoint();
        private void OnRestoreAsCopy() => SaveGuardSystem.Instance?.RestoreAsCopy();
        private void OnTestRecovery() => SaveGuardSystem.Instance?.TestRecoveryDryRun();
        private void OnRefresh() => SaveGuardSystem.Instance?.RefreshTimeline();

        protected override void OnDestroy()
        {
            base.OnDestroy();
        }
    }
}

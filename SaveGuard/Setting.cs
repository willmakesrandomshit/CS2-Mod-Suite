using System;
using System.Collections.Generic;
using System.IO;
using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;
using Game.UI.Widgets;

namespace SaveGuard
{
    [FileLocation(nameof(SaveGuard))]
    [SettingsUIGroupOrder(kMainGroup, kBackupGroup, kRecoveryGroup)]
    [SettingsUIShowGroupName(kMainGroup, kBackupGroup, kRecoveryGroup)]
    public class SaveGuardSetting : ModSetting
    {
        public const string kSection = "Main";
        public const string kMainGroup = "General";
        public const string kBackupGroup = "Backups";
        public const string kRecoveryGroup = "Safety & Recovery";

        public enum BackupRetention
        {
            Three = 3,
            Five = 5,
            Ten = 10,
            Twenty = 20
        }

        public SaveGuardSetting(IMod mod) : base(mod)
        {
            SetDefaults();
        }

        [SettingsUIHidden]
        public bool EnableAutoBackups { get; set; } = true;

        [SettingsUIHidden]
        public bool PreSaveValidation { get; set; } = true;

        [SettingsUISection(kSection, kBackupGroup)]
        public BackupRetention RetentionLimit { get; set; } = BackupRetention.Ten;

        [SettingsUIHidden]
        public bool BackupOnAutosave { get; set; } = true;

        [SettingsUIHidden]
        public bool BackupOnManualSave { get; set; } = true;

        [SettingsUIHidden]
        public bool CrashLensIntegration { get; set; } = true;

        [SettingsUIButton]
        [SettingsUISection(kSection, kRecoveryGroup)]
        public bool OpenBackupsFolder
        {
            set
            {
                try
                {
                    string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "..", "LocalLow", "Colossal Order", "Cities Skylines II", "SaveBackups", "SaveGuard");
                    string fullPath = Path.GetFullPath(path);
                    if (!Directory.Exists(fullPath)) Directory.CreateDirectory(fullPath);
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = fullPath, UseShellExecute = true });
                }
                catch { }
            }
        }

        public override void SetDefaults()
        {
            EnableAutoBackups = true;
            PreSaveValidation = true;
            RetentionLimit = BackupRetention.Ten;
            BackupOnAutosave = true;
            BackupOnManualSave = true;
            CrashLensIntegration = true;
        }
    }

    public class LocaleEN : IDictionarySource
    {
        private readonly SaveGuardSetting m_Setting;
        public LocaleEN(SaveGuardSetting setting) { m_Setting = setting; }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "SaveGuard" },
                { m_Setting.GetOptionTabLocaleID(SaveGuardSetting.kSection), "Main" },
                { m_Setting.GetOptionGroupLocaleID(SaveGuardSetting.kMainGroup), "General" },
                { m_Setting.GetOptionGroupLocaleID(SaveGuardSetting.kBackupGroup), "Backups" },
                { m_Setting.GetOptionGroupLocaleID(SaveGuardSetting.kRecoveryGroup), "Safety & Recovery" },
                { m_Setting.GetOptionLabelLocaleID(nameof(SaveGuardSetting.EnableAutoBackups)), "Enable Automatic Backups" },
                { m_Setting.GetOptionDescLocaleID(nameof(SaveGuardSetting.EnableAutoBackups)), "Creates versioned backups before writing saves." },
                { m_Setting.GetOptionLabelLocaleID(nameof(SaveGuardSetting.PreSaveValidation)), "Pre-Save State Validation" },
                { m_Setting.GetOptionDescLocaleID(nameof(SaveGuardSetting.PreSaveValidation)), "Validates ECS data structures before save commitment." },
                { m_Setting.GetOptionLabelLocaleID(nameof(SaveGuardSetting.RetentionLimit)), "Manual Restore-Point Retention" },
                { m_Setting.GetOptionDescLocaleID(nameof(SaveGuardSetting.RetentionLimit)), "Maximum number of restore points retained across the local SaveGuard folder." },
                { m_Setting.GetOptionLabelLocaleID(nameof(SaveGuardSetting.BackupOnAutosave)), "Backup on Autosave" },
                { m_Setting.GetOptionDescLocaleID(nameof(SaveGuardSetting.BackupOnAutosave)), "Preserves backup copy during periodic autosaves." },
                { m_Setting.GetOptionLabelLocaleID(nameof(SaveGuardSetting.BackupOnManualSave)), "Backup on Manual Save" },
                { m_Setting.GetOptionDescLocaleID(nameof(SaveGuardSetting.BackupOnManualSave)), "Preserves backup copy during manual user saves." },
                { m_Setting.GetOptionLabelLocaleID(nameof(SaveGuardSetting.CrashLensIntegration)), "CrashLens Cross-Diagnostics" },
                { m_Setting.GetOptionDescLocaleID(nameof(SaveGuardSetting.CrashLensIntegration)), "Shares save integrity diagnostics with CrashLens." },
                { m_Setting.GetOptionLabelLocaleID(nameof(SaveGuardSetting.OpenBackupsFolder)), "Open Backups Folder" },
                { m_Setting.GetOptionDescLocaleID(nameof(SaveGuardSetting.OpenBackupsFolder)), "Opens the local folder where SaveGuard snapshots are stored." },
                { m_Setting.GetEnumValueLocaleID(SaveGuardSetting.BackupRetention.Three), "3 Backups" },
                { m_Setting.GetEnumValueLocaleID(SaveGuardSetting.BackupRetention.Five), "5 Backups" },
                { m_Setting.GetEnumValueLocaleID(SaveGuardSetting.BackupRetention.Ten), "10 Backups" },
                { m_Setting.GetEnumValueLocaleID(SaveGuardSetting.BackupRetention.Twenty), "20 Backups" }
            };
        }

        public void Unload() { }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using Colossal.Logging;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Unity.Entities;

namespace SaveGuard
{
    public enum SaveHealthState { Healthy, Warning, Unknown, RecoveryRecommended }

    public struct SaveTimelineEntry
    {
        public string FileName;
        public string CityName;
        public string FullPath;
        public long SizeBytes;
        public DateTime Timestamp;
        public SaveHealthState Health;
        public string SaveType; // Current, Previous, Autosave, RestorePoint, RecoveredCopy
        public string ActiveModsSummary;
    }

    public struct HealthDiagnostic
    {
        public string DiagnosticSummary;
        public string FailureCategory; // Serialization, MissingDependency, StaleEcsSystem, ModInitFailure, SchemaFailure, None
        public string RecommendedAction;
        public string IntegrityDetails;
        public bool IsSaveCorrupted;
        public DateTime Timestamp;
    }

    public partial class SaveGuardSystem : GameSystemBase
    {
        public static ILog Log = LogManager.GetLogger(nameof(SaveGuard)).SetShowsErrorsInUI(false);
        public static SaveGuardSystem Instance { get; private set; }

        private string m_SavesRoot;
        private string m_BackupsRoot;
        private string m_RestorePointsRoot;
        private string m_CrashLensReportsRoot;

        private float m_ScanTimer = 0f;
        private const float kScanInterval = 30f;

        public List<SaveTimelineEntry> TimelineEntries { get; private set; } = new List<SaveTimelineEntry>();
        public SaveTimelineEntry? SelectedEntry { get; private set; }
        public HealthDiagnostic CurrentHealthReport { get; private set; }
        public string LastStatus { get; private set; } = "SaveGuard 1.2.4-beta.1 active — non-destructive protection engaged";
        public string TestRecoveryResult { get; private set; } = "";

        protected override void OnCreate()
        {
            base.OnCreate();
            Instance = this;

            string localLow = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "..", "LocalLow", "Colossal Order", "Cities Skylines II");
            m_SavesRoot = Path.GetFullPath(Path.Combine(localLow, "Saves"));
            m_BackupsRoot = Path.GetFullPath(Path.Combine(localLow, "SaveBackups", "SaveGuard"));
            m_RestorePointsRoot = Path.GetFullPath(Path.Combine(m_BackupsRoot, "RestorePoints"));
            m_CrashLensReportsRoot = Path.GetFullPath(Path.Combine(localLow, "ModsData", "CrashLens"));

            if (!Directory.Exists(m_BackupsRoot)) Directory.CreateDirectory(m_BackupsRoot);
            if (!Directory.Exists(m_RestorePointsRoot)) Directory.CreateDirectory(m_RestorePointsRoot);

            RefreshTimeline();
            Log.Info("SaveGuard 1.2.4-beta.1 system initialized. Non-destructive recovery-copy workflow active.");
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            if (World == null || !World.IsCreated) return;
            base.OnGamePreload(purpose, mode);
        }

        protected override void OnUpdate()
        {
            m_ScanTimer += UnityEngine.Time.deltaTime;
            if (m_ScanTimer >= kScanInterval)
            {
                m_ScanTimer = 0f;
                RefreshTimeline();
            }
        }

        public void RefreshTimeline()
        {
            try
            {
                var entries = new List<SaveTimelineEntry>();

                // Scan live saves
                if (Directory.Exists(m_SavesRoot))
                {
                    var files = Directory.GetFiles(m_SavesRoot, "*.cok", SearchOption.AllDirectories);
                    foreach (var f in files)
                    {
                        var fi = new FileInfo(f);
                        string fileName = Path.GetFileName(f);
                        string cityName = Path.GetFileNameWithoutExtension(f);

                        SaveHealthState health = SaveHealthState.Healthy;
                        if (fi.Length < 1024) health = SaveHealthState.Warning;
                        if (fileName.Contains("_RECOVERED_") || fileName.Contains("_RESTORED_")) health = SaveHealthState.Healthy;

                        string type = fileName.StartsWith("autoSave", StringComparison.OrdinalIgnoreCase) ? "Autosave"
                            : (fileName.Contains("_RESTORED_") || fileName.Contains("_RECOVERED_") ? "RecoveredCopy" : "Current");

                        entries.Add(new SaveTimelineEntry
                        {
                            FileName = fileName,
                            CityName = cityName,
                            FullPath = f,
                            SizeBytes = fi.Length,
                            Timestamp = fi.LastWriteTime,
                            Health = health,
                            SaveType = type,
                            ActiveModsSummary = "Readable Save Package"
                        });
                    }
                }

                // Scan restore points & backups
                if (Directory.Exists(m_BackupsRoot))
                {
                    var backupFiles = Directory.GetFiles(m_BackupsRoot, "*.cok", SearchOption.AllDirectories);
                    foreach (var f in backupFiles)
                    {
                        var fi = new FileInfo(f);
                        string fileName = Path.GetFileName(f);
                        string type = f.Contains("RestorePoints") ? "RestorePoint" : "Previous";

                        entries.Add(new SaveTimelineEntry
                        {
                            FileName = fileName,
                            CityName = Path.GetFileNameWithoutExtension(f),
                            FullPath = f,
                            SizeBytes = fi.Length,
                            Timestamp = fi.LastWriteTime,
                            Health = SaveHealthState.Unknown,
                            SaveType = type,
                            ActiveModsSummary = "Backup Snapshot"
                        });
                    }
                }

                TimelineEntries = entries.OrderByDescending(e => e.Timestamp).ToList();
                if (TimelineEntries.Count > 0 && !SelectedEntry.HasValue)
                {
                    SelectEntry(TimelineEntries[0].FullPath);
                }
            }
            catch (Exception ex)
            {
                LastStatus = $"Timeline scan error: {ex.Message}";
            }
        }

        public void SelectEntry(string fullPath)
        {
            int selectedIndex = TimelineEntries.FindIndex(e => e.FullPath == fullPath);
            SelectedEntry = selectedIndex < 0 ? (SaveTimelineEntry?)null : TimelineEntries[selectedIndex];
            TestRecoveryResult = "";

            if (SelectedEntry.HasValue)
            {
                EvaluateSaveHealth(SelectedEntry.Value);
            }
        }

        private void EvaluateSaveHealth(SaveTimelineEntry entry)
        {
            try
            {
                if (!File.Exists(entry.FullPath))
                {
                    CurrentHealthReport = new HealthDiagnostic
                    {
                        DiagnosticSummary = "Save file not found at target location.",
                        FailureCategory = "None",
                        RecommendedAction = "Verify local storage path.",
                        IntegrityDetails = "File missing",
                        IsSaveCorrupted = true,
                        Timestamp = DateTime.Now
                    };
                    return;
                }

                var fi = new FileInfo(entry.FullPath);
                if (fi.Length < 1024)
                {
                    CurrentHealthReport = new HealthDiagnostic
                    {
                        DiagnosticSummary = "Save file size is abnormally small (< 1 KB).",
                        FailureCategory = "Serialization",
                        RecommendedAction = "Restore from previous backup snapshot.",
                        IntegrityDetails = $"File size: {fi.Length} bytes (Truncated)",
                        IsSaveCorrupted = true,
                        Timestamp = DateTime.Now
                    };
                    return;
                }

                // Verify zip/container integrity
                bool validContainer = false;
                int entryCount = 0;
                try
                {
                    using (var zip = ZipFile.OpenRead(entry.FullPath))
                    {
                        entryCount = zip.Entries.Count;
                        validContainer = entryCount > 0;
                    }
                }
                catch { validContainer = false; }

                if (!validContainer)
                {
                    CurrentHealthReport = new HealthDiagnostic
                    {
                        DiagnosticSummary = "Save archive header is unreadable or malformed.",
                        FailureCategory = "SchemaFailure",
                        RecommendedAction = "Use [ RESTORE AS COPY ] from an earlier restore point.",
                        IntegrityDetails = "Archive compression structure damaged",
                        IsSaveCorrupted = true,
                        Timestamp = DateTime.Now
                    };
                    return;
                }

                // Opening the container verifies only its basic package structure. A complete
                // CS2 load is the only reliable end-to-end recovery test.
                CurrentHealthReport = new HealthDiagnostic
                {
                    DiagnosticSummary = "Save package structure is readable. Full in-game loading has not been verified.",
                    FailureCategory = "None",
                    RecommendedAction = "Create a restore point before testing this save in CS2.",
                    IntegrityDetails = $"Archive entries: {entryCount} | Size: {fi.Length / (1024 * 1024f):F1} MB",
                    IsSaveCorrupted = false,
                    Timestamp = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                CurrentHealthReport = new HealthDiagnostic
                {
                    DiagnosticSummary = $"Diagnostic scan encountered error: {ex.Message}",
                    FailureCategory = "None",
                    RecommendedAction = "Inspect local file permissions.",
                    IntegrityDetails = ex.Message,
                    IsSaveCorrupted = false,
                    Timestamp = DateTime.Now
                };
            }
        }

        public string CreateRestorePoint()
        {
            try
            {
                if (!SelectedEntry.HasValue || !File.Exists(SelectedEntry.Value.FullPath))
                {
                    LastStatus = "No valid save selected for restore point.";
                    return "";
                }

                string src = SelectedEntry.Value.FullPath;
                string baseName = Path.GetFileNameWithoutExtension(src);
                string timeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string dest = Path.Combine(m_RestorePointsRoot, $"{baseName}_RESTORE_POINT_{timeStr}.cok");

                int retentionLimit = Mod.SettingInstance == null
                    ? 10
                    : Math.Max(1, (int)Mod.SettingInstance.RetentionLimit);

                // Create the replacement before pruning. The old implementation deleted the
                // oldest restore point first, so a failed copy could reduce the user's recovery
                // history—and could even delete the selected source when copying a restore point.
                dest = GetUniqueDestination(dest);
                File.Copy(src, dest, false);

                // Enforce retention only after a complete new copy exists.
                var existing = Directory.GetFiles(m_RestorePointsRoot, "*.cok")
                    .OrderBy(f => File.GetCreationTimeUtc(f))
                    .ToList();
                while (existing.Count > retentionLimit)
                {
                    string prune = existing[0];
                    existing.RemoveAt(0);
                    if (!string.Equals(prune, dest, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Delete(prune);
                    }
                }

                RefreshTimeline();
                LastStatus = $"Restore point created: {Path.GetFileName(dest)}";
                return dest;
            }
            catch (Exception ex)
            {
                LastStatus = $"Failed to create restore point: {ex.Message}";
                return "";
            }
        }

        public string RestoreAsCopy()
        {
            try
            {
                if (!SelectedEntry.HasValue || !File.Exists(SelectedEntry.Value.FullPath))
                {
                    LastStatus = "No valid save selected.";
                    return "";
                }

                string src = SelectedEntry.Value.FullPath;
                string baseName = Path.GetFileNameWithoutExtension(SelectedEntry.Value.FileName);
                string timeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string dest = Path.Combine(m_SavesRoot, $"{baseName}_RESTORED_{timeStr}.cok");

                // Non-destructive copy to saves folder
                Directory.CreateDirectory(m_SavesRoot);
                dest = GetUniqueDestination(dest);
                File.Copy(src, dest, false);
                RefreshTimeline();
                SelectEntry(dest);
                LastStatus = $"Successfully restored copy: {Path.GetFileName(dest)} (Original untouched)";
                return dest;
            }
            catch (Exception ex)
            {
                LastStatus = $"Restore copy failed: {ex.Message}";
                return "";
            }
        }

        public void TestRecoveryDryRun()
        {
            try
            {
                if (!SelectedEntry.HasValue || !File.Exists(SelectedEntry.Value.FullPath))
                {
                    TestRecoveryResult = "Test failed: Target save not found.";
                    return;
                }

                using (var zip = ZipFile.OpenRead(SelectedEntry.Value.FullPath))
                {
                    int checkedEntries = 0;
                    long checkedBytes = 0;
                    byte[] buffer = new byte[64 * 1024];
                    foreach (var entry in zip.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) continue;
                        using (var stream = entry.Open())
                        {
                            int read;
                            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                checkedBytes += read;
                            }
                        }
                        checkedEntries++;
                    }
                    TestRecoveryResult = $"PACKAGE READ PASSED: Read {checkedEntries} archive entries ({checkedBytes / (1024f * 1024f):F1} MB). This does not guarantee that CS2 can load the save; test the restored copy in-game.";
                }
            }
            catch (Exception ex)
            {
                TestRecoveryResult = $"VALIDATION FAILED: Archive test failed with error: {ex.Message}";
            }
        }

        private static string GetUniqueDestination(string requestedPath)
        {
            if (!File.Exists(requestedPath)) return requestedPath;

            string directory = Path.GetDirectoryName(requestedPath);
            string fileName = Path.GetFileNameWithoutExtension(requestedPath);
            string extension = Path.GetExtension(requestedPath);
            for (int suffix = 2; suffix < 10000; suffix++)
            {
                string candidate = Path.Combine(directory, $"{fileName}_{suffix}{extension}");
                if (!File.Exists(candidate)) return candidate;
            }

            return Path.Combine(directory, $"{fileName}_{Guid.NewGuid():N}{extension}");
        }

        protected override void OnDestroy()
        {
            Instance = null;
            base.OnDestroy();
        }
    }
}

using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using System.Collections.Generic;

namespace FastTrack
{
    [FileLocation(nameof(FastTrack))]
    [SettingsUIGroupOrder(kCore, kRealOpts, kQualityScale, kDiagnostics, kRecovery)]
    [SettingsUIShowGroupName(kCore, kRealOpts, kQualityScale, kDiagnostics, kRecovery)]
    public sealed class FastTrackSetting : ModSetting
    {
        public const string kMain         = "Main";
        public const string kCore         = "General";
        public const string kRealOpts     = "Optimizations";
        public const string kQualityScale = "Quality Scaling";
        public const string kDiagnostics  = "Diagnostics";
        public const string kRecovery     = "Recovery";

        public FastTrackSetting(IMod mod) : base(mod) => SetDefaults();

        // ── core ──────────────────────────────────────────────────────────
        [SettingsUISection(kMain, kCore)]
        public bool Enabled { get; set; }

        [SettingsUISection(kMain, kCore)]
        [SettingsUIDisableByCondition(typeof(FastTrackSetting), nameof(IsSafeModeLocked))]
        public bool SafeMode { get; set; }

        // Retained only so older settings files deserialize cleanly. Presets no
        // longer select visual-quality reductions.
        [SettingsUIHidden]
        [SettingsUISection(kMain, kCore)]
        public OptimizationPreset Preset { get; set; }

        [SettingsUISlider(min = 20, max = 120, step = 5)]
        [SettingsUISection(kMain, kCore)]
        public int TargetFps { get; set; }

        // ── real optimizations ────────────────────────────────────────────
        [SettingsUIHidden]
        public bool ThrottleDistantShadows { get; set; }

        [SettingsUIHidden]
        public bool OptimizeDecalsAtAltitude { get; set; }

        [SettingsUIHidden]
        public bool OptimizeAsyncUpload { get; set; }

        [SettingsUISection(kMain, kRealOpts)]
        public bool ExperimentalLoadingBoost { get; set; }

        // ── removed visual features (hidden compatibility fields) ─────────
        [SettingsUIHidden]
        public bool AdaptiveVisuals { get; set; }

        [SettingsUIHidden]
        public bool AdaptiveResolution { get; set; }

        [SettingsUIHidden]
        public bool ReduceExpensiveEffects { get; set; }

        // Development-only feature-group gates used for controlled bisection.
        // Visual mutations remain hard-disabled even if a stale settings file
        // attempts to set DebugVisualMutationGroup to true.
        [SettingsUIHidden]
        public bool DebugProfilerGroup { get; set; }

        [SettingsUIHidden]
        public bool DebugLoadingGroup { get; set; }

        [SettingsUIHidden]
        public bool DebugCameraObserverGroup { get; set; }

        [SettingsUIHidden]
        public bool DebugVisualMutationGroup { get; set; }

        // ── interface ─────────────────────────────────────────────────────
        [SettingsUISection(kMain, kDiagnostics)]
        public bool ShowToolbarButton { get; set; }

        [SettingsUISection(kMain, kDiagnostics)]
        public bool DiagnosticsLogging { get; set; }

        // ── recovery ──────────────────────────────────────────────────────
        [SettingsUIButton]
        [SettingsUISection(kMain, kRecovery)]
        public bool RestoreVanilla
        {
            set { FastTrackRuntime.Disable(); }
        }

        public override void SetDefaults()
        {
            Enabled                    = true;
            SafeMode                   = true;
            Preset                     = OptimizationPreset.Balanced;
            TargetFps                  = 60;
            ThrottleDistantShadows     = false;
            OptimizeDecalsAtAltitude   = false;
            OptimizeAsyncUpload        = false;
            ExperimentalLoadingBoost   = false;
            AdaptiveVisuals            = false;
            AdaptiveResolution         = false;
            ReduceExpensiveEffects     = false;
            DebugProfilerGroup         = true;
            DebugLoadingGroup          = true;
            DebugCameraObserverGroup   = true;
            DebugVisualMutationGroup   = false;
            ShowToolbarButton          = true;
            DiagnosticsLogging         = false;
        }

        public bool IsSafeModeLocked() => true;

        /// <summary>
        /// Migrates persisted v1.3.3 settings to the vanilla-visual contract.
        /// Returns true when values changed and should be saved.
        /// </summary>
        public bool EnforceVisualContract()
        {
            bool changed = !SafeMode
                || ThrottleDistantShadows
                || OptimizeDecalsAtAltitude
                || AdaptiveVisuals
                || AdaptiveResolution
                || ReduceExpensiveEffects
                || DebugVisualMutationGroup
                || OptimizeAsyncUpload;

            SafeMode = true;
            ThrottleDistantShadows = false;
            OptimizeDecalsAtAltitude = false;
            AdaptiveVisuals = false;
            AdaptiveResolution = false;
            ReduceExpensiveEffects = false;
            DebugVisualMutationGroup = false;
            // v1.3.4 stored this as enabled. Do not silently carry that write
            // forward; the replacement is a new, explicitly opt-in setting.
            OptimizeAsyncUpload = false;
            return changed;
        }

        public override void Apply()
        {
            base.Apply();
            EnforceVisualContract();
            FastTrackRuntime.OnSettingsChanged();
        }
    }

    public enum OptimizationPreset { Quality, Balanced, Performance, Extreme }

    public sealed class LocaleEN : IDictionarySource
    {
        private readonly FastTrackSetting s;
        public LocaleEN(FastTrackSetting setting) => s = setting;
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> counts)
            => new Dictionary<string, string>
        {
            { s.GetSettingsLocaleID(),                                       "FastTrack" },
            { s.GetOptionTabLocaleID(FastTrackSetting.kMain),                "Main" },
            { s.GetOptionGroupLocaleID(FastTrackSetting.kCore),              "General" },
            { s.GetOptionGroupLocaleID(FastTrackSetting.kRealOpts),          "Optimizations" },
            { s.GetOptionGroupLocaleID(FastTrackSetting.kQualityScale),      "Quality Scaling" },
            { s.GetOptionGroupLocaleID(FastTrackSetting.kDiagnostics),       "Diagnostics" },
            { s.GetOptionGroupLocaleID(FastTrackSetting.kRecovery),          "Recovery" },

            // Core
            { s.GetOptionLabelLocaleID(nameof(FastTrackSetting.Enabled)),             "Enable FastTrack" },
            { s.GetOptionDescLocaleID(nameof(FastTrackSetting.Enabled)),              "Enables performance monitoring and optional experimental loading-budget tuning. Does not speed up simulation or change save data." },
            { s.GetOptionLabelLocaleID(nameof(FastTrackSetting.SafeMode)),            "Vanilla visual contract" },
            { s.GetOptionDescLocaleID(nameof(FastTrackSetting.SafeMode)),             "Locked on. FastTrack cannot change LOD, terrain, culling, shadows, decals, effects, render scale, or world visibility." },
            { s.GetOptionLabelLocaleID(nameof(FastTrackSetting.Preset)),              "Optimization preset" },
            { s.GetOptionDescLocaleID(nameof(FastTrackSetting.Preset)),               "Legacy setting. Visual-quality presets have been removed." },
            { s.GetOptionLabelLocaleID(nameof(FastTrackSetting.TargetFps)),           "Target FPS" },
            { s.GetOptionDescLocaleID(nameof(FastTrackSetting.TargetFps)),            "Diagnostic target used only to classify the current CPU/GPU bottleneck. It never changes visual quality." },

            // Real optimizations
            { s.GetOptionLabelLocaleID(nameof(FastTrackSetting.ThrottleDistantShadows)),   "Shadow update throttle" },
            { s.GetOptionDescLocaleID(nameof(FastTrackSetting.ThrottleDistantShadows)),    "Removed in v1.3.4 to guarantee vanilla-equivalent shadows." },
            { s.GetOptionLabelLocaleID(nameof(FastTrackSetting.OptimizeDecalsAtAltitude)), "Adaptive decal draw distance" },
            { s.GetOptionDescLocaleID(nameof(FastTrackSetting.OptimizeDecalsAtAltitude)),  "Removed in v1.3.4 to guarantee vanilla-equivalent road markings and surface detail." },
            { s.GetOptionLabelLocaleID(nameof(FastTrackSetting.OptimizeAsyncUpload)),      "Loading upload optimization" },
            { s.GetOptionDescLocaleID(nameof(FastTrackSetting.OptimizeAsyncUpload)),       "Legacy compatibility field. Disabled in v1.3.5." },
            { s.GetOptionLabelLocaleID(nameof(FastTrackSetting.ExperimentalLoadingBoost)), "Experimental loading boost (opt-in)" },
            { s.GetOptionDescLocaleID(nameof(FastTrackSetting.ExperimentalLoadingBoost)),  "Optionally raises Unity's async upload budget only during city loading, then restores captured values. Leave off if diagnosing GPU or loading instability." },

            // Quality scalers
            { s.GetOptionLabelLocaleID(nameof(FastTrackSetting.AdaptiveVisuals)),     "Adaptive distance detail (LOD)" },
            { s.GetOptionDescLocaleID(nameof(FastTrackSetting.AdaptiveVisuals)),      "Removed in v1.3.4. FastTrack no longer changes global LOD or shadow distance." },
            { s.GetOptionLabelLocaleID(nameof(FastTrackSetting.AdaptiveResolution)),  "Adaptive render resolution (disabled)" },
            { s.GetOptionDescLocaleID(nameof(FastTrackSetting.AdaptiveResolution)),   "Retained for settings compatibility but intentionally disabled. Runtime buffer resizing is unsafe with Cities: Skylines II's HDRP custom passes." },
            { s.GetOptionLabelLocaleID(nameof(FastTrackSetting.ReduceExpensiveEffects)), "Reduce effects under extreme pressure" },
            { s.GetOptionDescLocaleID(nameof(FastTrackSetting.ReduceExpensiveEffects)),  "Removed in v1.3.4. FastTrack no longer changes effects quality." },

            // Diagnostics
            { s.GetOptionLabelLocaleID(nameof(FastTrackSetting.ShowToolbarButton)),   "Show FastTrack toolbar button" },
            { s.GetOptionDescLocaleID(nameof(FastTrackSetting.ShowToolbarButton)),    "Shows the FT button in the game toolbar." },
            { s.GetOptionLabelLocaleID(nameof(FastTrackSetting.DiagnosticsLogging)),  "Diagnostics logging" },
            { s.GetOptionDescLocaleID(nameof(FastTrackSetting.DiagnosticsLogging)),   "Writes sampled performance counters every 60 seconds. These are diagnostic clues, not proof of a bottleneck or speedup." },

            // Recovery
            { s.GetOptionLabelLocaleID(nameof(FastTrackSetting.RestoreVanilla)),      "Stop and restore vanilla" },
            { s.GetOptionDescLocaleID(nameof(FastTrackSetting.RestoreVanilla)),       "Pauses monitoring and restores an active loading-budget experiment. FastTrack does not write visual-quality or HDRP settings." },

            // Preset enum
            { s.GetEnumValueLocaleID(OptimizationPreset.Quality),      "Quality"      },
            { s.GetEnumValueLocaleID(OptimizationPreset.Balanced),     "Balanced"     },
            { s.GetEnumValueLocaleID(OptimizationPreset.Performance),  "Performance"  },
            { s.GetEnumValueLocaleID(OptimizationPreset.Extreme),      "Extreme"      },
        };

        public void Unload() { }
    }
}

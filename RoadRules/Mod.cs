using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Unity.Entities;

namespace RoadRules
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger(nameof(RoadRules)).SetShowsErrorsInUI(false);
        public static RoadRulesSetting setting { get; private set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info("[RoadRules] MOD ONLOAD EXECUTED");
            log.Info("Road Rules v1.4.5-beta.1 BETA loaded");
            log.Info("[RoadRules] Rule translation harness: " + RoadRuleTranslator.RunTranslationHarness());
            log.Info("[RoadRules] Physical lane cross-section harness: " + PhysicalLaneCrossSection.RunRegressionHarness());



            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"[RoadRules] Executable asset location: {asset.path}");

            // ModSetting registers its instance by mod ID in its constructor.
            // Construct defaults first so the UI resolves the live settings object.
            var defaults = new RoadRulesSetting(this);
            setting = new RoadRulesSetting(this);
            setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(setting));

            AssetDatabase.global.LoadSettings(nameof(RoadRules), setting, defaults);

            // Register systems in appropriate simulation phases
            updateSystem.UpdateAt<PortfolioSupportUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<RoadRulesSystem>(SystemUpdatePhase.GameSimulation);
            log.Info("[RoadRules] System registered: RoadRulesSystem (GameSimulation)");

            updateSystem.UpdateAt<RoadRulesPathfindSystem>(SystemUpdatePhase.GameSimulation);
            log.Info("[RoadRules] System registered: RoadRulesPathfindSystem (GameSimulation)");

            updateSystem.UpdateAt<RoadRulesToolSystem>(SystemUpdatePhase.ToolUpdate);
            log.Info("[RoadRules] System registered: RoadRulesToolSystem (ToolUpdate)");

            updateSystem.UpdateAt<RoadRulesOverlaySystem>(SystemUpdatePhase.Rendering);
            log.Info("[RoadRules] System registered: RoadRulesOverlaySystem (Rendering)");

            updateSystem.UpdateAt<RoadRulesUISystem>(SystemUpdatePhase.UIUpdate);
            log.Info("[RoadRules] System registered: RoadRulesUISystem (UIUpdate)");

            // Register with SuiteBridge
            Colossal.UtilitySuite.Interop.SuiteBridge.Register("RoadRules", (entityIndex, pos) =>
            {
                var tool = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<RoadRulesToolSystem>();
                var toolSys = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<Game.Tools.ToolSystem>();
                if (tool != null && toolSys != null)
                {
                    toolSys.activeTool = tool;
                }
            });

            log.Info("[RoadRules] Mod initialization complete — all systems and UI registered.");
        }

        public void OnDispose()
        {
            log.Info("Road Rules disposing.");
            RoadRulesPathfindSystem.Instance?.RestoreAllToVanilla("mod disposal", queueNativeRebuilds: true);
            Colossal.UtilitySuite.Interop.SuiteBridge.Unregister("RoadRules");
            if (setting != null)
            {
                setting.UnregisterInOptionsUI();
                setting = null;
            }
        }
    }
}

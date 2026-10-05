using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;

namespace DiscordRPC
{
    public sealed class Mod : IMod
    {
        public const string Version = "0.9.0-beta.1";
        public static readonly ILog Log = LogManager.GetLogger(nameof(DiscordRPC)).SetShowsErrorsInUI(false);
        public static DiscordRPCSetting Settings { get; private set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info($"Discord RPC {Version} loading");
            Settings = new DiscordRPCSetting(this);
            Settings.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(Settings));
            AssetDatabase.global.LoadSettings(nameof(DiscordRPC), Settings, new DiscordRPCSetting(this));

            updateSystem.UpdateAt<PortfolioSupportUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<DiscordRPCSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<DiscordRPCUISystem>(SystemUpdatePhase.UIUpdate);
        }

        public void OnDispose()
        {
            DiscordRPCIntegration.ClearAll();
            Settings?.UnregisterInOptionsUI();
            Settings = null;
            Log.Info("Discord RPC disposed; vanilla rich presence ownership restored.");
        }
    }
}

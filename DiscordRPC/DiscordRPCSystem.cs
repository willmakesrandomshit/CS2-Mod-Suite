using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using Colossal.PSI.Common;
using Colossal.PSI.Discord;
using Colossal.Serialization.Entities;
using Discord;
using Game;
using Game.City;
using Game.Common;
using Game.Net;
using Game.PSI;
using Game.Rendering;
using Game.SceneFlow;
using Game.Simulation;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DiscordRPC
{
    public sealed partial class DiscordRPCSystem : GameSystemBase
    {
        private const string OfficialClientId = "1125009418476605441";
        private static readonly FieldInfo s_ActivityManagerField = typeof(DiscordRichPresence).GetField("m_ActivityManager", BindingFlags.Instance | BindingFlags.NonPublic);

        private RichPresenceUpdateSystem m_VanillaSystem;
        private DiscordRichPresence m_DiscordPresence;
        private ActivityManager m_ActivityManager;
        private CityConfigurationSystem m_CityConfiguration;
        private CitySystem m_CitySystem;
        private ClimateSystem m_ClimateSystem;
        private TimeSystem m_TimeSystem;
        private SimulationSystem m_SimulationSystem;
        private ToolSystem m_ToolSystem;
        private PhotoModeRenderSystem m_PhotoModeSystem;
        private EntityQuery m_MilestoneQuery;
        private EntityQuery m_RoadQuery;

        private DateTime m_SessionStart = DateTime.Now;
        private DateTime m_LastUpdate = DateTime.MinValue;
        private DateTime m_LastReconnect = DateTime.MinValue;
        private DateTime m_LastTrafficSample = DateTime.MinValue;
        private bool m_RestoredVanilla = true;
        private float m_TrafficFlow;

        public static DiscordRPCSystem Instance { get; private set; }
        public static string ConnectionStatus { get; private set; } = "Waiting";
        public static string ConnectionMessage { get; private set; } = "Waiting for the game's Discord service";
        public static string DetailsPreview { get; private set; } = "In the Main Menu";
        public static string StatePreview { get; private set; } = "Ready to build";
        public static string LastUpdateText { get; private set; } = "Not sent yet";
        public static string ActivitySource { get; private set; } = "Cities: Skylines II";
        public static string CurrentMode { get; private set; } = "Main Menu";
        public static bool LastCallbackSucceeded { get; private set; }
        public static long SuccessfulCallbacks { get; private set; }
        public static long FailedCallbacks { get; private set; }

        protected override void OnCreate()
        {
            base.OnCreate();
            Instance = this;
            m_VanillaSystem = World.GetOrCreateSystemManaged<RichPresenceUpdateSystem>();
            m_CityConfiguration = World.GetOrCreateSystemManaged<CityConfigurationSystem>();
            m_CitySystem = World.GetOrCreateSystemManaged<CitySystem>();
            m_ClimateSystem = World.GetOrCreateSystemManaged<ClimateSystem>();
            m_TimeSystem = World.GetOrCreateSystemManaged<TimeSystem>();
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_PhotoModeSystem = World.GetOrCreateSystemManaged<PhotoModeRenderSystem>();
            m_MilestoneQuery = GetEntityQuery(ComponentType.ReadOnly<MilestoneLevel>());
            m_RoadQuery = GetEntityQuery(ComponentType.ReadOnly<Aggregated>(), ComponentType.ReadOnly<Edge>(), ComponentType.ReadOnly<Road>(), ComponentType.Exclude<Temp>(), ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Native>());
            Mod.Log.Info($"DRPC_ARCH official_client_id={OfficialClientId} transport=shared_colossal_psi owner=PlatformManager vanilla_update=cooperative_replace");
            Mod.Log.Info($"DRPC_LIFECYCLE event=created system={nameof(DiscordRPCSystem)} world={DescribeWorld()}");
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            Mod.Log.Info($"DRPC_LIFECYCLE event=preload system={nameof(DiscordRPCSystem)} world={DescribeWorld()} purpose={purpose} mode={mode}");
            base.OnGamePreload(purpose, mode);
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            Mod.Log.Info($"DRPC_LIFECYCLE event=loading_complete system={nameof(DiscordRPCSystem)} world={DescribeWorld()} purpose={purpose} mode={mode}");
        }

        protected override void OnGameLoaded(Context serializationContext)
        {
            base.OnGameLoaded(serializationContext);
            m_SessionStart = DateTime.Now;
            m_LastUpdate = DateTime.MinValue;
            DiscordRPCRuntime.RequestImmediateUpdate();
        }

        protected override void OnUpdate()
        {
            DiscordRPCSetting settings = Mod.Settings;
            if (settings == null || !settings.Enabled)
            {
                RestoreVanilla();
                ConnectionStatus = "Disabled";
                ConnectionMessage = "Vanilla Cities: Skylines II rich presence is active";
                return;
            }

            TakeOwnership();
            EnsureDiscordClient();

            int interval = Math.Max(15, settings.UpdateIntervalSeconds);
            bool immediate = DiscordRPCRuntime.ConsumeImmediateUpdate();
            if (!immediate && (DateTime.UtcNow - m_LastUpdate).TotalSeconds < interval) return;

            PresenceSnapshot snapshot = CaptureSnapshot(settings);
            Publish(snapshot, settings);
            m_LastUpdate = DateTime.UtcNow;
        }

        private void TakeOwnership()
        {
            if (IsSystemAlive(m_VanillaSystem) && m_VanillaSystem.Enabled) m_VanillaSystem.Enabled = false;
            m_RestoredVanilla = false;
        }

        private void RestoreVanilla()
        {
            if (m_RestoredVanilla) return;
            if (IsSystemAlive(m_VanillaSystem)) m_VanillaSystem.Enabled = true;
            try { PlatformManager.instance.SetRichPresence(GameManager.instance.gameMode.ToRichPresence()); }
            catch (Exception ex) { Mod.Log.Debug(ex, "Could not immediately restore vanilla rich presence key"); }
            m_RestoredVanilla = true;
            m_ActivityManager = null;
            Mod.Log.Info("DRPC_OWNER vanilla_restored=true");
        }

        private void EnsureDiscordClient()
        {
            try
            {
                if (m_DiscordPresence == null)
                    m_DiscordPresence = PlatformManager.instance.GetPSI<DiscordRichPresence>("Discord");

                if (m_DiscordPresence == null)
                {
                    SetDisconnected("The game's Discord integration is unavailable on this platform");
                    return;
                }

                if (!m_DiscordPresence.isInitialized)
                {
                    m_ActivityManager = null;
                    SetDisconnected("Discord is not running or the Game SDK connection is unavailable");
                    if ((DateTime.UtcNow - m_LastReconnect).TotalSeconds >= 30)
                    {
                        m_LastReconnect = DateTime.UtcNow;
                        _ = m_DiscordPresence.Initialize(CancellationToken.None);
                        Mod.Log.Info("DRPC_RECONNECT requested=true owner=shared_psi");
                    }
                    return;
                }

                if (m_ActivityManager == null)
                    m_ActivityManager = s_ActivityManagerField?.GetValue(m_DiscordPresence) as ActivityManager;

                if (m_ActivityManager == null)
                {
                    SetDisconnected("Discord connected, but its activity manager is not available");
                    return;
                }

                ConnectionStatus = "Connected";
                ConnectionMessage = "Connected through the game's official Discord Game SDK client";
            }
            catch (Exception ex)
            {
                SetDisconnected(ex.GetType().Name + ": " + ex.Message);
                Mod.Log.Debug(ex, "Discord RPC connection check failed");
            }
        }

        private static void SetDisconnected(string reason)
        {
            ConnectionStatus = "Not connected";
            ConnectionMessage = reason;
        }

        private PresenceSnapshot CaptureSnapshot(DiscordRPCSetting settings)
        {
            var s = new PresenceSnapshot { SessionStart = m_SessionStart };
            GameMode mode = GameManager.instance.gameMode;
            s.Mode = mode.IsGame() ? "In Game" : mode.IsEditor() ? "Editor" : "Main Menu";
            CurrentMode = s.Mode;

            if (!mode.IsGameOrEditor())
            {
                s.Activity = "In the Main Menu";
                s.Source = "Cities: Skylines II";
                return s;
            }

            if (mode.IsEditor())
            {
                s.Activity = "Creating in the Editor";
                s.LargeImage = "editor";
                s.LargeText = "In-Game Editor";
                return s;
            }

            string actualCity = m_CityConfiguration?.cityName ?? string.Empty;
            s.City = settings.ShareCityName ? actualCity : string.Empty;

            int population = GetPopulation();
            s.Population = PresenceFormatting.FormatPopulation(population, settings.Population);
            s.PopulationLine = string.IsNullOrEmpty(s.Population) ? string.Empty : s.Population + " citizens";

            s.Money = PresenceFormatting.FormatMoney((long)(m_CitySystem?.moneyAmount ?? 0), settings.Money);
            s.MoneyLine = string.IsNullOrEmpty(s.Money) ? string.Empty : "Treasury " + s.Money;

            if (settings.ShowDate && m_TimeSystem != null)
                s.Date = m_TimeSystem.GetCurrentDateTime().ToString("MMM yyyy", CultureInfo.InvariantCulture);
            if (settings.ShowSeason && m_ClimateSystem != null)
                s.Season = m_ClimateSystem.currentSeasonName ?? string.Empty;
            if (settings.ShowWeather && m_ClimateSystem != null)
            {
                s.Weather = GetWeatherLabel();
                s.Temperature = math.round(m_ClimateSystem.temperature).ToString(CultureInfo.InvariantCulture) + "°C";
                s.WeatherLine = (s.Weather + " " + s.Temperature).Trim();
                s.SmallImage = GetWeatherIconKey();
                s.SmallText = s.WeatherLine;
            }

            if (settings.ShowTrafficFlow)
            {
                SampleTrafficFlowIfDue();
                s.Traffic = math.round(m_TrafficFlow).ToString(CultureInfo.InvariantCulture) + "%";
                s.TrafficLine = "Traffic flow " + s.Traffic;
            }

            s.Activity = settings.ShowActivity ? GetActivity(actualCity, settings.ShareCityName) : "Playing Cities: Skylines II";
            int milestone = GetMilestone();
            s.LargeImage = "milestone" + milestone;
            s.LargeText = milestone > 0 ? "Milestone " + milestone : "Building a new city";

            if (settings.DetectSuiteActivity && TryDetectSuiteActivity(out string suiteSource, out string suiteActivity, out string suiteState))
            {
                s.Source = suiteSource;
                s.Activity = suiteActivity;
                if (!string.IsNullOrWhiteSpace(suiteState)) s.TrafficLine = suiteState;
            }
            if (DiscordRPCIntegration.TryGetHighest(out string source, out string details, out string state))
            {
                s.Source = source;
                s.Activity = details;
                if (!string.IsNullOrWhiteSpace(state)) s.TrafficLine = state;
            }
            return s;
        }

        private string GetActivity(string actualCity, bool shareCity)
        {
            string suffix = shareCity && !string.IsNullOrWhiteSpace(actualCity) ? " in " + actualCity : string.Empty;
            if (m_PhotoModeSystem != null && m_PhotoModeSystem.Enabled) return "Capturing city views" + suffix;
            if (m_ToolSystem?.activeInfoview != null) return "Inspecting the city" + suffix;
            string toolId = m_ToolSystem?.activeTool?.toolID ?? string.Empty;
            if (toolId.IndexOf("Bulldoze", StringComparison.OrdinalIgnoreCase) >= 0) return "Bulldozing" + suffix;
            if (m_SimulationSystem != null && m_SimulationSystem.selectedSpeed == 0f) return "Planning while paused" + suffix;
            return "Building" + suffix;
        }

        private bool TryDetectSuiteActivity(out string source, out string activity, out string state)
        {
            source = null; activity = null; state = null;
            string toolId = m_ToolSystem?.activeTool?.toolID ?? string.Empty;
            if (toolId.IndexOf("AccessStudio", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                source = "Access Studio"; activity = "Editing building access"; return true;
            }
            if (toolId.IndexOf("RoadRules", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                source = "Road Rules"; activity = "Editing road rules"; return true;
            }
            if (toolId.IndexOf("EventEngine", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                source = "Event Engine"; activity = "Planning a city event"; return true;
            }

            try
            {
                Type trafficState = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("TrafficStressTester.TrafficStressState", false))
                    .FirstOrDefault(t => t != null);
                PropertyInfo multiplierProperty = trafficState?.GetProperty("Multiplier", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                int multiplier = multiplierProperty == null ? 1 : Convert.ToInt32(multiplierProperty.GetValue(null));
                if (multiplier > 1)
                {
                    source = "Traffic Stress Lab"; activity = "Stress-testing city traffic"; state = multiplier + "× trip demand"; return true;
                }
            }
            catch { }
            return false;
        }

        private int GetPopulation()
        {
            if (m_CitySystem == null || m_CitySystem.City == Entity.Null || !EntityManager.Exists(m_CitySystem.City) || !EntityManager.HasComponent<Population>(m_CitySystem.City)) return 0;
            return EntityManager.GetComponentData<Population>(m_CitySystem.City).m_Population;
        }

        private int GetMilestone()
        {
            if (m_MilestoneQuery.IsEmptyIgnoreFilter) return 0;
            return math.clamp(m_MilestoneQuery.GetSingleton<MilestoneLevel>().m_AchievedMilestone, 0, 20);
        }

        private string GetWeatherLabel()
        {
            if (m_ClimateSystem.isSnowing) return "Snow";
            if (m_ClimateSystem.isRaining) return m_ClimateSystem.classification == ClimateSystem.WeatherClassification.Stormy ? "Storm" : "Rain";
            return m_ClimateSystem.classification.ToString();
        }

        private string GetWeatherIconKey()
        {
            string weather = m_ClimateSystem.classification.ToString().ToLowerInvariant();
            if ((float)m_ClimateSystem.precipitation > 0.3f) weather = m_ClimateSystem.isRaining ? "rain" : m_ClimateSystem.isSnowing ? "snow" : weather;
            if (m_ClimateSystem.classification == ClimateSystem.WeatherClassification.Stormy && (float)m_ClimateSystem.precipitation > 0.9f)
                weather = m_ClimateSystem.isRaining ? "stormy" : "hail";
            string light = m_TimeSystem.normalizedTime < 7f / 24f || m_TimeSystem.normalizedTime > 0.875f ? "night" : "day";
            return weather + light;
        }

        private void SampleTrafficFlowIfDue()
        {
            if ((DateTime.UtcNow - m_LastTrafficSample).TotalSeconds < 30) return;
            m_LastTrafficSample = DateTime.UtcNow;
            NativeArray<Road> roads = default;
            try
            {
                roads = m_RoadQuery.ToComponentDataArray<Road>(Allocator.TempJob);
                if (roads.Length == 0) { m_TrafficFlow = 100f; return; }
                double total = 0;
                for (int i = 0; i < roads.Length; i++) total += NetUtils.GetTrafficFlowSpeed(roads[i]).x * 100f;
                m_TrafficFlow = (float)(total / roads.Length);
            }
            catch (Exception ex) { Mod.Log.Debug(ex, "Traffic-flow sample unavailable"); }
            finally { if (roads.IsCreated) roads.Dispose(); }
        }

        private void Publish(PresenceSnapshot snapshot, DiscordRPCSetting settings)
        {
            string details = PresenceFormatting.Render(settings.DetailsTemplate, snapshot, snapshot.Activity);
            string state = PresenceFormatting.Render(settings.StateTemplate, snapshot, snapshot.Mode == "Main Menu" ? "Ready to build" : snapshot.Mode);
            if (!string.IsNullOrWhiteSpace(snapshot.TrafficLine) && state.IndexOf(snapshot.TrafficLine, StringComparison.OrdinalIgnoreCase) < 0)
                state = PresenceFormatting.Render(state + " · " + snapshot.TrafficLine, snapshot, state);

            DetailsPreview = details;
            StatePreview = state;
            ActivitySource = snapshot.Source;

            if (m_ActivityManager == null)
            {
                LastUpdateText = "Preview ready; waiting for Discord";
                return;
            }

            try
            {
                var activity = new Activity
                {
                    Details = details,
                    State = state,
                    Timestamps = settings.ShowSessionTimer ? new ActivityTimestamps { Start = new DateTimeOffset(snapshot.SessionStart).ToUnixTimeSeconds() } : default(ActivityTimestamps),
                    Assets = new ActivityAssets
                    {
                        LargeImage = snapshot.LargeImage,
                        LargeText = snapshot.LargeText,
                        SmallImage = snapshot.SmallImage,
                        SmallText = snapshot.SmallText
                    }
                };
                m_ActivityManager.UpdateActivity(activity, result =>
                {
                    LastCallbackSucceeded = result == Result.Ok;
                    if (LastCallbackSucceeded)
                    {
                        SuccessfulCallbacks++;
                        ConnectionStatus = "Connected";
                        ConnectionMessage = "Discord accepted the latest activity";
                        LastUpdateText = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                        Mod.Log.Info($"DRPC_PROOF callback=Ok mode={CurrentMode} source={ActivitySource} success_count={SuccessfulCallbacks}");
                    }
                    else
                    {
                        FailedCallbacks++;
                        ConnectionStatus = "Update failed";
                        ConnectionMessage = "Discord returned " + result;
                        LastUpdateText = "Failed: " + result;
                        Mod.Log.Warn($"DRPC_PROOF callback={result} failure_count={FailedCallbacks}");
                    }
                });
                LastUpdateText = "Sent; awaiting Discord callback";
            }
            catch (Exception ex)
            {
                FailedCallbacks++;
                SetDisconnected(ex.GetType().Name + ": " + ex.Message);
                LastUpdateText = "Send failed";
                Mod.Log.Warn(ex, "Discord activity update failed");
            }
        }

        protected override void OnDestroy()
        {
            Mod.Log.Info($"DRPC_LIFECYCLE event=destroying system={nameof(DiscordRPCSystem)} world={DescribeWorld()}");
            RestoreVanilla();
            Instance = null;
            base.OnDestroy();
        }

        private static bool IsSystemAlive(ComponentSystemBase system)
        {
            if (system == null || system.World == null || !system.World.IsCreated || system.SystemHandle == SystemHandle.Null)
                return false;
            return system.World.Unmanaged.IsSystemValid(system.SystemHandle);
        }

        private string DescribeWorld()
        {
            return World == null ? "<null>" : $"{World.Name}#{World.GetHashCode():X8}:created={World.IsCreated}";
        }
    }
}

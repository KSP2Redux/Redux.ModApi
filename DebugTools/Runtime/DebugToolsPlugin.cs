using System;
using KSP.Logging;
using DebugTools.Runtime.Controllers;
using DebugTools.Runtime.Controllers.FlightTools;
using DebugTools.Runtime.Controllers.VesselTools;
using DebugTools.Utils;
using KSP.Game;
using KSP.OAB;
using SpaceWarp2.API.Logging;
using UitkForKsp2.API;
using UnityEngine;
using UnityEngine.UIElements;
using ILogger = SpaceWarp2.API.Logging.ILogger;
using Legacy = KSP.DebugTools;
using LogMarker = Redux.QA.LogMarker;
using ReduxManager = Redux.ReduxManager;
using StateChanges = Redux.Messages.StateChanges;
using Section = DebugTools.Runtime.Controllers.DebugWindowController.Section;
using ToolMode = DebugTools.Runtime.Controllers.DebugWindowController.ToolMode;
using ToolScene = DebugTools.Runtime.Controllers.DebugWindowController.ToolScene;

// ReSharper disable once CheckNamespace
namespace DebugTools
{
    /// <summary>
    /// Opens the Dev Tools hub on its keyboard shortcut and registers every debug tool in it.
    /// </summary>
    /// <remarks>
    /// Only the shortcut check runs from startup. The hub and each tool window are created the first time they are
    /// opened, so a tool that was never opened costs nothing.
    /// </remarks>
    public class DebugToolsPlugin : KerbalMonoBehaviour
    {
        private static DebugToolsPlugin _instance;
        private static DebugWindowController? DebugWindowController { get; set; }
        private static bool _isHubLoading;
        private static bool _openLocWorkbench;

        internal static ILogger Logger;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        public static void AttachToReduxManager()
        {
            // With Domain Reload disabled this runs on every Play Mode enter, so reset static
            // state (UDR0002) and re-subscribe idempotently to the static event (UDR0003).
            _instance = null;
            DebugWindowController = null;
            _isHubLoading = false;
            _openLocWorkbench = false;
            Logger = null;
            ReduxManager.OnInitialized -= PreInitialize;
            ReduxManager.OnInitialized += PreInitialize;
        }

        private static void PreInitialize()
        {
            Logger = LogManager.GetLogger("Debug Tools");
            Logger.LogInfo("Pre-initialized");
        }

        /// <summary>
        /// Runs when the mod is first initialized.
        /// </summary>
        public static void Initialize()
        {
            if (!Application.isPlaying || _instance != null)
                return;

            Configuration.Initialize(ReduxManager.ReduxCoreConfig);
            try
            {
                Redux.QA.LocWorkbench.Initialize();
            }
            catch (Exception exception)
            {
                GlobalLog.Warn(LogFilter.Gameplay, $"Localization workbench initialization failed: {exception}");
            }
            // Redux's state event, not a message subscription: a static handler subscribed to the message center is
            // dropped when another static handler already listens to the same message type.
            StateChanges.MainMenuStateEntered -= OnMainMenuEntered;
            StateChanges.MainMenuStateEntered += OnMainMenuEntered;

            // This object only watches the shortcut. The hub window is built the first time it is opened.
            // Returning to the main menu destroys every root object except those tagged like the Redux Manager
            // (GameManager.DestroyAllGameObjectsFlowAction), and Initialize runs only once, so the tag keeps the hub
            // and its tool windows alive across campaigns.
            var pluginObject = new GameObject("DebugTools") { tag = "Game Manager" };
            DontDestroyOnLoad(pluginObject);
            _instance = pluginObject.AddComponent<DebugToolsPlugin>();
        }

        // Test overrides never outlive the campaign they were set in.
        private static void OnMainMenuEntered(KSP.Messages.GameStateEnteredMessage message)
        {
            DebugOverrides.RevertTestOverrides();
        }

        private void OnDestroy()
        {
            DebugOverrides.RevertTestOverrides();
            StateChanges.MainMenuStateEntered -= OnMainMenuEntered;
        }

        private void Update()
        {
            if (!Configuration.KeyboardShortcut.Value.Down)
                return;

            ToggleHub();
        }

        /// <summary>
        /// Opens the localization workbench, creating the hub when necessary.
        /// </summary>
        /// <param name="key">The key to filter and select.</param>
        internal static void OpenLocWorkbench(string key)
        {
            LocWorkbenchWindowController.SelectKey(key);
            if (DebugWindowController != null)
            {
                DebugWindowController.OpenTextWorkbench();
                return;
            }
            _openLocWorkbench = true;
            ToggleHub();
        }

        private static void ToggleHub()
        {
            if (DebugWindowController != null)
            {
                DebugWindowController.IsWindowOpen = !DebugWindowController.IsWindowOpen;
                return;
            }

            if (_isHubLoading)
                return;

            _isHubLoading = true;
            UITKHelper.LoadUxml("DebugWindow", uxml =>
            {
                _isHubLoading = false;
                PanelRenderer hubWindow = UITKHelper.CreateWindowFromUxml(uxml, "DebugWindow", minWidth: 560f, minHeight: 360f);
                DebugWindowController = hubWindow.gameObject.AddComponent<DebugWindowController>();
                RegisterTools(DebugWindowController);
                DebugWindowController.IsWindowOpen = true;
                if (_openLocWorkbench)
                {
                    _openLocWorkbench = false;
                    DebugWindowController.OpenTextWorkbench();
                }
            });
        }

        /// <summary>
        /// Gives every debug tool its one home in the hub, grouped by the question it answers.
        /// </summary>
        /// <remarks>
        /// Stock debug windows whose prefabs the game no longer ships (Teleport, Teleport Bookmark Walk, Planet Viewer,
        /// Kerbal Roster, Flight Tools, Thermal Debug, Vessel Coordinates, Joints and Triggers, Vessel Science, Research
        /// Reports, Log Console, Terrain Debug, Rendering Debug and the OAB assembly overlay) are left out, since they
        /// cannot open.
        /// </remarks>
        private static void RegisterTools(DebugWindowController hub)
        {
            RegisterWindow<LocWorkbenchWindowController>(hub, Section.Text, "Localization", "LocWorkbenchWindow",
                "Loc workbench", ToolScene.Any, "Search, edit, reset and export runtime text", "localization translation keys text csv");
            hub.RegisterSwitch(Section.Text, "Display", "Show keys", ToolScene.Any,
                () => Redux.QA.LocWorkbench.IsShowingKeys, Redux.QA.LocWorkbench.SetShowKeys);
            hub.RegisterSwitch(Section.Text, "Display", "Pseudo-localization", ToolScene.Any,
                () => Redux.QA.LocWorkbench.IsPseudoLocalized, Redux.QA.LocWorkbench.SetPseudoLocalization);
            // Cheats: put the game in the situation a test needs. The cheat switches themselves come from the hub.
            hub.RegisterTool(Section.Cheats, "Teleport", "Orbit, surface, rendezvous", ToolScene.Campaign,
                () => CheatsMenu.IsWindowVisible, ToggleStockCheatsMenu,
                "Teleport to an orbit, a surface or a rendezvous, and add science",
                "teleport orbit surface rendezvous science stock", true);
            RegisterWindow<TeleportBookmarksWindowController>(hub, Section.Cheats, "Teleport", "TeleportBookmarksWindow",
                "Teleport Bookmarks", ToolScene.Flight, keywords: "teleport position save", mode: ToolMode.Inline, detachable: true);
            RegisterWindow<QuickSwitchWindowController>(hub, Section.Cheats, "Teleport", "QuickSwitchWindow",
                "Quick Switch", ToolScene.Campaign, "Jump to the VAB, launchpad, runway or orbit",
                "vab bae launchpad runway orbit", mode: ToolMode.Inline, detachable: true);
            RegisterWindow<FreeFlightWindowController>(hub, Section.Cheats, "Teleport", "FreeFlightWindow",
                "Free Flight", ToolScene.Campaign, keywords: "camera planet viewer fly", mode: ToolMode.Inline, collapsed: true);

            RegisterWindow<ResourceScanDebugWindowController>(hub, Section.Cheats, "Progress", "ResourceScanDebugWindow",
                "Resource Scanning", ToolScene.Campaign, keywords: "scan isru ore", mode: ToolMode.Inline, collapsed: true);
            RegisterWindow<MissionsDebugWindowController>(hub, Section.Cheats, "Progress", "MissionsDebugWindow",
                "Missions", ToolScene.Campaign, "Activate, complete, fail or reset missions and stages",
                "mission stage contract", mode: ToolMode.Window);

            RegisterWindow<KerbalRosterToolWindowController>(hub, Section.Cheats, "Crew", "KerbalRosterToolWindow",
                "Roster", ToolScene.Campaign, keywords: "kerbal crew add", mode: ToolMode.Inline, detachable: true);

            // Science: is science working, and move through progression. The stock Science Tools window does the same
            // as the Redux one, so only the Redux one is listed.
            RegisterWindow<ScienceToolsWindowController>(hub, Section.Science, "Progression", "ScienceToolsWindow",
                "Science Tools", ToolScene.Campaign, "Science points, tech nodes, Unlock All Parts",
                "tech tree node research unlock all parts", mode: ToolMode.Inline);
            RegisterWindow<VesselScienceWindowController>(hub, Section.Science, "Experiments", "VesselScienceWindow",
                "Vessel Science", ToolScene.Flight, "Situation, region and experiment reports",
                "experiment report region", mode: ToolMode.Window);

            // Inspect: what is going on.
            RegisterWindow<VesselToolsWindowController>(hub, Section.Overlays, "Vessel", "VesselToolsWindow",
                "Vessel Tools", ToolScene.Campaign,
                "Vessel markers and marker size",
                "com markers size", mode: ToolMode.Inline);
            RegisterWindow<JointsToolsWindowController>(hub, Section.Inspect, "Structure", "JointsToolsWindow",
                "Joints", ToolScene.Flight, keywords: "joint stress structural", mode: ToolMode.Window);
            RegisterWindow<FlightToolsWindowController>(hub, Section.Inspect, "Simulation", "FlightToolsWindow",
                "Sim Objects", ToolScene.Campaign, "Sim objects, proximity load and unload", "sim object load unload", mode: ToolMode.Window);

            RegisterWindow<VesselCoordinatesWindowController>(hub, Section.Inspect, "Vessel", "VesselCoordinatesWindow",
                "Vessel State", ToolScene.Flight, keywords: "coordinates control inputs", mode: ToolMode.Window);
            RegisterWindow<ThermalDataWindowController>(hub, Section.Inspect, "Vessel", "ThermalDataWindow",
                "Thermal Data", ToolScene.Flight, keywords: "heat temperature", mode: ToolMode.Window);
            RegisterWindow<MassStatsWindowController>(hub, Section.Inspect, "Vessel", "MassStatsWindow",
                "Mass Stats", ToolScene.Flight, keywords: "mass dry wet", mode: ToolMode.Window);
            RegisterWindow<ManeuverNodesWindowController>(hub, Section.Inspect, "Vessel", "ManeuversWindow",
                "Maneuvers", ToolScene.Flight, keywords: "node delta v", mode: ToolMode.Window);
            RegisterWindow<SASChartsWindowController>(hub, Section.Inspect, "Vessel", "SASChartsWindow",
                "SAS Charts", ToolScene.Flight, keywords: "sas control chart", mode: ToolMode.Window);

            // Overlays: show it in the world.
            RegisterWindow<TerrainDebugWindowController>(hub, Section.Overlays, "Planet", "TerrainDebugWindow",
                "Terrain Debug", ToolScene.Campaign, "Biome colors, wireframe, PhysX bubble, triplanar",
                "terrain biome wireframe pqs quad", mode: ToolMode.Inline);
            hub.RegisterSwitch(Section.Overlays, "Flight", "Aero data", ToolScene.Flight,
                () => GameManager.Instance.Game.AeroGUI != null && GameManager.Instance.Game.AeroGUI.IsVisible,
                value =>
                {
                    if (GameManager.Instance.Game.AeroGUI != null)
                    {
                        GameManager.Instance.Game.AeroGUI.IsVisible = value;
                    }
                }, "Aerodynamic data for the active vessel", "aero drag lift");
            hub.RegisterSwitch(Section.Overlays, "Part", "Drag Bounds", ToolScene.Campaign,
                () => DebugVisualizer.RenderPartDragBounds, value => DebugVisualizer.RenderPartDragBounds = value,
                "Drag cube bounds of every part", "drag bounds aero cube");
            hub.RegisterSwitch(Section.Overlays, "VAB", "Size Limits", ToolScene.Assembly,
                () => ObjectAssemblyBuilder.IsRenderingDebugSizeLimits, SetOabSizeLimits,
                "Assembly size limit boxes", "oab vab size limit");

            // Test benches: run a controlled effect.
            RegisterWindow<ModuleBenchWindowController>(hub, Section.TestBenches, "Modules", "ModuleBenchWindow",
                "Module bench", ToolScene.Flight, "Stated values against observed module output and resource rates",
                "generator solar radiator converter heat shield antenna", mode: ToolMode.Window);
            RegisterWindow<StaticFireStandWindowController>(hub, Section.TestBenches, "Engines", "StaticFireStandWindow",
                "Static-fire stand", ToolScene.Flight, "Hold, plume pressure, quality and throttle sweep",
                "engine plume static fire pressure thrust isp", mode: ToolMode.Window);
            hub.RegisterTool(Section.TestBenches, "Effects", "VFX Test Suite", ToolScene.Flight,
                () => GameManager.Instance.Game.VFXTestSuiteDialog != null &&
                    GameManager.Instance.Game.VFXTestSuiteDialog.IsVisible,
                ToggleVfxTestSuite, "Decouple, launch clamp, fairing and parachute effects", "vfx effect fx", true);
            hub.RegisterTool(Section.TestBenches, "Effects", "FX Debug Tools", ToolScene.Flight,
                () => GameManager.Instance.Game.FXDebugTools != null && GameManager.Instance.Game.FXDebugTools.IsVisible,
                ToggleFxDebugTools, "Destroy parts and break joints to see their effects", "vfx effect fx", true);

            hub.RegisterSwitch(Section.Text, "Pointer inspection", $"Pick text ({Redux.QA.TextInspector.GetHotkeyText()})", ToolScene.Any,
                () => Redux.QA.TextInspector.IsPicking, Redux.QA.TextInspector.SetPicking,
                "Hover text. Alt+F3 opens its key in the workbench", "text hover inspect key pointer");

            RegisterWindow<GuidedChecklistWindowController>(hub, Section.QaRuns, "Checklists", "GuidedChecklistWindow",
                "Guided part checklist", ToolScene.Campaign, "Manual part checks with screenshots and a per-part dossier",
                "part checklist pass fail review screenshot report");
            // QA runs: check many things and write it down.
            RegisterWindow<PartSweepWindowController>(hub, Section.QaRuns, "Parts", "PartSweepWindow",
                "Part sweep", ToolScene.Assembly, "Automated live checks in an empty VAB", "part sweep stock redux mod changed");
            RegisterWindow<PartSweepWindowController>(hub, Section.QaRuns, "Parts", "PartDossierWindow",
                "Part dossier", ToolScene.Any, "Enter a part ID or use the selected VAB part", "part dossier static live manual report", templateName: "PartSweepWindow");
            hub.RegisterAction(Section.QaRuns, "Log", "Log marker", ToolScene.Any, "Place marker",
                LogMarker.Place, LogMarker.GetHotkeyText, "Write a numbered marker line to the log",
                "log mark marker bug cut qa");
        }

        /// <summary>
        /// Registers a Redux debug window that is created the first time it is opened.
        /// </summary>
        private static void RegisterWindow<T>(DebugWindowController hub, Section section, string group,
            string windowName, string label, ToolScene scene, string note = null, string keywords = null,
            ToolMode mode = ToolMode.Window, bool detachable = false, bool collapsed = false, string templateName = null)
            where T : BaseWindowController
        {
            hub.RegisterHostedTool<T>(section, group, windowName, label, scene, mode, detachable, collapsed,
                note, keywords, _instance.transform, templateName);
        }

        private static void ToggleStockCheatsMenu()
        {
            // The game creates the stock menu at startup, so it has loaded by the time the hub can be opened.
            CheatsMenu cheatsMenu = CheatsMenu.GetOrCreateWindow();
            if (cheatsMenu == null)
                return;

            cheatsMenu.ToggleWindow();
        }

        private static void ToggleVfxTestSuite()
        {
            KSP.VFX.VFXTestSuiteDialog dialog = GameManager.Instance.Game.VFXTestSuiteDialog;
            if (dialog == null)
                return;

            dialog.IsVisible = !dialog.IsVisible;
        }

        private static void ToggleFxDebugTools()
        {
            if (GameManager.Instance.Game.FXDebugTools == null)
                return;

            Legacy.FXDebugTools.ToggleWindow();
        }

        private static void SetOabSizeLimits(bool value)
        {
            if (ObjectAssemblyBuilder.IsRenderingDebugSizeLimits == value)
                return;

            ObjectAssemblyBuilder.ToggleRenderDebugSizeLimits();
        }
    }
}

using System.Collections.Generic;
using DebugTools.Runtime.Controllers;
using DebugTools.Runtime.Controllers.FlightTools;
using DebugTools.Runtime.Controllers.VesselTools;
using DebugTools.Utils;
using KSP.Game;
using SpaceWarp2.API.Logging;
using UnityEngine;
using ILogger = SpaceWarp2.API.Logging.ILogger;
using ReduxManager = Redux.ReduxManager;

// ReSharper disable once CheckNamespace
namespace DebugTools
{
    public class DebugToolsPlugin : KerbalMonoBehaviour
    {
        internal const string VesselScienceWindowName = "VesselScienceWindow";
        internal const string TeleportBookmarksWindowName = "TeleportBookmarksWindow";
        internal const string KerbalRosterWindowName = "KerbalRosterToolWindow";
        internal const string FlightToolsWindowName = "FlightToolsWindow";
        internal const string TerrainDebugWindowName = "TerrainDebugWindow";

        private static readonly Dictionary<string, BaseWindowController> _windows = new();
        private static DebugWindowController? DebugWindowController { get; set; }

        internal static ILogger Logger;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        public static void AttachToReduxManager()
        {
            // With Domain Reload disabled this runs on every Play Mode enter, so reset static
            // state (UDR0002) and re-subscribe idempotently to the static event (UDR0003).
            DebugWindowController = null;
            _windows.Clear();
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
            if (!Application.isPlaying) return;

            Configuration.Initialize(ReduxManager.ReduxCoreConfig);

            UITKHelper.LoadUxml("DebugWindow", uxml =>
            {
                // Create main debug window
                var debugWindow = UITKHelper.CreateWindowFromUxml(uxml, "DebugWindow");
                // Add a controller for the UI to the window's game object
                DebugWindowController = debugWindow.gameObject.AddComponent<DebugWindowController>();
                DebugWindowController.IsWindowOpen = false;

                CreateDebugWindows();
                DebugWindowController.RegisterToggle("VFXTestSuite", "VFX Test Suite",
                    evt => GameManager.Instance.Game.VFXTestSuiteDialog.IsVisible = evt.newValue);
                DebugWindowController.RegisterToggle("FXDebugTools", "FX Debug Tools",
                    evt => GameManager.Instance.Game.FXDebugTools.IsVisible = evt.newValue);
            });
        }

        internal static bool TryToggleWindow(string windowName)
        {
            if (DebugWindowController == null || !_windows.TryGetValue(windowName, out var window) || window == null ||
                !DebugWindowController.WindowToggles.TryGetValue(windowName, out var toggle))
                return false;

            window.IsWindowOpen = !window.IsWindowOpen;
            toggle.SetValueWithoutNotify(window.IsWindowOpen);
            return true;
        }

        private static void CreateDebugWindow<T>(string windowName, string toggleLabel) where T : BaseWindowController
        {
            // Load window UXML
            UITKHelper.LoadUxml(windowName, uxml =>
            {
                // Create UITK window
                var window = UITKHelper.CreateWindowFromUxml(uxml, windowName);

                // Attach window controller
                var controller = window.gameObject.AddComponent<T>();
                controller.IsWindowOpen = false;

                if (DebugWindowController == null) return;
                
                // Setup window toggling & close button
                DebugWindowController.RegisterToggle(windowName, toggleLabel,
                    evt => controller.IsWindowOpen = evt.newValue);
                _windows[windowName] = controller;
                controller.CloseButton.clicked += () =>
                {
                    controller.IsWindowOpen = false;
                    DebugWindowController.WindowToggles[windowName].value = false;
                };
            });
        }

        private static void CreateDebugWindows()
        {
            // Vessel tools window
            CreateDebugWindow<VesselToolsWindowController>("VesselToolsWindow", "Vessel Tools");
            // Flight tools window
            CreateDebugWindow<FlightToolsWindowController>(FlightToolsWindowName, "Flight Tools");
            // Joints tools window
            CreateDebugWindow<JointsToolsWindowController>("JointsToolsWindow", "Joints Tools");
            // Quick actions window
            CreateDebugWindow<QuickSwitchWindowController>("QuickSwitchWindow", "Quick Switch");
            // Free flight window
            CreateDebugWindow<FreeFlightWindowController>("FreeFlightWindow", "Free Flight");
            // Terrain debug window
            CreateDebugWindow<TerrainDebugWindowController>(TerrainDebugWindowName, "Terrain Debug");
            // Science tools window
            CreateDebugWindow<ScienceToolsWindowController>("ScienceToolsWindow", "Science Tools");
            // Vessel science window
            CreateDebugWindow<VesselScienceWindowController>(VesselScienceWindowName, "Vessel Science");
            // Kerbal roster tools window
            CreateDebugWindow<KerbalRosterToolWindowController>(KerbalRosterWindowName, "Roster Tools");
            // Teleport bookmarks window
            CreateDebugWindow<TeleportBookmarksWindowController>(TeleportBookmarksWindowName, "Teleport Bookmarks");
            // Resource scanning window
            CreateDebugWindow<ResourceScanDebugWindowController>("ResourceScanDebugWindow", "Resource Scanning");
            // Missions debug window
            CreateDebugWindow<MissionsDebugWindowController>("MissionsDebugWindow", "Missions");
        }
    }
}
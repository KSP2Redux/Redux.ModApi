using System.IO;
using System.Reflection;
using JetBrains.Annotations;
using KSP.Game;
using Redux.ExtraModTypes;
using SpaceWarp2;
using SpaceWarp2.API.Logging;
using SpaceWarp2.API.Mods;
using UitkForKsp2.API;
using UnityEngine;
using UnityEngine.UIElements;
using VSwift.UI;
using ILogger = SpaceWarp2.API.Logging.ILogger;

namespace VSwift
{
    /// <summary>
    /// V-SwiFT entry point as a SpaceWarp <see cref="KerbalMod" />, loading the part-switch popout window assets.
    /// </summary>
    public class VSwiftPlugin : KerbalMod
    {
        private static ILogger _logger;

        /// <summary>
        /// Gets the singleton plugin instance, set during <see cref="OnInitialized" />.
        /// </summary>
        [PublicAPI] public static VSwiftPlugin Instance { get; set; }

        /// <summary>
        /// Gets the logger V-SwiFT's modules, loaders and transformers write to.
        /// </summary>
        internal static ILogger Logger => _logger ??= LogManager.GetLogger(ModIds.V_SWIFT);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            _logger = null;
        }

        /// <inheritdoc />
        public override void OnPreInitialized()
        {
        }

        /// <inheritdoc />
        public override void OnInitialized()
        {
            Instance = this;

            var assets = GameManager.Instance.Assets;

            // Load the popout window controller uxml
            var popoutWindowControllerUxmlHandle = assets.LoadAssetAsync<VisualTreeAsset>("vs/part_popout");
            popoutWindowControllerUxmlHandle.WaitForCompletion();
            var popoutWindowControllerUxml = popoutWindowControllerUxmlHandle.Result;


            var windowOptions = new WindowOptions
            {
                WindowId = "VSwift_PartSwitchPopout",
                Parent = null,
                IsHidingEnabled = true,
                DisableGameInputForTextFields = false, // There will be no text fields
                MoveOptions = new MoveOptions
                {
                    IsMovingEnabled = true,
                    CheckScreenBounds = true,
                }
            };

            var popOutWindow = Window.Create(windowOptions, popoutWindowControllerUxml);
            var popoutWindowController = popOutWindow.gameObject.AddComponent<PartSwitchPopoutWindowController>();

            // TODO: Refactor vswift ui loading to use addressables

            var partStatisticHandle = assets.LoadAssetAsync<VisualTreeAsset>("vs/part_statistic");
            partStatisticHandle.WaitForCompletion();
            var partStatistic = partStatisticHandle.Result;
            VSwiftUI.StatBlockContainer = partStatistic;

            var variantNameHandle =assets.LoadAssetAsync<VisualTreeAsset>("vs/ps_variant_name");
            variantNameHandle.WaitForCompletion();
            var variantName = variantNameHandle.Result;
            PartSwitchPopoutWindowController.VariantNameContainer = variantName;

            var requiredTechHandle = assets.LoadAssetAsync<VisualTreeAsset>("vs/required_tech");
            requiredTechHandle.WaitForCompletion();
            var requiredTech = requiredTechHandle.Result;
            PartSwitchPopoutWindowController.RequirementContainer = requiredTech;
        }

        /// <inheritdoc />
        public override void OnPostInitialized()
        {
        }
    }
}

using SpaceWarp2.API.Configuration;
using SpaceWarp2.API.Input;
using UnityEngine;

namespace DebugTools.Utils
{
    public static class Configuration
    {
        private const string HUB_SECTION = "Dev Tools";

        public static ConfigValue<KeyboardShortcut> KeyboardShortcut;
        public static ConfigValue<bool> StructuralStressGauges;

        /// <summary>
        /// Gets the name of the Dev Tools section that was shown last, so the hub reopens on it.
        /// </summary>
        public static ConfigValue<string> HubLastSection;

        /// <summary>
        /// Gets the Dev Tools window position and size as "left,top,width,height", or an empty string before it was
        /// first moved.
        /// </summary>
        public static ConfigValue<string> HubWindowRect;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            KeyboardShortcut = null;
            StructuralStressGauges = null;
            HubLastSection = null;
            HubWindowRect = null;
            PhysicsSettingsManager.OnPhysicsSettingsLoaded -= ApplyStructuralStressGaugeSetting;
        }

        public static void Initialize(IConfigFile config)
        {
            KeyboardShortcut = new ConfigValue<KeyboardShortcut>(config.Bind("Keybinding", "Debug UI Keyboard shortcut",
                new KeyboardShortcut(KeyCode.F12, KeyCode.LeftAlt), "Keyboard shortcut to toggle the main debug UI"));
            StructuralStressGauges = new ConfigValue<bool>(config.Bind(
                "Joints",
                "Show structural stress gauges",
                false,
                "Show structural stress gauges over loaded vessel attachment points"));

            // Window state, not a setting, so it stays out of the settings menu.
            IConfigSection hubSection = config.GetOrCreateSection(HUB_SECTION);
            HubLastSection = new ConfigValue<string>(hubSection.BindEntry(
                "Last section",
                "Cheats",
                "Dev Tools section shown when the window opens",
                tags: new[] { ConfigTags.Hidden }));
            HubWindowRect = new ConfigValue<string>(hubSection.BindEntry(
                "Window rect",
                string.Empty,
                "Dev Tools window position and size as left,top,width,height",
                tags: new[] { ConfigTags.Hidden }));

            PhysicsSettingsManager.OnPhysicsSettingsLoaded -= ApplyStructuralStressGaugeSetting;
            PhysicsSettingsManager.OnPhysicsSettingsLoaded += ApplyStructuralStressGaugeSetting;
            ApplyStructuralStressGaugeSetting();
        }

        public static void ApplyStructuralStressGaugeSetting()
        {
            PhysicsSettings.STRUCTURAL_STRESS_GAUGES_ENABLED = StructuralStressGauges?.Value ?? false;
        }
    }
}
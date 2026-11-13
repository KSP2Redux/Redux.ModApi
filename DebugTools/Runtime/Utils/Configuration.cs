using SpaceWarp2.API.Configuration;
using SpaceWarp2.API.Input;
using UnityEngine;

namespace DebugTools.Utils
{
    public static class Configuration
    {
        private static IConfigSection _toolSection;

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
            _toolSection = null;
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
            _toolSection = config.GetOrCreateSection("Dev Tools windows");
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

        /// <summary>
        /// Bind hidden placement and host state for a tool.
        /// </summary>
        /// <param name="name">The tool asset name.</param>
        /// <returns>The saved rect followed by the host state.</returns>
        public static ConfigValue<string> BindToolWindowRect(string name) => new(_toolSection.BindEntry(
            name, string.Empty, "Window rect and popped-out state", tags: new[] { ConfigTags.Hidden }));

        public static void ApplyStructuralStressGaugeSetting()
        {
            PhysicsSettings.STRUCTURAL_STRESS_GAUGES_ENABLED = StructuralStressGauges?.Value ?? false;
        }
    }
}
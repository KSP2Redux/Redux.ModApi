using ReduxLib.Configuration;
using ReduxLib.Input;
using UnityEngine;

namespace DebugTools.Utils
{
    public static class Configuration
    {
        public static ConfigValue<KeyboardShortcut> KeyboardShortcut;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            KeyboardShortcut = null;
        }

        public static void Initialize(IConfigFile config)
        {
            KeyboardShortcut = new ConfigValue<KeyboardShortcut>(config.Bind("Keybinding", "Debug UI Keyboard shortcut",
                new KeyboardShortcut(KeyCode.F12, KeyCode.LeftAlt), "Keyboard shortcut to toggle the main debug UI"));
        }
    }
}
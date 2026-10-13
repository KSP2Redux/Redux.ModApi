#nullable enable

using JetBrains.Annotations;
using UnityEngine;

namespace SpaceWarp2.API
{
    /// <summary>
    /// Contains paths to various directories.
    /// </summary>
    [PublicAPI]
    public static class CommonPaths
    {
        public static string ModsFolder = "Assets/Mods";
        public static string LuaFolder = "Assets/Lua";
        public static string DisabledPlugins = "disabled_plugins.cfg";
        public static string FeatureDefaults = "feature_defaults.cfg";
        public static string HashLocation = "mod_list_hash.txt";

        /// <summary>
        /// The core Redux settings file, which Redux, SpaceWarp and the built-in tools bind into.
        /// </summary>
        public static string CoreConfig = "Redux/config.json";

        /// <summary>
        /// The folder holding the built-in mods' configs, one subfolder per mod id.
        /// </summary>
        public static string ConfigFolder = "Redux/Config";

        /// <summary>
        /// The folder holding SpaceWarp's module configs, one JSON file per module name.
        /// </summary>
        public static string ModuleConfigFolder = "Redux/Config/SWModuleConfig";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ModsFolder = Application.isEditor ? "Assets/Mods" : "./mods";
            LuaFolder = Application.isEditor ? "Assets/Lua" : "./scripts";
            DisabledPlugins = "disabled_plugins.cfg";
            FeatureDefaults = "feature_defaults.cfg";
            HashLocation = "mod_list_hash.txt";
            CoreConfig = "Redux/config.json";
            ConfigFolder = "Redux/Config";
            ModuleConfigFolder = "Redux/Config/SWModuleConfig";
        }
    }
}

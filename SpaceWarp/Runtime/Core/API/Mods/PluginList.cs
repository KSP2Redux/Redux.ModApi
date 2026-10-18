#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using SpaceWarp2.API.Backend.Modding;
using SpaceWarp2.API.Mods.JSON;
using SpaceWarp2.API.Versions;
using UnityEngine;

namespace SpaceWarp2.API.Mods
{
    /// <summary>
    /// API for accessing information about currently loaded and disabled plugins.
    /// </summary>
    [PublicAPI]
    public static class PluginList
    {
        // Its own logger rather than SpaceWarpPlugin's, which is null until the plugin boots
        private static readonly Logging.ILogger Logger = Logging.LogManager.GetLogger("Space Warp");

        #region Reading Plugins

        /// <summary>
        /// Set if the plugin list is different in any way since last run (version differences, new mods, mods removed,
        /// mods disabled, description differences, any different in any swinfo file and the disabled mod list).
        /// </summary>
        public static bool ModListChangedSinceLastRun => ModList.ChangedSinceLastRun;


        /// <summary>
        /// Retrieves the <see cref="ModInfo" /> of the specified plugin. Returns null if the specified plugin guid doesn't
        /// have an associated <see cref="ModInfo" />.
        /// </summary>
        /// <param name="guid">GUID of the plugin</param>
        /// <returns><see cref="ModInfo" /> of the plugin or null if not found</returns>
        public static ModInfo TryGetSwinfo(string guid)
        {
            if (TryGetDescriptor(guid) is { SWInfo: var swInfo })
            {
                return swInfo;
            }

            var disabledModInfo = AllDisabledPlugins
                .Where(item => item.Guid == guid)
                .Select(item => item.SWInfo)
                .FirstOrDefault();

            return disabledModInfo;
        }

        /// <summary>
        /// Retrieves the <see cref="SpaceWarpPluginDescriptor" /> of the specified plugin. Returns null if the specified plugin guid doesn't
        /// have an associated <see cref="SpaceWarpPluginDescriptor" />.
        /// </summary>
        /// <param name="guid">GUID of the plugin</param>
        /// <returns><see cref="SpaceWarpPluginDescriptor" /> of the plugin or null if not found</returns>
        public static SpaceWarpPluginDescriptor? TryGetDescriptor(string guid) => AllLoadedPlugins.FirstOrDefault(item => item.Guid == guid);

        /// <summary>
        /// Gets the mod an assembly belongs to, for registrations made through APIs that take no mod id.
        /// </summary>
        /// <remarks>
        /// The game assembly belongs to Redux, though SpaceWarp, Patch Manager and V-SwiFT compile into it too. A mod
        /// that needs its own id there names it. Any other assembly belongs to the mod that loaded it, as its main
        /// assembly or a library.
        /// </remarks>
        /// <param name="assembly">The assembly.</param>
        /// <returns>The mod's id, or null when no loaded mod owns the assembly, or several do.</returns>
        public static string? ModIdOf(System.Reflection.Assembly assembly)
        {
            if (assembly == typeof(PluginList).Assembly)
                return ModIds.KSP2_REDUX;

            var owners = AllLoadedPlugins
                .Where(descriptor => descriptor.Assemblies.Contains(assembly))
                .Take(2)
                .ToList();
            return owners.Count == 1 ? owners[0].Guid : null;
        }

        /// <summary>
        /// Retrieves the instance of the specified plugin class.
        /// </summary>
        /// <typeparam name="T">The type of the plugin class</typeparam>
        /// <returns>Plugin instance or null if not found</returns>
        public static T? TryGetPlugin<T>() where T : class, ISpaceWarpMod =>
            AllLoadedPlugins.FirstOrDefault(x => x.Plugin is T)?.Plugin as T;

        /// <summary>
        /// Retrieves the instance of a plugin class with the given GUID.
        /// </summary>
        /// <param name="guid">GUID of the plugin</param>
        /// <typeparam name="T">The type of the plugin class</typeparam>
        /// <returns>Plugin instance or null if not found</returns>
        public static T? TryGetPlugin<T>(string guid) where T : class, ISpaceWarpMod =>
            TryGetDescriptor(guid)?.Plugin as T;

        #endregion

        #region Registering Plugins

        private static List<SpaceWarpPluginDescriptor> _allLoadedPlugins = new() { };
        private static List<SpaceWarpPluginDescriptor> _allDisabledPlugins = new() { };
        private static List<SpaceWarpErrorDescription> _allErroredPlugins = new() { };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStaticState()
        {
            _allLoadedPlugins = new() { };
            _allDisabledPlugins = new() { };
            _allErroredPlugins = new() { };
        }

        /// <summary>
        /// All plugins that loaded: not disabled in the mod list and not errored.
        /// </summary>
        /// <remarks>
        /// A loaded mod can still be off in a campaign. Whether it is on in the active feature set is
        /// <see cref="SpaceWarpPluginDescriptor.Enabled" />.
        /// </remarks>
        public static IReadOnlyList<SpaceWarpPluginDescriptor> AllLoadedPlugins => _allLoadedPlugins;

        /// <summary>
        /// All disabled plugins
        /// </summary>
        public static IReadOnlyList<SpaceWarpPluginDescriptor> AllDisabledPlugins => _allDisabledPlugins;

        /// <summary>
        /// All errored plugins
        /// </summary>
        public static IReadOnlyList<SpaceWarpErrorDescription> AllErroredPlugins => _allErroredPlugins;

        /// <summary>
        /// All plugins, including disabled and errored
        /// </summary>
        public static IEnumerable<SpaceWarpPluginDescriptor> AllPlugins => _allLoadedPlugins
            .Concat(_allDisabledPlugins)
            .Concat(_allErroredPlugins.Select(x => x.Plugin));

        internal static void RegisterPlugin(SpaceWarpPluginDescriptor plugin)
        {
            if (AllPlugins.Any(x => x.Guid == plugin.Guid))
            {
                Logger.LogError($"Attempting to register a mod with a duplicate GUID: {plugin.Guid}");
            }

            Logger.LogInfo($"Registered plugin: {plugin.Guid}");
            _allLoadedPlugins.Add(plugin);
        }

        internal static void Disable(string guid)
        {
            var descriptor = _allLoadedPlugins.FirstOrDefault(
                x => string.Equals(
                    x.Guid,
                    guid,
                    StringComparison.InvariantCultureIgnoreCase
                )
            );

            if (descriptor != null)
            {
                _allLoadedPlugins.Remove(descriptor);
                _allDisabledPlugins.Add(descriptor);
            }
        }

        internal static SpaceWarpErrorDescription GetErrorDescriptor(SpaceWarpPluginDescriptor plugin)
        {
            if (_allErroredPlugins.Any(x => x.Plugin == plugin))
                return _allErroredPlugins.First(x => x.Plugin == plugin);

            if (_allLoadedPlugins.Any(x => x == plugin))
            {
                _allLoadedPlugins.Remove(plugin);
            }

            var newError = new SpaceWarpErrorDescription(plugin);
            _allErroredPlugins.Add(newError);
            return newError;
        }

        internal static void NoteMissingSwinfoError(SpaceWarpPluginDescriptor plugin)
        {
            var errorDescriptor = GetErrorDescriptor(plugin);
            errorDescriptor.MissingSwinfo = true;
        }

        internal static void NoteBadDirectoryError(SpaceWarpPluginDescriptor plugin)
        {
            var errorDescriptor = GetErrorDescriptor(plugin);
            errorDescriptor.BadDirectory = true;
        }

        internal static void NoteBadIDError(SpaceWarpPluginDescriptor plugin)
        {
            var errorDescriptor = GetErrorDescriptor(plugin);
            errorDescriptor.BadID = true;
        }

        internal static void NoteMismatchedVersionError(SpaceWarpPluginDescriptor plugin)
        {
            var errorDescriptor = GetErrorDescriptor(plugin);
            errorDescriptor.MismatchedVersion = true;
        }

        internal static void NoteBadAssemblyError(SpaceWarpPluginDescriptor plugin)
        {
            var errorDescriptor = GetErrorDescriptor(plugin);
            errorDescriptor.BadAssembly = true;
        }

        internal static void NoteUnspecifiedDependencyError(SpaceWarpPluginDescriptor plugin, string dependency)
        {
            var errorDescriptor = GetErrorDescriptor(plugin);
            errorDescriptor.UnspecifiedDependencies.Add(dependency);
        }

        private static SemanticVersion PadVersion(string version)
        {
            var length = version.Split('.').Length;
            for (var i = 0; i < 3 - length; i++)
            {
                version += ".0";
            }

            return new SemanticVersion(version);
        }

        private static bool IsSupportedSemver(string version, string min, string max)
        {
            var basicVersion = PadVersion(version);
            var minVersion = PadVersion(min.Replace("*", "0"));
            var maxVersion = PadVersion(max.Replace("*", $"{int.MaxValue}"));
            return basicVersion >= minVersion && basicVersion <= maxVersion;
        }

        private static bool DependencyResolved(
            SpaceWarpPluginDescriptor descriptor,
            List<SpaceWarpPluginDescriptor> resolvedPlugins
        )
        {
            if (descriptor.SWInfo.Spec < SpecVersion.V1_3)
                return true;

            return !(from dependency in descriptor.SWInfo.Dependencies
                let info = resolvedPlugins.FirstOrDefault(
                    x => string.Equals(
                        x.Guid,
                        dependency.ID,
                        StringComparison.InvariantCultureIgnoreCase
                    )
                )
                where info == null || !IsSupportedSemver(
                    info.SWInfo.Version,
                    dependency.Version.Min,
                    dependency.Version.Max
                )
                select dependency).Any();
        }

        private static void GetLoadOrder()
        {
            // Convert spacewarp dependencies if there isn't a legacy wrapper, as this usually means a part mod
            if (_allLoadedPlugins.All(x => x.Guid != "com.github.x606.spacewarp"))
            {
                foreach (var dependency in _allLoadedPlugins.SelectMany(x => x.SWInfo.Dependencies))
                {
                    if (dependency.ID == "com.github.x606.spacewarp")
                    {
                        dependency.ID = ModIds.SPACE_WARP;
                    }
                }
            }
            var changed = true;
            List<SpaceWarpPluginDescriptor> newOrder = new();
            while (changed)
            {
                changed = false;
                for (var i = _allLoadedPlugins.Count - 1; i >= 0; i--)
                {
                    if (!DependencyResolved(_allLoadedPlugins[i], newOrder))
                        continue;

                    newOrder.Add(_allLoadedPlugins[i]);
                    _allLoadedPlugins.RemoveAt(i);
                    changed = true;
                }
            }

            for (var i = _allLoadedPlugins.Count - 1; i >= 0; i--)
            {
                var info = _allLoadedPlugins[i];
                Logger.LogError($"Missing dependency for mod: {info.Name}, this mod will not be loaded");
                var error = GetErrorDescriptor(info);
                error.MissingDependencies = info.SWInfo
                    .Dependencies
                    .Select(x => x.ID)
                    .Where(x => !newOrder.Any(y => string.Equals(x, y.Guid, StringComparison.InvariantCultureIgnoreCase)))
                    .ToList();
            }

            _allLoadedPlugins = newOrder;
        }

        private static void GetDependencyErrors()
        {
            foreach (var erroredPlugin in _allErroredPlugins.Where(
                         erroredPlugin => erroredPlugin.MissingDependencies.Count != 0
                     ))
            {
                for (var i = erroredPlugin.MissingDependencies.Count - 1; i >= 0; i--)
                {
                    var dep = erroredPlugin.MissingDependencies[i];
                    if (AllLoadedPlugins.Any(x =>
                            string.Equals(x.Guid, dep, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        erroredPlugin.UnsupportedDependencies.Add(dep);
                        erroredPlugin.MissingDependencies.RemoveAt(i);
                    }
                    else if (AllErroredPlugins.Any(x =>
                                 string.Equals(x.Plugin.Guid, dep, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        erroredPlugin.ErroredDependencies.Add(dep);
                        erroredPlugin.MissingDependencies.RemoveAt(i);
                    }
                    else if (AllDisabledPlugins.Any(x =>
                                 string.Equals(x.Guid, dep, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        erroredPlugin.DisabledDependencies.Add(dep);
                        erroredPlugin.MissingDependencies.RemoveAt(i);
                    }
                }
            }
        }

        private static void CheckCompatibility()
        {
            var incompatibilities = _allLoadedPlugins
                .Select(x => (Key: x.Guid, Value: new HashSet<string>()))
                .ToDictionary(x => x.Key, x => x.Value);
            var versionLookup = _allLoadedPlugins
                .Select(x => (Key: x.Guid, Value: x.SWInfo.Version))
                .ToDictionary(x => x.Key, x => x.Value);
            var pluginDictionary = _allLoadedPlugins
                .ToDictionary(x => x.Guid, x => x);
            foreach (var mod in _allLoadedPlugins)
            {
                var swinfo = mod.SWInfo;
                if (swinfo.Spec < SpecVersion.V2_0)
                    continue;

                foreach (var conflict in swinfo.Conflicts)
                {
                    if (!versionLookup.TryGetValue(conflict.ID, out var conflictingVersion) ||
                        !IsSupportedSemver(conflictingVersion, conflict.Version.Min, conflict.Version.Max))
                    {
                        continue;
                    }

                    incompatibilities[mod.Guid].Add(conflict.ID);
                    incompatibilities[conflict.ID].Add(mod.Guid);
                }
            }

            foreach (var incompatibility in incompatibilities)
            {
                if (incompatibility.Value.Count <= 0)
                    continue;

                var descriptor = GetErrorDescriptor(pluginDictionary[incompatibility.Key]);
                descriptor.Incompatibilities.AddRange(incompatibility.Value);
            }
        }


        internal static void LoadAllMods()
        {
            foreach (var mod in AllLoadedPlugins)
            {
                if (mod.Plugin is UnloadedMod unloadedMod)
                {
                    mod.Plugin = unloadedMod.Load();
                }
            }
        }

        /// <summary>
        /// This is done after Awake/LoadModule(), so that everything else can use it
        /// </summary>
        internal static void ResolveDependenciesAndLoadOrder()
        {
            GetLoadOrder();
            GetDependencyErrors();
            CheckCompatibility();
        }

        #endregion
    }
}

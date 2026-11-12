#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Redux.ApiImpls;
using SpaceWarp2.API.Configuration;
using SpaceWarp2.API.Logging;
using SpaceWarp2.API.Mods;
using SpaceWarp2.API.Mods.JSON;
using SpaceWarp2.API.Reflection;
using UnityEngine;
using ReduxManager = Redux.ReduxManager;
namespace SpaceWarp2.API.Backend.Modding
{
    internal static class PluginRegister
    {
        public static void RegisterAllMods()
        {
            RegisterSpaceWarp();
            RegisterInternalMods();
            RegisterKsp1Mods();
            RegisterMods();
            RegisterStandaloneLuaMods();
            DisableMods();
        }

        /// <summary>
        /// Registers a descriptor for each standalone drop-in .lua under the mods folder - a .lua with no swinfo
        /// mod folder around it.
        /// </summary>
        /// <remarks>
        /// These are mods-of-one whose ModId is the filename, matching PatchManager's LoadSinglePatchFile
        /// convention, so the SW lifecycle global can resolve their descriptor by ModId.
        /// </remarks>
        private static void RegisterStandaloneLuaMods()
        {
            var modsFolder = new DirectoryInfo(CommonPaths.ModsFolder);
            if (!modsFolder.Exists)
                return;

            foreach (var lua in modsFolder.GetFiles("*.lua", SearchOption.AllDirectories))
            {
                // A .lua inside a swinfo mod folder belongs to that mod, not its own mod-of-one.
                if (HasSwinfoAncestor(lua.Directory, modsFolder))
                    continue;

                var modId = Path.GetFileNameWithoutExtension(lua.Name);
                if (ModList.DisabledPluginGuids.Contains(modId) || PluginList.TryGetDescriptor(modId) != null)
                    continue;

                ISpaceWarpMod swMod = new AssetOnlyMod(modId);
                var info = new ModInfo
                {
                    Spec = SpecVersion.Default,
                    ModID = modId,
                    Name = modId,
                    Version = "0.0.0"
                };
                swMod.SWConfiguration =
                    new JsonConfigFile(Path.Combine(lua.Directory!.FullName, modId + "-config.json"));
                var descriptor = new SpaceWarpPluginDescriptor(
                    swMod, modId, modId, info, lua.Directory!, true, swMod.SWConfiguration);
                swMod.SWMetadata = descriptor;
                swMod.SWLogger = descriptor.Logger;
                descriptor.ScriptFiles.Add(lua.FullName);
                PluginList.RegisterPlugin(descriptor);
                Logger.LogInfo($"Registered standalone Lua mod: {modId}");
            }
        }

        /// <summary>
        /// Returns true if <paramref name="dir" /> or any ancestor up to <paramref name="stopAt" /> contains a
        /// swinfo.json - meaning the file belongs to that swinfo mod rather than being a standalone drop-in.
        /// </summary>
        private static bool HasSwinfoAncestor(DirectoryInfo dir, DirectoryInfo stopAt)
        {
            for (var current = dir; current != null; current = current.Parent)
            {
                if (File.Exists(Path.Combine(current.FullName, "swinfo.json")))
                    return true;

                if (string.Equals(current.FullName, stopAt.FullName, StringComparison.OrdinalIgnoreCase))
                    break;
            }

            return false;
        }

        private static void RegisterKsp1Mods()
        {
            foreach (var mod in ReduxInternalModRegister.Ksp1PluginDescriptors)
            {
                mod.IsKsp1 = true;
                if (mod.Plugin != null)
                {
                    mod.Plugin.SWLogger ??= LogManager.GetLogger(mod.Guid);
                }
                PluginList.RegisterPlugin(mod);
            }
        }

        private static void RegisterInternalMods()
        {
            foreach (var mod in ReduxInternalModRegister.InternalPluginDescriptors)
            {
                mod.Plugin.SWLogger ??= LogManager.GetLogger(mod.Guid);
                // Redux's settings live in the core file, so a script asking for Redux's config gets them
                mod.Plugin.SWConfiguration = mod.ConfigFile = mod.Guid == ModIds.KSP2_REDUX
                    ? ReduxManager.ReduxCoreConfig
                    : new JsonConfigFile(Path.Combine(mod.Folder.FullName, "config.json"));
                mod.IsCore = true;
                PluginList.RegisterPlugin(mod);
            }
        }


        private static readonly SpaceWarp2.API.Logging.ILogger Logger = LogManager.GetLogger("Space Warp");

        private static bool AssertFolderPath(ISpaceWarpMod plugin, string folderPath)
        {
            if (Path.GetFileName(folderPath) != "mods")
                return true;

            Logger.LogError(
                $"Found Space Warp mod in the BepInEx/plugins directory. This mod will " +
                $"not be initialized."
            );

            return false;
        }

        private static bool AssertModInfoExistence(ISpaceWarpMod plugin, string modInfoPath, string folderPath)
        {
            if (File.Exists(modInfoPath))
                return true;

            Logger.LogError(
                $"Found Space Warp plugin at {modInfoPath} without a swinfo.json in its folder. This mod " +
                $"will not be initialized."
            );

            PluginList.NoteMissingSwinfoError(new SpaceWarpPluginDescriptor(plugin, "unknown", Path.GetFileName(folderPath),
                new ModInfo(), new DirectoryInfo(folderPath)));

            return false;
        }

        private static bool TryReadModInfo(
            ISpaceWarpMod plugin,
            string modInfoPath,
            string folderPath,
            out ModInfo metadata
        )
        {
            try
            {
                metadata = JsonConvert.DeserializeObject<ModInfo>(File.ReadAllText(modInfoPath));
            }
            catch
            {
                Logger.LogError(
                    $"Error reading metadata for spacewarp plugin at {folderPath}. This mod will not be initialized");
                PluginList.NoteMissingSwinfoError(new SpaceWarpPluginDescriptor(plugin, "unknown", Path.GetFileName(folderPath),
                    new ModInfo(), new DirectoryInfo(folderPath)));
                metadata = null;
                return false;
            }

            return true;
        }

        private static void RegisterSpaceWarp()
        {
            var mod = new UnloadedMod(typeof(SpaceWarpPlugin))
            {
                SWConfiguration = ReduxManager.ReduxCoreConfig
            };
            var descriptor = new SpaceWarpPluginDescriptor(mod,
                SpaceWarpPlugin.SpaceWarpModInfo.ModID, SpaceWarpPlugin.SpaceWarpModInfo.Name,
                SpaceWarpPlugin.SpaceWarpModInfo, new DirectoryInfo(ReduxManager.REDUX_FOLDER), true, ReduxManager.ReduxCoreConfig);
            mod.SWMetadata = descriptor;
            descriptor.Logger = SpaceWarpPlugin.Logger;
            descriptor.IsCore = true;
            PluginList.RegisterPlugin(descriptor);
        }

        private static void RegisterMods()
        {
            var pluginPath = new DirectoryInfo(CommonPaths.ModsFolder);
            if (!pluginPath.Exists)
            {
                pluginPath.Create();
            }
            foreach (var swinfo in pluginPath.GetFiles("swinfo.json", SearchOption.AllDirectories))
            {
                RegisterMod(swinfo);
            }
        }

        /// <summary>
        /// Registers the mod described by one swinfo.json, or records it as errored when its assemblies do not load.
        /// </summary>
        /// <param name="swinfo">The mod's swinfo.json.</param>
        internal static void RegisterMod(FileInfo swinfo)
        {
            ModInfo swinfoData;
            try
            {
                swinfoData = JsonConvert.DeserializeObject<ModInfo>(File.ReadAllText(swinfo.FullName));
            }
            catch
            {
                Logger.LogError($"Error reading metadata file: {swinfo.FullName}, this mod will be ignored");
                return;
            }

            if (swinfoData.Spec < SpecVersion.V1_3)
            {
                Logger.LogWarning(
                    $"Found swinfo information for: {swinfoData.Name}, but its spec is less than 1.3, as such this " +
                    $"mod will be ignored"
                );
                return;
            }

            var modAssemblies = new List<Assembly>();
            ISpaceWarpMod swMod = new AssetOnlyMod(swinfoData.Name);
            try
            {
                // Load the libraries as we get them
                if (Directory.Exists(Path.Combine(swinfo.Directory!.FullName, "lib")) && !ModList.DisabledPluginGuids.Contains(swinfoData.ModID))
                {
                    var dirInfo = new DirectoryInfo(Path.Combine(swinfo.Directory!.FullName, "lib"));
                    foreach (var dll in dirInfo.GetFiles("*.dll", SearchOption.AllDirectories))
                    {
                        modAssemblies.Add(Assembly.LoadFile(dll.FullName));
                    }
                }

                if (swinfoData.MainAssembly != null && !ModList.DisabledPluginGuids.Contains(swinfoData.ModID))
                {
                    Assembly? asm = FindModAssembly(swinfo.Directory!, swinfoData.MainAssembly);
                    if (asm is null)
                    {
                        Logger.LogError(
                            $"Main assembly {swinfoData.MainAssembly} for {swinfoData.Name} could not be found, this mod will be ignored");
                        return;
                    }

                    modAssemblies.Add(asm);

                    Type? modType = FindModType(asm);
                    if (modType != null)
                        swMod = new UnloadedMod(modType);
                }
            }
            catch (Exception exception) when (exception is ReflectionTypeLoadException or BadImageFormatException or FileLoadException)
            {
                RejectMod(swinfoData, swinfo.Directory!, modAssemblies, exception);
                return;
            }

            swMod.SWConfiguration =
                new JsonConfigFile(Path.Combine(swinfo.Directory.FullName, swinfoData.ModID + "-config.json"));

            var descriptor = new SpaceWarpPluginDescriptor(
                swMod,
                swinfoData.ModID,
                swinfoData.Name,
                swinfoData, swinfo.Directory,
                true,
                swMod.SWConfiguration
            );
            swMod.SWMetadata = descriptor;
            swMod.SWLogger = descriptor.Logger;
            descriptor.Assemblies.AddRange(modAssemblies);
            descriptor.ScriptFiles.AddRange(Directory
                .GetFiles(swinfo.Directory!.FullName, "*.lua", SearchOption.AllDirectories)
                .Where(f => !Path.GetFileName(f).StartsWith("_")));

            Logger.LogInfo($"Attempting to register mod: {swinfoData.ModID}, {swinfoData.Name}");

            if (PluginList.AllPlugins.Any(
                    x => string.Equals(x.Guid, swinfoData.ModID, StringComparison.InvariantCultureIgnoreCase)
                ))
            {
                return;
            }

            // Now we can just add it to our plugin list
            PluginList.RegisterPlugin(descriptor);
        }

        // Throws ReflectionTypeLoadException when any type in the assembly fails to load, which is how a mod built
        // against types this game version no longer has shows up.
        private static Type? FindModType(Assembly assembly)
        {
            foreach (Type type in assembly.GetTypes())
            {
                if (typeof(ISpaceWarpMod).IsAssignableFrom(type) && !type.IsAbstract)
                    return type;
            }

            return null;
        }

        // In the editor a mod's assemblies are compiled by Unity and already loaded, so they are found by name
        private static Assembly? FindModAssembly(DirectoryInfo folder, string relativePath)
        {
            if (Application.isEditor)
            {
                string shortName = Path.GetFileNameWithoutExtension(relativePath);
                return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly => assembly.GetName().Name == shortName);
            }

            string path = Path.Combine(folder.FullName, relativePath);
            return File.Exists(path) ? Assembly.LoadFile(path) : null;
        }

        private static string LoadErrorsOf(Exception exception)
        {
            IEnumerable<string> reasons = exception is ReflectionTypeLoadException typeLoadException
                ? typeLoadException.LoaderExceptions.OfType<Exception>().Select(loaderException => loaderException.Message).Distinct()
                : new[] { exception.Message };
            return string.Join("\n", reasons);
        }

        /// <summary>
        /// Creates the integrations of every loaded mod whose integrated mod is loaded too, loading each integration's
        /// assembly only then.
        /// </summary>
        internal static void LoadIntegrations()
        {
            foreach (SpaceWarpPluginDescriptor descriptor in PluginList.AllLoadedPlugins)
            {
                foreach (ModIntegrationInfo info in descriptor.SWInfo.Integrations)
                {
                    SpaceWarpPluginDescriptor? integrated = PluginList.AllLoadedPlugins.FirstOrDefault(other =>
                        string.Equals(other.Guid, info.ModID, StringComparison.OrdinalIgnoreCase));
                    if (integrated == null || integrated == descriptor)
                        continue;

                    Assembly? assembly;
                    try
                    {
                        assembly = FindModAssembly(descriptor.Folder, info.Assembly);
                    }
                    catch (Exception exception) when (exception is BadImageFormatException or FileLoadException)
                    {
                        descriptor.Logger.LogError(
                            $"Integration with {info.ModID} was not loaded because its assembly failed to load.\n{exception.Message}");
                        continue;
                    }

                    if (assembly == null)
                    {
                        descriptor.Logger.LogError($"Integration assembly {info.Assembly} for {info.ModID} could not be found");
                        continue;
                    }

                    CreateIntegration(descriptor, integrated, assembly);
                }
            }
        }

        /// <summary>
        /// Creates the integration an assembly holds and adds it to its mod, or logs why it cannot.
        /// </summary>
        /// <param name="current">The mod the integration belongs to.</param>
        /// <param name="integrated">The mod it integrates with.</param>
        /// <param name="assembly">The integration's assembly.</param>
        internal static void CreateIntegration(SpaceWarpPluginDescriptor current, SpaceWarpPluginDescriptor integrated,
            Assembly assembly)
        {
            Type[] integrationTypes;
            try
            {
                integrationTypes = assembly.GetTypes()
                    .Where(type => typeof(ModIntegration).IsAssignableFrom(type) && !type.IsAbstract)
                    .ToArray();
            }
            catch (ReflectionTypeLoadException exception)
            {
                // The integrated mod is present, so a type that cannot load means one of the two changed its API
                current.Logger.LogError(
                    $"Integration with {integrated.Guid} was not loaded because its assembly failed to load. " +
                    $"It was likely built for a different version of either mod.\n{LoadErrorsOf(exception)}");
                assembly.Reject();
                return;
            }

            if (integrationTypes.Length != 1)
            {
                current.Logger.LogError(
                    $"Integration assembly {assembly.GetName().Name} for {integrated.Guid} must hold exactly one " +
                    $"{nameof(ModIntegration)} subclass, but holds {integrationTypes.Length}");
                return;
            }

            ModIntegration integration;
            try
            {
                integration = (ModIntegration)Activator.CreateInstance(integrationTypes[0]);
            }
            catch (Exception exception)
            {
                current.Logger.LogError($"Integration with {integrated.Guid} could not be created: {exception}");
                return;
            }

            integration.CurrentMod = current;
            integration.IntegratedMod = integrated;
            current.AddIntegration(integration);
            current.Assemblies.Add(assembly);
        }

        // Records a mod whose assemblies failed to load as errored instead of registering it, and hides whatever of
        // it did load from the domain scans, since a loaded assembly cannot be unloaded.
        private static void RejectMod(ModInfo swinfoData, DirectoryInfo folder, List<Assembly> modAssemblies, Exception exception)
        {
            Logger.LogError(
                $"Mod {swinfoData.Name} ({swinfoData.ModID}) was not loaded because its assemblies failed to load. " +
                $"It was likely built for a different game version.\n{LoadErrorsOf(exception)}");

            foreach (Assembly assembly in modAssemblies)
            {
                assembly.Reject();
            }

            PluginList.NoteBadAssemblyError(new SpaceWarpPluginDescriptor(new AssetOnlyMod(swinfoData.Name),
                swinfoData.ModID, swinfoData.Name, swinfoData, folder));
        }

        private static void DisableMods()
        {
            foreach (var mod in ModList.DisabledPluginGuids)
            {
                PluginList.Disable(mod);
            }
        }
    }
}

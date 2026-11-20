using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;
using KSP.Game;
using KSP.Game.Flow;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PatchManager.Core.Cache;
using PatchManager.Core.Cache.Json;
using PatchManager.Core.Utility;
using PatchManager.LuaPatching;
using PatchManager.LuaPatching.Builtin;
using PatchManager.PrefabPatching;
using PatchManager.Shared;
using Redux.Features;
using Redux.IO;
using SpaceWarp2.API.Configuration;
using SpaceWarp2.API.Features;
using SpaceWarp2.API.Loading;
using SpaceWarp2.API.Mods;
using UniLinq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace PatchManager.Core.Assets
{
    /// <summary>
    /// Bridges the Lua patch <see cref="LuaPatching.Universe" /> to addressables: imports patch files, runs the
    /// patching pipeline against each addressable, and writes the results into the on-disk archive cache.
    /// </summary>
    internal static class PatchingManager
    {
        /// <summary>
        /// The current patch universe, created by <see cref="GenerateUniverse" />.
        /// </summary>
        internal static Universe Universe;

        private static PatchHashes CurrentPatchHashes = PatchHashes.CreateDefault();

        private static Dictionary<string, List<(string name, LuaAsset data)>> _createdAssets = new();

        // Script errors from running every mod's scripts once at boot, by mod, for each set's summary to pick from
        private static readonly Dictionary<string, List<(string filename, string reason)>> ScriptErrorsByMod = new();

        internal static bool UseIndentedOutput;

        // The umbrella feature the main menu set starts from
        private const string MAIN_MENU_FEATURE = "Ksp2Redux:All";

        /// <summary>
        /// Whether each set's cache is rebuilt the first time it is patched for in a play session, as in the editor.
        /// </summary>
        internal static bool AlwaysInvalidate;

        // A set's cache key does not cover the code that builds it, so an always-invalidating session rebuilds each
        // set once rather than only the one built at boot
        private static readonly HashSet<string> SetsBuiltThisSession = new(StringComparer.Ordinal);

        // Assets loaded into a label's rebuild by their own key, for stock assets that carry no label of their own
        private static readonly Dictionary<string, HashSet<string>> LabelMembers = new();

        /// <summary>
        /// Makes the asset with the given key part of a label's rebuild, so patches and duplicates on the label reach it.
        /// </summary>
        /// <remarks>
        /// The asset is still loaded by its own key afterwards, since the archive keeps it under its own name.
        /// </remarks>
        /// <param name="label">The label whose rebuild loads the asset.</param>
        /// <param name="key">The asset's addressables key.</param>
        internal static void AddLabelMember(string label, string key)
        {
            if (!LabelMembers.TryGetValue(label, out var members))
            {
                LabelMembers[label] = members = new HashSet<string>();
            }

            members.Add(key);
        }

        /// <summary>
        /// Total patches successfully applied this run.
        /// </summary>
        internal static int TotalPatchCount;

        /// <summary>
        /// Total patch errors encountered this run.
        /// </summary>
        internal static int TotalErrorCount;

        /// <summary>
        /// Total new assets created this run.
        /// </summary>
        internal static int TotalNewAssetCount;

        /// <summary>
        /// Total distinct asset definitions modified by patches this run.
        /// </summary>
        internal static int TotalDefinitionPatchCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ReleaseRebuildLoads();
            AbortAuthoredLuaCapture(null);
            Universe = null;
            CurrentPatchHashes = PatchHashes.CreateDefault();
            _createdAssets = new Dictionary<string, List<(string name, LuaAsset data)>>();
            ScriptErrorsByMod.Clear();
            UseIndentedOutput = false;
            TotalPatchCount = 0;
            TotalErrorCount = 0;
            TotalNewAssetCount = 0;
            TotalDefinitionPatchCount = 0;
            _rebuildStates = null;
            SetsBuiltThisSession.Clear();
        }

        /// <summary>
        /// Constructs a new patch <see cref="LuaPatching.Universe" /> seeded with every loaded SpaceWarp plugin's
        /// GUID plus the supplied single-file mod IDs.
        /// </summary>
        /// <param name="singleFileModIds">Mod IDs for single-file patches that are not registered as plugins.</param>
        public static void GenerateUniverse(HashSet<string> singleFileModIds)
        {
            var loadedPlugins = PluginList.AllLoadedPlugins.Select(x => x.Guid).ToList();
            loadedPlugins.AddRange(singleFileModIds);
            Universe = new(ObserveAuthoredLuaEngineError, PatchManager.Logger.LogMessage, loadedPlugins);
            BeginAuthoredLuaCapture();
        }

        /// <summary>
        /// Picks the feature set this boot patches for: the main menu umbrella plus the modding framework's defaults.
        /// </summary>
        /// <remarks>
        /// Runs once registration has closed, so features declared from Lua are in the registry.
        /// </remarks>
        public static void SelectBootFeatures()
        {
            ActivateSet(MainMenuFeatures());
        }

        /// <summary>
        /// Gets the set the main menu runs with: the main menu umbrella plus the modding framework's defaults.
        /// </summary>
        /// <returns>The enabled feature ids.</returns>
        internal static IReadOnlyCollection<string> MainMenuFeatures()
        {
            var installed = PluginList.AllLoadedPlugins.Select(descriptor => descriptor.Guid);
            return MainMenuSet(FeatureRegistry.Instance, installed);
        }

        /// <summary>
        /// Builds the set the main menu and a fresh boot run with: the main menu umbrella plus the default feature of
        /// each installed framework mod, closed over their requirements. Every other mod is off.
        /// </summary>
        /// <param name="registry">The feature registry.</param>
        /// <param name="installedModIds">The ids of the installed, enabled mods.</param>
        /// <returns>The enabled feature ids.</returns>
        internal static IReadOnlyCollection<string> MainMenuSet(FeatureRegistry registry, IEnumerable<string> installedModIds)
        {
            var requested = new List<string> { MAIN_MENU_FEATURE };
            requested.AddRange(installedModIds.Where(ModIds.AlwaysOn.Contains).Select(FeatureRegistry.DefaultFor));
            FeatureResolution resolution = registry.Resolve(requested);

            // No player chooses between a feature and its stand-ins here, so the feature itself stays on
            List<string> unchosen = resolution.Missing.Where(id => registry.TryGet(id, out _)).ToList();
            return unchosen.Count == 0 ? resolution.Enabled : registry.Resolve(requested.Concat(unchosen)).Enabled;
        }

        /// <summary>
        /// Gets the id of the set patched for and loaded from, which content cached across loads is keyed on.
        /// </summary>
        public static string ActiveSetId => CacheManager.ActiveSet;

        /// <summary>
        /// Switches to a feature set from inside a running flow, the way boot patches for its own set: the set's
        /// cache steps, its per-set data and the loading screen's patch counts run as the flow's next steps.
        /// </summary>
        /// <remarks>
        /// Does nothing when the set is already active.
        /// </remarks>
        /// <param name="flow">The flow running the calling step.</param>
        /// <param name="features">The set's enabled features, already closed over their requirements.</param>
        internal static void SwitchToSet(SequentialFlow flow, IEnumerable<string> features)
        {
            var set = features.ToList();
            if (IsActive(set))
                return;

            ActivateSet(set);
            var steps = SetRunSteps(next => InsertNext(flow, next), out _);
            steps.AddRange(SetLoadingSteps());
            steps.Add(new GenericFlowAction("Patch Manager: Showing patch counts", (resolve, _) =>
            {
                ShowInventoryOnLoadingScreen();
                resolve();
            }));
            InsertNext(flow, steps);
        }

        /// <summary>
        /// Splices steps into a running flow straight after the step running now.
        /// </summary>
        /// <param name="flow">The running flow.</param>
        /// <param name="actions">The steps, in order.</param>
        internal static void InsertNext(SequentialFlow flow, List<GenericFlowAction> actions)
        {
            // Inserted back to front so each insert at the same index pushes the previous one down
            var insertIndex = flow.flowIndex + 1;
            for (var i = actions.Count - 1; i >= 0; i--)
            {
                flow.FlowActions.Insert(insertIndex, actions[i]);
            }
        }

        /// <summary>
        /// Shows the active set's patch counts on the loading screen.
        /// </summary>
        internal static void ShowInventoryOnLoadingScreen()
        {
            var data = GameManager.Instance.Game.UI.UitkLoadingCurtain.Data;
            data.PatchManagerDefinitionsModifiedCount = CacheManager.Inventory.DefinitionCount;
            data.PatchManagerNewAssetCount = CacheManager.Inventory.NewAssetCount;
            data.PatchManagerPatchCount = CacheManager.Inventory.PatchCount;
            data.PatchManagerErrorCount = CacheManager.Inventory.ErrorCount;
        }

        /// <summary>
        /// Checks whether a set is the one already patched for and loaded from.
        /// </summary>
        /// <param name="features">The set's enabled features.</param>
        /// <returns>True if the set is active, false otherwise.</returns>
        internal static bool IsActive(IEnumerable<string> features) =>
            CacheManager.ActiveSet == SetIdOf(features);

        /// <summary>
        /// Makes a feature set the one patched for and loaded from: gates assets by it and opens its cache.
        /// </summary>
        /// <param name="features">The set's enabled features.</param>
        public static void ActivateSet(IEnumerable<string> features)
        {
            ActiveFeatures.Activate(features);
            PatchManager.Logger.LogInfo($"Patching for {Universe.EnabledFeatures.Count} enabled feature(s)");
            var setId = SetIdOf(Universe.EnabledFeatures);
            CacheManager.UseSet(setId);
            var ordered = Universe.EnabledFeatures.OrderBy(id => id, StringComparer.OrdinalIgnoreCase);
            PatchManagerSummaryLog.BeginSet(
                setId,
                $"Features: {string.Join(", ", ordered)}",
                CacheManager.SetDirectory(setId)
            );
        }

        /// <summary>
        /// Decides whether patching for a set rebuilds its cache, and counts it as rebuilt this session if so.
        /// </summary>
        /// <param name="setId">The set's id.</param>
        /// <returns>True if the set's cache is rebuilt even when valid, false otherwise.</returns>
        internal static bool RebuildsSet(string setId)
        {
            bool firstThisSession = SetsBuiltThisSession.Add(setId);
            return AlwaysInvalidate && firstThisSession;
        }

        /// <summary>
        /// Lists the steps that bring the active set's cache up to date, rebuilding it when it is stale.
        /// </summary>
        /// <remarks>
        /// The rebuild step learns its labels only once patches are set up, so it hands its per-label steps to
        /// <paramref name="insertNext" />, which must run them straight after the step itself.
        /// </remarks>
        /// <param name="insertNext">Schedules steps to run right after the one running now.</param>
        /// <param name="rebuilds">Whether the steps rebuild the cache.</param>
        /// <returns>The steps, in order.</returns>
        internal static List<GenericFlowAction> SetRunSteps(
            Action<List<GenericFlowAction>> insertNext,
            out bool rebuilds
        )
        {
            if (RebuildsSet(CacheManager.ActiveSet))
            {
                CacheManager.InvalidateCache();
            }

            var steps = new List<GenericFlowAction>();
            rebuilds = !InvalidateCacheIfNeeded();
            if (rebuilds)
            {
                steps.Add(new GenericFlowAction("Patch Manager: Registering all patches", (resolve, _) =>
                {
                    RegisterPatches();
                    resolve();
                }));
                steps.Add(new GenericFlowAction("Patch Manager: Creating New Assets", CreateNewAssets));
                steps.Add(new GenericFlowAction("Patch Manager: Rebuilding Cache",
                    (resolve, reject) => RebuildAllCache(insertNext, resolve)));
                steps.Add(new GenericFlowAction("Patch Manager: Saving Patch Summary", SavePatchSummary));
            }

            steps.Add(new GenericFlowAction("Patch Manager: Resolving Prefab Patch Plans", ResolvePrefabPatchPlans));
            return steps;
        }

        /// <summary>
        /// Lists the steps that clear and reload the data mods keep per feature set, for the set just activated.
        /// </summary>
        /// <returns>The steps, in order.</returns>
        internal static IEnumerable<GenericFlowAction> SetLoadingSteps() => SaveLoad.SetSteps();

        private static void SavePatchSummary(Action resolve, Action<string> reject)
        {
            Universe.Summary.RecognizedModIds = Universe.AllMods;
            Universe.Summary.ErroredFiles = ScriptErrorsInSet(ScriptErrorsByMod, Universe.EnabledFeatures);
            CacheManager.SaveSummary(Universe.Summary);
            resolve();
        }

        private static void ResolvePrefabPatchPlans(Action resolve, Action<string> reject)
        {
            var manifestSources = PluginList.AllLoadedPlugins
                .Where(descriptor => !string.IsNullOrWhiteSpace(descriptor.AddressablePrefabPatchLabel))
                .Select(descriptor => new PrefabPatchManifestSource
                {
                    OwnerModId = descriptor.Guid,
                    AddressablesLabel = descriptor.AddressablePrefabPatchLabel
                })
                .ToArray();
            PrefabPatchRuntime.DiscoverAndResolve(
                Universe.ModsOn,
                Universe.EnabledFeatures,
                manifestSources,
                () =>
                {
                    // The set's plans are known only now, so this is where its inventory is complete and the
                    // caches no set uses any more can go
                    CacheManager.Inventory.PlanFiles = PrefabPatchRuntime.PlanFiles.ToList();
                    CacheManager.SaveInventory();
                    CacheManager.EvictStaleSets(PinnedSetIds());
                    resolve();
                },
                reject
            );
        }

        /// <summary>
        /// Keeps the script hashes that can affect a feature set's output: those of mods whose default feature is
        /// in the set, those of Redux, and those whose owner cannot be told.
        /// </summary>
        /// <remarks>
        /// Every mod patch needs its mod's default feature, so a mod outside the set contributes nothing to the set's
        /// output and editing its scripts leaves the set's cache valid.
        /// </remarks>
        /// <param name="hashes">Script hashes keyed by file path or by <c>modId/asset</c> for addressable scripts.</param>
        /// <param name="folderByMod">Each mod's folder, by mod id.</param>
        /// <param name="enabled">The set's enabled features.</param>
        /// <returns>The hashes that belong in the set's cache key.</returns>
        internal static Dictionary<string, string> ScopeHashesToSet(
            IReadOnlyDictionary<string, string> hashes,
            IReadOnlyDictionary<string, string> folderByMod,
            ISet<string> enabled
        )
        {
            var scoped = new Dictionary<string, string>();
            foreach (var (key, hash) in hashes)
            {
                var owner = OwnerOf(key, folderByMod);
                if (FeatureSelector.AllowsMod(owner, enabled))
                {
                    scoped[key] = hash;
                }
            }

            return scoped;
        }

        // A file path is owned by the mod whose folder holds it, and an addressable script by the mod its key names
        [CanBeNull]
        private static string OwnerOf(string key, IReadOnlyDictionary<string, string> folderByMod)
        {
            var path = key.Replace('\\', '/');
            foreach (var (modId, folder) in folderByMod)
            {
                if (string.IsNullOrEmpty(folder))
                    continue;

                var prefix = folder.Replace('\\', '/').TrimEnd('/') + "/";
                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return modId;
            }

            var slash = path.IndexOf('/');
            if (slash <= 0)
                return null;

            var head = path.Substring(0, slash);
            return folderByMod.Keys.FirstOrDefault(modId => string.Equals(modId, head, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Picks the script errors a feature set's summary reports: those of Redux and of mods whose default
        /// feature is in the set.
        /// </summary>
        /// <param name="errorsByMod">Every mod's script errors, by mod id.</param>
        /// <param name="enabled">The set's enabled features.</param>
        /// <returns>The errors to report.</returns>
        internal static List<(string filename, string reason)> ScriptErrorsInSet(
            IReadOnlyDictionary<string, List<(string filename, string reason)>> errorsByMod,
            ISet<string> enabled
        ) => errorsByMod
            .Where(pair => FeatureSelector.AllowsMod(pair.Key, enabled))
            .SelectMany(pair => pair.Value)
            .ToList();

        /// <summary>
        /// Names a feature set's cache by its features, whatever order they come in.
        /// </summary>
        /// <param name="features">The set's enabled features.</param>
        /// <returns>The set's id.</returns>
        internal static string SetIdOf(IEnumerable<string> features)
        {
            var ordered = features.Select(id => id.ToLowerInvariant()).OrderBy(id => id, StringComparer.Ordinal);
            // A collision would serve one set's content to another without a word, so the id is a full SHA-256
            return Sha256.OfString(string.Join("\n", ordered));
        }

        // Every pack's and every single-player save's set, whose caches eviction keeps whatever their age
        private static ISet<string> PinnedSetIds()
        {
            var installed = PluginList.AllLoadedPlugins.Select(descriptor => descriptor.Guid);
            var packs = GameManager.Instance?.CampaignPacks?.AllCampaignPacks()
                    .Select(pack => (IReadOnlyCollection<string>)pack.Features)
                ?? Enumerable.Empty<IReadOnlyCollection<string>>();
            var saves = GameManager.Instance?.Game?.SaveLoadManager?.FeaturesOfSaves()
                ?? Enumerable.Empty<IReadOnlyCollection<string>>();
            return new HashSet<string>(PinnedSets(FeatureRegistry.Instance, packs, saves, installed).Select(SetIdOf));
        }

        /// <summary>
        /// Lists the sets whose patch caches are kept whatever their age: the set a new campaign of each pack starts
        /// with, and the set each save plays with.
        /// </summary>
        /// <param name="registry">The feature registry.</param>
        /// <param name="packFeatures">Each installed pack's features.</param>
        /// <param name="saveFeatures">Each save's requested features.</param>
        /// <param name="installedModIds">The installed, enabled mods.</param>
        /// <returns>Each pinned set's enabled features.</returns>
        internal static List<IReadOnlyList<string>> PinnedSets(
            FeatureRegistry registry,
            IEnumerable<IReadOnlyCollection<string>> packFeatures,
            IEnumerable<IReadOnlyCollection<string>> saveFeatures,
            IEnumerable<string> installedModIds
        )
        {
            var installed = installedModIds.ToList();
            return packFeatures
                .Select(features => (IReadOnlyCollection<string>)SessionManager.ForNewCampaign(features, installed, SpaceWarp2.API.ModList.FeatureDefaults, registry))
                .Concat(saveFeatures)
                .Select(requested => registry.Resolve(requested).Enabled)
                .ToList();
        }

        /// <summary>
        /// Finalizes the universe's patch registry and logs the total registered-patch and new-asset counts.
        /// </summary>
        public static void RegisterPatches()
        {
            PatchManager.Logger.LogInfo("Registering all patches!");
            TotalPatchCount = 0;
            TotalErrorCount = 0;
            TotalNewAssetCount = 0;
            TotalDefinitionPatchCount = 0;
            CSharpPatching.FluentPatchRegistry.Flush(Universe);
            Universe.SetupPatchesForRun();
            ObserveAuthoredLuaRegistration();
            PatchManager.Logger.LogInfo($"{Universe.TotalPatchCount} patchers registered!");
            PatchManager.Logger.LogInfo($"{Universe.AllNewAssets.Count} assets created!");
        }

        /// <summary>
        /// Hashes every mod's Lua files into the patch-cache checksum.
        /// </summary>
        /// <remarks>
        /// Runs before the cache-validity decision, so the decision sees whether any patch source changed since
        /// last launch. Uses the same string hash as <see cref="CollectScriptResults" /> so the two agree.
        /// </remarks>
        public static void HashScriptFiles()
        {
            foreach (var descriptor in PluginList.AllLoadedPlugins)
            {
                // KSP1 mods carry no Lua and their folders are whole GameData trees, so sweeping them is a lot
                // of directory walking for nothing.
                if (descriptor.IsKsp1)
                    continue;

                HashScriptsForMod(descriptor.Folder?.FullName, descriptor.ScriptFiles, CurrentPatchHashes);
            }
        }

        /// <summary>
        /// Hashes one mod's declared script files plus every other Lua file in its folder into
        /// <paramref name="into" />.
        /// </summary>
        /// <remarks>
        /// The folder sweep is what catches the <c>_</c>-prefixed library files. Those are excluded from a
        /// descriptor's declared scripts because they are pulled in with <c>require</c> rather than run as
        /// bodies, but they shape patch output all the same, so leaving them out of the key lets an edited
        /// library serve stale archives. Sweeping the folder also covers a library required across mods, since
        /// every mod's own folder gets swept in turn.
        /// </remarks>
        /// <param name="modFolder">The mod's folder, or null when it has none.</param>
        /// <param name="declaredScriptFiles">The script files the descriptor declared at discovery.</param>
        /// <param name="into">The patch hash set to record into.</param>
        internal static void HashScriptsForMod(
            string modFolder,
            IEnumerable<string> declaredScriptFiles,
            PatchHashes into
        )
        {
            foreach (var file in declaredScriptFiles)
            {
                if (File.Exists(file))
                {
                    into.Patches[file] = Hash.FromString(File.ReadAllText(file));
                }
            }

            if (string.IsNullOrEmpty(modFolder) || !Directory.Exists(modFolder))
                return;

            // Keyed by path, so a file the descriptor already declared just gets the same hash written twice.
            foreach (var file in Directory.EnumerateFiles(modFolder, "*.lua", SearchOption.AllDirectories))
            {
                into.Patches[file] = Hash.FromString(File.ReadAllText(file));
            }
        }

        /// <summary>
        /// Hashes one mod's addressables catalog into <paramref name="into" />.
        /// </summary>
        /// <remarks>
        /// The cache holds patched copies of a mod's content assets, so a rebuilt mod has to invalidate it. The
        /// manifest cannot be trusted for that, since authors rebuild without bumping its version. The catalog
        /// names each bundle by its content hash and CRC, so any rebuild that changes content changes it.
        /// </remarks>
        /// <param name="modFolder">The mod's folder, or null when it has none.</param>
        /// <param name="into">The patch hash set to record into.</param>
        internal static void HashCatalogForMod(string modFolder, PatchHashes into)
        {
            if (string.IsNullOrEmpty(modFolder))
                return;

            // Keyed by path, so set scoping finds the owning mod by its folder as it does for Lua files
            var catalogPath = Path.Combine(modFolder, "addressables", "catalog.json");
            if (!File.Exists(catalogPath))
                return;

            into.Patches[catalogPath] = Hash.FromString(File.ReadAllText(catalogPath));
        }

        /// <summary>
        /// Collects what the runtime exposed per descriptor: every ran script's hash into the patch-cache
        /// checksum, and every script error into the patch summary.
        /// </summary>
        public static void CollectScriptResults()
        {
            if (Redux.ReduxAssets.IsSourceOnly)
                CurrentPatchHashes.Patches["Ksp2Redux/<authored-source>"] = Hash.FromString(
                    SpaceWarp2.API.Lifecycle.ModScriptRuntime.AuthoredSourceContractHash + ":" + Application.buildGUID
                    + ":" + (string)(_authoredLuaRequest?["diagnosticCandidate"] as JObject)?["sha256"]
                    + ":" + (string)_authoredLuaCapture?["controlSha256"]);
            foreach (var descriptor in PluginList.AllLoadedPlugins)
            {
                foreach (var script in descriptor.LoadedScripts)
                {
                    CurrentPatchHashes.Patches[script.Key] = Hash.FromString(script.Value);
                }

                if (descriptor.ScriptErrors.Count > 0)
                {
                    ScriptErrorsByMod[descriptor.Guid] = descriptor.ScriptErrors.ToList();
                }
            }
        }

        /// <summary>
        /// Decides whether the set's cache still holds what patching would produce now, and starts it afresh
        /// when it does not.
        /// </summary>
        /// <remarks>
        /// Only scripts, mod manifests and addressables catalogs of mods in the set count, so changing a mod the
        /// set leaves out keeps the set's cache.
        /// </remarks>
        /// <returns>True if the cache is valid, false if it was invalidated.</returns>
        public static bool InvalidateCacheIfNeeded()
        {
            var folderByMod = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var descriptor in PluginList.AllLoadedPlugins)
            {
                folderByMod[descriptor.Guid] = descriptor.Folder?.FullName;
                CurrentPatchHashes.Patches[$"{descriptor.Guid}/<swinfo>"] =
                    Hash.FromString(JsonConvert.SerializeObject(descriptor.SWInfo));
                HashCatalogForMod(descriptor.Folder?.FullName, CurrentPatchHashes);
            }

            // Every set scopes the same full hashes, so the full ones are kept for the next set's run
            var setHashes = PatchHashes.CreateDefault();
            setHashes.Patches = ScopeHashesToSet(CurrentPatchHashes.Patches, folderByMod, Universe.EnabledFeatures);
            var checksum = Hash.FromJsonObject(setHashes);

            // Imported source edits have no player build GUID while running in the authoring editor.
            if (CacheManager.Inventory.Checksum == checksum && !(Redux.ReduxAssets.IsSourceOnly && Application.isEditor))
            {
                PatchManager.Logger.LogInfo("Cache is valid, skipping rebuild.");
                CacheManager.CacheValidLabels.AddRange(CacheManager.Inventory.CacheEntries.Keys);
                return true;
            }

            PatchManager.Logger.LogInfo("Cache is invalid, rebuilding.");
            CacheManager.InvalidateCache();
            CacheManager.Inventory.Checksum = checksum;
            CacheManager.Inventory.Patches = setHashes;

            return false;
        }

        /// <summary>
        /// Collects every queued new asset from the universe into the per-label staging dictionary, then resolves
        /// the supplied callback.
        /// </summary>
        /// <param name="resolve">Callback invoked once collection finishes.</param>
        /// <param name="reject">Reject callback (currently unused).</param>
        public static void CreateNewAssets(Action resolve, Action<string> reject)
        {
            var enabledAssets = Universe.StageNewAssetsForRun();
            foreach (var generator in enabledAssets)
            {
                try
                {
                    PatchManager.Logger.LogDebug($"Generated an asset with the label {generator.Label}, and name {generator.Name}");

                    if (!_createdAssets.ContainsKey(generator.Label))
                    {
                        _createdAssets[generator.Label] = new List<(string name, LuaAsset data)>();
                    }

                    if (!_createdAssets[generator.Label].Any(x => x.name == generator.Name))
                    {
                        _createdAssets[generator.Label].Add((generator.Name, generator));
                    }

                }
                catch (Exception e)
                {
                    TotalErrorCount += 1;
                    PatchManager.Logger.LogError($"Failed to generate an asset due to: {e}");
                    Universe.Summary.BeginLabel(generator.Label);
                    Universe.Summary.BeginAsset(generator.Name, null);
                    Universe.Summary.Error(generator.Name, e);
                }
            }

            TotalNewAssetCount = enabledAssets.Count;
            UpdateLoadingBarData();

            resolve();
        }


        private static Dictionary<string, LabelRebuildState> _rebuildStates;

        /// <summary>
        /// Schedules per-(pass, label) flow actions in pass-major order: every label's actions for one pass come
        /// before any label's actions for the next, in the order the passes sort into.
        /// </summary>
        /// <remarks>
        /// A label only receives an action for a pass if it has a patch in that pass. The first action a label
        /// receives lazily loads its addressables. The last action writes the label's archive and releases its
        /// load handle. A final action persists totals and inventory.
        /// </remarks>
        /// <param name="insertNext">Schedules the per-label actions to run straight after this one.</param>
        /// <param name="resolve">Callback invoked once scheduling finishes.</param>
        public static void RebuildAllCache(Action<List<GenericFlowAction>> insertNext, Action resolve)
        {
            Universe.BeginRun();
            var labels = Universe.PatchedLabels.Concat(_createdAssets.Keys).Distinct().ToList();
            string diagnosticLabel = (string)_authoredLuaRequest?["label"];
            if (_authoredLuaPlan == null && IsAuthoredLuaTarget(diagnosticLabel) && !labels.Contains(diagnosticLabel))
                labels.Add(diagnosticLabel);

            if (labels.Count == 0)
            {
                Universe.EndRun();
                FinishAuthoredLuaCapture();
                resolve();
                return;
            }

            InitRebuildStates(labels);

            var activePassesPerLabel = new Dictionary<string, List<string>>(labels.Count);
            foreach (var label in labels)
            {
                activePassesPerLabel[label] = ActivePassesFor(label);
            }

            var actions = new List<GenericFlowAction>();

            foreach (var pass in Universe.OrderedPasses)
            {
                foreach (var label in labels)
                {
                    var active = activePassesPerLabel[label];
                    if (!active.Contains(pass))
                        continue;

                    var isFirst = active[0] == pass;
                    var isLast = active[active.Count - 1] == pass;
                    actions.Add(MakePassAction(label, pass, isFirst, isLast));
                }
            }

            actions.Add(new GenericFlowAction(
                "Patching: Finalize",
                (r, _) =>
                {
                    try
                    {
                        FinalizeRebuild();
                        Universe.EndRun();
                        FinishAuthoredLuaCapture();
                    }
                    catch (Exception error)
                    {
                        AbortAuthoredLuaCapture(error);
                        throw;
                    }
                    r();
                }
            ));

            insertNext(actions);
            resolve();
        }

        private static void InitRebuildStates(List<string> labels)
        {
            ReleaseRebuildLoads();
            _rebuildStates = new Dictionary<string, LabelRebuildState>(labels.Count);
            foreach (var label in labels)
            {
                var state = new LabelRebuildState
                {
                    Label = label
                };
                _rebuildStates[label] = state;

                if (_createdAssets.TryGetValue(label, out var created))
                {
                    foreach (var (name, luaAsset) in created)
                    {
                        state.CreatedAssets.Add((name, luaAsset));
                        state.Unchanged = false;
                        var addressAlias = name.EndsWith(".json") ? null : name + ".json";
                        if (addressAlias != null) state.AddressAliases[name] = addressAlias;
                    }
                    created.Clear();
                    _createdAssets.Remove(label);
                }
            }
        }

        private static List<string> ActivePassesFor(string label)
        {
            var result = new List<string>();
            foreach (var pass in Universe.OrderedPasses)
            {
                if (HasPatchesInPass(label, pass)) result.Add(pass);
            }
            if (_authoredLuaPlan == null && result.Count == 0 && IsAuthoredLuaTarget(label))
            {
                if (!Universe.OrderedPasses.Contains(PassDefinition.DEFAULT))
                    throw new InvalidDataException("The diagnostic source observation has no ordinary default pass.");
                result.Add(PassDefinition.DEFAULT);
            }
            if (result.Count == 0
                && _rebuildStates.TryGetValue(label, out var state)
                && state.CreatedAssets.Count > 0)
            {
                result.Add(PassDefinition.DEFAULT);
            }
            return result;
        }

        private static bool HasPatchesInPass(string label, string pass)
        {
            if (!Universe.AllPatchesBuckets.TryGetValue(label, out var perPass))
                return false;
            if (!perPass.TryGetValue(pass, out var buckets))
                return false;
            return buckets.MatchAll.Length > 0
                || buckets.Wildcard.Length > 0
                || buckets.Exact.Count > 0;
        }

        private static GenericFlowAction MakePassAction(string label, string pass, bool loadFirst, bool writeLast)
        {
            var labelCopy = label;
            var passCopy = pass;
            var loadCopy = loadFirst;
            var writeCopy = writeLast;

            var passSuffix = pass == PassDefinition.DEFAULT ? "" : $" [{pass.ToUpperInvariant()}]";

            return new GenericFlowAction(
                $"Patching: {label}{passSuffix}",
                (resolve, _) => CoroutineUtil.Instance.DoCoroutine(
                    RunPassActionCoroutine(labelCopy, passCopy, loadCopy, writeCopy, resolve))
            );
        }

        private static IEnumerator RunPassActionCoroutine(
            string label,
            string pass,
            bool loadFirst,
            bool writeLast,
            Action resolve
        )
        {
            bool completed = false;
            IEnumerator load = loadFirst ? LoadLabel(label) : null;
            try
            {
                if (load != null)
                {
                    while (load.MoveNext())
                    {
                        yield return load.Current;
                    }
                }
                RunPassForLabel(label, pass);
                if (writeLast)
                    WriteAndReleaseLabel(label);
                resolve();
                completed = true;
            }
            finally
            {
                (load as IDisposable)?.Dispose();
                if (!completed)
                    ReleaseRebuildLoads();
            }
        }

        private static IEnumerator LoadLabel(string label)
        {
            if (_rebuildStates == null || !_rebuildStates.TryGetValue(label, out var state))
                yield break;

            var locations = new List<IResourceLocation>();
            var locHandle = Addressables.LoadResourceLocationsAsync(label, typeof(TextAsset));
            try
            {
                while (!locHandle.IsDone)
                {
                    UpdateLoadingBarData();
                    yield return null;
                }
                if (locHandle.Status != AsyncOperationStatus.Succeeded || locHandle.Result == null)
                    throw new InvalidOperationException($"Failed to resolve patch input locations for {label}.", locHandle.OperationException);
                locations.AddRange(FeatureAssetIndex.FilterContent(locHandle.Result, label));
            }
            finally
            {
                if (locHandle.IsValid())
                    Addressables.Release(locHandle);
            }

            if (LabelMembers.TryGetValue(label, out var members))
            {
                foreach (string key in members)
                {
                    if (FeatureAssetIndex.TryResolveKey(key, typeof(TextAsset), label, true, out var memberLocation))
                    {
                        if (memberLocation != null)
                            locations.Add(memberLocation);
                        continue;
                    }
                    // Resolve an ungated member's real location before loading it by any alias.
                    var candidates = new List<IResourceLocation>();
                    foreach (var locator in Addressables.ResourceLocators)
                    {
                        if (locator.Locate(key, typeof(TextAsset), out IList<IResourceLocation> found) && found != null)
                            candidates.AddRange(found);
                    }
                    List<IResourceLocation> accepted = FeatureAssetIndex.FilterContent(candidates, label);
                    if (accepted.Count > 1)
                        throw new InvalidOperationException($"Ambiguous patch member location for {label}:{key}.");
                    locations.AddRange(accepted);
                }
            }

            bool loaded = false;
            try
            {
                var acceptedByKey = new Dictionary<string, IResourceLocation>(StringComparer.Ordinal);
                foreach (IResourceLocation location in locations)
                {
                    string key = FeatureAssetIndex.KeyOf(location);
                    if (acceptedByKey.TryGetValue(key, out IResourceLocation previous))
                    {
                        if (!SameTextLocation(previous, location))
                            throw new InvalidOperationException($"Contradictory patch input locations for {label}:{key}.");
                        continue;
                    }
                    acceptedByKey.Add(key, location);
                    // Start every accepted load before waiting. Each handle keeps its own exact location.
                    state.LoadHandles.Add((location, Addressables.LoadAssetAsync<TextAsset>(location)));
                }
                foreach (var (location, handle) in state.LoadHandles)
                {
                    while (!handle.IsDone)
                    {
                        UpdateLoadingBarData();
                        yield return null;
                    }
                    if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                        throw new InvalidOperationException($"Failed to load patch input {label}:{location.PrimaryKey}.", handle.OperationException);
                    BindLoadedText(state, location, handle.Result);
                }
                loaded = true;
            }
            finally
            {
                if (!loaded)
                    ReleaseLabelLoads(state);
            }

            ObserveAuthoredLuaLocations(state);
            ObserveAuthoredLuaValues(state, true);
            if (state.CreatedAssets.Count > 0)
            {
                Universe.Summary.BeginLabel(label);
                foreach (var (name, _) in state.CreatedAssets)
                {
                    state.AddressAliases.TryGetValue(name, out var alias);
                    Universe.Summary.BeginNewAsset(name, alias);
                }
            }
        }

        private static bool SameTextLocation(IResourceLocation first, IResourceLocation second) =>
            JToken.DeepEquals(AuthoredLuaLocationGraph(first, new HashSet<IResourceLocation>()),
                AuthoredLuaLocationGraph(second, new HashSet<IResourceLocation>()));

        private static void BindLoadedText(LabelRebuildState state, IResourceLocation location, TextAsset asset)
        {
            if (asset == null || string.IsNullOrEmpty(asset.name) || string.IsNullOrEmpty(location.PrimaryKey))
                throw new InvalidOperationException("Patch input has no loaded name or primary address.");
            string assetType = asset.GetType().AssemblyQualifiedName;
            if (state.LocationsByName.TryGetValue(asset.name, out var previous))
            {
                if (!SameTextLocation(previous.location, location) || previous.assetType != assetType)
                    throw new InvalidOperationException($"Distinct patch input locations share the loaded name {state.Label}:{asset.name}.");
                if (state.RawTexts.TryGetValue(asset.name, out string previousText) && previousText != asset.text)
                    throw new InvalidOperationException($"One patch input location returned conflicting text for {state.Label}:{asset.name}.");
                return;
            }
            state.LocationsByName.Add(asset.name, (location, assetType));
            state.PrimaryKeyMap.Add(asset.name, location.PrimaryKey);
            if (!string.IsNullOrEmpty(asset.text))
                state.RawTexts.Add(asset.name, asset.text);
        }

        private static void ReleaseLabelLoads(LabelRebuildState state)
        {
            foreach (var (_, handle) in state.LoadHandles)
            {
                // Failed and unfinished operations still own a reference and must be released.
                if (handle.IsValid())
                    Addressables.Release(handle);
            }
            state.LoadHandles.Clear();
        }

        private static void ReleaseRebuildLoads()
        {
            if (_rebuildStates == null)
                return;
            foreach (var state in _rebuildStates.Values)
            {
                ReleaseLabelLoads(state);
            }
        }

        private static void RunPassForLabel(string label, string pass)
        {
            if (_rebuildStates == null || !_rebuildStates.TryGetValue(label, out var state))
                return;

            Universe.Summary.BeginPass(pass);
            Universe.Summary.BeginLabel(label);

            var duplicates = new List<Universe.DuplicatedAsset>();
            var assetNames = state.RawTexts.Keys.Concat(state.Tokens.Keys).Distinct().ToList();
            foreach (var assetName in assetNames)
            {
                if (!Universe.HasAnyPatchInPass(label, assetName, pass))
                    continue;

                var token = EnsureParsed(state, assetName);
                if (token == null)
                    continue;

                var address = state.PrimaryKeyMap.TryGetValue(assetName, out var a) ? a : "<unknown>";
                Universe.Summary.BeginAsset(assetName, address);

                var result = Universe.RunAllPatchesFor(label, assetName, token, pass, duplicates, -1, out var pc, out var ec);
                TotalPatchCount += pc;
                TotalErrorCount += ec;
                if (pc > 0)
                {
                    state.Unchanged = false;
                    if (state.PatchedAssetNames.Add(assetName))
                    {
                        TotalDefinitionPatchCount++;
                    }
                }
                if (result == null)
                {
                    state.Tokens.Remove(assetName);
                }
                else
                {
                    state.Tokens[assetName] = result;
                }
            }

            foreach (var (name, luaAsset) in state.CreatedAssets)
            {
                state.AddressAliases.TryGetValue(name, out var alias);
                Universe.Summary.BeginAsset(name, alias);
                Universe.RunAllPatchesFor(luaAsset, pass, duplicates, out var pc, out var ec);
                TotalPatchCount += pc;
                TotalErrorCount += ec;
            }

            AddDuplicates(state, label, pass, duplicates);
            ObserveAuthoredLuaPass(label, pass);
            UpdateLoadingBarData();
        }

        // Keeps the copies a pass's duplicate patches made, each patched by the rest of that pass. The list grows
        // while it is walked, so a copy that is itself duplicated later in the pass is handled the same way.
        private static void AddDuplicates(LabelRebuildState state, string label, string pass,
            List<Universe.DuplicatedAsset> duplicates)
        {
            for (var i = 0; i < duplicates.Count; i++)
            {
                var duplicate = duplicates[i];
                if (HasAsset(state, duplicate.Name))
                {
                    TotalErrorCount++;
                    Universe.Summary.BeginAsset(duplicate.Name, null);
                    Universe.Summary.Error(duplicate.PatchName,
                        $"an asset named {duplicate.Name} already exists in {label}, so the copy was dropped");
                    continue;
                }

                var alias = duplicate.Name.EndsWith(".json") ? null : duplicate.Name + ".json";
                if (alias != null) state.AddressAliases[duplicate.Name] = alias;
                Universe.Summary.BeginNewAsset(duplicate.Name, alias);
                state.Unchanged = false;

                var result = Universe.RunAllPatchesFor(label, duplicate.Name, duplicate.Data, pass, duplicates,
                    duplicate.AfterOrder, out var pc, out var ec);
                TotalPatchCount += pc;
                TotalErrorCount += ec;
                if (result != null)
                {
                    state.Tokens[duplicate.Name] = result;
                }
            }
        }

        private static bool HasAsset(LabelRebuildState state, string assetName) =>
            state.Tokens.ContainsKey(assetName) ||
            state.RawTexts.ContainsKey(assetName) ||
            state.CreatedAssets.Any(created => created.name == assetName);

        private static JToken EnsureParsed(LabelRebuildState state, string assetName)
        {
            if (state.Tokens.TryGetValue(assetName, out var token))
                return token;
            if (!state.RawTexts.TryGetValue(assetName, out var rawText))
                return null;
            try
            {
                token = JToken.Parse(rawText);
            }
            catch (Exception e)
            {
                TotalErrorCount += 1;
                PatchManager.Logger.LogError($"Failed to parse {state.Label}:{assetName}: {e.Message}");
                Universe.Summary.BeginLabel(state.Label);
                Universe.Summary.BeginAsset(assetName, null);
                Universe.Summary.Error(assetName, e);
                state.RawTexts.Remove(assetName);
                return null;
            }
            state.Tokens[assetName] = token;
            state.RawTexts.Remove(assetName);
            return token;
        }

        private static void WriteAndReleaseLabel(string label)
        {
            if (_rebuildStates == null || !_rebuildStates.TryGetValue(label, out var state))
                return;
            try
            {
                ObserveAuthoredLuaValues(state, false);
                if (!state.Unchanged)
                    WriteArchive(state);
            }
            finally
            {
                ReleaseLabelLoads(state);
                _rebuildStates.Remove(label);
            }
        }

        private static void FinalizeRebuild()
        {
            try
            {
                CacheManager.SetTotalPatchCount(TotalPatchCount);
                CacheManager.SetTotalErrorCount(TotalErrorCount);
                CacheManager.SetTotalDefinitionCount(TotalDefinitionPatchCount);
                CacheManager.SetTotalAssetCount(TotalNewAssetCount);
                CacheManager.SaveInventory();
                UpdateLoadingBarData();
            }
            finally
            {
                ReleaseRebuildLoads();
                _rebuildStates = null;
            }
        }

        private static void WriteArchive(LabelRebuildState state)
        {
            var formatting = UseIndentedOutput ? Formatting.Indented : Formatting.None;
            var entries = new List<(string name, string text)>();

            foreach (var (assetName, token) in state.Tokens)
            {
                var serialized = token?.ToString(formatting);
                if (!string.IsNullOrEmpty(serialized))
                {
                    entries.Add((assetName, serialized));
                }
            }

            foreach (var (assetName, rawText) in state.RawTexts)
            {
                if (!string.IsNullOrEmpty(rawText))
                {
                    entries.Add((assetName, rawText));
                }
            }

            foreach (var (name, luaAsset) in state.CreatedAssets)
            {
                try
                {
                    var serialized = luaAsset.ConverterInstance.ToJson(luaAsset.CurrentValue)?.ToString(formatting);
                    if (!string.IsNullOrEmpty(serialized))
                    {
                        entries.Add((name, serialized));
                    }
                }
                catch (Exception e)
                {
                    TotalErrorCount += 1;
                    PatchManager.Logger.LogError($"Failed to serialize {state.Label}:{name}: {e.Message}");
                    Universe.Summary.BeginLabel(state.Label);
                    Universe.Summary.BeginAsset(name, null);
                    Universe.Summary.Error(name, e);
                }
            }

            // Named by content, so a set whose label patches out the same as another set's reuses its file
            var archiveFilename = CacheManager.WriteArchive(entries);

            var labelCacheEntry = new CacheEntry
            {
                Label = state.Label,
                ArchiveFilename = archiveFilename,
                Assets = new List<string>()
            };
            var assetsCacheEntries = new Dictionary<string, CacheEntry>();

            void AddKey(string key, string assetName)
            {
                assetsCacheEntries[key] = new CacheEntry
                {
                    Label = key,
                    ArchiveFilename = archiveFilename,
                    Assets = new List<string> { assetName }
                };
            }

            foreach (var (assetName, _) in entries)
            {
                labelCacheEntry.Assets.Add(assetName);
                AddKey(assetName, assetName);
                if (state.PrimaryKeyMap.TryGetValue(assetName, out var address) && address != assetName)
                {
                    AddKey(address, assetName);
                }

                // Created assets and copies made by duplicate patches have no address of their own, so they take an alias
                if (state.AddressAliases.TryGetValue(assetName, out var alias))
                {
                    AddKey(alias, assetName);
                }
            }

            CacheManager.CacheValidLabels.Add(state.Label);
            CacheManager.Inventory.CacheEntries.Add(state.Label, labelCacheEntry);
            CacheManager.Inventory.CacheEntries.AddRangeUnique(assetsCacheEntries);
        }

        // Source-player diagnostics attach before mod bodies. The ordinary patch engine remains the only runner.
        private static JObject _authoredLuaPlan;
        private static JObject _authoredLuaPlanPin;
        private static string _authoredLuaEpisodeId;
        private static List<(JObject request, JObject capture)> _authoredLuaWindows;

        private static readonly string[] AUTHORED_LUA_SHARED_FIELDS =
        {
            "engine", "productionEngine", "captureFailurePolicy", "sourceOnly", "coreMvid", "coreFullName",
            "sources", "sourceSaveTraceVersion", "patchIdentity", "bodyEffects", "bodyRegistry",
            "qualifiedSourceExclusions", "diagnosticCandidateExclusions", "engineErrors", "sourceWrites",
            "runtimeBaseApplicationRetired", "rootEnvironment", "interpreter", "interpreterMvid",
            "runtimeInterpreter", "features", "mods", "orderedPasses", "beginCallbacks", "endCallbacks",
            "converters", "contributors", "isTargetRun", "allRequestedSourcesObserved",
            "allObservedSourcesSucceeded", "totalPatchErrors", "normalPipelineCompleted", "inputsUnchanged",
            "observerSlotOwned"
        };

        private static JObject ReadAuthoredLuaWindow(JObject row, string scope, string root)
        {
            JObject pin = (JObject)row["request"];
            string path = (string)pin["path"];
            CheckAuthoredLuaPath(path, scope);
            if (new FileInfo(path).Length >= 8 * 1024 * 1024 || Sha256.OfFile(path) != (string)pin["sha256"])
                throw new InvalidDataException("A predeclared Lua window request changed or exceeded its bound.");
            var request = JObject.Parse(File.ReadAllText(path));
            if ((string)request["schema"] != "omnipatching.lua-capture-request.v1"
                || (string)request["captureFailurePolicy"] != "detach-and-continue-v1"
                || (string)request["projectRoot"] != root || request["names"] is not JArray names
                || names.Count is < 1 or > 32 || names.Values<string>().Distinct(StringComparer.Ordinal).Count() != names.Count
                || (string)request["episodePlanId"] != (string)_authoredLuaPlan["id"]
                || (string)request["windowId"] != (string)row["id"] || string.IsNullOrEmpty((string)row["id"])
                || (string)request["label"] != (string)_authoredLuaPlan["label"])
                throw new InvalidDataException("Foreign or unbounded predeclared Lua window.");
            CheckAuthoredLuaPath((string)request["output"], scope);
            foreach (string filename in new[] { "input.json", "projection.json", "report.json", "failed.json" })
            {
                CheckAuthoredLuaPath(Path.Combine((string)request["output"], filename), scope);
                CheckAuthoredLuaPath(Path.Combine((string)request["output"], filename) + ".tmp", scope);
            }
            if (Directory.Exists((string)request["output"]) || File.Exists((string)request["output"]))
                throw new IOException("Preserve the previous planned Lua window output.");
            return request;
        }

        private static JObject BeginAuthoredLuaPlan(JObject plan, string control, string expected, string root, string scope)
        {
            if ((string)plan["schema"] != "omnipatching.lua-window-plan.v1" || (string)plan["projectRoot"] != root
                || (string)plan["captureFailurePolicy"] != "detach-and-continue-v1"
                || string.IsNullOrEmpty((string)plan["id"]) || string.IsNullOrEmpty((string)plan["label"])
                || plan["windows"] is not JArray windows || windows.Count is < 1 or > 256)
                throw new InvalidDataException("Unsupported finite Lua window plan.");
            _authoredLuaPlan = plan;
            _authoredLuaPlanPin = new JObject { ["path"] = control, ["size"] = new FileInfo(control).Length, ["sha256"] = expected };
            _authoredLuaEpisodeId = Guid.NewGuid().ToString("N");
            CheckAuthoredLuaPath((string)plan["output"], scope);
            CheckAuthoredLuaPath(Path.Combine((string)plan["output"], "execution.json.tmp"), scope);
            CheckAuthoredLuaPath(Path.Combine((string)plan["output"], "failed.json.tmp"), scope);
            if (Directory.Exists((string)plan["output"]) || File.Exists((string)plan["output"]))
                throw new IOException("Preserve the previous Lua episode output.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var selected = new HashSet<string>(StringComparer.Ordinal);
            var outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            JObject first = null;
            foreach (JObject row in windows)
            {
                JObject request = ReadAuthoredLuaWindow(row, scope, root);
                if (!ids.Add((string)row["id"]) || !outputs.Add(Path.GetFullPath((string)request["output"]))
                    || request["names"].Values<string>().Any(name => !selected.Add(name)))
                    throw new InvalidDataException("Repeated Lua window, output or requested spelling.");
                if (first == null)
                    first = request;
                foreach (string field in new[] { "features", "sources", "suppressedSources", "diagnosticCandidate", "runtimeCore", "patchIdentity" })
                {
                    if (!JToken.DeepEquals(first[field], request[field]))
                        throw new InvalidDataException("A Lua plan mixed initialized source or feature contexts: " + field);
                }
                foreach (JObject input in request["inputs"])
                {
                    if (!plan["inputs"].Any(pin => JToken.DeepEquals(pin, input)))
                        throw new InvalidDataException("A Lua window input is absent from the plan.");
                }
                if (!plan["inputs"].Any(pin => JToken.DeepEquals(pin, row["request"])))
                    throw new InvalidDataException("A Lua request is absent from its plan inputs.");
            }
            foreach (JObject pin in plan["inputs"])
            {
                CheckAuthoredLuaPath((string)pin["path"], root);
                if (Sha256.OfFile((string)pin["path"]) != (string)pin["sha256"])
                    throw new InvalidDataException("A Lua plan input changed.");
            }
            Directory.CreateDirectory((string)plan["output"]);
            return first;
        }

        private static void InitializeAuthoredLuaWindows(string scope, string root)
        {
            if (_authoredLuaPlan == null)
                return;
            _authoredLuaWindows = new List<(JObject request, JObject capture)>();
            foreach (JObject row in _authoredLuaPlan["windows"])
            {
                JObject request = (string)row["id"] == (string)_authoredLuaRequest["windowId"]
                    ? _authoredLuaRequest : ReadAuthoredLuaWindow(row, scope, root);
                JObject capture = _authoredLuaWindows.Count == 0 ? _authoredLuaCapture : (JObject)_authoredLuaCapture.DeepClone();
                capture["controlSha256"] = row["request"]["sha256"].DeepClone();
                capture["windowStages"] = new JArray();
                if (_authoredLuaWindows.Count > 0)
                    Directory.CreateDirectory((string)request["output"]);
                _authoredLuaWindows.Add((request, capture));
            }
        }

        private static void ForEachAuthoredLuaWindow(Action observe)
        {
            if (_authoredLuaWindows == null)
            {
                observe();
                return;
            }
            JObject request = _authoredLuaRequest;
            JObject capture = _authoredLuaCapture;
            try
            {
                foreach (var window in _authoredLuaWindows)
                {
                    if (_authoredLuaPlan == null)
                        break;
                    window.capture["isTargetRun"] = capture["isTargetRun"]?.DeepClone();
                    _authoredLuaRequest = window.request;
                    _authoredLuaCapture = window.capture;
                    observe();
                }
            }
            finally
            {
                if (_authoredLuaPlan != null)
                {
                    _authoredLuaRequest = request;
                    _authoredLuaCapture = capture;
                }
            }
        }

        private static bool HasAuthoredLuaWindow(string label, string name)
        {
            if (_authoredLuaWindows == null)
                return IsAuthoredLuaTarget(label) && AuthoredLuaSelectedNames().Contains(name);
            bool selected = false;
            ForEachAuthoredLuaWindow(() => selected |= IsAuthoredLuaTarget(label) && AuthoredLuaSelectedNames().Contains(name));
            return selected;
        }

        private static void RecordAuthoredLuaWindowStage(bool isInput)
        {
            if (_authoredLuaPlan == null)
                return;
            var stages = (JArray)_authoredLuaCapture["windowStages"];
            if (isInput ? stages.Count != 0 : stages.Count != 1 || (string)stages[0] != "before-patches")
                throw new InvalidDataException("A Lua window missed or repeated a genuine capture stage.");
            stages.Add(isInput ? "before-patches" : "after-patches");
        }

        private static void FinishAuthoredLuaPlan()
        {
            JObject common = _authoredLuaCapture;
            JObject first = _authoredLuaRequest;
            if ((bool?)common["isTargetRun"] != true)
                throw new InvalidDataException("The normal initialized episode did not enter the planned feature context.");
            if (!ReferenceEquals(SpaceWarp2.API.Lifecycle.ModScriptRuntime.AuthoredSourceObserver, _authoredLuaObserver))
                throw new InvalidDataException("The Lua episode lost its real body observer.");
            var observed = common["sources"].Children<JObject>().ToList();
            if (observed.Count != first["sources"].Count() || observed.Select(row => (string)row["modId"] + "/" + (string)row["key"]).Distinct().Count() != observed.Count
                || !first["sources"].Children<JObject>().All(source => observed.Count(row =>
                    (string)row["modId"] == (string)source["modId"] && (string)row["key"] == (string)source["key"]
                    && (string)row["sha256"] == (string)source["textSha256"] && (bool?)row["succeeded"] == true
                    && (bool?)row["sourceMatches"] == true) == 1)
                || TotalErrorCount != 0 || common["engineErrors"].Any())
                throw new InvalidDataException("The real Lua episode has missing, repeated, failed or foreign execution events.");
            if (!JToken.DeepEquals(new JArray((ActiveFeatures.Enabled ?? throw new InvalidDataException("No actual feature context.")).OrderBy(value => value, StringComparer.OrdinalIgnoreCase)), common["features"]))
                throw new InvalidDataException("The Lua episode changed its feature context.");
            foreach (JObject pin in _authoredLuaPlan["inputs"])
            {
                if (Sha256.OfFile((string)pin["path"]) != (string)pin["sha256"])
                    throw new InvalidDataException("A planned Lua input changed during the real episode.");
            }
            if (Sha256.OfFile((string)_authoredLuaPlanPin["path"]) != (string)_authoredLuaPlanPin["sha256"]
                || Sha256.OfFile((string)common["runtimeInterpreter"]["path"]) != (string)common["runtimeInterpreter"]["sha256"])
                throw new InvalidDataException("The Lua plan or actual interpreter changed during execution.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            var windows = new JArray();
            foreach (var window in _authoredLuaWindows)
            {
                if (!JToken.DeepEquals(window.capture["windowStages"], new JArray("before-patches", "after-patches"))
                    || window.capture["input"] == null || window.capture["projection"] == null
                    || window.capture["outputLineage"].Any(row => !names.Add((string)row["name"])))
                    throw new InvalidDataException("A planned Lua window was not naturally captured once or overlaps another actual output.");
                foreach (JObject binding in (window.request["sourceBindings"] as JArray ?? new JArray()))
                {
                    JObject asset = (JObject)binding["sourceIdentity"]["asset"];
                    if (Sha256.OfFile((string)asset["path"]) != (string)asset["sha256"])
                        throw new InvalidDataException("A bound Lua source changed during execution.");
                }
                windows.Add(new JObject { ["id"] = window.request["windowId"].DeepClone(),
                    ["requestSha256"] = window.capture["controlSha256"].DeepClone(), ["names"] = window.request["names"].DeepClone(),
                    ["stages"] = window.capture["windowStages"].DeepClone(), ["input"] = window.capture["input"].DeepClone(),
                    ["projection"] = window.capture["projection"].DeepClone() });
            }
            foreach (string field in new[] { "allRequestedSourcesObserved", "allObservedSourcesSucceeded", "normalPipelineCompleted", "inputsUnchanged", "observerSlotOwned" })
            {
                common[field] = true;
            }
            common["totalPatchErrors"] = TotalErrorCount;
            var shared = new JObject();
            foreach (string field in AUTHORED_LUA_SHARED_FIELDS)
            {
                shared[field] = common[field]?.DeepClone() ?? throw new InvalidDataException("Missing genuine Lua episode context: " + field);
            }
            var execution = new JObject { ["schema"] = "omnipatching.lua-execution-episode.v1", ["episodeId"] = _authoredLuaEpisodeId,
                ["plan"] = _authoredLuaPlanPin.DeepClone(), ["label"] = first["label"].DeepClone(), ["stage"] = "sealed",
                ["shared"] = shared, ["windows"] = windows, ["success"] = true };
            string manifest = Path.Combine((string)_authoredLuaPlan["output"], "execution.json");
            WriteAuthoredLuaFile(manifest, execution);
            var executionPin = new JObject { ["path"] = manifest, ["size"] = new FileInfo(manifest).Length, ["sha256"] = Sha256.OfFile(manifest) };
            foreach (var window in _authoredLuaWindows)
            {
                foreach (JProperty field in shared.Properties())
                {
                    window.capture[field.Name] = field.Value.DeepClone();
                }
                // These compatibility views refer to the one execution above. They are never new body events.
                window.capture["episode"] = new JObject { ["plan"] = _authoredLuaPlanPin.DeepClone(), ["execution"] = executionPin.DeepClone(),
                    ["episodeId"] = _authoredLuaEpisodeId, ["windowId"] = window.request["windowId"].DeepClone(), ["stage"] = "sealed",
                    ["executionView"] = "shared-real-events-v1" };
                window.capture["success"] = true;
                WriteAuthoredLuaFile(Path.Combine((string)window.request["output"], "report.json"), window.capture);
            }
            RestoreAuthoredLuaObserver();
        }

        private static JObject _authoredLuaCapture;

        private static JObject _authoredLuaRequest;

        private static Action<string, string, string, bool> _authoredLuaPriorObserver;

        private static Action<string, string, string, bool> _authoredLuaObserver;

        private static object _authoredLuaGeneration;

        private static void BeginAuthoredLuaCapture()
        {
            RunAuthoredLuaCapture(() =>
            {
                string control = Environment.GetEnvironmentVariable("KSP2REDUX_LUA_SOURCE_BAKE_CONTROL");
                if (string.IsNullOrEmpty(control))
                    return;
                if (!Redux.ReduxAssets.IsSourceOnly)
                    throw new InvalidOperationException("Source Lua capture requires the actual source-only player.");
                if (_authoredLuaCapture != null)
                    throw new InvalidOperationException("A source Lua capture is already armed.");
                string root = Path.GetFullPath(Environment.GetEnvironmentVariable("KSP2REDUX_LUA_SOURCE_BAKE_PROJECT") ?? Path.Combine(Application.dataPath, ".."));
                string scope = Path.Combine(root, "reference/omnipatching/source-player/current-authored-lua-source-bake-r1");
                string saveScope = Path.GetFullPath(Path.Combine(root, "reference/omnipatching/source-player/lua-transactional-source-save-r1"));
                if (Path.GetFullPath(control).StartsWith(saveScope + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    scope = saveScope;
                CheckAuthoredLuaPath(control, scope);
                string expected = Environment.GetEnvironmentVariable("KSP2REDUX_LUA_SOURCE_BAKE_SHA256");
                if (Sha256.OfFile(control) != expected)
                    throw new InvalidOperationException("Source Lua capture control changed.");
                if (new FileInfo(control).Length >= 8 * 1024 * 1024)
                    throw new InvalidDataException("The finite Lua control exceeded 8 MiB.");
                var request = JObject.Parse(File.ReadAllText(control));
                if ((string)request["schema"] == "omnipatching.lua-window-plan.v1")
                {
                    request = BeginAuthoredLuaPlan(request, control, expected, root, scope);
                    expected = (string)_authoredLuaPlan["windows"][0]["request"]["sha256"];
                }
                else if (request["episodePlanId"] != null || request["windowId"] != null)
                    throw new InvalidDataException("A planned window must run through its real episode plan.");
                if ((string)request["schema"] != "omnipatching.lua-capture-request.v1" || (string)request["captureFailurePolicy"] != "detach-and-continue-v1" || (string)request["projectRoot"] != root || string.IsNullOrEmpty((string)request["label"]) || request["names"] is not JArray names || names.Count is < 1 or > 32)
                    throw new InvalidOperationException("Unsupported finite source Lua capture request.");
                if (request["patchIdentity"] != null && (string)request["patchIdentity"] != "label-registration-ordinal-v1")
                    throw new InvalidOperationException("Unsupported requested Lua registration identity.");
                if (request["diagnosticCandidate"] is JToken candidate && candidate.Type != JTokenType.Null && candidate is not JObject)
                    throw new InvalidOperationException("Unsupported diagnostic Lua candidate pin.");
                string output = (string)request["output"];
                CheckAuthoredLuaPath(output, scope);
                foreach (string filename in new[]
                {
                    "input.json",
                    "projection.json",
                    "report.json",
                    "failed.json"
                }

                )
                {
                    CheckAuthoredLuaPath(Path.Combine(output, filename), scope);
                    CheckAuthoredLuaPath(Path.Combine(output, filename) + ".tmp", scope);
                }

                if (Directory.Exists(output) || File.Exists(output))
                    throw new IOException("Preserve the previous source Lua capture output.");
                foreach (var pin in request["inputs"].Children<JObject>())
                {
                    string path = (string)pin["path"];
                    CheckAuthoredLuaPath(path, root);
                    if (Sha256.OfFile(path) != (string)pin["sha256"])
                        throw new InvalidOperationException($"Source Lua capture input changed: {path}");
                }

                var core = typeof(GameManager).Assembly;
                if (Sha256.OfFile(CheckAuthoredLuaAssemblyPath(core.Location, root)) != (string)request["runtimeCore"]["sha256"])
                    throw new InvalidOperationException("The loaded game assembly differs from the pinned source-player assembly.");
                Directory.CreateDirectory(output);
                _authoredLuaRequest = request;
                _authoredLuaCapture = new JObject
                {
                    ["schema"] = "omnipatching.lua-capture-report.v1",
                    ["controlSha256"] = expected,
                    ["engine"] = "ModScriptRuntime/Descant/MoonSharp + PatchingManager/Universe",
                    ["productionEngine"] = true,
                    ["captureFailurePolicy"] = "detach-and-continue-v1",
                    ["sourceOnly"] = Redux.ReduxAssets.IsSourceOnly,
                    ["coreMvid"] = core.ManifestModule.ModuleVersionId.ToString(),
                    ["coreFullName"] = core.FullName,
                    ["sources"] = new JArray(),
                    ["passes"] = new JArray(),
                    ["sourceSaveTraceVersion"] = 1,
                    ["patchIdentity"] = "label-registration-ordinal-v1",
                    ["applications"] = new JArray(),
                    ["bodyEffects"] = new JArray(),
                    ["normalMembers"] = new JObject(),
                    ["loadedNameIdentity"] = "observed-textasset-location-v1",
                    ["loadedAssets"] = new JArray(),
                    ["requestedBindings"] = new JArray(),
                    ["bodyRegistry"] = AuthoredLuaBodyRegistry(),
                    ["qualifiedSourceExclusions"] = request["suppressedSources"]?.DeepClone() ?? new JArray(),
                    ["diagnosticCandidateExclusions"] = new JArray(),
                    ["outputLineage"] = new JArray(),
                    ["locations"] = new JArray(),
                    ["registration"] = new JArray(),
                    ["engineErrors"] = new JArray(),
                    ["sourceWrites"] = false,
                    ["runtimeBaseApplicationRetired"] = false,
                    ["normalPipelineCompleted"] = false,
                    ["success"] = false
                };
                InitializeAuthoredLuaWindows(scope, root);
                AttachAuthoredLuaObserver();
            });
        }


        private static void ObserveAuthoredLuaBody(string modId, string key, string content, bool succeeded)
        {
            RunAuthoredLuaCapture(() =>
            {
                if (_authoredLuaCapture == null)
                    return;
                try
                {
                    var sources = (JArray)_authoredLuaCapture["sources"];
                    if (sources.Count >= 256)
                        throw new InvalidOperationException("The finite Lua source observation window overflowed.");
                    var matches = _authoredLuaRequest["sources"].Children<JObject>().Where(row => (string)row["modId"] == modId && (string)row["key"] == key).ToList();
                    string hash = Sha256.OfString(content);
                    sources.Add(new JObject { ["sequence"] = sources.Count, ["modId"] = modId, ["key"] = key, ["sha256"] = hash, ["succeeded"] = succeeded, ["expectedSource"] = matches.Count == 1, ["sourceMatches"] = matches.Count == 1 && hash == (string)matches[0]["textSha256"] });
                    ObserveAuthoredLuaBodyRegistry(modId, key, hash);
                }
                catch (Exception error)
                {
                    FailAuthoredLuaCapture(error);
                }
            });
        }


        private static void ObserveAuthoredLuaRegistration()
        {
            RunAuthoredLuaCapture(() =>
            {
                if (_authoredLuaCapture == null)
                    return;
                try
                {
                    string[] actual = (ActiveFeatures.Enabled ?? throw new InvalidOperationException("No actual feature set.")).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
                    bool first = _authoredLuaRequest["features"].Type == JTokenType.Null;
                    string[] expected = first ? actual : _authoredLuaRequest["features"].Values<string>().OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
                    _authoredLuaCapture["isTargetRun"] = actual.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase);
                    if (!(bool)_authoredLuaCapture["isTargetRun"])
                        return;
                    var root = GameManager.Instance.Game.ScriptEnvironment as KSP.ScriptInterop.impl.moonsharp.ScriptEnvironment;
                    if (root == null || root.globals == null || root.globals.OwnerScript != root.script)
                        throw new InvalidOperationException("The genuine game root Lua environment is unavailable.");
                    _authoredLuaCapture["rootEnvironment"] = root.GetType().AssemblyQualifiedName;
                    _authoredLuaCapture["interpreter"] = root.script.GetType().Assembly.FullName;
                    _authoredLuaCapture["interpreterMvid"] = root.script.GetType().Assembly.ManifestModule.ModuleVersionId.ToString();
                    string interpreterPath = CheckAuthoredLuaAssemblyPath(root.script.GetType().Assembly.Location, (string)_authoredLuaRequest["projectRoot"]);
                    _authoredLuaCapture["runtimeInterpreter"] = new JObject { ["path"] = interpreterPath, ["size"] = new FileInfo(interpreterPath).Length, ["sha256"] = Sha256.OfFile(interpreterPath) };
                    _authoredLuaCapture["features"] = new JArray(actual);
                    _authoredLuaCapture["mods"] = new JArray(Universe.AllMods.OrderBy(value => value, StringComparer.Ordinal));
                    _authoredLuaCapture["orderedPasses"] = new JArray(Universe.OrderedPasses);
                    _authoredLuaCapture["beginCallbacks"] = Universe.BeginRunCallbacks.Count;
                    _authoredLuaCapture["endCallbacks"] = Universe.EndRunCallbacks.Count;
                    _authoredLuaCapture["converters"] = new JArray(Universe.Converters.OrderBy(row => row.Key, StringComparer.Ordinal).Select(row => new JObject { ["name"] = row.Key, ["type"] = row.Value.GetType().AssemblyQualifiedName }));
                    _authoredLuaCapture["contributors"] = new JArray(SpaceWarp2.API.Lifecycle.ModRuntime.Contributors.Select(value => value.GetType().AssemblyQualifiedName));
                    string label = (string)_authoredLuaRequest["label"];
                    // Selection and patch membership need actual loaded object names.
                    _authoredLuaCapture["selectedInputAbsent"] = false;
                }
                catch (Exception error)
                {
                    FailAuthoredLuaCapture(error);
                }
            });
        }


        private static void ObserveAuthoredLuaLocations(LabelRebuildState state)
        {
            if (_authoredLuaWindows == null)
            {
                ObserveAuthoredLuaLocationsWindow(state);
                return;
            }
            ForEachAuthoredLuaWindow(() => ObserveAuthoredLuaLocationsWindow(state));
        }

        private static void ObserveAuthoredLuaLocationsWindow(LabelRebuildState state)
        {
            RunAuthoredLuaCapture(() =>
            {
                if (!IsAuthoredLuaTarget(state.Label))
                    return;
                var loadedAssets = new JArray();
                foreach (var (name, binding) in state.LocationsByName.OrderBy(row => row.Key, StringComparer.Ordinal))
                {
                    IResourceLocation location = binding.location;
                    JObject graph = AuthoredLuaLocationGraph(location, new HashSet<IResourceLocation>());
                    var bindings = (_authoredLuaRequest["sourceBindings"] as JArray)?.Children<JObject>()
                        .Where(row => JToken.DeepEquals(row["location"], graph)).ToList() ?? new List<JObject>();
                    if (bindings.Count > 1)
                        throw new InvalidOperationException("Ambiguous pinned Lua source identity: " + name);
                    JObject sourceIdentity = null;
                    if (bindings.Count == 1)
                    {
                        sourceIdentity = (JObject)bindings[0]["sourceIdentity"];
                        if ((string)sourceIdentity["identity"]["name"] != name
                            || (string)sourceIdentity["assetType"] != binding.assetType)
                            throw new InvalidOperationException("Pinned Lua source identity differs from the loaded object: " + name);
                        JObject asset = (JObject)sourceIdentity["asset"];
                        CheckAuthoredLuaPath((string)asset["path"], (string)_authoredLuaRequest["projectRoot"]);
                        if (Sha256.OfFile((string)asset["path"]) != (string)asset["sha256"])
                            throw new InvalidOperationException("Pinned Lua source bytes differ at the load boundary: " + name);
                    }
                    loadedAssets.Add(new JObject { ["name"] = name, ["assetType"] = binding.assetType,
                        ["location"] = graph, ["sourceIdentity"] = sourceIdentity?.DeepClone() });
                }
                _authoredLuaCapture["loadedAssets"] = loadedAssets;
                var requestedBindings = new JArray();
                var selectedLocations = new JArray();
                foreach (string requested in _authoredLuaRequest["names"].Values<string>())
                {
                    var matches = loadedAssets.Children<JObject>().Where(row => requested == (string)row["name"]
                        || requested == (string)row["location"]["primaryKey"]
                        || requested == Path.GetFileNameWithoutExtension((string)row["location"]["primaryKey"])).ToList();
                    if (matches.Count > 1)
                        throw new InvalidOperationException("Requested Lua spelling identifies distinct loaded assets: " + requested);
                    foreach (JObject match in matches)
                    {
                        requestedBindings.Add(new JObject { ["requested"] = requested, ["name"] = match["name"].DeepClone(),
                            ["location"] = match["location"].DeepClone() });
                        if (!selectedLocations.Any(row => JToken.DeepEquals(row, match["location"])))
                            selectedLocations.Add(match["location"].DeepClone());
                    }
                }
                _authoredLuaCapture["requestedBindings"] = requestedBindings;
                _authoredLuaCapture["locations"] = selectedLocations;
                _authoredLuaCapture["selectedInputAbsent"] = requestedBindings.Count == 0
                    && !state.CreatedAssets.Any(asset => _authoredLuaRequest["names"].Values<string>().Contains(asset.name));
                ObserveAuthoredLuaMembers(state);
                foreach (string pass in Universe.OrderedPasses)
                {
                    foreach (string name in AuthoredLuaSelectedNames())
                    {
                        foreach (PatchDefinition patch in Universe.GetAllSortedPatchesFor(state.Label, name, pass))
                        {
                            RecordAuthoredLuaPatchRegistration(name, pass, patch);
                        }
                    }
                }
            });
        }


        /// <summary>
        /// Exclude a genuinely saved body only inside an explicitly pinned diagnostic capture.
        /// </summary>
        /// <param name="modId">The executing mod identity.</param>
        /// <param name="key">The executing source key.</param>
        /// <param name="content">The actual loaded source text.</param>
        /// <returns>True if this diagnostic capture excludes the exact saved body, false otherwise.</returns>
        /// <exception cref="InvalidDataException">Thrown when candidate source or saved-owner pins changed.</exception>
        internal static bool ExcludeDiagnosticAuthoredLuaBody(string modId, string key, string content)
        {
            if (_authoredLuaRequest == null || _authoredLuaCapture == null || modId != "Ksp2Redux"
                || _authoredLuaRequest["diagnosticCandidate"] is not JObject candidatePin)
                return false;
            string root = (string)_authoredLuaRequest["projectRoot"];
            string path = (string)candidatePin["path"];
            CheckAuthoredLuaPath(path, Path.Combine(root, "reference/omnipatching/source-player/lua-transactional-source-save-r1/review-r1"));
            if (Sha256.OfFile(path) != (string)candidatePin["sha256"] || new FileInfo(path).Length > 8 * 1024 * 1024)
                throw new InvalidDataException("Diagnostic Lua candidate changed.");
            JObject candidate = JObject.Parse(File.ReadAllText(path));
            if ((string)candidate["schema"] != "omnipatching.lua-diagnostic-exclusion.v1"
                || (bool?)candidate["diagnosticOnly"] != true || (bool?)candidate["runtimeRetired"] != false
                || candidate["body"] is not JObject body || (string)body["modId"] != "Ksp2Redux"
                || (string)body["key"] is "Ksp2Redux/Features" or "Ksp2Redux/Passes" or "Ksp2Redux/PantherNozzle")
                throw new InvalidDataException("Foreign diagnostic Lua candidate.");
            if ((string)body["key"] != key)
                return false;
            if ((string)body["bodyTextSha256"] != Sha256.OfString(content))
                throw new InvalidDataException("The diagnostic Lua body changed.");
            foreach (JObject pin in candidate["currentInputs"])
            {
                string input = (string)pin["path"];
                CheckAuthoredLuaPath(input, root);
                if (new FileInfo(input).Length != (long)pin["size"] || Sha256.OfFile(input) != (string)pin["sha256"])
                    throw new InvalidDataException("Diagnostic saved-source input changed: " + input);
            }
            var excluded = (JArray)_authoredLuaCapture["diagnosticCandidateExclusions"];
            if (excluded.Count != 0)
                throw new InvalidDataException("A diagnostic body was excluded more than once.");
            excluded.Add(new JObject { ["modId"] = modId, ["key"] = key, ["sha256"] = Sha256.OfString(content), ["candidate"] = candidatePin.DeepClone() });
            return true;
        }

        private static IEnumerable<string> AuthoredLuaSelectedNames()
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (string requested in _authoredLuaRequest["names"].Values<string>())
            {
                var bindings = _authoredLuaCapture["requestedBindings"].Children<JObject>()
                    .Where(row => (string)row["requested"] == requested).ToList();
                if (bindings.Count == 0)
                    names.Add(requested);
                foreach (JObject binding in bindings)
                {
                    names.Add((string)binding["name"]);
                }
            }
            if (_authoredLuaCapture["outputLineage"] is JArray lineage)
            {
                foreach (JObject row in lineage)
                {
                    names.Add((string)row["name"]);
                }
            }
            return names.OrderBy(value => value, StringComparer.Ordinal);
        }

        /// <summary>
        /// Observe the actual accepted duplicate and its source without changing the patch result.
        /// </summary>
        /// <param name="patch">The real executing duplicate patch.</param>
        /// <param name="sourceName">The actual source asset name.</param>
        /// <param name="duplicate">The accepted duplicate queued by the engine.</param>
        internal static void ObserveAuthoredLuaDuplicate(PatchDefinition patch, string sourceName, Universe.DuplicatedAsset duplicate)
        {
            if (_authoredLuaWindows == null)
            {
                ObserveAuthoredLuaDuplicateWindow(patch, sourceName, duplicate);
                return;
            }
            ForEachAuthoredLuaWindow(() => ObserveAuthoredLuaDuplicateWindow(patch, sourceName, duplicate));
        }

        private static void ObserveAuthoredLuaDuplicateWindow(PatchDefinition patch, string sourceName, Universe.DuplicatedAsset duplicate)
        {
            RunAuthoredLuaCapture(() =>
            {
                if (!IsAuthoredLuaTarget(patch.Label) || !AuthoredLuaSelectedNames().Contains(sourceName))
                    return;
                var lineage = (JArray)_authoredLuaCapture["outputLineage"];
                if (lineage.Any(row => (string)row["name"] == duplicate.Name))
                    throw new InvalidOperationException("An observed generated Lua name is ambiguous: " + duplicate.Name);
                lineage.Add(new JObject { ["name"] = duplicate.Name, ["kind"] = "duplicate", ["sourceName"] = sourceName,
                    ["patch"] = patch.Name, ["patchId"] = AuthoredLuaPatchId(patch.Label, AuthoredLuaPatchOrdinal(patch)),
                    ["label"] = patch.Label, ["registrationOrdinal"] = AuthoredLuaPatchOrdinal(patch), ["pass"] = patch.PassName, ["modId"] = patch.PatchModId, ["converter"] = patch.ConverterInstance.GetType().AssemblyQualifiedName,
                    ["afterOrder"] = duplicate.AfterOrder, ["initialValue"] = duplicate.Data.DeepClone(),
                    ["features"] = new JArray(patch.Selector.Features), ["noFeatures"] = new JArray(patch.Selector.NoFeatures) });
                foreach (string pass in Universe.OrderedPasses)
                {
                    foreach (PatchDefinition registered in Universe.GetAllSortedPatchesFor(patch.Label, duplicate.Name, pass))
                    {
                        RecordAuthoredLuaPatchRegistration(duplicate.Name, pass, registered);
                    }
                }
            });
        }

        private static string AuthoredLuaPatchId(string label, int ordinal)
        {
            if (string.IsNullOrEmpty(label) || ordinal < 0)
                throw new InvalidOperationException("Malformed Lua registration identity.");
            return label + "/" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static int AuthoredLuaPatchOrdinal(PatchDefinition patch)
        {
            if (!Universe.AllPatches.TryGetValue(patch.Label, out var definitions))
                throw new InvalidOperationException("Unregistered Lua patch label: " + patch.Label);
            int ordinal = -1;
            for (int index = 0; index < definitions.Count; index++)
            {
                if (!ReferenceEquals(definitions[index], patch))
                    continue;
                if (ordinal >= 0)
                    throw new InvalidOperationException("Repeated Lua definition reference in registration list.");
                ordinal = index;
            }
            if (ordinal < 0)
                throw new InvalidOperationException("Unregistered Lua patch definition: " + patch.Name);
            return ordinal;
        }

        private static List<Summary.SummaryEntry> AuthoredLuaEntries(Summary summary, PatchDefinition patch, string name)
        {
            foreach (var label in summary.Summaries)
            {
                if (label.labelName != patch.Label)
                    continue;
                foreach (var asset in label.assets)
                {
                    if (asset.AssetName == name && asset.EntriesByPass.TryGetValue(patch.PassName, out var entries))
                        return entries;
                }
            }
            return null;
        }

        /// <summary>
        /// Apply an ordinary patch and attribute its actual summary entries to the registered definition.
        /// </summary>
        /// <param name="patch">The actual definition selected by the engine.</param>
        /// <param name="value">The ordinary converter value.</param>
        /// <param name="summary">The engine summary receiving the actual outcome.</param>
        /// <param name="name">The current source or generated asset name.</param>
        /// <param name="removed">True if the callback removed the asset, false otherwise.</param>
        /// <param name="errored">True if the patch reported an error, false otherwise.</param>
        /// <returns>True if the patch ran without error or predicate failure, false otherwise.</returns>
        internal static bool ApplyAuthoredLuaPatch(PatchDefinition patch, MoonSharp.Interpreter.DynValue value,
            Summary summary, string name, out bool removed, out bool errored)
        {
            if (!HasAuthoredLuaWindow(patch.Label, name))
                return patch.Apply(value, summary, out removed, out errored);
            int start = 0;
            RunAuthoredLuaCapture(() => start = AuthoredLuaEntries(summary, patch, name)?.Count ?? 0);
            bool result = patch.Apply(value, summary, out removed, out errored);
            ForEachAuthoredLuaWindow(() => RunAuthoredLuaCapture(() =>
            {
                if (!IsAuthoredLuaTarget(patch.Label) || !AuthoredLuaSelectedNames().Contains(name))
                    return;
                List<Summary.SummaryEntry> entries = AuthoredLuaEntries(summary, patch, name);
                if (entries == null || entries.Count <= start)
                    throw new InvalidOperationException("The actual Lua operation produced no summary entry.");
                int ordinal = AuthoredLuaPatchOrdinal(patch);
                string identity = AuthoredLuaPatchId(patch.Label, ordinal);
                for (int index = start; index < entries.Count; index++)
                {
                    Summary.SummaryEntry entry = entries[index];
                    if (entry.Name != patch.Name || entry.Pass != patch.PassName)
                        throw new InvalidOperationException("The actual Lua operation summary attribution changed.");
                    ((JArray)_authoredLuaCapture["applications"]).Add(new JObject
                    {
                        ["target"] = name, ["pass"] = entry.Pass, ["patch"] = entry.Name,
                        ["patchId"] = identity, ["label"] = patch.Label, ["registrationOrdinal"] = ordinal,
                        ["state"] = entry.State.ToString(), ["context"] = entry.Context
                    });
                }
            }));
            return result;
        }


        private static void RecordAuthoredLuaPatchRegistration(string name, string pass, PatchDefinition patch)
        {
            int ordinal = AuthoredLuaPatchOrdinal(patch);
            string identity = AuthoredLuaPatchId(patch.Label, ordinal);
            var registrations = (JArray)_authoredLuaCapture["registration"];
            if (registrations.Any(row => (string)row["target"] == name && (string)row["pass"] == pass && (string)row["patchId"] == identity))
                return;
            registrations.Add(new JObject { ["patchId"] = identity, ["label"] = patch.Label, ["registrationOrdinal"] = ordinal, ["target"] = name, ["name"] = patch.Name, ["modId"] = patch.PatchModId, ["pass"] = pass, ["order"] = patch.Order, ["converter"] = patch.ConverterInstance.GetType().AssemblyQualifiedName, ["features"] = new JArray(patch.Selector.Features), ["noFeatures"] = new JArray(patch.Selector.NoFeatures), ["needsMods"] = new JArray(patch.NeedsMods), ["conflictsMods"] = new JArray(patch.ConflictsMods), ["needsPatches"] = new JArray(patch.NeedsPatches), ["conflictsPatches"] = new JArray(patch.ConflictsPatches), ["duplicate"] = patch.DuplicateNameTemplate });
        }

        private static void ObserveAuthoredLuaOutputLineage(LabelRebuildState state, bool isInput)
        {
            if (!IsAuthoredLuaTarget(state.Label))
                return;
            var lineage = (JArray)_authoredLuaCapture["outputLineage"];
            foreach (string name in AuthoredLuaSelectedNames())
            {
                if (lineage.Any(row => (string)row["name"] == name))
                    continue;
                var created = state.CreatedAssets.Where(row => row.name == name).ToList();
                if (created.Count > 1)
                    throw new InvalidOperationException("Ambiguous selected Lua creation: " + name);
                if (created.Count == 1)
                {
                    LuaAsset asset = created[0].asset;
                    lineage.Add(new JObject { ["name"] = name, ["kind"] = "created", ["modId"] = asset.ModId,
                        ["converter"] = asset.ConverterInstance.GetType().AssemblyQualifiedName, ["label"] = asset.Label,
                        ["features"] = new JArray(asset.Selector.Features), ["noFeatures"] = new JArray(asset.Selector.NoFeatures),
                        ["initialValue"] = asset.ConverterInstance.ToJson(asset.CurrentValue)?.DeepClone() });
                }
                else if (state.RawTexts.ContainsKey(name) || state.Tokens.ContainsKey(name))
                {
                    if (!state.PrimaryKeyMap.TryGetValue(name, out string primaryKey))
                        throw new InvalidOperationException("Selected Lua source lacks its actual primary key: " + name);
                    JObject loaded = _authoredLuaCapture["loadedAssets"].Children<JObject>().Single(row => (string)row["name"] == name);
                    lineage.Add(new JObject { ["name"] = name, ["kind"] = "existing", ["primaryKey"] = primaryKey,
                        ["location"] = loaded["location"].DeepClone(), ["assetType"] = loaded["assetType"].DeepClone(),
                        ["sourceIdentity"] = loaded["sourceIdentity"]?.DeepClone() });
                }
            }
            if (!isInput)
            {
                JObject values = AuthoredLuaValues(state);
                foreach (JObject row in lineage)
                {
                    row["presentAtOutput"] = values[(string)row["name"]] != null;
                }
            }
        }

        private static JObject AuthoredLuaLocationGraph(IResourceLocation location, HashSet<IResourceLocation> stack)
        {
            if (!stack.Add(location))
                throw new InvalidOperationException("A selected Lua catalog location has cyclic dependencies.");
            var dependencies = new JArray();
            if (location.Dependencies != null)
            {
                foreach (IResourceLocation dependency in location.Dependencies)
                {
                    dependencies.Add(AuthoredLuaLocationGraph(dependency, stack));
                }
            }
            stack.Remove(location);
            return new JObject { ["primaryKey"] = location.PrimaryKey, ["locationKey"] = FeatureAssetIndex.KeyOf(location),
                ["internalId"] = location.InternalId, ["providerId"] = location.ProviderId,
                ["resourceType"] = location.ResourceType?.AssemblyQualifiedName, ["dependencies"] = dependencies,
                ["gate"] = "FeatureAssetIndex.Filter" };
        }

        private static JObject AuthoredLuaBodyRegistry()
        {
            var patches = new JObject();
            foreach (var label in Universe.AllPatches)
            {
                for (int ordinal = 0; ordinal < label.Value.Count; ordinal++)
                {
                    PatchDefinition patch = label.Value[ordinal];
                    if (patch.Label != label.Key || AuthoredLuaPatchOrdinal(patch) != ordinal)
                        throw new InvalidOperationException("Lua definition registration label/reference differs.");
                    string identity = AuthoredLuaPatchId(label.Key, ordinal);
                    patches.Add(identity, new JObject { ["patchId"] = identity, ["registrationOrdinal"] = ordinal,
                        ["name"] = patch.Name, ["modId"] = patch.PatchModId, ["label"] = patch.Label,
                        ["names"] = new JArray(patch.Names.OrderBy(value => value, StringComparer.Ordinal)), ["pass"] = patch.PassName, ["order"] = patch.Order,
                        ["features"] = new JArray(patch.Selector.Features), ["noFeatures"] = new JArray(patch.Selector.NoFeatures),
                        ["converter"] = patch.ConverterInstance.GetType().AssemblyQualifiedName,
                        ["predicates"] = patch.DescribeAuthoredLuaPredicates(),
                        ["needsMods"] = new JArray(patch.NeedsMods), ["conflictsMods"] = new JArray(patch.ConflictsMods),
                        ["needsPatches"] = new JArray(patch.NeedsPatches), ["conflictsPatches"] = new JArray(patch.ConflictsPatches),
                        ["duplicate"] = patch.DuplicateNameTemplate });
                }
            }
            var created = new JObject();
            foreach (LuaAsset asset in Universe.AllNewAssets)
            {
                if (created.Property(asset.Label + "/" + asset.Name) != null)
                    throw new InvalidOperationException("Ambiguous full Lua registry creation identity: " + asset.Label + "/" + asset.Name);
                created[asset.Label + "/" + asset.Name] = new JObject { ["name"] = asset.Name, ["label"] = asset.Label, ["modId"] = asset.ModId,
                    ["features"] = new JArray(asset.Selector.Features), ["noFeatures"] = new JArray(asset.Selector.NoFeatures),
                    ["converter"] = asset.ConverterInstance.GetType().AssemblyQualifiedName };
            }
            return new JObject { ["patchIdentity"] = "label-registration-ordinal-v1", ["patches"] = patches, ["created"] = created,
                ["converters"] = new JArray(Universe.Converters.OrderBy(row => row.Key, StringComparer.Ordinal).Select(row => new JObject { ["name"] = row.Key, ["type"] = row.Value.GetType().AssemblyQualifiedName })),
                ["passes"] = new JArray(Universe.Passes.OrderBy(row => row.Key, StringComparer.Ordinal).Select(row => new JObject
                { ["name"] = row.Key, ["before"] = new JArray(row.Value.BeforePasses), ["after"] = new JArray(row.Value.AfterPasses) })),
                ["features"] = new JArray(FeatureRegistry.Instance.All.OrderBy(value => value.Id, StringComparer.Ordinal).Select(value => new JObject
                { ["id"] = value.Id, ["requires"] = new JArray(value.Requires), ["conflicts"] = new JArray(value.Conflicts), ["provides"] = new JArray(value.Provides) })),
                ["beginCallbacks"] = new JArray(Universe.BeginRunCallbacks.Select(value => value.ModId + ":" + value.Name)),
                ["endCallbacks"] = new JArray(Universe.EndRunCallbacks.Select(value => value.ModId + ":" + value.Name)) };
        }

        private static void ObserveAuthoredLuaBodyRegistry(string modId, string key, string hash)
        {
            JObject after = AuthoredLuaBodyRegistry();
            ((JArray)_authoredLuaCapture["bodyEffects"]).Add(new JObject { ["modId"] = modId, ["key"] = key, ["sha256"] = hash,
                ["before"] = _authoredLuaCapture["bodyRegistry"].DeepClone(), ["after"] = after.DeepClone() });
            _authoredLuaCapture["bodyRegistry"] = after;
        }

        private static void ObserveAuthoredLuaMembers(LabelRebuildState state)
        {
            if (!IsAuthoredLuaTarget(state.Label))
                return;
            var members = new JObject();
            foreach (var (name, binding) in state.LocationsByName)
            {
                IResourceLocation location = binding.location;
                var patches = new JArray();
                foreach (string pass in Universe.OrderedPasses)
                {
                    foreach (PatchDefinition patch in Universe.GetAllSortedPatchesFor(state.Label, name, pass))
                    {
                        int ordinal = AuthoredLuaPatchOrdinal(patch);
                        patches.Add(new JObject { ["patchId"] = AuthoredLuaPatchId(state.Label, ordinal), ["label"] = state.Label,
                            ["registrationOrdinal"] = ordinal, ["name"] = patch.Name, ["pass"] = pass, ["modId"] = patch.PatchModId });
                    }
                }
                if (members.Property(location.PrimaryKey) != null)
                    throw new InvalidOperationException("Distinct loaded Lua inputs share a primary address: " + location.PrimaryKey);
                members[location.PrimaryKey] = new JObject { ["name"] = name, ["primaryKey"] = location.PrimaryKey,
                    ["location"] = AuthoredLuaLocationGraph(location, new HashSet<IResourceLocation>()), ["patches"] = patches };
            }
            _authoredLuaCapture["normalMembers"] = members;
        }


        private static JObject AuthoredLuaValues(LabelRebuildState state)
        {
            var assets = new JObject();
            foreach (string name in AuthoredLuaSelectedNames())
            {
                if (state.Tokens.TryGetValue(name, out JToken token))
                {
                    assets[name] = token.DeepClone();
                    continue;
                }

                if (state.RawTexts.TryGetValue(name, out string text))
                {
                    assets[name] = JToken.Parse(text);
                    continue;
                }

                var created = state.CreatedAssets.Where(row => row.name == name).ToList();
                if (created.Count > 1)
                    throw new InvalidOperationException($"Duplicate created definition: {name}");
                if (created.Count == 1)
                    assets[name] = created[0].asset.ConverterInstance.ToJson(created[0].asset.CurrentValue)?.DeepClone();
            }

            return assets;
        }


        private static void ObserveAuthoredLuaValues(LabelRebuildState state, bool isInput)
        {
            if (_authoredLuaWindows == null)
            {
                ObserveAuthoredLuaValuesWindow(state, isInput);
                return;
            }
            ForEachAuthoredLuaWindow(() => ObserveAuthoredLuaValuesWindow(state, isInput));
        }

        private static void ObserveAuthoredLuaValuesWindow(LabelRebuildState state, bool isInput)
        {
            RunAuthoredLuaCapture(() =>
            {
                if (!IsAuthoredLuaTarget(state.Label))
                    return;
                try
                {
                    RecordAuthoredLuaWindowStage(isInput);
                    if (!isInput)
                        ObserveAuthoredLuaEngineSummary(state.Label);
                    ObserveAuthoredLuaOutputLineage(state, isInput);
                    var values = AuthoredLuaValues(state);
                    string filename = isInput ? "input.json" : "projection.json";
                    string path = Path.Combine((string)_authoredLuaRequest["output"], filename);
                    WriteAuthoredLuaFile(path, values);
                    _authoredLuaCapture[isInput ? "input" : "projection"] = new JObject
                    {
                        ["path"] = path,
                        ["sha256"] = Sha256.OfFile(path),
                        ["count"] = values.Count
                    };

                }
                catch (Exception error)
                {
                    FailAuthoredLuaCapture(error);
                }
            });
        }


        private static void ObserveAuthoredLuaEngineSummary(string selectedLabel)
        {
            if (!IsAuthoredLuaTarget(selectedLabel))
                return;
            var witness = new JArray();
            foreach (var label in Universe.Summary.Summaries)
            {
                if (label.labelName != selectedLabel)
                    continue;
                foreach (var asset in label.assets)
                {
                    if (!AuthoredLuaSelectedNames().Contains(asset.AssetName))
                        continue;
                    foreach (var pass in asset.EntriesByPass)
                    {
                        foreach (Summary.SummaryEntry entry in pass.Value)
                        {
                            witness.Add(new JObject { ["target"] = asset.AssetName, ["pass"] = entry.Pass,
                                ["patch"] = entry.Name, ["state"] = entry.State.ToString(), ["context"] = entry.Context });
                        }
                    }
                }
            }
            // Engine errors outside Apply have no retained definition reference. Keep the raw outcomes separate.
            _authoredLuaCapture["selectedEngineSummary"] = witness;
        }

        private static void ObserveAuthoredLuaPass(string label, string pass)
        {
            if (_authoredLuaWindows == null)
            {
                ObserveAuthoredLuaPassWindow(label, pass);
                return;
            }
            ForEachAuthoredLuaWindow(() => ObserveAuthoredLuaPassWindow(label, pass));
        }

        private static void ObserveAuthoredLuaPassWindow(string label, string pass)
        {
            RunAuthoredLuaCapture(() =>
            {
                if (!IsAuthoredLuaTarget(label))
                    return;
                if (_authoredLuaPlan != null && (((JArray)_authoredLuaCapture["windowStages"]).Count != 1
                    || _authoredLuaCapture["passes"].Any(row => (string)row["pass"] == pass)))
                    throw new InvalidDataException("Repeated or out-of-stage Lua pass observation.");
                ObserveAuthoredLuaEngineSummary(label);
                JObject values = AuthoredLuaValues(_rebuildStates[label]);
                ((JArray)_authoredLuaCapture["passes"]).Add(new JObject { ["pass"] = pass, ["totalPatchErrors"] = TotalErrorCount, ["valueSha256"] = Sha256.OfString(values.ToString(Formatting.None)), ["values"] = values });
            });
        }


        private static void FinishAuthoredLuaCapture()
        {
            RunAuthoredLuaCapture(() =>
            {
                if (_authoredLuaPlan != null)
                {
                    FinishAuthoredLuaPlan();
                    return;
                }
                if (_authoredLuaCapture == null || (bool? )_authoredLuaCapture["isTargetRun"] != true)
                    return;
                try
                {
                    if ((bool? )_authoredLuaCapture["selectedInputAbsent"] == true && _authoredLuaCapture["input"] == null)
                    {
                        var empty = new LabelRebuildState
                        {
                            Label = (string)_authoredLuaRequest["label"]
                        };
                        ObserveAuthoredLuaValues(empty, true);
                        ObserveAuthoredLuaValues(empty, false);
                    }

                    foreach (var pin in _authoredLuaRequest["inputs"].Children<JObject>())
                    {
                        if (Sha256.OfFile((string)pin["path"]) != (string)pin["sha256"])
                            throw new InvalidOperationException("A source Lua capture input changed during the real run.");
                    }

                    foreach (JObject binding in (_authoredLuaRequest["sourceBindings"] as JArray ?? new JArray()).Children<JObject>())
                    {
                        JObject asset = (JObject)binding["sourceIdentity"]["asset"];
                        CheckAuthoredLuaPath((string)asset["path"], (string)_authoredLuaRequest["projectRoot"]);
                        if (Sha256.OfFile((string)asset["path"]) != (string)asset["sha256"])
                            throw new InvalidOperationException("A bound Lua source changed during capture.");
                    }
                    var sources = _authoredLuaCapture["sources"].Children<JObject>().ToList();
                    bool sourceMatch = _authoredLuaRequest["sources"].Children<JObject>().All(expected => sources.Count(actual => (string)actual["modId"] == (string)expected["modId"] && (string)actual["key"] == (string)expected["key"] && (bool)actual["succeeded"] && (bool)actual["sourceMatches"]) == 1);
                    var interpreterPin = (JObject)_authoredLuaCapture["runtimeInterpreter"];
                    if (Sha256.OfFile((string)interpreterPin["path"]) != (string)interpreterPin["sha256"])
                        throw new InvalidOperationException("The actual interpreter changed during capture.");
                    _authoredLuaCapture["allRequestedSourcesObserved"] = sourceMatch;
                    _authoredLuaCapture["allObservedSourcesSucceeded"] = sources.All(row => (bool)row["succeeded"]);
                    _authoredLuaCapture["totalPatchErrors"] = TotalErrorCount;
                    _authoredLuaCapture["normalPipelineCompleted"] = true;
                    _authoredLuaCapture["inputsUnchanged"] = true;
                    _authoredLuaCapture["observerSlotOwned"] = SpaceWarp2.API.Lifecycle.ModScriptRuntime.AuthoredSourceObserver == _authoredLuaObserver;
                    _authoredLuaCapture["success"] = sourceMatch && sources.All(row => (bool)row["succeeded"]) && TotalErrorCount == 0 && !_authoredLuaCapture["engineErrors"].Any() && (bool)_authoredLuaCapture["observerSlotOwned"] && _authoredLuaCapture["input"] != null && _authoredLuaCapture["projection"] != null;
                    string path = Path.Combine((string)_authoredLuaRequest["output"], "report.json");
                    WriteAuthoredLuaFile(path, _authoredLuaCapture);
                }
                catch (Exception error)
                {
                    FailAuthoredLuaCapture(error);
                }
                finally
                {
                    RestoreAuthoredLuaObserver();
                }
            });
        }


        private static void FailAuthoredLuaCapture(Exception error)
        {
            try
            {
                if (_authoredLuaPlan != null && Directory.Exists((string)_authoredLuaPlan["output"]))
                    WriteAuthoredLuaFile(Path.Combine((string)_authoredLuaPlan["output"], "failed.json"),
                        new JObject { ["plan"] = _authoredLuaPlanPin?.DeepClone(), ["error"] = error.ToString(), ["success"] = false });
                if (_authoredLuaCapture == null || _authoredLuaRequest == null)
                    return;
                _authoredLuaCapture["error"] = error.ToString();
                string path = Path.Combine((string)_authoredLuaRequest["output"], "failed.json");
                WriteAuthoredLuaFile(path, _authoredLuaCapture);
            }
            catch (Exception)
            {
            // Failure evidence is best effort. Diagnostic IO must not interrupt the real patch run.
            }
            finally
            {
                RestoreAuthoredLuaObserver();
            }
        }


        private static void RestoreAuthoredLuaObserver()
        {
            if (SpaceWarp2.API.Lifecycle.ModScriptRuntime.AuthoredSourceObserver == _authoredLuaObserver)
                SpaceWarp2.API.Lifecycle.ModScriptRuntime.AuthoredSourceObserver = _authoredLuaPriorObserver;
            _authoredLuaPlan = null;
            _authoredLuaPlanPin = null;
            _authoredLuaWindows = null;
            _authoredLuaEpisodeId = null;
            _authoredLuaGeneration = null;
            _authoredLuaObserver = null;
            _authoredLuaPriorObserver = null;
            _authoredLuaCapture = null;
            _authoredLuaRequest = null;
        }


        private static void WriteAuthoredLuaFile(string path, JObject value)
        {
            string text = value.ToString(Formatting.Indented);
            if (System.Text.Encoding.UTF8.GetByteCount(text) >= 8 * 1024 * 1024)
                throw new InvalidOperationException("The finite Lua capture output exceeded 8 MiB.");
            string temporary = path + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(text);
            }

            File.Move(temporary, path);
        }


        private static void CheckAuthoredLuaPath(string path, string scope)
        {
            string full = Path.GetFullPath(path);
            string prefix = Path.GetFullPath(scope).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (full.Length > 240 || !full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Source Lua capture path escaped its scope or Mono path budget.");
            for (string current = full; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            {
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Source Lua capture paths must not cross filesystem links.");
            }
        }


        private static string CheckAuthoredLuaAssemblyPath(string path, string root)
        {
            CheckAuthoredLuaPath(path, root);
            return path;
        }


        private static bool IsAuthoredLuaTarget(string label) => _authoredLuaCapture != null && (bool? )_authoredLuaCapture["isTargetRun"] == true && label == (string)_authoredLuaRequest["label"];

        private static void ObserveAuthoredLuaEngineError(string message)
        {
            PatchManager.Logger.LogError(message);
            RunAuthoredLuaCapture(() =>
            {
                if (_authoredLuaCapture == null)
                    return;
                var errors = (JArray)_authoredLuaCapture["engineErrors"];
                if (errors.Count < 32)
                    errors.Add(message.Length > 1000 ? message.Substring(0, 1000) : message);
            });
        }


        /// <summary>
        /// Restores the previous diagnostic observer when a source-player run is cancelled or fails.
        /// </summary>
        /// <param name = "error">The actual failure to retain, or null for ordinary teardown.</param>
        internal static void AbortAuthoredLuaCapture(Exception error)
        {
            if (error != null)
            {
                FailAuthoredLuaCapture(error);
                return;
            }

            if (_authoredLuaCapture != null)
                RestoreAuthoredLuaObserver();
        }


        private static void RunAuthoredLuaCapture(Action observe)
        {
            try
            {
                observe();
            }
            catch (Exception error)
            {
                FailAuthoredLuaCapture(error);
            }
        }


        private static void AttachAuthoredLuaObserver()
        {
            _authoredLuaPriorObserver = SpaceWarp2.API.Lifecycle.ModScriptRuntime.AuthoredSourceObserver;
            _authoredLuaGeneration = new object ();
            _authoredLuaObserver = CreateAuthoredLuaObserver(_authoredLuaGeneration, _authoredLuaPriorObserver);
            SpaceWarp2.API.Lifecycle.ModScriptRuntime.AuthoredSourceObserver = _authoredLuaObserver;
        }


        private static Action<string, string, string, bool> CreateAuthoredLuaObserver(object generation, Action<string, string, string, bool> prior)
        {
            // Keep the prior chain independent of mutable capture state and forward genuine prior errors unchanged.
            return (modId, key, content, succeeded) =>
            {
                prior?.Invoke(modId, key, content, succeeded);
                if (!ReferenceEquals(_authoredLuaGeneration, generation))
                    return;
                ObserveAuthoredLuaBody(modId, key, content, succeeded);
            };
        }


        private class LabelRebuildState
        {
            public string Label;
            public Dictionary<string, string> RawTexts = new();
            public Dictionary<string, JToken> Tokens = new();
            /// <summary>
            /// Primary addresses bound at the actual TextAsset load boundary.
            /// </summary>
            public Dictionary<string, string> PrimaryKeyMap = new(StringComparer.Ordinal);
            /// <summary>
            /// Accepted locations keyed by their observed Unity object names.
            /// </summary>
            public Dictionary<string, (IResourceLocation location, string assetType)> LocationsByName = new(StringComparer.Ordinal);
            /// <summary>
            /// Parallel per-location loads owned until archive completion or cancellation.
            /// </summary>
            public List<(IResourceLocation location, AsyncOperationHandle<TextAsset> handle)> LoadHandles = new();
            public List<(string name, LuaAsset asset)> CreatedAssets = new();
            public Dictionary<string, string> AddressAliases = new();
            public HashSet<string> PatchedAssetNames = new();
            public bool Unchanged = true;
        }

        private static void UpdateLoadingBarData()
        {
            GameManager.Instance.Game.UI.UitkLoadingCurtain.Data.PatchManagerDefinitionsModifiedCount =
                TotalDefinitionPatchCount;

            GameManager.Instance.Game.UI.UitkLoadingCurtain.Data.PatchManagerErrorCount =
                TotalErrorCount;

            GameManager.Instance.Game.UI.UitkLoadingCurtain.Data.PatchManagerNewAssetCount =
                TotalNewAssetCount;

            GameManager.Instance.Game.UI.UitkLoadingCurtain.Data.PatchManagerPatchCount = TotalPatchCount;
        }
    }
}

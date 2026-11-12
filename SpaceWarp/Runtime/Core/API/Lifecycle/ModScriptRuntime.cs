#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KSP.Game;
using KSP.ScriptInterop.impl.moonsharp;
using MoonSharp.Interpreter;
using Newtonsoft.Json.Linq;
using SpaceWarp2.API.Configuration;
using SpaceWarp2.API.Logging;
using SpaceWarp2.API;
using SpaceWarp2.API.Config;
using SpaceWarp2.API.Features;
using SpaceWarp2.API.Mods;

namespace SpaceWarp2.API.Lifecycle
{
    /// <summary>
    /// The SpaceWarp-owned Lua mod runtime. Forks per-mod environments off the game's root
    /// <see cref="ScriptEnvironment" />, contributes the general mod-loading globals, runs registered
    /// <see cref="IModEnvContributor" />s, and executes mod bodies.
    /// </summary>
    /// <remarks>
    /// SpaceWarp owns env creation and execution. PatchManager and others contribute their own globals through
    /// <see cref="IModEnvContributor" />. A mod that runs several files in one environment uses
    /// <see cref="CreateModEnv" /> once and <see cref="RunBodyIn" /> per file.
    /// </remarks>
    public static class ModScriptRuntime
    {
        /// <summary>
        /// The ModId and logger source name of the shared console environment. Console and CLI scripts run here, so
        /// their Log output is attributed to this source - the console REPL filters on it to show only that output.
        /// </summary>
        public const string ConsoleModId = "spacewarp-console";

        // The global holding the id of the mod an environment belongs to
        private const string MOD_ID_GLOBAL = "ModId";

        /// <summary>
        /// Observes completed real mod bodies for an explicitly armed source-player capture.
        /// </summary>
        /// <remarks>
        /// Arguments are the real mod id, source key, unmodified body and execution success.
        /// The capture owner restores the previous observer during completion or teardown.
        /// </remarks>
        internal static Action<string, string, string, bool> AuthoredSourceObserver;

        private static readonly ModScriptLoader Loader = new();
        // Taken on first use, so tracking feature environments does not need the log provider to be set up
        private static ILogger _logger;
        private static ILogger Logger => _logger ??= LogManager.GetLogger("ModScriptRuntime");
        private static readonly Dictionary<string, DynValue> ConsoleGlobals = new();
        private static Table _consoleEnv;

        // Mod environments whose Enabled follows the active feature set. Environments are forked per mod and per
        // console run, so they are held weakly and dropped once collected
        private static readonly List<(WeakReference<Table> env, string modId)> FeatureEnvironments = new();

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            FeatureEnvironments.Clear();
            _authoredSourceBodies = null;
            _authoredSourceContractHash = null;
        }

        /// <summary>
        /// Gets the mod a script environment belongs to.
        /// </summary>
        /// <param name="env">The environment's globals, or null.</param>
        /// <returns>The mod id, or null for an environment no mod owns.</returns>
        public static string? ModIdOf(Table? env) => env?.Get(MOD_ID_GLOBAL).CastToString();

        /// <summary>
        /// Gets the mod whose script is making a call.
        /// </summary>
        /// <param name="context">The calling script's context.</param>
        /// <returns>The mod id, or null for a script no mod owns.</returns>
        public static string? ModIdOf(ScriptExecutionContext context) => ModIdOf(context.CurrentGlobalEnv);

        /// <summary>
        /// Starts keeping an environment's <c>Enabled</c> in step with the active feature set, and sets it now.
        /// </summary>
        /// <param name="env">The environment's globals.</param>
        /// <param name="modId">The mod the environment belongs to.</param>
        public static void TrackFeatureEnvironment(Table env, string modId)
        {
            FeatureEnvironments.Add((new WeakReference<Table>(env), modId));
            env["Enabled"] = ActiveFeatures.IsModEnabled(modId);
        }

        internal static void RefreshFeatureEnvironments()
        {
            FeatureEnvironments.RemoveAll(entry => !entry.env.TryGetTarget(out _));
            foreach (var (reference, modId) in FeatureEnvironments)
            {
                if (reference.TryGetTarget(out Table env))
                {
                    env["Enabled"] = ActiveFeatures.IsModEnabled(modId);
                }
            }
        }

        /// <summary>
        /// Runs every script file the descriptor declared at discovery, in one forked environment.
        /// </summary>
        /// <param name="descriptor">The mod whose <see cref="SpaceWarpPluginDescriptor.ScriptFiles" /> to run.</param>
        public static void RunMod(SpaceWarpPluginDescriptor descriptor)
        {
            if (descriptor.ScriptFiles.Count == 0)
                return;

            var env = CreateModEnv(descriptor);
            foreach (var file in descriptor.ScriptFiles)
            {
                RunSource(descriptor, env, file, File.ReadAllText(file));
            }
        }

        /// <summary>
        /// Runs a set of named script sources for the descriptor in one forked environment.
        /// </summary>
        /// <remarks>
        /// For sources that are not files on disk, such as addressable text assets loaded by the owning subsystem.
        /// </remarks>
        /// <param name="descriptor">The mod the sources belong to.</param>
        /// <param name="sources">The script sources, each a (key, content) pair.</param>
        public static void RunModSources(SpaceWarpPluginDescriptor descriptor, IReadOnlyList<(string key, string content)> sources)
        {
            if (sources.Count == 0)
                return;

            var env = CreateModEnv(descriptor);
            foreach (var (key, content) in sources)
            {
                RunSource(descriptor, env, key, content);
            }
        }

        private static void RunSource(SpaceWarpPluginDescriptor descriptor, Table env, string key, string content)
        {
            if (PatchManager.Core.Assets.PatchingManager.ExcludeDiagnosticAuthoredLuaBody(descriptor.Guid, key, content)
                || IsAuthoredSourceBody(descriptor.Guid, key, content))
            {
                descriptor.LoadedScripts[key] = content;
                return;
            }
            bool succeeded = false;
            try
            {
                RunBodyIn(env, content, key);
                succeeded = true;
            }
            catch (Exception e)
            {
                var reason = e is InterpreterException ie ? ie.DecoratedMessage : e.ToString();
                descriptor.ScriptErrors.Add((key, reason));
                Logger.LogError($"Mod script '{key}' failed to run: {reason}");
            }

            descriptor.LoadedScripts[key] = content;
            AuthoredSourceObserver?.Invoke(descriptor.Guid, key, content, succeeded);
        }

        /// <summary>
        /// Runs a Lua body in an already-forked mod environment.
        /// </summary>
        /// <param name="env">An environment from <see cref="CreateModEnv" />.</param>
        /// <param name="code">The Lua body to run.</param>
        /// <param name="chunkName">A name for the chunk, used in error messages.</param>
        public static void RunBodyIn(Table env, string code, string chunkName)
        {
            env.OwnerScript.DoString(code, env, chunkName);
        }

        /// <summary>
        /// Forks a per-mod environment and contributes its globals, without running anything.
        /// </summary>
        /// <remarks>
        /// Reads fall through to the root globals, writes stay local to the child, so each mod is isolated on the
        /// one shared VM. The runtime contributes the general mod-loading globals (<c>ModId</c>, <c>Location</c>, the
        /// <c>require</c> loader, and the <c>Log</c> logger), then runs every registered contributor.
        /// </remarks>
        /// <param name="descriptor">The mod to fork an environment for.</param>
        /// <returns>The forked environment's globals table.</returns>
        public static Table CreateModEnv(SpaceWarpPluginDescriptor descriptor) =>
            CreateEnv(
                descriptor.Guid,
                descriptor.Folder?.FullName,
                descriptor.Logger,
                descriptor.ConfigFile,
                descriptor.CampaignConfigFile
            );

        /// <summary>
        /// Forks the shared console environment, carrying the same globals a mod environment gets.
        /// </summary>
        /// <remarks>
        /// The in-game console runs its scripts on this environment, so a console script sees the same Lua surface
        /// (<c>SW</c>, <c>Game</c>, <c>Config</c>, contributor globals) a mod does. Its <c>Location</c> is the Lua
        /// folder, so console scripts can <c>require</c> libraries kept there.
        /// </remarks>
        /// <returns>The console environment's globals table.</returns>
        public static Table CreateConsoleEnv()
        {
            var logger = LogManager.GetLogger(ConsoleModId);
            var configFile = new JsonConfigFile(Path.Combine(CommonPaths.LuaFolder, "spacewarp-console-config.json"));
            // The console belongs to no campaign, so its campaign config is never stored
            var env = CreateEnv(
                ConsoleModId,
                Path.GetFullPath(CommonPaths.LuaFolder),
                logger,
                configFile,
                JsonConfigFile.InMemory()
            );
            foreach (var pair in ConsoleGlobals)
            {
                env[pair.Key] = pair.Value;
            }

            return env;
        }

        private static Table CreateEnv(
            string modId,
            string location,
            ILogger logger,
            IConfigFile configFile,
            IConfigFile campaignConfigFile
        )
        {
            // Every library the env gets is userdata, so their types register before the first fork uses them
            LuaRegistration.RegisterLoadedAssemblies();

            // Read on each fork, since the game creates its root environment after the mod runtime first loads
            var root = ((ScriptEnvironment)GameManager.Instance.Game.ScriptEnvironment).globals;
            var script = root.OwnerScript;
            script.Options.ScriptLoader = Loader;

            var child = new Table(script);
            var meta = new Table(script);
            meta["__index"] = DynValue.NewTable(root);
            child.MetaTable = meta;

            child[MOD_ID_GLOBAL] = modId;
            if (location != null)
            {
                child["Location"] = location;
            }

            ModRequire.InstallOn(child);
            SwLibrary.ContributeTo(child);
            child["Log"] = new ModLogger(logger);
            ConfigTypes.ContributeTo(child);
            child["Config"] = new ModConfig(configFile);
            child["CampaignConfig"] = new ModConfig(campaignConfigFile, descriptor => descriptor.CampaignConfigFile);
            child["Console"] = new ConsoleLibrary();
            child["Features"] = FeaturesLibrary.Instance;
            // The console is not a mod, so it has no Enabled to keep up to date
            if (modId != ConsoleModId)
            {
                TrackFeatureEnvironment(child, modId);
            }

            foreach (var contributor in ModRuntime.Contributors)
            {
                contributor.Contribute(child);
            }

            return child;
        }

        /// <summary>
        /// Loads Lua source as a coroutine on the given environment, suspended at its start and ready to resume.
        /// </summary>
        /// <param name="env">The environment to load the coroutine on.</param>
        /// <param name="code">The Lua source to run.</param>
        /// <param name="chunkName">A name for the chunk, used in error messages.</param>
        /// <returns>The coroutine value.</returns>
        public static DynValue CreateCoroutine(Table env, string code, string chunkName)
        {
            var function = env.OwnerScript.LoadString(code, env, chunkName);
            var coroutine = env.OwnerScript.CreateCoroutine(function);
            coroutine.Coroutine.AutoYieldCounter = 1000;
            return coroutine;
        }

        /// <summary>
        /// Starts a console Lua run: a coroutine of <paramref name="code" /> on the shared console environment, which
        /// is forked on first use.
        /// </summary>
        /// <param name="code">The Lua source to run.</param>
        /// <returns>The run, which the console resumes each frame.</returns>
        public static ConsoleScriptRun RunConsole(string code)
        {
            _consoleEnv ??= CreateConsoleEnv();
            var coroutine = CreateCoroutine(_consoleEnv, code, "spacewarp-console");
            return new ConsoleScriptRun(coroutine);
        }

        /// <summary>
        /// Runs Lua synchronously on the shared console environment and returns its result, for callers that need an
        /// immediate value (such as the CLI eval bridge) rather than the frame-resumed coroutine <see cref="RunConsole" /> returns.
        /// </summary>
        /// <remarks>
        /// The console environment carries the same contributed surface a mod gets, so evaluated code sees the SW,
        /// Game, Config, and other contributor globals - not just the root globals.
        /// </remarks>
        /// <param name="code">The Lua source to evaluate.</param>
        /// <param name="chunkName">A name for the chunk, used in error messages.</param>
        /// <returns>The value the chunk returned.</returns>
        public static DynValue EvalConsoleSync(string code, string chunkName)
        {
            _consoleEnv ??= CreateConsoleEnv();
            return _consoleEnv.OwnerScript.DoString(code, _consoleEnv, chunkName);
        }

        /// <summary>
        /// Registers a value or function as a global on the console environment, so console scripts can use it.
        /// </summary>
        /// <remarks>
        /// Applies to the live console environment immediately when one exists, and is replayed onto the console
        /// environment whenever it is forked.
        /// </remarks>
        /// <param name="name">The global name.</param>
        /// <param name="value">The value or function to expose.</param>
        public static void RegisterConsoleGlobal(string name, DynValue value)
        {
            ConsoleGlobals[name] = value;
            if (_consoleEnv != null)
            {
                _consoleEnv[name] = value;
            }
        }
        private static Newtonsoft.Json.Linq.JObject _authoredSourceBodies;
        private static string _authoredSourceContractHash;

        /// <summary>
        /// Gets the portable source-bake contract hash included in the ordinary patch-cache checksum.
        /// </summary>
        internal static string AuthoredSourceContractHash
        {
            get
            {
                LoadAuthoredSourceBodies();
                return _authoredSourceContractHash ?? string.Empty;
            }
        }

        private static void LoadAuthoredSourceBodies()
        {
            if (_authoredSourceBodies != null)
                return;
            if (!Redux.ReduxAssets.IsSourceOnly)
            {
                _authoredSourceBodies = new Newtonsoft.Json.Linq.JObject();
                return;
            }
            string path = Path.Combine(UnityEngine.Application.streamingAssetsPath, "Redux", "lua-source-bakes.json");
            if (!File.Exists(path))
            {
                _authoredSourceBodies = new Newtonsoft.Json.Linq.JObject();
                return;
            }
            if (path.Length > 240 || new FileInfo(path).Length > 8 * 1024 * 1024)
                throw new InvalidDataException("Portable authored Lua source contract exceeds its file budget.");
            var contract = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
            ValidateAuthoredSourceContract(contract);
            var bodies = new Newtonsoft.Json.Linq.JObject();
            foreach (Newtonsoft.Json.Linq.JObject row in contract["bodies"])
            {
                bodies[(string)row["key"]] = row.DeepClone();
            }
            _authoredSourceBodies = bodies;
            _authoredSourceContractHash = Redux.IO.Sha256.OfFile(path);
        }

        /// <summary>
        /// Validate portable static-body exclusions without relying on conversion folders or the stock installation.
        /// </summary>
        /// <param name="contract">The source-owned build and runtime contract.</param>
        /// <exception cref="InvalidDataException">Thrown when body coverage or saved-source evidence is incomplete.</exception>
        internal static void ValidateAuthoredSourceContract(Newtonsoft.Json.Linq.JObject contract)
        {
            if ((string)contract["schema"] == "redux.authored-lua-source.v2")
            {
                ValidateFixedBaselineSourceContract(contract);
                return;
            }
            if ((string)contract["schema"] != "redux.authored-lua-source.v1" || contract["bodies"] is not Newtonsoft.Json.Linq.JArray bodies || bodies.Count == 0)
                throw new InvalidDataException("Unknown or empty authored Lua source contract.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (Newtonsoft.Json.Linq.JObject row in bodies)
            {
                string key = (string)row["key"];
                if ((string)row["modId"] != "Ksp2Redux" || string.IsNullOrEmpty(key) || !keys.Add(key) || !key.StartsWith("Ksp2Redux/", StringComparison.Ordinal)
                    || key is "Ksp2Redux/Features" or "Ksp2Redux/Passes" or "Ksp2Redux/PantherNozzle"
                    || (string)row["classification"] != "complete-static-definitions" || row["contexts"] is not Newtonsoft.Json.Linq.JArray contexts || contexts.Count == 0
                    || row["owners"] is not Newtonsoft.Json.Linq.JArray owners || owners.Count == 0
                    || row["sourceSaveEvidence"] is not Newtonsoft.Json.Linq.JArray saves || saves.Count == 0
                    || row["bodyEffects"] is not Newtonsoft.Json.Linq.JObject effects
                    || (bool?)effects["changesDynamicRegistry"] != false || (int?)effects["definitionOperations"] is not > 0
                    || effects["patches"] is not Newtonsoft.Json.Linq.JObject patches || effects["created"] is not Newtonsoft.Json.Linq.JObject created
                    || patches.Count + created.Count != (int)effects["definitionOperations"])
                    throw new InvalidDataException("A dynamic or partially covered Lua body cannot be excluded: " + key);
                foreach (string hash in new[] { (string)row["bodyTextSha256"], (string)row["coverageSha256"] })
                {
                    if (!IsAuthoredSourceHash(hash))
                        throw new InvalidDataException("Authored Lua body hashes are missing: " + key);
                }
                foreach (Newtonsoft.Json.Linq.JObject save in saves)
                {
                    foreach (string field in new[] { "applyReceiptSha256", "postflightReceiptSha256", "applyReportSha256", "postflightReportSha256" })
                    {
                        if (!IsAuthoredSourceHash((string)save[field]))
                            throw new InvalidDataException("Actual completed Lua source-save lineage is missing: " + key);
                    }
                }
                foreach (Newtonsoft.Json.Linq.JObject context in contexts)
                {
                    if (context["features"] is not Newtonsoft.Json.Linq.JArray || context["targets"] is not Newtonsoft.Json.Linq.JArray targets || targets.Count == 0
                        || !IsAuthoredSourceHash((string)context["beforeCaptureSha256"]) || !IsAuthoredSourceHash((string)context["authoredCaptureSha256"])
                        || context["behaviorEvidence"] is not Newtonsoft.Json.Linq.JArray behavior || behavior.Count < 2)
                        throw new InvalidDataException("Complete normal feature and external/late behavior contexts are required: " + key);
                    foreach (Newtonsoft.Json.Linq.JObject proof in behavior)
                    {
                        if (!IsAuthoredSourceHash((string)proof["baselineCaptureSha256"]) || !IsAuthoredSourceHash((string)proof["authoredCaptureSha256"]))
                            throw new InvalidDataException("An authored Lua behavior proof is incomplete: " + key);
                    }
                }
                var features = new HashSet<string>(StringComparer.Ordinal);
                var labels = new HashSet<string>(StringComparer.Ordinal);
                foreach (Newtonsoft.Json.Linq.JProperty operation in patches.Properties())
                {
                    labels.Add((string)operation.Value["label"]);
                    foreach (string feature in operation.Value["features"])
                    {
                        features.Add(feature);
                    }
                    foreach (string feature in operation.Value["noFeatures"])
                    {
                        features.Add(feature);
                    }
                }
                foreach (Newtonsoft.Json.Linq.JProperty operation in created.Properties())
                {
                    labels.Add((string)operation.Value["label"]);
                    foreach (string feature in operation.Value["features"])
                    {
                        features.Add(feature);
                    }
                    foreach (string feature in operation.Value["noFeatures"])
                    {
                        features.Add(feature);
                    }
                }
                if (features.Count > 10 || labels.Count == 0)
                    throw new InvalidDataException("A dynamic Lua context set remains pending: " + key);
                var ordered = new List<string>(features);
                ordered.Sort(StringComparer.Ordinal);
                var vectors = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
                foreach (Newtonsoft.Json.Linq.JObject context in contexts)
                {
                    string label = (string)context["label"];
                    if (!labels.Contains(label))
                        throw new InvalidDataException("Foreign Lua source context label: " + label);
                    var active = new HashSet<string>(context["features"].Values<string>(), StringComparer.Ordinal);
                    string vector = string.Empty;
                    foreach (string feature in ordered)
                    {
                        vector += active.Contains(feature) ? "1" : "0";
                    }
                    if (!vectors.TryGetValue(label, out HashSet<string> values))
                    {
                        values = new HashSet<string>(StringComparer.Ordinal);
                        vectors.Add(label, values);
                    }
                    values.Add(vector);
                }
                foreach (string label in labels)
                {
                    if (!vectors.TryGetValue(label, out HashSet<string> values) || values.Count != 1 << ordered.Count)
                        throw new InvalidDataException("Complementary actual Lua contexts are incomplete: " + label);
                }
            }
        }

        private static void ValidateFixedBaselineSourceContract(Newtonsoft.Json.Linq.JObject contract)
        {
            if ((string)contract["baseline"] != "fixed-redux" || contract["bodies"] is not Newtonsoft.Json.Linq.JArray bodies
                || bodies.Count == 0 || bodies.Count > 64)
                throw new InvalidDataException("Unknown or empty fixed baseline source contract.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (Newtonsoft.Json.Linq.JObject row in bodies)
            {
                string key = (string)row["key"];
                if ((string)row["modId"] != "Ksp2Redux" || string.IsNullOrEmpty(key) || !key.StartsWith("Ksp2Redux/", StringComparison.Ordinal)
                    || !keys.Add(key) || key is "Ksp2Redux/Features" or "Ksp2Redux/Passes" or "Ksp2Redux/PantherNozzle"
                    || (string)row["classification"] != "complete-static-definitions"
                    || !IsAuthoredSourceHash((string)row["bodyTextSha256"]) || !IsAuthoredSourceHash((string)row["coverageSha256"])
                    || row["bodyEffects"] is not Newtonsoft.Json.Linq.JObject effects
                    || (bool?)effects["changesDynamicRegistry"] != false || (int?)effects["definitionOperations"] is not > 0
                    || effects["patches"] is not Newtonsoft.Json.Linq.JObject patches || effects["created"] is not Newtonsoft.Json.Linq.JObject created
                    || patches.Count + created.Count != (int)effects["definitionOperations"]
                    || row["owners"] is not Newtonsoft.Json.Linq.JArray owners || owners.Count == 0
                    || row["contexts"] is not Newtonsoft.Json.Linq.JArray contexts || contexts.Count == 0
                    || row["sourceEvidence"] is not Newtonsoft.Json.Linq.JArray saves || saves.Count == 0)
                    throw new InvalidDataException("Incomplete fixed baseline static body coverage: " + key);
                var expectedLabels = new HashSet<string>(StringComparer.Ordinal);
                foreach (Newtonsoft.Json.Linq.JProperty operation in patches.Properties().Concat(created.Properties()))
                {
                    string label = (string)operation.Value["label"];
                    if (string.IsNullOrEmpty(label))
                        throw new InvalidDataException("A baked static operation omitted its label: " + key);
                    expectedLabels.Add(label);
                    foreach (string field in new[] { "needsMods", "conflictsMods", "needsPatches", "conflictsPatches" })
                    {
                        if (operation.Value[field] is Newtonsoft.Json.Linq.JArray dependencies && dependencies.Count != 0)
                            throw new InvalidDataException("Dynamic dependencies cannot enter a fixed source baseline: " + key);
                    }
                }
                var observedLabels = new HashSet<string>(StringComparer.Ordinal);
                foreach (Newtonsoft.Json.Linq.JObject context in contexts)
                {
                    string label = (string)context["label"];
                    if (string.IsNullOrEmpty(label) || !observedLabels.Add(label) || !expectedLabels.Contains(label)
                        || context["capturedFeatures"] is not Newtonsoft.Json.Linq.JArray
                        || !IsAuthoredSourceHash((string)context["registrationCaptureSha256"])
                        || context["targets"] is not Newtonsoft.Json.Linq.JArray targets || targets.Count == 0)
                        throw new InvalidDataException("Incomplete fixed baseline source context: " + key);
                    var names = new HashSet<string>(StringComparer.Ordinal);
                    foreach (string target in targets)
                    {
                        if (string.IsNullOrEmpty(target) || !names.Add(target) || !owners.Any(owner =>
                            (string)owner["label"] == label && (string)owner["name"] == target))
                            throw new InvalidDataException("A static target lacks ordinary saved source ownership: " + key);
                    }
                }
                if (!expectedLabels.SetEquals(observedLabels))
                    throw new InvalidDataException("A static body still has uncovered labels: " + key);
                foreach (Newtonsoft.Json.Linq.JObject save in saves)
                {
                    if ((string)save["kind"] is not ("ordinary-source-batch-v1" or "existing-source-native-projection-v1")
                        || !IsAuthoredSourceHash((string)save["sourceBatchSha256"])
                        || !IsAuthoredSourceHash((string)save["nativeReadbackReceiptSha256"])
                        || !IsAuthoredSourceHash((string)save["nativeReadbackReportSha256"]))
                        throw new InvalidDataException("Actual ordinary source or native projection evidence is missing: " + key);
                }
            }
        }

        private static bool IsAuthoredSourceHash(string value)
        {
            if (value == null || value.Length != 64)
                return false;
            foreach (char character in value)
            {
                if (character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
                    return false;
            }
            return true;
        }

        private static bool IsAuthoredSourceBody(string modId, string key, string content)
        {
            if (modId != "Ksp2Redux" || !Redux.ReduxAssets.IsSourceOnly)
                return false;
            LoadAuthoredSourceBodies();
            string hash = Redux.IO.Sha256.OfString(content);
            if (_authoredSourceBodies[key] is JObject row && (string)row["bodyTextSha256"] != hash)
                throw new InvalidDataException("An authored Lua body changed. Move intentional new behavior into a separate ordinary source body or explicitly reconcile the baked defaults: " + key);
            return MatchesAuthoredSourceBody(_authoredSourceBodies, modId, key, hash);
        }

        private static bool MatchesAuthoredSourceBody(Newtonsoft.Json.Linq.JObject bodies, string modId, string key, string textHash)
            => modId == "Ksp2Redux" && bodies[key] is Newtonsoft.Json.Linq.JObject row && (string)row["bodyTextSha256"] == textHash;

    }
}

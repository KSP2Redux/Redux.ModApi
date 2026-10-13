using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using SpaceWarp2.API.Mods.JSON;
using UnityEngine;

namespace SpaceWarp2.API.Features
{
    /// <summary>
    /// Every feature the installed mods declare, and the rules that turn a requested set into an enabled one.
    /// </summary>
    /// <remarks>
    /// Each mod gets a default feature, <c>ModId:ModId</c>, which requires the default features of its dependencies and
    /// conflicts with those of its conflicts. Every other feature of a mod requires that mod's default feature. A
    /// feature may provide others, standing in for them in requirements. Ids match case-insensitively and resolve to
    /// the casing they were declared with.
    /// </remarks>
    [PublicAPI]
    public class FeatureRegistry
    {
        /// <summary>
        /// The character between the mod id and the feature id.
        /// </summary>
        public const char SEPARATOR = ':';

        /// <summary>
        /// Qualifies a feature id with a mod id unless it already names a mod.
        /// </summary>
        /// <param name="modId">The mod the id is relative to.</param>
        /// <param name="featureId">A bare or already qualified feature id.</param>
        /// <returns>The qualified feature id.</returns>
        public static string Qualify(string modId, string featureId) =>
            featureId.IndexOf(SEPARATOR) >= 0 ? featureId : $"{modId}{SEPARATOR}{featureId}";

        /// <summary>
        /// Gets the id of a mod's default feature.
        /// </summary>
        /// <param name="modId">The mod id.</param>
        /// <returns>The default feature id, <c>ModId:ModId</c>.</returns>
        public static string DefaultFor(string modId) => $"{modId}{SEPARATOR}{modId}";
        /// <summary>
        /// Gets the namespace of a qualified feature id, the part before the separator.
        /// </summary>
        /// <param name="featureId">The qualified feature id.</param>
        /// <returns>The namespace, or the whole id when it has no separator.</returns>
        public static string NamespaceOf(string featureId)
        {
            int separator = featureId.IndexOf(SEPARATOR);
            return separator < 0 ? featureId : featureId.Substring(0, separator);
        }

        private readonly Dictionary<string, FeatureDefinition> _features = new(StringComparer.OrdinalIgnoreCase);
        // The features that provide each provided id, sorted
        private readonly Dictionary<string, List<string>> _standIns = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the one registry every feature is declared in: the loaded mods' swinfo features, then those Lua and C#
        /// declare. It is replaced only when a play session starts, so a reference to it never goes stale.
        /// </summary>
        public static FeatureRegistry Instance { get; private set; } = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Instance = new FeatureRegistry();

        /// <summary>
        /// Declares the loaded mods' swinfo features in <see cref="Instance" />, once their load order is resolved.
        /// </summary>
        /// <param name="mods">The loaded mods' infos.</param>
        internal static void AddLoadedMods(IEnumerable<ModInfo> mods)
        {
            foreach (ModInfo mod in mods)
            {
                if (mod == null)
                    continue;

                try
                {
                    Instance.AddMod(mod);
                }
                catch (Exception exception)
                {
                    SpaceWarpPlugin.Logger.LogError($"Could not read the features of {mod.Name}: {exception.Message}");
                }
            }
        }

        /// <summary>
        /// Gets every declared feature.
        /// </summary>
        public IEnumerable<FeatureDefinition> All => _features.Values;

        /// <summary>
        /// Declares a mod's default feature and the features in its mod info.
        /// </summary>
        /// <param name="info">The mod info.</param>
        public void AddMod(ModInfo info)
        {
            string modId = info.ModID;
            Define(
                modId,
                modId,
                info.Dependencies.Select(dependency => FeatureRegistry.DefaultFor(dependency.ID)),
                info.Conflicts.Select(conflict => FeatureRegistry.DefaultFor(conflict.ID))
            );

            foreach (KeyValuePair<string, FeatureInfo> feature in info.Features)
            {
                Define(
                    modId,
                    feature.Key,
                    feature.Value.Requires,
                    feature.Value.Conflicts,
                    feature.Value.Provides,
                    feature.Value.Recommended
                );
            }
        }

        /// <summary>
        /// Declares one feature of a mod outside its mod info.
        /// </summary>
        /// <remarks>
        /// Declaring an id that already exists adds its requirements, conflicts and provided features to the earlier
        /// declaration's, which is how a mod states its default feature's relations beyond its swinfo dependencies and
        /// conflicts. A feature either declaration recommends stays recommended. A mod declares only its own features,
        /// but may provide any mod's.
        /// </remarks>
        /// <param name="modId">The declaring mod.</param>
        /// <param name="featureId">The feature id, bare or qualified with the declaring mod.</param>
        /// <param name="requires">The required feature ids, bare ones relative to the declaring mod.</param>
        /// <param name="conflicts">The conflicting feature ids, bare ones relative to the declaring mod.</param>
        /// <param name="provides">The provided feature ids, bare ones relative to the declaring mod.</param>
        /// <param name="isRecommended">Whether the mod recommends the feature.</param>
        /// <returns>The declared feature.</returns>
        /// <exception cref="ArgumentException">Thrown when the id names another mod, or the feature provides itself.</exception>
        public FeatureDefinition Define(
            string modId,
            string featureId,
            IEnumerable<string>? requires = null,
            IEnumerable<string>? conflicts = null,
            IEnumerable<string>? provides = null,
            bool isRecommended = false
        )
        {
            string id = FeatureRegistry.Qualify(modId, featureId);
            string owner = FeatureRegistry.NamespaceOf(id);
            if (!string.Equals(owner, modId, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"{modId} cannot declare {id}, which belongs to {owner}.", nameof(featureId));

            List<string> newProvides = QualifyAll(owner, provides ?? Array.Empty<string>());
            if (newProvides.Contains(id, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException($"{id} cannot provide itself.", nameof(provides));

            string defaultId = FeatureRegistry.DefaultFor(owner);

            // A namespace's own default feature is the one feature that does not require itself.
            IEnumerable<string> implicitRequires = string.Equals(id, defaultId, StringComparison.OrdinalIgnoreCase)
                ? Array.Empty<string>()
                : new[] { defaultId };

            IEnumerable<string> allRequires = implicitRequires.Concat(requires ?? Array.Empty<string>());
            IEnumerable<string> allConflicts = conflicts ?? Array.Empty<string>();
            IEnumerable<string> allProvides = newProvides;

            // A redeclaration adds to the feature, so a mod's default keeps the edges its swinfo relations gave it
            if (_features.TryGetValue(id, out FeatureDefinition existing))
            {
                id = existing.Id;
                allRequires = existing.Requires.Concat(allRequires);
                allConflicts = existing.Conflicts.Concat(allConflicts);
                allProvides = existing.Provides.Concat(allProvides);
                isRecommended |= existing.IsRecommended;
            }

            var definition = new FeatureDefinition(
                id,
                owner,
                QualifyAll(owner, allRequires),
                QualifyAll(owner, allConflicts),
                QualifyAll(owner, allProvides),
                isRecommended
            );
            _features[id] = definition;
            foreach (string provided in newProvides)
            {
                if (!_standIns.TryGetValue(provided, out List<string> standIns))
                {
                    standIns = new List<string>();
                    _standIns[provided] = standIns;
                }

                if (!standIns.Contains(id, StringComparer.OrdinalIgnoreCase))
                {
                    standIns.Add(id);
                    standIns.Sort(StringComparer.Ordinal);
                }
            }

            return definition;
        }

        /// <summary>
        /// Lists the features that can meet a requirement on a feature id: every feature that provides it, and the
        /// feature itself when one is declared under that id.
        /// </summary>
        /// <remarks>
        /// A virtual id, one nothing declares, has only its stand-ins. More than one provider means a set needs a
        /// choice between them, since they all conflict with each other.
        /// </remarks>
        /// <param name="featureId">The qualified feature id, in any casing.</param>
        /// <returns>The providers' ids, sorted.</returns>
        public List<string> ProvidersOf(string featureId)
        {
            var providers = new List<string>(StandInsOf(featureId));
            if (_features.TryGetValue(featureId, out FeatureDefinition definition))
            {
                providers.Add(definition.Id);
                providers.Sort(StringComparer.Ordinal);
            }

            return providers;
        }

        /// <summary>
        /// Looks a feature up by id.
        /// </summary>
        /// <param name="featureId">The qualified feature id, in any casing.</param>
        /// <param name="definition">The feature, when found.</param>
        /// <returns>True if the feature is declared, false otherwise.</returns>
        public bool TryGet(string featureId, out FeatureDefinition definition) =>
            _features.TryGetValue(featureId, out definition);

        /// <summary>
        /// Checks whether a feature and everything it requires, transitively, is declared.
        /// </summary>
        /// <remarks>
        /// An unsatisfiable feature is hidden from players, for example one whose mod depends on a mod that is not
        /// installed.
        /// </remarks>
        /// <param name="featureId">The qualified feature id.</param>
        /// <returns>True if the feature can be turned on, false otherwise.</returns>
        public bool IsSatisfiable(string featureId) =>
            IsSatisfiable(featureId, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        /// <summary>
        /// Turns a requested set of features into the set that ends up on.
        /// </summary>
        /// <remarks>
        /// A requirement on an id something provides is met by any provider in the set, and a stand-in meets it without
        /// turning a real feature under that id on. With no provider in the set, the one installed provider that can
        /// be turned on is chosen, the real feature counting as one. A requested real feature is on whatever stands in
        /// for it. A provided virtual id is on whenever one of its providers is.
        /// </remarks>
        /// <param name="requested">The requested qualified feature ids.</param>
        /// <returns>The enabled closure, the unmet ids, and the conflicts between enabled features.</returns>
        public FeatureResolution Resolve(IEnumerable<string> requested)
        {
            var enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // Held back until the rest of the set is in, since a provider anywhere in it meets them
            var deferred = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending = new Stack<(string Id, bool IsRequirement)>(requested.Select(id => (id, false)));
            var chosen = new List<string>();
            while (true)
            {
                while (pending.Count > 0)
                {
                    (string id, bool isRequirement) = pending.Pop();
                    bool isDeclared = _features.TryGetValue(id, out FeatureDefinition definition);
                    if ((isRequirement || !isDeclared) && _standIns.ContainsKey(id))
                    {
                        deferred.Add(id);
                        continue;
                    }

                    if (!isDeclared)
                    {
                        missing.Add(id);
                        continue;
                    }

                    if (!enabled.Add(definition.Id))
                        continue;

                    foreach (string required in definition.Requires)
                    {
                        pending.Push((required, true));
                    }
                }

                string? choice = deferred
                    .Where(id => !IsMet(id, enabled))
                    .Select(SoleProviderOf)
                    .FirstOrDefault(provider => provider != null);
                if (choice == null)
                    break;

                chosen.Add(choice);
                pending.Push((choice, false));
            }

            missing.UnionWith(deferred.Where(id => !IsMet(id, enabled)));

            foreach (string id in enabled.OrderBy(id => id, StringComparer.Ordinal).ToList())
            {
                foreach (string provided in _features[id].Provides)
                {
                    if (!_features.ContainsKey(provided))
                    {
                        enabled.Add(provided);
                    }
                }
            }

            return new FeatureResolution(
                enabled.OrderBy(id => id, StringComparer.Ordinal).ToList(),
                missing.OrderBy(id => id, StringComparer.Ordinal).ToList(),
                FindConflicts(enabled),
                chosen.OrderBy(id => id, StringComparer.Ordinal).ToList()
            );
        }

        private IReadOnlyList<string> StandInsOf(string featureId) =>
            _standIns.TryGetValue(featureId, out List<string> standIns) ? standIns : Array.Empty<string>();

        private bool IsMet(string featureId, HashSet<string> enabled) =>
            enabled.Contains(featureId) || StandInsOf(featureId).Any(enabled.Contains);

        private string? SoleProviderOf(string featureId)
        {
            List<string> providers = ProvidersOf(featureId)
                .Where(provider => CanTurnOn(provider, new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
                .ToList();
            return providers.Count == 1 ? providers[0] : null;
        }

        private bool IsSatisfiable(string featureId, HashSet<string> onPath)
        {
            // An id already on the path is being checked further up, so a cycle does not make it unsatisfiable on its own
            if (!onPath.Add(featureId))
                return true;

            bool isSatisfiable = CanTurnOn(featureId, onPath)
                || StandInsOf(featureId).Any(standIn => CanTurnOn(standIn, onPath));
            onPath.Remove(featureId);
            return isSatisfiable;
        }

        private bool CanTurnOn(string featureId, HashSet<string> onPath) =>
            _features.TryGetValue(featureId, out FeatureDefinition definition)
            && definition.Requires.All(required => IsSatisfiable(required, onPath));

        private List<(string First, string Second)> FindConflicts(HashSet<string> enabled)
        {
            var conflicts = new List<(string First, string Second)>();
            var seen = new HashSet<(string, string)>();
            foreach (string id in enabled.OrderBy(id => id, StringComparer.Ordinal))
            {
                // A provided virtual id has no definition, so it declares no conflicts
                if (!_features.TryGetValue(id, out FeatureDefinition definition))
                    continue;

                foreach (string conflicting in definition.Conflicts)
                {
                    if (!_features.TryGetValue(conflicting, out FeatureDefinition other) || !enabled.Contains(other.Id))
                        continue;

                    var conflict = string.CompareOrdinal(id, other.Id) <= 0 ? (id, other.Id) : (other.Id, id);
                    if (seen.Add(conflict))
                    {
                        conflicts.Add(conflict);
                    }
                }
            }

            // Every provider of an id conflicts with every other, the real feature under that id included
            foreach (string provided in _standIns.Keys.OrderBy(id => id, StringComparer.Ordinal))
            {
                List<string> providers = ProvidersOf(provided).Where(enabled.Contains).ToList();
                for (int first = 0; first < providers.Count; first++)
                {
                    for (int second = first + 1; second < providers.Count; second++)
                    {
                        var conflict = (providers[first], providers[second]);
                        if (seen.Add(conflict))
                        {
                            conflicts.Add(conflict);
                        }
                    }
                }
            }

            return conflicts;
        }

        private static List<string> QualifyAll(string modId, IEnumerable<string> ids)
        {
            var qualified = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string id in ids)
            {
                if (string.IsNullOrEmpty(id))
                    continue;

                string full = FeatureRegistry.Qualify(modId, id);
                if (seen.Add(full))
                {
                    qualified.Add(full);
                }
            }

            return qualified;
        }
    }
}

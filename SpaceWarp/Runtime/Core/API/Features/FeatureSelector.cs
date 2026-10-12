#nullable enable

using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using SpaceWarp2.API.Mods;

namespace SpaceWarp2.API.Features
{
    /// <summary>
    /// The features something a mod registers needs on and off: a patch, a created asset, a prefab patch, a gated
    /// action or a config entry. Besides those, it always needs its mod's default feature.
    /// </summary>
    /// <remarks>
    /// Ids are stored qualified. The registering mod is not stored, since every owner already keeps it.
    /// </remarks>
    [PublicAPI]
    public class FeatureSelector
    {
        /// <summary>
        /// The tag prefix of a config entry shown only while a feature is on, such as <c>feature:Fast</c>.
        /// </summary>
        public const string FEATURE_TAG_PREFIX = "feature:";

        /// <summary>
        /// The tag prefix of a config entry shown only while a feature is off.
        /// </summary>
        public const string NO_FEATURE_TAG_PREFIX = "nofeature:";

        // Features of the PM:WithFeature scopes open now, outermost first. Lua runs on one thread, so a scope
        // covers exactly the registrations its body makes.
        private static readonly List<string> OpenScopes = new();

        private readonly HashSet<string> _features = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _noFeatures = new(StringComparer.OrdinalIgnoreCase);

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            OpenScopes.Clear();
        }

        /// <summary>
        /// Gets the qualified features that must be on.
        /// </summary>
        public IReadOnlyCollection<string> Features => _features;

        /// <summary>
        /// Gets the qualified features that must be off.
        /// </summary>
        public IReadOnlyCollection<string> NoFeatures => _noFeatures;

        /// <summary>
        /// Builds a selector from config entry tags, ignoring tags that select no feature.
        /// </summary>
        /// <param name="modId">The mod whose config holds the entry, which bare ids belong to.</param>
        /// <param name="tags">The entry's tags.</param>
        /// <returns>The selector.</returns>
        public static FeatureSelector FromConfigTags(string modId, IEnumerable<string> tags)
        {
            var selector = new FeatureSelector();
            foreach (string tag in tags)
            {
                if (tag.StartsWith(FEATURE_TAG_PREFIX, StringComparison.Ordinal))
                {
                    selector.Feature(modId, tag.Substring(FEATURE_TAG_PREFIX.Length));
                }
                else if (tag.StartsWith(NO_FEATURE_TAG_PREFIX, StringComparison.Ordinal))
                {
                    selector.NoFeature(modId, tag.Substring(NO_FEATURE_TAG_PREFIX.Length));
                }
            }

            return selector;
        }

        /// <summary>
        /// Builds a selector from features already qualified, such as those a prefab patch manifest stores.
        /// </summary>
        /// <param name="features">The features that must be on.</param>
        /// <param name="noFeatures">The features that must be off.</param>
        /// <returns>The selector.</returns>
        public static FeatureSelector Of(IEnumerable<string> features, IEnumerable<string> noFeatures)
        {
            var selector = new FeatureSelector();
            selector._features.UnionWith(features);
            selector._noFeatures.UnionWith(noFeatures);
            return selector;
        }

        /// <summary>
        /// Checks whether a mod's registrations can take part in a set, before their own features are looked at.
        /// </summary>
        /// <remarks>
        /// This is the one place Redux is exempt: it is the game, so it is enabled in every set, and each piece of its
        /// content states the feature it belongs to instead. A registration with no mod is not gated by one either.
        /// </remarks>
        /// <param name="modId">The registering mod, or null.</param>
        /// <param name="enabled">The set's enabled features, or null before a set activates, which allows everything.</param>
        /// <returns>True if the mod is enabled in the set.</returns>
        public static bool AllowsMod(string? modId, ISet<string>? enabled) =>
            enabled == null
            || string.IsNullOrEmpty(modId)
            || string.Equals(modId, ModIds.KSP2_REDUX, StringComparison.OrdinalIgnoreCase)
            || enabled.Contains(FeatureRegistry.DefaultFor(modId!));

        /// <summary>
        /// Requires a feature to be on.
        /// </summary>
        /// <param name="modId">The registering mod, which a bare id belongs to.</param>
        /// <param name="featureId">The feature id, bare or qualified.</param>
        public void Feature(string modId, string featureId) => _features.Add(FeatureRegistry.Qualify(modId, featureId));

        /// <summary>
        /// Requires a feature to be off.
        /// </summary>
        /// <param name="modId">The registering mod, which a bare id belongs to.</param>
        /// <param name="featureId">The feature id, bare or qualified.</param>
        public void NoFeature(string modId, string featureId) =>
            _noFeatures.Add(FeatureRegistry.Qualify(modId, featureId));

        /// <summary>
        /// Requires the features of every <c>PM:WithFeature</c> scope open now. Each Lua-facing registration calls
        /// this once, as it is made.
        /// </summary>
        public void AddOpenScopes() => _features.UnionWith(OpenScopes);

        /// <summary>
        /// Checks whether a registration takes part in a set.
        /// </summary>
        /// <param name="modId">The registering mod, or null.</param>
        /// <param name="enabled">The set's enabled features, or null before a set activates, which allows everything.</param>
        /// <returns>True if the mod and every selected feature allow it.</returns>
        public bool Allows(string? modId, ISet<string>? enabled) => Allows(modId, enabled, out _);

        /// <summary>
        /// Checks whether a registration takes part in a set, and names what stops it.
        /// </summary>
        /// <param name="modId">The registering mod, or null.</param>
        /// <param name="enabled">The set's enabled features, or null before a set activates, which allows everything.</param>
        /// <param name="unmet">The first unmet requirement, such as <c>ModA:Fast</c> or <c>not ModA:Slow</c>, or null.</param>
        /// <returns>True if the mod and every selected feature allow it.</returns>
        public bool Allows(string? modId, ISet<string>? enabled, out string? unmet)
        {
            unmet = null;
            if (enabled == null)
                return true;

            if (!AllowsMod(modId, enabled))
            {
                unmet = FeatureRegistry.DefaultFor(modId!);
                return false;
            }

            foreach (string feature in _features)
            {
                if (enabled.Contains(feature))
                    continue;

                unmet = feature;
                return false;
            }

            foreach (string feature in _noFeatures)
            {
                if (!enabled.Contains(feature))
                    continue;

                unmet = $"not {feature}";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the qualified features of every <c>PM:WithFeature</c> scope open now, for registrations that store
        /// their features in some other form, such as config entry tags.
        /// </summary>
        internal static IReadOnlyList<string> OpenScopeFeatures => OpenScopes;

        /// <summary>
        /// Opens a <c>PM:WithFeature</c> scope. Every registration made before it closes needs the feature.
        /// </summary>
        /// <param name="featureId">The qualified feature id.</param>
        internal static void OpenScope(string featureId) => OpenScopes.Add(featureId);

        /// <summary>
        /// Closes the innermost <c>PM:WithFeature</c> scope.
        /// </summary>
        internal static void CloseScope() => OpenScopes.RemoveAt(OpenScopes.Count - 1);
    }
}

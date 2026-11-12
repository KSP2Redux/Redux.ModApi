using System.Collections.Generic;
using JetBrains.Annotations;

namespace SpaceWarp2.API.Features
{
    /// <summary>
    /// The outcome of resolving a requested set of features against a <see cref="FeatureRegistry" />.
    /// </summary>
    [PublicAPI]
    public class FeatureResolution
    {
        /// <summary>
        /// Gets the features that end up on, in ordinal order: the request plus everything it requires, and the
        /// virtual ids its features provide.
        /// </summary>
        public IReadOnlyList<string> Enabled { get; }

        /// <summary>
        /// Gets the requested or required ids nothing in the set meets, in ordinal order. An id with more than one
        /// installed provider (see <see cref="FeatureRegistry.ProvidersOf" />) is a choice left to the player, any
        /// other is one no installed mod declares.
        /// </summary>
        public IReadOnlyList<string> Missing { get; }

        /// <summary>
        /// Gets each pair of enabled features where one conflicts with the other, or both provide the same id, the
        /// ordinally first id first.
        /// </summary>
        public IReadOnlyList<(string First, string Second)> Conflicts { get; }

        /// <summary>
        /// Gets the providers the resolution chose on its own, as the sole installed provider of an id nothing in the
        /// request provided, in ordinal order. A campaign adds them to its request so the choice holds when another
        /// provider is installed later.
        /// </summary>
        public IReadOnlyList<string> Chosen { get; }

        /// <summary>
        /// Creates a resolution.
        /// </summary>
        /// <param name="enabled">The enabled feature ids, in ordinal order.</param>
        /// <param name="missing">The unmet feature ids, in ordinal order.</param>
        /// <param name="conflicts">The conflicting pairs.</param>
        /// <param name="chosen">The providers chosen on the request's behalf, in ordinal order.</param>
        public FeatureResolution(
            IReadOnlyList<string> enabled,
            IReadOnlyList<string> missing,
            IReadOnlyList<(string First, string Second)> conflicts,
            IReadOnlyList<string> chosen
        )
        {
            Enabled = enabled;
            Missing = missing;
            Conflicts = conflicts;
            Chosen = chosen;
        }
    }
}

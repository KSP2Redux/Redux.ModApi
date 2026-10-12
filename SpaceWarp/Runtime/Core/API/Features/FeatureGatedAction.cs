using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using MoonSharp.Interpreter;

namespace SpaceWarp2.API.Features
{
    /// <summary>
    /// Work a mod registers to run when the active feature set allows it: only while the mod is enabled, and only while
    /// the features selected with <see cref="Feature" /> are on and those selected with <see cref="NoFeature" /> are off.
    /// </summary>
    /// <remarks>
    /// Per-set data, campaign load work and Patch Manager run callbacks are all registered as this, and the registry
    /// they are added to decides when they run. Lua registrations hand it back so selectors can be chained.
    /// </remarks>
    [PublicAPI]
    [MoonSharpUserData]
    public class FeatureGatedAction
    {
        private readonly Action<Action> _work;

        static FeatureGatedAction()
        {
            UserData.RegisterType<FeatureGatedAction>();
        }

        /// <summary>
        /// Creates an action without registering it.
        /// </summary>
        /// <param name="modId">The mod the action belongs to.</param>
        /// <param name="name">The name shown while it runs.</param>
        /// <param name="work">The work, calling its argument once done.</param>
        /// <param name="reset">Work that runs whether or not the action applies, such as clearing what an earlier set
        /// loaded, or null.</param>
        public FeatureGatedAction(string modId, string name, Action<Action> work, Action reset = null)
        {
            ModId = modId;
            Name = name;
            _work = work;
            Reset = reset;
        }

        /// <summary>
        /// Gets the mod the action belongs to, whose default feature must be on for it to run unless the mod is on in
        /// every set.
        /// </summary>
        public string ModId { get; }

        /// <summary>
        /// Gets the features the action needs on and off.
        /// </summary>
        [MoonSharpHidden]
        public FeatureSelector Selector { get; } = new();

        /// <summary>
        /// Gets the name shown while the action runs.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the work that runs whether or not the action applies, or null.
        /// </summary>
        [MoonSharpHidden]
        public Action Reset { get; }

        /// <summary>
        /// Runs only while a feature is on.
        /// </summary>
        /// <param name="featureId">The feature id, bare ids belonging to the action's mod.</param>
        /// <returns>This action, for chaining.</returns>
        public FeatureGatedAction Feature(string featureId)
        {
            Selector.Feature(ModId, featureId);
            return this;
        }

        /// <summary>
        /// Runs only while a feature is off.
        /// </summary>
        /// <param name="featureId">The feature id, bare ids belonging to the action's mod.</param>
        /// <returns>This action, for chaining.</returns>
        public FeatureGatedAction NoFeature(string featureId)
        {
            Selector.NoFeature(ModId, featureId);
            return this;
        }

        /// <summary>
        /// Checks whether the action runs in the active set.
        /// </summary>
        /// <returns>True if the mod and every selected feature allow it.</returns>
        public bool Applies() => AppliesIn(ActiveFeatures.EnabledSet);

        /// <summary>
        /// Checks whether the action runs in a given set.
        /// </summary>
        /// <param name="enabled">The set's enabled features, or null before a set activates, which allows everything.</param>
        /// <returns>True if the mod and every selected feature allow it.</returns>
        [MoonSharpHidden]
        public bool AppliesIn(ISet<string> enabled) => Selector.Allows(ModId, enabled);

        /// <summary>
        /// Runs the work, without checking whether the action applies.
        /// </summary>
        /// <param name="done">Called once the work is done.</param>
        [MoonSharpHidden]
        public void Run(Action done)
        {
            _work(done);
        }
    }
}

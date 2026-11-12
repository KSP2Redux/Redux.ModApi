using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using MoonSharp.Interpreter;
using SpaceWarp2.API.Features;

namespace SpaceWarp2.API.Lifecycle
{
    /// <summary>
    /// The <c>Features</c> global: a read-only view of the active feature set.
    /// </summary>
    [PublicAPI]
    [MoonSharpUserData]
    public class FeaturesLibrary
    {
        /// <summary>
        /// Gets the one instance every environment shares.
        /// </summary>
        [MoonSharpHidden]
        public static readonly FeaturesLibrary Instance = new();

        /// <summary>
        /// Checks whether a feature is on.
        /// </summary>
        /// <param name="context">The calling script, whose <c>ModId</c> a bare id belongs to.</param>
        /// <param name="featureId">The feature id, bare or qualified.</param>
        /// <returns>True if the feature is on.</returns>
        public bool Has(ScriptExecutionContext context, string featureId)
        {
            string modId = ModScriptRuntime.ModIdOf(context);
            return ActiveFeatures.IsEnabled(modId == null ? featureId : FeatureRegistry.Qualify(modId, featureId));
        }

        /// <summary>
        /// Calls a function after each feature set activates, with the new set as <see cref="All" /> gives it.
        /// </summary>
        /// <remarks>
        /// Like the mod's other hooks, it is only called while the calling mod is on in the new set.
        /// </remarks>
        /// <param name="context">The calling script.</param>
        /// <param name="fn">The function to call.</param>
        /// <exception cref="ScriptRuntimeException">Thrown when <paramref name="fn" /> is not a function.</exception>
        public void OnChanged(ScriptExecutionContext context, DynValue fn)
        {
            if (fn.Type != DataType.Function)
                throw new ScriptRuntimeException("Features:OnChanged expects a function.");

            string modId = ModScriptRuntime.ModIdOf(context);
            Closure closure = fn.Function;
            ActiveFeatures.Changed += enabled =>
            {
                if (modId != null && !ActiveFeatures.RunsHooksOf(modId))
                    return;

                closure.Call(enabled == null ? DynValue.Nil : SetTable(closure.OwnerScript, enabled));
            };
        }

        /// <summary>
        /// Lists the enabled features as a set.
        /// </summary>
        /// <param name="context">The calling script.</param>
        /// <returns>A new table with each enabled id as a key mapped to true, or nil before any set is active.</returns>
        public DynValue All(ScriptExecutionContext context)
        {
            return ActiveFeatures.Enabled == null ? DynValue.Nil : SetTable(context.GetScript(), ActiveFeatures.Enabled);
        }

        /// <summary>
        /// Builds a feature set as Lua sees it, wherever a script is handed one.
        /// </summary>
        /// <param name="script">The script the table belongs to.</param>
        /// <param name="ids">The feature ids.</param>
        /// <returns>A new table with each id as a key mapped to true.</returns>
        [MoonSharpHidden]
        public static DynValue SetTable(Script script, IEnumerable<string> ids)
        {
            var set = new Table(script);
            foreach (string id in ids)
            {
                set[id] = true;
            }

            return DynValue.NewTable(set);
        }
    }
}

#nullable enable

using System;
using JetBrains.Annotations;

namespace SpaceWarp2.API.Mods
{
    /// <summary>
    /// Code a mod runs only when a mod it integrates with is loaded too.
    /// </summary>
    /// <remarks>
    /// <para>An integration lives in its own assembly, listed under <c>integrations</c> in the mod's swinfo with the id
    /// of the mod it integrates with. That assembly is only loaded when the other mod is, so it can reference the other
    /// mod's types freely. It holds one concrete subclass of this class, which needs a parameterless constructor.</para>
    /// <para>Each initialization callback runs once both mods have finished that phase. The campaign callbacks run only
    /// in campaigns where both mods are enabled. Bool fields marked with <see cref="Features.FeatureAttribute" /> follow
    /// the active feature set as they do on a plugin, with bare ids belonging to <see cref="CurrentMod" />.</para>
    /// <para>An integration that throws is logged and switched off for the rest of the session. Neither mod is
    /// affected.</para>
    /// </remarks>
    [PublicAPI]
    public abstract class ModIntegration
    {
        /// <summary>
        /// Gets the mod the integration belongs to.
        /// </summary>
        public SpaceWarpPluginDescriptor CurrentMod { get; internal set; } = null!;

        /// <summary>
        /// Gets the mod the integration integrates with.
        /// </summary>
        public SpaceWarpPluginDescriptor IntegratedMod { get; internal set; } = null!;

        /// <summary>
        /// Gets whether the integration still runs, which stops once one of its callbacks throws.
        /// </summary>
        public bool IsActive => CurrentMod.Integrations.TryGetValue(IntegratedMod.Guid, out ModIntegration? live) && live == this;

        /// <summary>
        /// Called once both mods have finished their pre-initialization.
        /// </summary>
        public virtual void OnPreInitialized()
        {
        }

        /// <summary>
        /// Called once both mods have finished their initialization.
        /// </summary>
        public virtual void OnInitialized()
        {
        }

        /// <summary>
        /// Called once both mods have finished their post-initialization.
        /// </summary>
        public virtual void OnPostInitialized()
        {
        }

        /// <summary>
        /// Called once a campaign both mods are enabled in has loaded.
        /// </summary>
        public virtual void OnCampaignLoaded()
        {
        }

        /// <summary>
        /// Called before that campaign unloads, to undo what <see cref="OnCampaignLoaded" /> set up.
        /// </summary>
        public virtual void OnCampaignUnloading()
        {
        }

        // Runs one callback and switches the integration off when it throws, so neither mod is affected
        internal bool TryRun(string phase, Action<ModIntegration> callback)
        {
            try
            {
                callback(this);
                return true;
            }
            catch (Exception exception)
            {
                CurrentMod.Logger.LogError(
                    $"Integration with {IntegratedMod.Guid} failed during {phase} and is switched off: {exception}");
                CurrentMod.RemoveIntegration(this);
                return false;
            }
        }
    }

    /// <summary>
    /// An integration with typed access to both mods' plugins.
    /// </summary>
    /// <remarks>
    /// The base class names the other mod's plugin type, so the integration only loads when that mod's assembly does.
    /// </remarks>
    /// <typeparam name="TCurrent">The plugin type of the mod the integration belongs to.</typeparam>
    /// <typeparam name="TIntegrated">The plugin type of the mod the integration integrates with.</typeparam>
    [PublicAPI]
    public abstract class ModIntegration<TCurrent, TIntegrated> : ModIntegration
        where TCurrent : class, ISpaceWarpMod
        where TIntegrated : class, ISpaceWarpMod
    {
        /// <summary>
        /// Gets the plugin of the mod the integration belongs to.
        /// </summary>
        public TCurrent Current => (TCurrent)CurrentMod.Plugin!;

        /// <summary>
        /// Gets the plugin of the mod the integration integrates with.
        /// </summary>
        public TIntegrated Integrated => (TIntegrated)IntegratedMod.Plugin!;
    }
}

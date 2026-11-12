#nullable enable

using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using KSP.Game;
using KSP.Game.Flow;
using SpaceWarp2.API.Features;
using SpaceWarp2.InternalUtilities;
using SpaceWarp2.API.Loading.LoadingActions;
using SpaceWarp2.API.Mods;
using SpaceWarp2.Patching.LoadingActions;
using UnityEngine;
using UnityObject = UnityEngine.Object;

#pragma warning disable CS0618 // Type or member is obsolete

namespace SpaceWarp2.API.Loading
{
    /// <summary>
    /// API for mods to register their actions for the loading of assets.
    /// </summary>
    [PublicAPI]
    public static class Loading
    {
        public static List<Func<SpaceWarpPluginDescriptor, DescriptorLoadingAction>> DescriptorLoadingActionGenerators =
            new();

        public static List<Func<FlowAction>> GeneralLoadingActions = new();

        /// <summary>
        /// Gets the addressables labels loaded through <see cref="AddSetLoadingAction{T}" />, whose assets are
        /// content the active feature set gates.
        /// </summary>
        internal static ISet<string> SetLabels { get; } = new HashSet<string>(StringComparer.Ordinal);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            DescriptorLoadingActionGenerators = new();
            GeneralLoadingActions = new();
            SetLabels.Clear();
        }

        /// <summary>
        /// Registers a loading action run once for each mod.
        /// </summary>
        /// <remarks>
        /// Register it in <c>OnPreInitialized</c> at the latest. The actions are built when boot reaches them.
        /// </remarks>
        /// <param name="name">The name of the action</param>
        /// <param name="action">The action</param>
        public static void AddDescriptorLoadingAction(string name, Action<SpaceWarpPluginDescriptor> action)
        {
            DescriptorLoadingActionGenerators.Add(p => new DescriptorLoadingAction(name, action, p));
        }

        /// <summary>
        /// Registers a general loading action.
        /// </summary>
        /// <remarks>
        /// Register it in <c>OnPreInitialized</c> at the latest. The actions are built when boot reaches them.
        /// </remarks>
        /// <param name="actionGenerator">The action generator</param>
        public static void AddGeneralLoadingAction(Func<FlowAction> actionGenerator)
        {
            GeneralLoadingActions.Add(actionGenerator);
        }

        /// <summary>
        /// Registers an action to be done on addressables after addressables have been loaded.
        /// </summary>
        /// <remarks>
        /// Register it in <c>OnPreInitialized</c> at the latest. The actions are built when boot reaches them.
        /// </remarks>
        /// <param name="name">The name of the action</param>
        /// <param name="label">The addressables label to hook into</param>
        /// <param name="action">The action to be done on each addressables asset</param>
        /// <typeparam name="T">The type of asset that this action is done upon</typeparam>
        public static void AddAddressablesLoadingAction<T>(string name, string label, Action<T> action)
            where T : UnityObject
        {
            AddGeneralLoadingAction(() => new AddressableAction<T>(name, label, action));
        }

        /// <summary>
        /// Registers an action to be done on addressables after addressables have been loaded, optionally keeping the
        /// assets in memory, as textures and UXML templates need.
        /// </summary>
        /// <remarks>
        /// Register it in <c>OnPreInitialized</c> at the latest. The actions are built when boot reaches them.
        /// </remarks>
        /// <param name="name">The name of the action</param>
        /// <param name="label">The addressables label to hook into</param>
        /// <param name="keepAssets">Indicates if assets should be kept in memory after loading them.</param>
        /// <param name="action">The action to be done on each addressables asset</param>
        /// <typeparam name="T">The type of asset that this action is done upon</typeparam>
        public static void AddAddressablesLoadingAction<T>(string name, string label, bool keepAssets, Action<T> action)
            where T : UnityObject
        {
            AddGeneralLoadingAction(() => new AddressableAction<T>(name, label, keepAssets, action));
        }

        /// <summary>
        /// Registers data a mod loads for the active feature set, cleared and loaded again whenever the set changes.
        /// </summary>
        /// <remarks>
        /// Data is loaded on boot and on every switch to another set, never on a reload into the same set, since it is
        /// the same for every save on one set. Every action clears before any loads, so no load reads another mod's
        /// half-cleared data, and nothing from the previous set survives in an action that no longer applies.
        /// </remarks>
        /// <param name="modId">The mod the data belongs to.</param>
        /// <param name="name">What is loaded, shown as "Clearing" and "Loading" it.</param>
        /// <param name="reset">Clears the data loaded for the previous set.</param>
        /// <param name="load">Loads the data for the active set, calling its argument once done.</param>
        /// <returns>The action, for chaining feature selectors.</returns>
        public static FeatureGatedAction AddSetAction(string modId, string name, Action reset, Action<Action> load) =>
            SaveLoad.AddSetAction(new FeatureGatedAction(modId, name, load, reset));

        /// <summary>
        /// Registers an addressables label a mod loads for the active feature set, so its patched and gated assets
        /// are read again whenever the set changes.
        /// </summary>
        /// <remarks>
        /// The label counts as content from then on, so a mod's untagged assets in it load only while that mod is on.
        /// </remarks>
        /// <typeparam name="T">The asset type.</typeparam>
        /// <param name="modId">The mod the data belongs to.</param>
        /// <param name="name">The name shown while it loads.</param>
        /// <param name="label">The addressables label.</param>
        /// <param name="onAsset">Called with each loaded asset.</param>
        /// <param name="reset">Clears what the previous set's assets registered.</param>
        /// <returns>The action, for chaining feature selectors.</returns>
        public static FeatureGatedAction AddSetLoadingAction<T>(
            string modId,
            string name,
            string label,
            Action<T> onAsset,
            Action reset
        ) where T : UnityObject
        {
            SetLabels.Add(label);
            return AddSetAction(
                modId,
                name,
                reset,
                done =>
                {
                    // No catalog may ship the label at all, which is not an error for data that is optional
                    if (!GameManager.Instance.Assets.HasLocations(label, typeof(T)))
                    {
                        done();
                        return;
                    }

                    GameManager.Instance.Assets.LoadByLabel<T>(
                    label,
                    onAsset,
                    assets =>
                    {
                        // A set can have nothing under the label, which comes back as no list at all
                        if (assets != null)
                        {
                            UnityEngine.AddressableAssets.Addressables.Release(assets);
                        }

                        done();
                    }
                );
                }
            );
        }
    }
}

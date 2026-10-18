#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using KSP.Game.Flow;
using SpaceWarp2.API.Features;
using SpaceWarp2.API.Mods;
using UnityEngine;

namespace SpaceWarp2.API.Loading
{
    /// <summary>
    /// The one registry of work mods do as feature sets activate and campaigns load, save and unload, and the runner
    /// that turns it into steps of the game's flows.
    /// </summary>
    /// <remarks>
    /// Every piece of work is a <see cref="FeatureGatedAction" />, registered under what triggers it. Each step
    /// decides when it runs whether its action applies, which is after the set it belongs to has activated.
    /// </remarks>
    [PublicAPI]
    public static class SaveLoad
    {
        private const string SET_TRIGGER = "set activated";

        // Every registered action, by what triggers it, in registration order
        private static readonly Dictionary<string, List<FeatureGatedAction>> Actions = new();

        // Teardown owed for the campaign loaded now, run latest first when it unloads
        private static readonly List<Action> OwedUnloads = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            // A play session that ended without unloading leaves nothing for the next one to tear down
            Actions.Clear();
            OwedUnloads.Clear();
        }

        /// <summary>
        /// Registers work a mod does every time a campaign it is enabled in loads, a quickload or revert into the
        /// same set included.
        /// </summary>
        /// <param name="modId">The mod the work belongs to.</param>
        /// <param name="name">The name shown while it runs.</param>
        /// <param name="phase">Where in loading it runs.</param>
        /// <param name="load">The work, calling its argument once done. Teardown for what it sets up is registered
        /// with <see cref="OnCampaignUnload" />.</param>
        /// <returns>The action, for chaining feature selectors.</returns>
        public static FeatureGatedAction AddCampaignLoadAction(
            string modId,
            string name,
            CampaignPhase phase,
            Action<Action> load
        ) => Register(TriggerOf(phase), new FeatureGatedAction(modId, name, load));

        /// <summary>
        /// Registers a step a mod adds straight after a named step of one of the game's own flows.
        /// </summary>
        /// <remarks>
        /// A step can follow another mod's step too, by its name. A step added to the campaign load or save flow
        /// runs on every load or save.
        /// </remarks>
        /// <param name="modId">The mod the step belongs to.</param>
        /// <param name="name">The step's name, which other steps can follow.</param>
        /// <param name="flow">The flow the step joins.</param>
        /// <param name="afterStep">The name of the step it follows.</param>
        /// <param name="work">The work, given a callback to call once done and one to call with an error, which
        /// stops the flow.</param>
        /// <returns>The action, for chaining feature selectors.</returns>
        public static FeatureGatedAction AddStepAfter(
            string modId,
            string name,
            StockFlow flow,
            string afterStep,
            Action<Action, Action<string>> work
        ) => Register(TriggerAfter(flow, afterStep), new FeatureGatedAction(modId, name, work));

        /// <summary>
        /// Registers teardown for something set up for the campaign loaded now, run when the campaign unloads.
        /// </summary>
        /// <remarks>
        /// Unloads run in the reverse order they were registered, on quitting to the main menu, before any other load
        /// replaces the campaign, and when a load fails part way.
        /// </remarks>
        /// <param name="unload">The teardown.</param>
        public static void OnCampaignUnload(Action unload)
        {
            OwedUnloads.Add(unload);
        }

        /// <summary>
        /// Runs every unload owed for the campaign loaded now, latest first, and forgets them.
        /// </summary>
        public static void UnloadCampaign()
        {
            // Taken out first, so an unload that triggers another unload finds nothing left to run twice
            var owed = new List<Action>(OwedUnloads);
            OwedUnloads.Clear();
            for (int i = owed.Count - 1; i >= 0; i--)
            {
                try
                {
                    owed[i]();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SpaceWarp] A campaign unload failed: {e}");
                }
            }
        }

        /// <summary>
        /// Lists the steps that run one phase of campaign load work.
        /// </summary>
        /// <param name="phase">The phase.</param>
        /// <returns>The steps, in registration order.</returns>
        public static List<GenericFlowAction> CampaignSteps(CampaignPhase phase) => StepsFor(TriggerOf(phase));

        /// <summary>
        /// Lists the steps that follow a named step of one of the game's own flows.
        /// </summary>
        /// <param name="flow">The flow.</param>
        /// <param name="stepName">The name of the step just added to it.</param>
        /// <returns>The steps, in registration order.</returns>
        public static List<GenericFlowAction> StepsAfter(StockFlow flow, string stepName) =>
            StepsFor(TriggerAfter(flow, stepName));

        /// <summary>
        /// Lists the steps that load mods' per-set data: every action's clearing first, then each load that applies.
        /// </summary>
        /// <returns>The steps, in order.</returns>
        public static List<GenericFlowAction> SetSteps() => SetSteps(ActionsFor(SET_TRIGGER));

        /// <summary>
        /// Lists the steps that load per-set data for given actions: every action's clearing first, then each load
        /// that applies.
        /// </summary>
        /// <param name="actions">The actions, in registration order.</param>
        /// <returns>The steps, in order.</returns>
        internal static List<GenericFlowAction> SetSteps(IReadOnlyList<FeatureGatedAction> actions)
        {
            var steps = new List<GenericFlowAction>();
            foreach (FeatureGatedAction action in actions)
            {
                steps.Add(new GenericFlowAction($"Clearing {action.Name}", (resolve, _) =>
                {
                    action.Reset?.Invoke();
                    resolve();
                }));
            }

            foreach (FeatureGatedAction action in actions)
            {
                steps.Add(StepFor(action, $"Loading {action.Name}"));
            }

            return steps;
        }

        /// <summary>
        /// Registers a plugin to hear when each campaign its mod is enabled in loads, and owes it the matching unload.
        /// </summary>
        /// <param name="modId">The plugin's mod.</param>
        /// <param name="name">The plugin's name.</param>
        /// <param name="plugin">The plugin.</param>
        internal static void AddCampaignLifecycle(string modId, string name, ISpaceWarpMod plugin) =>
            Register(TriggerOf(CampaignPhase.Loaded), LifecycleAction(modId, name, plugin));

        /// <summary>
        /// Builds the action that tells a plugin its campaign has loaded, and owes it the matching unload.
        /// </summary>
        /// <param name="modId">The plugin's mod.</param>
        /// <param name="name">The plugin's name.</param>
        /// <param name="plugin">The plugin.</param>
        /// <returns>The action.</returns>
        internal static FeatureGatedAction LifecycleAction(string modId, string name, ISpaceWarpMod plugin) =>
            new(modId, $"{name}: campaign state", done =>
            {
                plugin.OnCampaignLoaded();
                OnCampaignUnload(plugin.OnCampaignUnloading);
                done();
            });

        /// <summary>
        /// Registers an integration to hear when each campaign both its mods are enabled in loads.
        /// </summary>
        /// <param name="integration">The integration.</param>
        internal static void AddIntegrationLifecycle(ModIntegration integration) =>
            Register(TriggerOf(CampaignPhase.Loaded), IntegrationLifecycleAction(integration));

        /// <summary>
        /// Builds the action that tells an integration its campaign has loaded, and owes it the matching unload.
        /// </summary>
        /// <remarks>
        /// The action belongs to the integration's own mod, so the feature gate covers that one, and the integrated
        /// mod is checked when it runs.
        /// </remarks>
        /// <param name="integration">The integration.</param>
        /// <returns>The action.</returns>
        internal static FeatureGatedAction IntegrationLifecycleAction(ModIntegration integration) =>
            new(integration.CurrentMod.Guid,
                $"{integration.CurrentMod.Name}: campaign state for {integration.IntegratedMod.Name}", done =>
                {
                    if (integration.IsActive && integration.IntegratedMod.Enabled &&
                        integration.TryRun("campaign load", loaded => loaded.OnCampaignLoaded()))
                    {
                        OnCampaignUnload(() => integration.TryRun("campaign unload", unloading => unloading.OnCampaignUnloading()));
                    }

                    done();
                });

        /// <summary>
        /// Registers work a mod loads per feature set.
        /// </summary>
        /// <param name="action">The action, whose reset clears what an earlier set loaded.</param>
        /// <returns>The action.</returns>
        internal static FeatureGatedAction AddSetAction(FeatureGatedAction action) => Register(SET_TRIGGER, action);

        /// <summary>
        /// Builds the step that runs one action: nothing when it does not apply, and logging rather than stopping
        /// the flow when it throws.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <param name="name">The step's name, the action's own when null.</param>
        /// <returns>The step.</returns>
        internal static GenericFlowAction StepFor(FeatureGatedAction action, string? name = null) =>
            new(name ?? action.Name, (resolve, reject) =>
            {
                if (!action.Applies())
                {
                    resolve();
                    return;
                }

                try
                {
                    action.Run(resolve, reject);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SpaceWarp] {action.Name} of {action.ModId} failed: {e}");
                    resolve();
                }
            });

        private static FeatureGatedAction Register(string trigger, FeatureGatedAction action)
        {
            if (!Actions.TryGetValue(trigger, out List<FeatureGatedAction> actions))
            {
                Actions[trigger] = actions = new List<FeatureGatedAction>();
            }

            actions.Add(action);
            return action;
        }

        private static IReadOnlyList<FeatureGatedAction> ActionsFor(string trigger) =>
            Actions.TryGetValue(trigger, out List<FeatureGatedAction> actions)
                ? actions
                : Array.Empty<FeatureGatedAction>();

        private static List<GenericFlowAction> StepsFor(string trigger) =>
            ActionsFor(trigger).Select(action => StepFor(action)).ToList();

        private static string TriggerOf(CampaignPhase phase) => $"campaign {phase}";

        private static string TriggerAfter(StockFlow flow, string stepName) => $"{flow} after {stepName}";
    }

    /// <summary>
    /// Where in loading a campaign the work registered with <see cref="SaveLoad.AddCampaignLoadAction" /> runs.
    /// </summary>
    [PublicAPI]
    public enum CampaignPhase
    {
        /// <summary>
        /// Right after the campaign's feature set activates, before any of its content loads.
        /// </summary>
        Content,

        /// <summary>
        /// After the parts are read, before the universe is created.
        /// </summary>
        BeforeUniverse,

        /// <summary>
        /// Once the campaign has finished loading.
        /// </summary>
        Loaded
    }

    /// <summary>
    /// One of the game's own flows, which <see cref="SaveLoad.AddStepAfter" /> adds steps to.
    /// </summary>
    [PublicAPI]
    public enum StockFlow
    {
        /// <summary>
        /// Starting the game, up to the main menu.
        /// </summary>
        GameLoad,

        /// <summary>
        /// Loading a campaign.
        /// </summary>
        CampaignLoad,

        /// <summary>
        /// Saving a campaign.
        /// </summary>
        CampaignSave
    }
}

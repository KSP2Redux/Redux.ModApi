using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using SpaceWarp2.API.Backend.SaveGameManager;
using SpaceWarp2.API.Configuration;
using SpaceWarp2.API.Features;
using SpaceWarp2.API.Mods;

namespace SpaceWarp2.API.SaveGameManager
{
    /// <summary>
    /// The mod settings a campaign stores, kept with the rest of its campaign data, and the one place mods' campaign
    /// configs are tied to campaigns: registered with saves, loaded, saved, reset and filtered for the settings menu.
    /// </summary>
    [PublicAPI]
    public class CampaignConfigData
    {
        /// <summary>
        /// The id a campaign's mod settings are saved under.
        /// </summary>
        public const string SAVE_ID = "SpaceWarp.CampaignConfig";

        /// <summary>
        /// Each mod's campaign config values by section and key, by mod id. Mods not installed now keep theirs.
        /// </summary>
        public Dictionary<string, JObject> Mods = new();

        /// <summary>
        /// Ties every loaded mod's campaign config to campaigns, so each campaign loads, saves and starts its own.
        /// </summary>
        public static void Register()
        {
            Dictionary<string, JsonConfigFile> configs = PluginList.AllLoadedPlugins
                .ToDictionary(mod => mod.Guid, mod => mod.CampaignConfigFile);
            ModSaves.RegisterSaveLoadGameData<CampaignConfigData>(
                SAVE_ID,
                onSave: data => data.ReadFrom(configs),
                onLoad: data => data.ApplyTo(configs),
                onNew: _ => Reset(configs),
                persistenceKind: PersistenceKind.PerCampaign
            );
        }

        /// <summary>
        /// Checks whether the settings menu shows a campaign config entry in the active feature set.
        /// </summary>
        /// <param name="modId">The mod whose campaign config holds the entry.</param>
        /// <param name="entry">The entry.</param>
        /// <returns>True if the mod is enabled and every feature the entry's tags select is on.</returns>
        public static bool Shows(string modId, IConfigEntry entry) =>
            FeatureSelector.FromConfigTags(modId, entry.Tags).Allows(modId, ActiveFeatures.EnabledSet);

        /// <summary>
        /// Starts a new campaign's mod settings from every mod's defaults.
        /// </summary>
        /// <param name="configs">Each mod's campaign config, by mod id.</param>
        /// <returns>The new campaign's settings, holding none of the previous campaign's.</returns>
        public static CampaignConfigData Reset(IReadOnlyDictionary<string, JsonConfigFile> configs)
        {
            var reset = new CampaignConfigData();
            reset.ApplyTo(configs);
            return reset;
        }

        /// <summary>
        /// Applies these settings to each mod's campaign config. A mod they hold none for gets its defaults.
        /// </summary>
        /// <param name="configs">Each mod's campaign config, by mod id.</param>
        public void ApplyTo(IReadOnlyDictionary<string, JsonConfigFile> configs)
        {
            foreach (KeyValuePair<string, JsonConfigFile> config in configs)
            {
                config.Value.Load(Mods.GetValueOrDefault(config.Key));
            }
        }

        /// <summary>
        /// Stores each mod's campaign config into these settings, keeping those of mods not installed now.
        /// </summary>
        /// <param name="configs">Each mod's campaign config, by mod id.</param>
        public void ReadFrom(IReadOnlyDictionary<string, JsonConfigFile> configs)
        {
            foreach (KeyValuePair<string, JsonConfigFile> config in configs)
            {
                Mods[config.Key] = config.Value.ToJObject();
            }
        }
    }
}

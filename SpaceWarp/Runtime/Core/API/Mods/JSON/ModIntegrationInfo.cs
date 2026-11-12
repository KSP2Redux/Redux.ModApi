using JetBrains.Annotations;
using Newtonsoft.Json;

namespace SpaceWarp2.API.Mods.JSON
{
    /// <summary>
    /// One entry of the <c>integrations</c> section of a mod info file: an assembly loaded only when another mod is.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    [PublicAPI]
    public class ModIntegrationInfo
    {
        /// <summary>
        /// The id of the mod the integration integrates with.
        /// </summary>
        [JsonProperty("mod_id", Required = Required.Always)]
        public string ModID { get; internal set; } = "";

        /// <summary>
        /// The integration's assembly, relative to the mod's folder.
        /// </summary>
        [JsonProperty("assembly", Required = Required.Always)]
        public string Assembly { get; internal set; } = "";
    }
}

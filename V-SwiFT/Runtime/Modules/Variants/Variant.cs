using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;
using VSwift.Modules.Transformers;

namespace VSwift.Modules.Variants
{
    /// <summary>
    /// A single variant within a <see cref="VariantSet" />, applying its <see cref="Transformers" /> when active.
    /// </summary>
    [Serializable]
    [UsedImplicitly]
    public class Variant
    {
        /// <summary>
        /// The variant's identifier. Also used as the localization-key fallback.
        /// </summary>
        [Tooltip("Identifier for this variant. Must be unique within the variant set.")]
        [UsedImplicitly]
        public string VariantId = "";

        /// <summary>
        /// The variant's localization key. Defaults to <c>VariantId</c> when empty.
        /// </summary>
        [Tooltip("Localization key for the variant's display name. Falls back to ID when empty.")]
        [UsedImplicitly]
        public string VariantLocalizationKey = ""; // If null or empty, defaults to the variant ID

        /// <summary>
        /// Gets the key the variant's name is localized under: its localization key, or its ID when it has none.
        /// </summary>
        [JsonIgnore]
        public string DisplayNameKey => string.IsNullOrEmpty(VariantLocalizationKey) ? VariantId : VariantLocalizationKey;

        /// <summary>
        /// The transformers applied to the part when this variant is active.
        /// </summary>
        [SerializeReference]
        [UsedImplicitly]
        public List<ITransformer> Transformers = new() { };

        // TODO: Allow for custom unlock conditions
    }
}

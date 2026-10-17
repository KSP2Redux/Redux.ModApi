using System;
using System.Collections.Generic;
using System.Linq;
using Redux;
using KSP.Game;
using KSP.IO;
using KSP.Sim;
using KSP.Sim.Definitions;
using Newtonsoft.Json.Linq;
using VSwift.Extensions;
using VSwift.Modules.Extensions;
using VSwift.Modules.Data;
using VSwift.Modules.InformationLoaders;
using VSwift.Modules.Transformers;
using VSwift.Modules.Variants;

namespace VSwift.Patches
{
    /// <summary>
    /// Reconstructs a part's <see cref="PartCore" /> with the active variant's transformer outputs applied, used when a saved part is loaded.
    /// </summary>
    public static class LoadVariantPartData
    {
        private static int Compare(string a, string b) =>
            int.TryParse(a, out var aInt) && int.TryParse(b, out var bInt)
                ? aInt.CompareTo(bInt)
                : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Returns the part-core data for the saved part with its stored transformer overrides applied, or the original part-core when no overrides exist.
        /// </summary>
        /// <param name="originalPartCore">The base part-core to start from.</param>
        /// <param name="part">The serialized part carrying the variant overrides.</param>
        /// <returns>The transformed part-core, cached against <see cref="GameManager" />.<c>Game.Parts</c> by <c>partName+variantName</c>.</returns>
        internal static PartCore GetVariantPartData(PartCore originalPartCore, SerializedPart part)
        {
            if (part.GetPartSwitchOverride() is not { } data)
                return originalPartCore;
            // Try to get the original variant name
            var variantName = part.GetCurrentVariantNameString();
            if (!variantName.IsNullOrEmpty() &&
                GameManager.Instance.Game.Parts.TryGet($"{part.partName}+{variantName}", out var cached))
            {
                return cached;
            }

            var newData = originalPartCore.JsonClone().data;
            LoadOverridesInto(newData, data);

            // GUIUtility.systemCopyBuffer = IOProvider.ToJson(newData);
            var result = new PartCore
            {
                version = PartCore.PART_SERIALIZATION_VERSION, // Todo replace this with reflection
                data = newData
            };

            if (variantName.IsNullOrEmpty())
                return result;

            GameManager.Instance.Game.Parts.PartData[$"{part.partName}+{variantName}"] = result;
            GameManager.Instance.Game.Parts.PartJson[$"{part.partName}+{variantName}"] = IOProvider.ToJson(result);
            return result;
        }

        /// <summary>
        /// Returns a copy of a part as the assembly building builds it with one variant active in its set and every
        /// other set on its default variant.
        /// </summary>
        /// <remarks>
        /// The copy is not cached. Its mass includes the active variants' <see cref="MassModifier" /> values, which a
        /// placed part carries on its part-switch data instead.
        /// </remarks>
        /// <param name="originalPartCore">The part to copy.</param>
        /// <param name="variantSetId">The ID of the variant set to change, or <c>null</c> to leave every set on its default.</param>
        /// <param name="variantId">The ID of the variant to make active in that set.</param>
        /// <returns>The copy, or <paramref name="originalPartCore" /> when the part has no part-switch data.</returns>
        public static PartCore GetVariantPreviewPartData(
            PartCore originalPartCore,
            string? variantSetId = null,
            string? variantId = null)
        {
            PartCore preview = originalPartCore.JsonClone();
            PartData newData = preview.data;
            Data_PartSwitch? partSwitch = newData.serializedPartModules
                .Where(module => module.ModuleData != null)
                .SelectMany(module => module.ModuleData)
                .Select(data => data.DataObject)
                .OfType<Data_PartSwitch>()
                .FirstOrDefault();
            if (partSwitch == null)
                return originalPartCore;

            partSwitch.ActiveVariants = new List<string>();
            for (int i = 0; i < partSwitch.VariantSets.Count; i++)
            {
                VariantSet variantSet = partSwitch.VariantSets[i];
                Variant? chosen = variantSet.VariantSetId == variantSetId
                    ? variantSet.Variants.FirstOrDefault(variant => variant.VariantId == variantId)
                    : null;
                string activeVariantId = chosen?.VariantId ?? partSwitch.GetDefaultVariant(i);
                partSwitch.ActiveVariants.Add(activeVariantId);
                newData.mass += variantSet.Variants
                    .First(variant => variant.VariantId == activeVariantId)
                    .Transformers
                    .OfType<MassModifier>()
                    .Sum(modifier => modifier.Modifier);
            }

            LoadOverridesInto(newData, partSwitch.GetStoredVariantInformation()!);
            return preview;
        }

        // Loaders run in variant set order, then transformer order within each set
        private static void LoadOverridesInto(
            PartData partData,
            Dictionary<string, Dictionary<string, (string savedType, JToken savedValue)>> overrides)
        {
            List<(string, List<(string, IInformationLoader, JToken)>)> toBeDoublySorted = new() { };
            foreach (var (idx, variant) in overrides)
            {
                List<(string, IInformationLoader, JToken)> toBeSorted = new() { };
                foreach (var (key, (type, obj)) in variant)
                {
                    var t = Type.GetType(type);
                    if (t == null)
                        continue;

                    var instance = Activator.CreateInstance(t) as IInformationLoader;
                    toBeSorted.Add((key, instance, obj));
                }

                toBeSorted.Sort((a, b) => Compare(a.Item1, b.Item1));
                toBeDoublySorted.Add((idx, toBeSorted));
            }

            toBeDoublySorted.Sort((a, b) => Compare(a.Item1, b.Item1));
            foreach (var (_, variant) in toBeDoublySorted)
            {
                foreach (var (_, loader, d) in variant)
                {
                    loader.LoadInformationInto(partData, d);
                }
            }
        }
    }
}

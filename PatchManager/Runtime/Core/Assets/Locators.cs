using System.Collections.Generic;
using KSP.Game;
using UnityEngine;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace PatchManager.Core.Assets
{
    /// <summary>
    /// Patch Manager's door to the game's override locators, the ones whose patched assets replace the catalogs'.
    /// </summary>
    /// <remarks>
    /// The locators live on the game's <see cref="KSP.Assets.AssetProvider" />, which is created for each play
    /// session, so registrations do not outlive the session that made them.
    /// </remarks>
    public static class Locators
    {
        /// <summary>
        /// Registers an override locator, whose locations replace every other source's for the keys it knows.
        /// </summary>
        /// <param name="locator">Locator to register.</param>
        public static void Register(IResourceLocator locator)
        {
            GameManager.Instance.Assets.RegisterOverrideLocator(locator);
        }

        /// <summary>
        /// Locates patched text assets by label.
        /// </summary>
        /// <param name="label">Label of the assets to be located.</param>
        /// <param name="locations">List of locations of the found assets.</param>
        /// <returns>True if any assets were found, false otherwise.</returns>
        public static bool LocateAll(object label, out List<IResourceLocation> locations)
        {
            return LocateAll(label, typeof(TextAsset), out locations);
        }

        /// <summary>
        /// Locates assets by key and requested type across the override locators.
        /// </summary>
        /// <param name="key">The key or label.</param>
        /// <param name="type">The asset type.</param>
        /// <param name="locations">The override locations, empty when none knows the key.</param>
        /// <returns>True if any assets were found, false otherwise.</returns>
        public static bool LocateAll(object key, System.Type type, out List<IResourceLocation> locations)
        {
            if (GameManager.Instance?.Assets == null)
            {
                locations = new List<IResourceLocation>();
                return false;
            }

            return GameManager.Instance.Assets.LocateOverrides(key, type, out locations);
        }
    }
}

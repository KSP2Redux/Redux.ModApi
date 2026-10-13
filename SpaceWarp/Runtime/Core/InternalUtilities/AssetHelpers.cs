#nullable enable

using System.IO;
using SpaceWarp2.API.Logging;
using SpaceWarp2.API.Mods;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.AddressableAssets.ResourceLocators;
using ILocalizer = UitkForKsp2.API.ILocalizer;

namespace SpaceWarp2.InternalUtilities
{
    internal static class AssetHelpers
    {
        // Fetched per use, since Redux's catalog can load before the game installs its log provider
        private static ILogger CatalogLogger => LogManager.GetLogger("Addressables Loader");

        /// <summary>
        /// Starts loading a mod's addressables catalog, and records its locator on the mod's descriptor once it loads,
        /// which is how the feature gate tells whose assets are whose.
        /// </summary>
        /// <param name="catalogPath">The catalog file.</param>
        /// <param name="modId">The mod the catalog belongs to.</param>
        /// <returns>The catalog load, for the caller to wait on or yield.</returns>
        internal static AsyncOperationHandle<IResourceLocator> LoadModCatalog(string catalogPath, string modId)
        {
            CatalogLogger.LogInfo($"Loading addressables catalog {catalogPath}");
            AsyncOperationHandle<IResourceLocator> operation = Addressables.LoadContentCatalogAsync(catalogPath);
            operation.Completed += loaded =>
            {
                if (loaded.Status != AsyncOperationStatus.Succeeded || loaded.Result == null)
                {
                    CatalogLogger.LogError($"Failed to load addressables catalog {catalogPath}");
                    return;
                }

                CatalogLogger.LogInfo($"Loaded addressables catalog {catalogPath} as {loaded.Result.LocatorId}");
                SpaceWarpPluginDescriptor descriptor = PluginList.TryGetDescriptor(modId);
                if (descriptor != null && !descriptor.AddressableResourceLocators.Contains(loaded.Result))
                {
                    descriptor.AddressableResourceLocators.Add(loaded.Result);
                }
            };
            return operation;
        }

        internal static void LoadLocalizationFromFolder(string folder)
        {
            SpaceWarpPlugin.Instance.SWLogger.LogInfo($"Attempting to load localizations from {folder}");
            if (!Directory.Exists(folder))
            {
                SpaceWarpPlugin.Instance.SWLogger.LogInfo($"{folder} does not exist, not loading localizations.");
                return;
            }

            int loadedCount = 0;

            var info = new DirectoryInfo(folder);
            foreach (var csvFile in info.GetFiles("*.csv", SearchOption.AllDirectories))
            {
                ILocalizer.Instance.AddCsvSource(File.ReadAllText(csvFile.FullName));
                loadedCount++;
            }

            foreach (var i2CsvFile in info.GetFiles("*.i2csv", SearchOption.AllDirectories))
            {
                ILocalizer.Instance.AddI2CsvSource(File.ReadAllText(i2CsvFile.FullName));
                loadedCount++;
            }

            SpaceWarpPlugin.Instance.SWLogger.LogInfo(
                loadedCount > 0 ? $"Loaded localizations from {folder}" : $"No localizations found in {folder}"
            );
        }
    }
}

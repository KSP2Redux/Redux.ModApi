using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Redux.QA;
using SpaceWarp2.API.Mods;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.UIElements;

namespace DebugTools.Runtime.Controllers
{
    /// <summary>
    /// On-demand catalog resolution and dependency inspection without asset loads.
    /// </summary>
    public class AddressablesDoctorWindowController : BaseWindowController
    {
        private TextField _key;
        private Label _output;
        private Label _status;
        private VisualElement _matches;
        private bool _isTrying;
        private bool _isBound;
        private string _snapshotKey;

        private void OnEnable()
        {
            if (_isBound)
                return;
            _isBound = true;
            _key = RootElement.Q<TextField>("key");
            _output = RootElement.Q<Label>("output");
            _status = RootElement.Q<Label>("status");
            _matches = RootElement.Q<VisualElement>("matches");
            RootElement.Q<Button>("find").clicked += Find;
            RootElement.Q<Button>("inspect").clicked += Inspect;
            RootElement.Q<Button>("try-load").clicked += TryLoad;
            RootElement.Q<Button>("capture").clicked += Capture;
            _status.text = "Enter an address or label, then find or inspect it.";
        }

        private IEnumerable<UnityEngine.AddressableAssets.ResourceLocators.IResourceLocator> Locators()
        {
            var seen = new HashSet<UnityEngine.AddressableAssets.ResourceLocators.IResourceLocator>();
            if (Assets != null)
            {
                foreach (var locator in Assets.OverrideResourceLocators.Concat(Assets.RegisteredResourceLocators))
                {
                    if (seen.Add(locator))
                        yield return locator;
                }
            }
            foreach (var locator in Addressables.ResourceLocators)
            {
                if (seen.Add(locator))
                    yield return locator;
            }
        }

        private void Find()
        {
            _matches.Clear();
            string search = _key.value.Trim();
            var keys = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var locator in Locators())
            {
                foreach (object key in locator.Keys)
                {
                    if (key is string text && text.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                        keys.Add(text);
                }
            }
            foreach (string key in keys.Take(100))
            {
                var button = new Button(() => { _key.value = key; Inspect(); }) { text = key };
                button.AddToClassList("kui-button");
                _matches.Add(button);
            }
            _matches.EnableInClassList("doctor-hidden", _matches.childCount == 0);
            _status.text = FormattableString.Invariant($"{keys.Count} matching addresses or labels. Showing up to 100. Refine the text to find more.");
        }

        private void Inspect()
        {
            try
            {
                string key = _key.value.Trim();
                if (string.IsNullOrEmpty(key))
                {
                    RootElement.Q("results").AddToClassList("doctor-hidden");
                    _status.text = "Enter an address or label first.";
                    return;
                }
                var text = new StringBuilder();
                text.AppendLine($"Requested: {key}\nType for winner and Try load: UnityEngine.Object\nGame resolution: overrides, registered locators, feature gate, then Addressables catalog order.\nLabels can return multiple assets. Type-specific callers can resolve differently.\n");
                IResourceLocation gameWinner = null;
                bool external = Assets != null && Assets.LocateAssetInExternalData(key, typeof(UnityEngine.Object), out gameWinner);
                text.AppendLine(external ? gameWinner == null ? "GAME RESULT: gated off" : "GAME WINNER: external override, registered locator or feature gate" : "GAME RESULT: falls back to Addressables");
                var loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (AssetBundle bundle in AssetBundle.GetAllLoadedAssetBundles())
                {
                    if (bundle != null)
                        loaded.Add(bundle.name);
                }
                bool hasWinner = false;
                int index = 0;
                foreach (var locator in Locators())
                {
                    index++;
                    if (!locator.Locate(key, null, out var locations))
                        continue;
                    string owner = string.Join(", ", PluginList.AllLoadedPlugins
                        .Where(mod => mod.AddressableResourceLocators.Contains(locator)).Select(mod => mod.Guid));
                    if (string.IsNullOrEmpty(owner))
                        owner = "game catalog or runtime locator (no mod descriptor owner)";
                    var catalog = Addressables.GetLocatorInfo(locator)?.CatalogLocation;
                    string catalogSource = catalog == null ? "not retained for this locator" : Addressables.ResourceManager.TransformInternalId(catalog);
                    string kind = Assets != null && Assets.OverrideResourceLocators.Contains(locator) ? "override" :
                        Assets != null && Assets.RegisteredResourceLocators.Contains(locator) ? "registered external" : "catalog";
                    text.AppendLine(FormattableString.Invariant($"Source #{index} ({kind}): {locator.LocatorId}\nCatalog file: {catalogSource}\nOwner: {owner}"));
                    foreach (IResourceLocation location in locations)
                    {
                        locator.Locate(key, typeof(UnityEngine.Object), out var typedLocations);
                        bool provider = typedLocations != null && typedLocations.Contains(location) &&
                            Addressables.ResourceManager.GetResourceProvider(typeof(UnityEngine.Object), location) != null;
                        bool winner = external ? ReferenceEquals(gameWinner, location) : !hasWinner && provider;
                        hasWinner |= winner;
                        text.AppendLine(winner ? "GAME WINNER" : provider ? "Later match" : "No compatible provider");
                        AppendLocation(text, location, loaded, new HashSet<IResourceLocation>(), 0);
                    }
                }
                if (!hasWinner)
                    text.AppendLine("No loadable game winner in the currently loaded catalogs (or the key is gated off).");
                text.AppendLine("\nRECENT WRAPPER FAILURES (last 128, oldest first)");
                foreach (var failure in ModDiagnostics.GetLoadFailures())
                    text.AppendLine($"Key: {failure.key}\n{failure.exception}\n");
                _output.text = text.ToString();
                RootElement.Q("results").RemoveFromClassList("doctor-hidden");
                _snapshotKey = key;
                _status.text = "Snapshot only. Refresh after catalog or bundle state changes.";
            }
            catch (Exception exception)
            {
                _status.text = exception.Message;
            }
        }

        private static void AppendLocation(StringBuilder text, IResourceLocation location, HashSet<string> loaded,
            HashSet<IResourceLocation> path, int depth)
        {
            string indent = new(' ', Math.Min(depth, 32) * 2);
            if (!path.Add(location))
            {
                text.AppendLine($"{indent}[cycle] {location.PrimaryKey}");
                return;
            }
            string resolved = Addressables.ResourceManager.TransformInternalId(location);
            string state = "not a bundle";
            if (location.Data is AssetBundleRequestOptions options)
                state = $"bundle {options.BundleName}: {(loaded.Contains(options.BundleName) ? "loaded" : "not loaded")}";
            text.AppendLine($"{indent}{location.PrimaryKey} [{state}]\n{indent}Provider: {location.ProviderId}\n{indent}Source: {resolved}");
            if (depth >= 32)
                text.AppendLine($"{indent}Depth limit reached.");
            else if (location.HasDependencies)
            {
                foreach (IResourceLocation dependency in location.Dependencies)
                    AppendLocation(text, dependency, loaded, path, depth + 1);
            }
            path.Remove(location);
        }

        private void TryLoad()
        {
            if (_isTrying || string.IsNullOrWhiteSpace(_key.value))
                return;
            string key = _key.value.Trim();
            _isTrying = true;
            _status.text = "Loading one UnityEngine.Object. The handle will be released on completion.";
            try
            {
                var handle = Assets.LoadAssetAsync<UnityEngine.Object>(key);
                handle.Completed += completed =>
                {
                    string result;
                    try
                    {
                        result = completed.Status == AsyncOperationStatus.Succeeded ? "Try load succeeded. Released." : $"Try load failed: {completed.OperationException}";
                    }
                    finally
                    {
                        Addressables.Release(completed);
                        _isTrying = false;
                    }
                    if (this != null)
                        _status.text = result;
                };
            }
            catch (Exception exception)
            {
                _isTrying = false;
                ModDiagnostics.RecordLoadFailure(key, exception);
                _status.text = exception.Message;
            }
        }

        private void Capture()
        {
            try
            {
                var report = new QaReport("Addressables doctor", "bench-run");
                var result = new QaReportResult(string.IsNullOrWhiteSpace(_snapshotKey) ? "catalogs" : _snapshotKey, _snapshotKey ?? "Catalogs", "addressables-doctor", "snapshot",
                    QaVerdict.NeedsReview, _output.text);
                report.Add(result);
                _status.text = $"Captured: {report.Write()}";
            }
            catch (Exception exception)
            {
                _status.text = exception.Message;
            }
        }
    }
}

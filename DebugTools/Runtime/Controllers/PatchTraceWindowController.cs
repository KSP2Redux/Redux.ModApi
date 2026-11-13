using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using PatchManager.Core.Assets;
using PatchManager.Core.Cache;
using UitkForKsp2.Controls;
using UnityEngine.UIElements;

namespace DebugTools.Runtime.Controllers
{
    /// <summary>
    /// The lazy UITK controller for selecting a patch trace and reading its detached load-time evidence.
    /// </summary>
    public class PatchTraceWindowController : BaseWindowController
    {
        private TextField _label;
        private TextField _asset;
        private TextField _path;
        private TextField _search;
        private TextField _output;
        private VisualElement _matches;

        private void OnEnable()
        {
            _label = RootElement.Q<TextField>("label");
            _asset = RootElement.Q<TextField>("asset");
            _path = RootElement.Q<TextField>("path");
            _search = RootElement.Q<TextField>("search");
            _output = RootElement.Q<TextField>("output");
            _matches = RootElement.Q<VisualElement>("matches");
            RootElement.Q<Button>("find").clicked += Find;
            RootElement.Q<Button>("arm").clicked += Arm;
            RootElement.Q<Button>("refresh").clicked += Refresh;
            RootElement.Q<Button>("clear").clicked += Clear;
            Refresh();
        }

        private void Arm()
        {
            try
            {
                PatchingManager.RequestTrace(_label.value.Trim(), _asset.value.Trim(), _path.value.Trim());
                Refresh();
            }
            catch (Exception exception)
            {
                _output.value = exception.Message;
            }
        }

        private void Refresh() => _output.value = PatchingManager.TraceResult?.ToString() ??
            "No capture. Select a target and field, then arm before loading a campaign.";

        private void Clear()
        {
            PatchingManager.ClearTrace();
            Refresh();
        }

        private void Find()
        {
            _matches.Clear();
            string search = _search.value.Trim();
            var targets = new HashSet<(string label, string name)>();
            var universe = PatchingManager.Universe;
            if (universe != null)
            {
                foreach (var group in universe.AllPatches)
                {
                    foreach (var patch in group.Value)
                    {
                        foreach (string name in patch.Names)
                        {
                            if (name.IndexOfAny(new[] { '*', '?' }) < 0)
                                targets.Add((group.Key, name));
                        }
                    }
                }
                foreach (var group in universe.Summary.Summaries)
                {
                    foreach (var asset in group.assets)
                    {
                        targets.Add((group.labelName, asset.AssetName));
                    }
                }
            }
            if (CacheManager.ActiveSet != null)
            {
                foreach (var group in CacheManager.Inventory.CacheEntries)
                {
                    if (universe == null || !universe.PatchedLabels.Contains(group.Key))
                        continue;
                    foreach (string name in group.Value.Assets)
                    {
                        targets.Add((group.Key, name));
                    }
                }
            }
            var matches = targets.Where(t => ($"{t.label}/{t.name}").IndexOf(search,
                StringComparison.OrdinalIgnoreCase) >= 0).OrderBy(t => t.label).ThenBy(t => t.name).Take(100);
            foreach (var target in matches)
            {
                var button = new OabButton { text = $"{target.label} / {target.name}" };
                button.AddToClassList("kui-button");
                button.clicked += () =>
                {
                    _label.value = target.label;
                    _asset.value = target.name;
                };
                _matches.Add(button);
            }
            _output.value = "Up to 100 matches from registered exact targets and the current cache. " +
                "Enter an ID and label manually for an uncached wildcard target.";
        }
    }
}

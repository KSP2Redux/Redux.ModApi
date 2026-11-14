using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KSP.Game;
using KSP.IO;
using SpaceWarp2.API.Features;
using SpaceWarp2.API.Mods;
using Redux.QA;
using UnityEngine.UIElements;

namespace DebugTools.Runtime.Controllers
{
    /// <summary>
    /// Tests campaign feature sets through the normal in-memory campaign reload flow.
    /// </summary>
    public class CampaignFeaturesWindowController : BaseWindowController
    {
        private const string OVERRIDE_ID = "campaign:features";
        private readonly HashSet<string> _requested = new(StringComparer.OrdinalIgnoreCase);
        private List<string> _original;
        private byte[] _originalBuffer;
        private string _campaign;
        private Label _status;
        private VisualElement _list;
        private bool _busy;
        private bool _wasOpen;
        private bool _revertRequested;

        private void OnEnable()
        {
            _status = RootElement.Q<Label>("status");
            _list = RootElement.Q<VisualElement>("features");
            RootElement.Q<Button>("refresh").clicked += Refresh;
            RootElement.Q<Button>("apply").clicked += Apply;
            RootElement.Q<Button>("revert").clicked += Revert;
            RootElement.Q<Button>("capture").clicked += Capture;
            Refresh();
        }

        private bool CanEdit => Game.SaveLoadManager.IsLoaded &&
            Game.GlobalGameState.GetState() is not (GameState.MainMenu or GameState.Loading) &&
            Game.SessionManager.IsSPCampaign() && !Game.SessionManager.IsReadOnlyCampaign() &&
            !string.IsNullOrEmpty(Game.SessionManager.ActiveCampaignName) &&
            !string.Equals(Game.SessionManager.ActiveCampaignName, "Testing", StringComparison.OrdinalIgnoreCase);

        private void Refresh()
        {
            if (_busy)
                return;
            _requested.Clear();
            _requested.UnionWith(Game.SessionManager.RequestedFeatures);
            Draw();
            _status.text = CanEdit ? "Changes are staged. Apply reloads the current state from memory and runs feature patch selection. Revert also reloads. No save file is rewritten." :
                "Load a campaign other than Testing. The Testing campaign cannot be changed here.";
        }

        private void Draw()
        {
            _list.Clear();
            FeatureResolution resolution = FeatureRegistry.Instance.Resolve(_requested);
            foreach (FeatureDefinition feature in FeatureRegistry.Instance.All.OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase))
            {
                var row = new VisualElement();
                row.AddToClassList("feature-row");
                var toggle = new Toggle(feature.Id);
                toggle.AddToClassList("kui-field");
                toggle.SetValueWithoutNotify(_requested.Contains(feature.Id));
                toggle.SetEnabled(CanEdit && !_busy && !ModIds.AlwaysOn.Contains(feature.ModId));
                toggle.RegisterValueChangedCallback(change =>
                {
                    if (change.newValue)
                    {
                        _requested.Add(feature.Id);
                    }
                    else
                    {
                        _requested.Remove(feature.Id);
                    }
                    Draw();
                });
                row.Add(toggle);
                var detail = new Label($"Source mod: {feature.ModId} | Loaded: {(ActiveFeatures.IsEnabled(feature.Id) ? "On" : "Off")} | After reload: {(resolution.Enabled.Contains(feature.Id, StringComparer.OrdinalIgnoreCase) ? "On" : "Off")}");
                detail.AddToClassList("qa-preview-wrap");
                row.Add(detail);
                _list.Add(row);
            }
        }

        private void Apply()
        {
            if (_busy || !CanEdit)
                return;
            FeatureResolution resolution = FeatureRegistry.Instance.Resolve(_requested);
            if (resolution.Missing.Count > 0 || resolution.Conflicts.Count > 0)
            {
                _status.text = "Resolve missing requirements or conflicting features before reloading.";
                return;
            }
            _original ??= Game.SessionManager.RequestedFeatures.ToList();
            _campaign = Game.SessionManager.ActiveCampaignName;
            Reload(_requested.ToList(), false);
        }

        private void Capture()
        {
            var report = new QaReport("Campaign features", "bench-run");
            foreach (FeatureDefinition feature in FeatureRegistry.Instance.All)
            {
                report.Add(new QaReportResult(feature.Id, feature.Id, "campaign-features", "capture", QaVerdict.NeedsReview,
                    $"Source mod: {feature.ModId}. Loaded: {ActiveFeatures.IsEnabled(feature.Id)}. Requested: {_requested.Contains(feature.Id)}."));
            }
            _status.text = $"Captured: {report.Write()}";
        }

        private void Revert()
        {
            if (_busy)
            {
                _revertRequested = true;
                return;
            }
            if (_original == null)
                return;
            if (Game.GlobalGameState.GetState() == GameState.MainMenu || Game.SessionManager.ActiveCampaignName != _campaign)
            {
                _original = null;
                _originalBuffer = null;
                DebugOverrides.Clear(OVERRIDE_ID);
                return;
            }
            if (_originalBuffer == null)
            {
                _original = null;
                DebugOverrides.Clear(OVERRIDE_ID);
                return;
            }
            _revertRequested = false;
            _busy = true;
            _status.text = "Restoring the in-memory snapshot from before the first feature change...";
            StartCoroutine(LoadNextFrame(_originalBuffer, true));
        }

        private void Reload(List<string> features, bool reverting)
        {
            _revertRequested = false;
            _busy = true;
            _status.text = "Capturing current state in memory for feature reload...";
            DebugOverrides.Report(OVERRIDE_ID, "Campaign features (reload required to revert)", Revert);
            if (!Game.SaveLoadManager.SaveGameToMemory(SaveJsonFormatting.None, (ticket, success) =>
            {
                if (!success)
                {
                    _busy = false;
                    _status.text = "Memory capture failed. Revert remains available.";
                    return;
                }
                try
                {
                    // Keep the original content as well as the feature set for rollback after content removal.
                    _originalBuffer ??= ticket.NewJsonBuffer;
                    ticket.LoadGameData.SavedGame.Metadata.Features = features;
                    IOProvider.ToBuffer(ticket.LoadGameData.SavedGame, out byte[] buffer,
                        IOProvider.GetDontSerializeKspDefinitionSerializerSettings());
                    StartCoroutine(LoadNextFrame(buffer, reverting));
                }
                catch (Exception exception)
                {
                    _busy = false;
                    _status.text = $"Memory serialization failed: {exception.Message}. Revert remains available.";
                }
            }))
            {
                _busy = false;
                _status.text = "Memory capture could not start. Revert remains available.";
            }
        }

        private IEnumerator LoadNextFrame(byte[] buffer, bool reverting)
        {
            yield return null;
            if (!Game.SaveLoadManager.LoadGameFromBuffer(buffer, (_, success) =>
            {
                _busy = false;
                if (success && reverting)
                {
                    _original = null;
                    _originalBuffer = null;
                    DebugOverrides.Clear(OVERRIDE_ID);
                }
                _status.text = success ? "Feature reload completed. Loaded values now reflect the re-patched campaign." :
                    "Feature reload failed. The override remains visible. Reload an ordinary campaign save to recover.";
                Draw();
                if (_revertRequested && _original != null && success && !reverting)
                    Revert();
            }))
            {
                _busy = false;
                _status.text = "Reload could not start. Revert remains available.";
            }
        }

        private void LateUpdate()
        {
            // Closing the tool or the hub keeps the override. Only Revert, here or in the status strip, reloads.
            if (!IsRequestedOpen)
            {
                _wasOpen = false;
                return;
            }
            if (!_wasOpen && !_busy)
                Refresh();
            _wasOpen = true;
        }
    }
}

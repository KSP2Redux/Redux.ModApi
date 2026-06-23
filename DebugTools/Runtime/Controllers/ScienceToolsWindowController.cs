using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace DebugTools.Runtime.Controllers
{
    public class ScienceToolsWindowController : BaseWindowController
    {
        private Label? _agencyName;
        private Label? _scienceState;

        private Toggle? _unlockAllParts;

        private TextField? _addSciencePointsField;

        private DropdownField? _availableNodes;

        private ScrollView? _loadedNodes;
        private ScrollView? _unlockedNodes;

        private const float UpdateSeconds = 2f;
        private float _lastUpdate = -1f;

        private void OnEnable()
        {
            Enable();

            _agencyName = RootElement.Q<Label>("agency-name");
            _scienceState = RootElement.Q<Label>("science-state");

            _unlockAllParts = RootElement.Q<Toggle>("unlock-all-parts");
            _unlockAllParts.value = Game.CheatSystem.Get(CheatSystemItemID.UnlockAllParts);
            _unlockAllParts.RegisterValueChangedCallback(UnlockAllPartsChanged);

            _addSciencePointsField = RootElement.Q<TextField>("add-points-field");
            var addPoints = RootElement.Q<Button>("add-points");
            addPoints.clicked += AddSciencePoints;

            _availableNodes = RootElement.Q<DropdownField>("available-nodes");
            var unlockNode = RootElement.Q<Button>("unlock-node");
            unlockNode.clicked += UpdateUnlockedTechNodes;

            _loadedNodes = RootElement.Q<ScrollView>("loaded-nodes");
            _unlockedNodes = RootElement.Q<ScrollView>("unlocked-nodes");
        }

        private void LateUpdate()
        {
            if (!IsWindowOpen) return;

            if (_unlockAllParts != null)
                _unlockAllParts.value = Game.CheatSystem.Get(CheatSystemItemID.UnlockAllParts);
            
            _lastUpdate -= Time.unscaledDeltaTime;
            if (_lastUpdate >= 0f) return;
            _lastUpdate = UpdateSeconds;
            
            UpdateScienceMetadata();
            UpdateTechNodeDropdown();
            UpdateLoadedTechNodesView();
            UpdateUnlockedTechNodesView();
        }

        private void UnlockAllPartsChanged(ChangeEvent<bool> evt)
        {
            Game.CheatSystem.Set(CheatSystemItemID.UnlockAllParts, evt.newValue);
            UpdateUnlockedTechNodesView();
        }

        private void AddSciencePoints()
        {
            if (int.TryParse(_addSciencePointsField?.text, out var result))
            {
                Game.SessionManager.AddMyAgencyAvailableSciencePoints(result);
                UpdateScienceMetadata();
            }
        }

        private void UpdateScienceMetadata()
        {
            var sessionManager = Game.SessionManager;

            _agencyName!.text = "Agency Name: <b>" + sessionManager.GetMyAgencyName() + "</b>";

            var text = "Available: <b>" + sessionManager.GetMyAgencyAvailableSciencePoints() + "</b>";
            _scienceState!.text = text;
        }

        private void UpdateTechNodeDropdown()
        {
            if (_availableNodes == null) return;

            List<string> list = new();
            if (Game.AgencyManager.TryGetMyAgencyEntry(out var agencyEntry))
            {
                foreach (var value in Game.ScienceManager.TechNodeDataStore.AvailableData.Values)
                {
                    if (agencyEntry.UnlockedTechNodes.Contains(value.ID))
                        continue;

                    var allRequiredUnlocked = true;
                    foreach (var nodeIds in value.RequiredTechNodeIDs)
                    {
                        if (!Game.ScienceManager.IsNodeUnlocked(nodeIds))
                        {
                            allRequiredUnlocked = false;
                            break;
                        }
                    }

                    if (allRequiredUnlocked)
                        list.Add("<b>" + value.ID + "</b>");
                    else
                        list.Add(value.ID);
                }
            }

            list.Sort();
            _availableNodes.choices = list;
        }

        private void UpdateUnlockedTechNodes()
        {
            if (_availableNodes?.choices.Count <= 0) return;
            
            var node = _availableNodes!.value;
            if (!string.IsNullOrWhiteSpace(node))
            {
                Game.ScienceManager.UnlockTechNode(node.Replace("<b>", "").Replace("</b>", ""));
            }

            UpdateTechNodeDropdown();
            UpdateUnlockedTechNodesView();
            UpdateScienceMetadata();
        }

        private void UpdateLoadedTechNodesView()
        {
            _loadedNodes!.Clear();
            foreach (var nodeData in Game.ScienceManager.TechNodeDataStore.AvailableData.Values)
            {
                var label = new Label { text = nodeData.ID };
                _loadedNodes!.Add(label);
            }
        }

        private void UpdateUnlockedTechNodesView()
        {
            _unlockedNodes!.Clear();
            if (!Game.AgencyManager.TryGetMyAgencyEntry(out var agencyEntry)) return;

            foreach (var unlockedTechNode in agencyEntry.UnlockedTechNodes)
            {
                var label = new Label { text = unlockedTechNode };
                _unlockedNodes!.Add(label);
            }
        }
    }
}
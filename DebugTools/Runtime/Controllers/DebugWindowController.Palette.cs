using System;
using System.Collections.Generic;
using DebugTools.Utils;
using KSP.Game;
using KSP.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Redux.CliIntegration;
using UitkForKsp2.API;
using UitkForKsp2.Controls;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace DebugTools.Runtime.Controllers
{
    public partial class DebugWindowController
    {
        private PanelRenderer _paletteWindow;
        private AppShell _paletteRoot;
        private TextField _paletteSearch;
        private ScrollView _paletteResults;
        private Label _paletteDetails;
        private Button _paletteActivate;
        private IVisualElementScheduledItem _paletteRefresh;
        private bool _isPaletteLoading;
        private int _paletteLoadVersion;
        private int _paletteSelection;
        private string _paletteCopied;
        private readonly List<(string Name, string Search, HubEntry Entry, JObject Method)> _paletteItems = new();
        private readonly List<int> _paletteMatches = new();
        private readonly List<Button> _paletteRows = new();
        private readonly HashSet<ToggleableInputAction> _paletteEscapeActions = new();

        internal void TogglePalette()
        {
            if (_paletteWindow != null || _isPaletteLoading)
            {
                ClosePalette();
                return;
            }

            ClosePalette();
            _isPaletteLoading = true;
            int version = ++_paletteLoadVersion;
            UITKHelper.LoadUxml("DebugPalette", asset =>
            {
                if (!_isPaletteLoading || version != _paletteLoadVersion || this == null)
                    return;

                _isPaletteLoading = false;
                var template = asset.Instantiate();
                _paletteRoot = template.Q<AppShell>("root");
                _paletteRoot.Title = $"Debug palette - {Game.Input.Global.ToggleDebugPalette.GetBindingDisplayString()}";
                _paletteWindow = Window.Create(new WindowOptions
                {
                    WindowId = "DebugTools_Palette",
                    IsHidingEnabled = false,
                    DisableGameInputForTextFields = true,
                    MoveOptions = new MoveOptions { IsMovingEnabled = true, CheckScreenBounds = true },
                    ResizeOptions = new ResizeOptions { IsResizingEnabled = false }
                }, template);
                _paletteWindow.panelSettings.sortingOrder = 10000;
                _paletteWindow.gameObject.tag = "Game Manager";
                _paletteRoot.SetDefaultPosition(size => new Vector2(
                    (ReferenceResolution.Width - size.x) / 2, 100));
                _paletteSearch = _paletteRoot.Q<TextField>("paletteSearch");
                _paletteResults = _paletteRoot.Q<ScrollView>("paletteResults");
                _paletteDetails = _paletteRoot.Q<Label>("paletteDetails");
                _paletteActivate = _paletteRoot.Q<Button>("paletteActivate");
                _paletteActivate.clicked += ActivatePaletteSelection;
                _paletteRoot.CloseClicked += ClosePalette;
                _paletteRoot.RegisterCallback<KeyDownEvent>(OnPaletteKeyDown, TrickleDown.TrickleDown);
                SuppressPaletteEscape();
                _paletteSearch.RegisterValueChangedCallback(_ => FilterPalette());

                // Snapshot at open time so registration by another tool needs no palette changes.
                foreach (HubEntry entry in _entries)
                {
                    _paletteItems.Add((entry.Label, $"{entry.SearchText} {entry.Note}", entry, null));
                }
                foreach (JObject method in (JArray)CliIntegrationServer.GetMethodCatalog()["methods"])
                {
                    string name = (string)method["method"];
                    _paletteItems.Add((name, method.ToString(Formatting.None), null, method));
                }
                if (!CliIntegrationServer.ListeningPort.HasValue)
                {
                    _paletteRoot.Q<Label>("paletteHelp").text += "\nCLI integration is disabled. Enable it and reopen to list CLI methods.";
                }
                FilterPalette();
                _paletteSearch.Focus();
                // The only refresh belongs to this visible tree and is paused before destruction.
                _paletteRefresh = _paletteRoot.schedule.Execute(RefreshPalette).Every(250);
            });
        }

        private void ClosePalette()
        {
            _isPaletteLoading = false;
            _paletteLoadVersion++;
            _paletteRefresh?.Pause();
            _paletteRefresh = null;
            if (_paletteWindow != null)
            {
                _paletteSearch.Blur();
                _paletteRoot.RemoveFromHierarchy();
                _paletteWindow.enabled = false;
                Destroy(_paletteWindow.gameObject);
            }
            _paletteWindow = null;
            _paletteRoot = null;
            _paletteSearch = null;
            _paletteResults = null;
            _paletteDetails = null;
            _paletteActivate = null;
            _paletteCopied = null;
            _paletteItems.Clear();
            _paletteMatches.Clear();
            _paletteRows.Clear();
        }

        private void SuppressPaletteEscape()
        {
            foreach (InputDefinition definition in Game.InputManager.InputDefinitions.Values)
            {
                foreach (ToggleableInputAction action in definition.Actions.Values)
                {
                    foreach (InputBinding binding in action.InputAction.bindings)
                    {
                        if (binding.effectivePath == null ||
                            !binding.effectivePath.EndsWith("/escape", StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (_paletteEscapeActions.Add(action))
                            action.SetSuppressed(_paletteEscapeActions, true);
                        break;
                    }
                }
            }
            InputSystem.onAfterUpdate -= ReleasePaletteEscapeWhenClosed;
            InputSystem.onAfterUpdate += ReleasePaletteEscapeWhenClosed;
        }

        private void ReleasePaletteEscapeWhenClosed()
        {
            // Keep the game action disabled until the key that closed the palette is released.
            if (_paletteWindow != null || (Keyboard.current != null && Keyboard.current.escapeKey.isPressed))
                return;
            ReleasePaletteEscape();
        }

        private void ReleasePaletteEscape()
        {
            InputSystem.onAfterUpdate -= ReleasePaletteEscapeWhenClosed;
            foreach (ToggleableInputAction action in _paletteEscapeActions)
            {
                action.SetSuppressed(_paletteEscapeActions, false);
            }
            _paletteEscapeActions.Clear();
        }

        private static int PaletteRank(string name, string searchable, string query)
        {
            foreach (string term in query.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (searchable.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0)
                    return -1;
            }
            if (string.Equals(name, query, StringComparison.OrdinalIgnoreCase))
                return 0;
            if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                return 1;
            return name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ? 2 : 3;
        }

        private void FilterPalette()
        {
            string query = _paletteSearch.value?.Trim() ?? string.Empty;
            _paletteMatches.Clear();
            for (int index = 0; index < _paletteItems.Count; index++)
            {
                var item = _paletteItems[index];
                if (PaletteRank(item.Name, item.Search, query) >= 0)
                {
                    _paletteMatches.Add(index);
                }
            }
            _paletteMatches.Sort((left, right) =>
            {
                var first = _paletteItems[left];
                var second = _paletteItems[right];
                int rank = PaletteRank(first.Name, first.Search, query).CompareTo(
                    PaletteRank(second.Name, second.Search, query));
                return rank != 0 ? rank : StringComparer.OrdinalIgnoreCase.Compare(first.Name, second.Name);
            });
            _paletteSelection = 0;
            _paletteCopied = null;
            _paletteResults.Clear();
            _paletteRows.Clear();
            for (int index = 0; index < _paletteMatches.Count; index++)
            {
                int selection = index;
                var button = new OabButton(() =>
                {
                    _paletteSelection = selection;
                    _paletteCopied = null;
                    RefreshPalette();
                });
                button.AddToClassList("kui-button");
                button.AddToClassList("dt-palette-result");
                _paletteRows.Add(button);
                _paletteResults.Add(button);
            }
            RefreshPalette();
        }

        private void RefreshPalette()
        {
            _gameState = Game != null && Game.GlobalGameState != null
                ? Game.GlobalGameState.GetGameState().GameState : GameState.Invalid;
            for (int index = 0; index < _paletteRows.Count; index++)
            {
                var item = _paletteItems[_paletteMatches[index]];
                HubEntry entry = item.Entry;
                string location = entry == null ? "CLI" : $"{SECTION_NAMES[(int)entry.Section]} / {entry.Group}";
                string reason = entry == null ? null : GetUnavailableReason(entry);
                string state = entry is { IsSwitch: true } ? (entry.IsActive() ? " [On]" : " [Off]") : string.Empty;
                string hotkey = entry?.HotkeyText?.Invoke();
                _paletteRows[index].text = $"{item.Name}{state}    {location}" +
                    (string.IsNullOrEmpty(hotkey) ? string.Empty : $"    {hotkey}") +
                    (reason == null ? string.Empty : $"\n{reason}");
                _paletteRows[index].EnableInClassList("active", index == _paletteSelection);
                _paletteRows[index].EnableInClassList("dt-palette-unavailable", reason != null);
            }
            if (_paletteMatches.Count == 0)
            {
                _paletteDetails.text = "No matches. CLI methods are listed when CLI integration is enabled.";
                _paletteActivate.SetEnabled(false);
                return;
            }
            var selected = _paletteItems[_paletteMatches[_paletteSelection]];
            string unavailable = selected.Entry == null ? null : GetUnavailableReason(selected.Entry);
            _paletteActivate.SetEnabled(unavailable == null);
            _paletteActivate.text = selected.Method != null ? "Copy CLI command" :
                selected.Entry.IsSwitch ? "Toggle" : selected.Entry.ActionText ?? "Open";
            _paletteDetails.text = _paletteCopied ?? (selected.Method != null
                ? $"{selected.Method["description"]}\nParameters:\n{selected.Method["schema"]}\nCopy only. Replace required parameter placeholders before running."
                : $"{selected.Entry.Note}\n{unavailable}");
        }

        private void OnPaletteKeyDown(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.Escape:
                    ClosePalette();
                    break;
                case KeyCode.UpArrow:
                case KeyCode.DownArrow:
                    if (_paletteMatches.Count > 0)
                    {
                        int direction = evt.keyCode == KeyCode.DownArrow ? 1 : -1;
                        _paletteSelection = (_paletteSelection + direction + _paletteMatches.Count) % _paletteMatches.Count;
                        _paletteCopied = null;
                        RefreshPalette();
                        _paletteResults.ScrollTo(_paletteRows[_paletteSelection]);
                    }
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    ActivatePaletteSelection();
                    break;
                default:
                    return;
            }
            evt.StopImmediatePropagation();
        }

        private void ActivatePaletteSelection()
        {
            if (_paletteMatches.Count == 0)
                return;

            RefreshPalette();
            var item = _paletteItems[_paletteMatches[_paletteSelection]];
            if (item.Method != null)
            {
                var parameters = new JObject();
                if (item.Method["schema"]?["required"] is JArray required)
                {
                    foreach (string name in required)
                    {
                        parameters[name] = $"<{name}>";
                    }
                }
                string json = parameters.ToString(Formatting.None).Replace("'", "''");
                string method = item.Name.Replace("'", "''");
                GUIUtility.systemCopyBuffer = $".\\scripts\\Ksp2Redux.Cli.exe call '{method}' '{json}' --port {CliIntegrationServer.ListeningPort}";
                _paletteCopied = "Copied CLI command template. Replace required placeholders before running.\n" +
                    GUIUtility.systemCopyBuffer;
                RefreshPalette();
                return;
            }
            if (GetUnavailableReason(item.Entry) != null)
                return;

            HubEntry entry = item.Entry;
            if (entry.IsSwitch)
            {
                ActivateEntry(entry);
                RefreshPalette();
                return;
            }
            ClosePalette();
            // Docked tools need the hub visible. Popped tools retain their saved host and open state.
            if (entry.Load != null && entry.Mode == ToolMode.Inline && !entry.PoppedOut)
            {
                IsWindowOpen = true;
                OpenHostedTool(entry);
                return;
            }
            OpenSearchResult(entry);
        }
    }
}

using System;
using System.Collections.Generic;
using I2.Loc;
using Redux.QA;
using UitkForKsp2.API;
using UnityEngine;
using UnityEngine.UIElements;

namespace DebugTools.Runtime.Controllers
{
    /// <summary>
    /// Binds the virtualized runtime localization grid to the shared override state.
    /// </summary>
    public class LocWorkbenchWindowController : BaseWindowController
    {
        private static LocWorkbenchWindowController _instance;
        private static string _pendingKey = string.Empty;
        private ListView _list;
        private TextField _search;
        private Label _status;
        private VisualTreeAsset _rowTemplate;
        private IVisualElementScheduledItem _searchDelay;
        private readonly List<string> _filtered = new();
        private List<string> _keys;
        private bool _dirty = true;
        private Func<string, (string Value, string Origin)> _readCurrent;
        private Func<string, (string Value, string Origin)> _readEnglish;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            _pendingKey = string.Empty;
        }

        /// <summary>
        /// Selects a key now or when the window is first created.
        /// </summary>
        /// <param name="key">The key to filter and select.</param>
        internal static void SelectKey(string key)
        {
            _pendingKey = key;
            if (_instance != null && _instance._search != null)
            {
                _instance._search.SetValueWithoutNotify(key);
                _instance.Refresh();
            }
        }

        private void OnEnable()
        {
            _instance = this;
            _search = RootElement.Q<TextField>("loc-search");
            _status = RootElement.Q<Label>("loc-status");
            _list = RootElement.Q<ListView>("loc-list");
            _rowTemplate = _list.itemTemplate;
            _list.makeItem = MakeRow;
            _list.bindItem = BindRow;
            _list.itemsSource = _filtered;
            _list.fixedItemHeight = 52;
            _search.SetValueWithoutNotify(_pendingKey);
            _search.RegisterValueChangedCallback(_ =>
            {
                _searchDelay?.Pause();
                _searchDelay = _search.schedule.Execute(() =>
                {
                    _dirty = true;
                    if (IsWindowOpen)
                    {
                        Refresh();
                    }
                }).StartingIn(200);
            });
            RootElement.Q<Button>("loc-reload").clicked += LocWorkbench.Reload;
            RootElement.Q<Button>("loc-export").clicked += LocWorkbench.Export;
            Button reset = RootElement.Q<Button>("loc-reset-all");
            reset.clicked += () =>
            {
                if (reset.text != "Confirm reset")
                {
                    reset.text = "Confirm reset";
                    return;
                }
                reset.text = "Reset all";
                LocWorkbench.ResetAll();
            };
            LocWorkbench.OnChanged += OnChanged;
            Refresh();
        }

        private void OnDestroy()
        {
            LocWorkbench.OnChanged -= OnChanged;
            _searchDelay?.Pause();
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnChanged() => _dirty = true;

        private void Update()
        {
            if (!IsWindowOpen || !_dirty)
                return;
            Refresh();
        }

        private void Refresh()
        {
            _dirty = false;
            _keys = LocWorkbench.GetKeys();
            _readCurrent = LocWorkbench.CreateReader(LocalizationManager.CurrentLanguage, true);
            _readEnglish = LocWorkbench.CreateReader("English", false);
            _filtered.Clear();
            string search = _search.value;
            foreach (string key in _keys)
            {
                string current = _readCurrent(key).Value;
                string english = _readEnglish(key).Value;
                if (key.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    current.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    english.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _filtered.Add(key);
                }
            }
            _list.RefreshItems();
            int selected = _filtered.IndexOf(_pendingKey);
            if (selected >= 0)
            {
                _list.SetSelection(selected);
                _list.ScrollToItem(selected);
                _pendingKey = string.Empty;
            }
            _status.text = $"{LocalizationManager.CurrentLanguage} | {_filtered.Count} / {_keys.Count} keys\n{LocWorkbench.Status}";
        }

        private VisualElement MakeRow()
        {
            TemplateContainer row = _rowTemplate.Instantiate();
            TextField value = row.Q<TextField>("loc-value");
            value.isDelayed = true;
            value.DisableGameInputOnFocus();
            value.RegisterValueChangedCallback(evt =>
            {
                if (row.userData is string key)
                {
                    LocWorkbench.Edit(key, evt.newValue);
                }
            });
            row.Q<Button>("loc-reset").clicked += () =>
            {
                if (row.userData is string key)
                {
                    LocWorkbench.Reset(key);
                }
            };
            return row;
        }

        private void BindRow(VisualElement row, int index)
        {
            string key = _filtered[index];
            row.userData = key;
            row.Q<Label>("loc-key").text = key;
            row.Q<Label>("loc-key").tooltip = key;
            var (value, source) = _readCurrent(key);
            row.Q<TextField>("loc-value").SetValueWithoutNotify(value);
            TermData term = LocalizationManager.GetTermData(key);
            row.Q<TextField>("loc-value").SetEnabled(term == null || term.TermType == eTermType.Text);
            string english = _readEnglish(key).Value;
            row.Q<Label>("loc-english").text = english;
            row.Q<Label>("loc-english").tooltip = english;
            row.Q<Label>("loc-source").text = source;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using DebugTools.Utils;
using KSP.Game;
using KSP.Messages;
using UitkForKsp2.API;
using UitkForKsp2.Controls;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace DebugTools.Runtime.Controllers
{
    /// <summary>
    /// Controller for the Dev Tools hub, the one debug window. It holds the section rail, search, the cheats and the
    /// status strip of active overrides, and opens every other debug tool.
    /// </summary>
    public class DebugWindowController : KerbalMonoBehaviour
    {
        /// <summary>
        /// The hub sections in rail order. Each one answers a question a tester asks.
        /// </summary>
        public enum Section
        {
            /// <summary>
            /// Put the game in the situation a test needs.
            /// </summary>
            Cheats,

            /// <summary>
            /// Check that science works, and move through progression.
            /// </summary>
            Science,

            /// <summary>
            /// See what is going on.
            /// </summary>
            Inspect,

            /// <summary>
            /// Show it in the world.
            /// </summary>
            Overlays,

            /// <summary>
            /// Measure it under controlled conditions.
            /// </summary>
            TestBenches,

            /// <summary>
            /// Check that text fits and reads right.
            /// </summary>
            Text,

            /// <summary>
            /// Check many things and write them down.
            /// </summary>
            QaRuns,

            /// <summary>
            /// Find out whether it is fast enough.
            /// </summary>
            Performance,

            /// <summary>
            /// See what mods and patches changed.
            /// </summary>
            Mods
        }

        /// <summary>
        /// The scene a tool needs before it can be opened.
        /// </summary>
        public enum ToolScene
        {
            /// <summary>
            /// Any scene of a loaded campaign.
            /// </summary>
            Campaign,

            /// <summary>
            /// Flight or the map, with a vessel to work on.
            /// </summary>
            Flight,

            /// <summary>
            /// The vehicle or base assembly building.
            /// </summary>
            Assembly
        }

        private const int SECTION_COUNT = 9;
        private const float REFRESH_INTERVAL = 0.25f;
        private const float CONFIRM_SECONDS = 3f;
        private const float TOOL_WINDOW_GAP = 12f;
        private const float TOOL_WINDOW_CASCADE = 48f;
        private const string LOAD_A_CAMPAIGN = "Load a campaign first";
        private const string WAIT_FOR_LOADING = "Wait for loading to finish";
        private const string NOT_IN_THIS_CAMPAIGN = "Not available in this campaign";

        private static readonly string[] SECTION_NAMES =
        {
            "Cheats", "Science", "Inspect", "Overlays", "Test benches", "Text", "QA runs", "Performance", "Mods"
        };

        private static readonly string[] SECTION_PURPOSES =
        {
            "Put the game in the situation you need.",
            "Is science working? Move through progression.",
            "See what is going on.",
            "Show it in the world.",
            "Measure it under controlled conditions.",
            "Check that text fits and reads right.",
            "Check many things and write it down.",
            "Is it fast enough, and why not?",
            "What did mods and patches change?"
        };

        /// <summary>
        /// One thing the hub can open or switch: a tool window, a cheat or an overlay switch.
        /// </summary>
        private sealed class HubEntry
        {
            public Section Section;
            public string Group;
            public string Label;
            public string Note;
            public string SearchText;
            public ToolScene Scene;
            public bool IsLegacy;
            public bool IsSwitch;
            public bool IsOverride;
            public bool IsCampaignSetting;
            public bool NormalValue;
            public Func<bool> IsActive;
            public Func<bool> IsSupported;
            public Action Toggle;
            public Action<bool> SetActive;
            public EntryRow SectionView;
            public EntryRow SearchView;
        }

        /// <summary>
        /// The elements of one row that change while the hub is open.
        /// </summary>
        private struct EntryRow
        {
            public VisualElement Row;
            public Label Reason;
            public Button Button;
            public Toggle Switch;
        }

        // The PanelRenderer component of the window game object
        private PanelRenderer _window;

        private VisualElement _rootElement;
        private TextField _search;
        private VisualElement _rail;
        private Label _paneTitle;
        private Label _panePurpose;
        private Label _paneNotice;
        private ScrollView _content;
        private VisualElement _statusStrip;
        private Label _statusSummary;
        private Button _statusExpand;
        private Button _revertAll;
        private VisualElement _statusList;

        private readonly List<HubEntry> _entries = new();
        private readonly List<string> _overrideNames = new();
        private readonly VisualElement[] _sectionPanes = new VisualElement[SECTION_COUNT];
        private readonly VisualElement[] _railItems = new VisualElement[SECTION_COUNT];
        private readonly Label[] _railBadges = new Label[SECTION_COUNT];
        private readonly int[] _openCounts = new int[SECTION_COUNT];
        private readonly int[] _shownOpenCounts = new int[SECTION_COUNT];
        private VisualElement _searchResults;
        private HubEntry _firstSearchResult;

        private Section _section;
        private bool _isViewListingEntries;
        private bool _isBuilt;
        private bool _isWindowOpen;
        private bool _isStatusExpanded;
        private int _overrideMask = -1;
        private int _overrideCount;
        // A revert waiting for its Confirm click: the button that asked, and the override it reverts, or null for all.
        private float _confirmUntil;
        private Button _confirmButton;
        private HubEntry _confirmEntry;
        private float _nextRefresh;
        private GameState _gameState;
        private MessageCenter _messageCenter;

        /// <summary>
        /// Gets or sets whether the hub is shown. A closed hub runs no per-frame code.
        /// </summary>
        public bool IsWindowOpen
        {
            get => _isWindowOpen;
            set
            {
                _isWindowOpen = value;
                // A closed window's renderer is switched off, which keeps its tree, as for the other debug windows.
                _window.enabled = value;
                // Switching the controller back on runs OnEnable, which picks up a rebuilt panel root.
                enabled = value;
                if (value && !_isBuilt)
                {
                    BuildHub();
                }

                _rootElement.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
                if (value)
                {
                    OnOpened();
                }
                else
                {
                    OnClosed();
                }
            }
        }

        /// <summary>
        /// Adds a tool window to a section of the hub.
        /// </summary>
        /// <param name="section">The section the tool lives in.</param>
        /// <param name="group">The heading the tool is listed under within its section.</param>
        /// <param name="label">The tool name.</param>
        /// <param name="scene">The scene the tool needs before it can be opened.</param>
        /// <param name="isOpen">Returns whether the tool window is open.</param>
        /// <param name="toggle">Opens the tool window, or closes it if it is open.</param>
        /// <param name="note">A short line under the name, or <c>null</c> for none.</param>
        /// <param name="keywords">Extra words search matches the tool by, or <c>null</c> for none.</param>
        /// <param name="isLegacy">True for a stock window not yet moved into the Dev Tools look.</param>
        public void RegisterTool(Section section, string group, string label, ToolScene scene, Func<bool> isOpen,
            Action toggle, string note = null, string keywords = null, bool isLegacy = false)
        {
            AddEntry(new HubEntry
            {
                Section = section,
                Group = group,
                Label = label,
                Note = note,
                Scene = scene,
                IsLegacy = isLegacy,
                IsActive = isOpen,
                Toggle = toggle
            }, keywords);
        }

        /// <summary>
        /// Adds an on and off switch, such as a world overlay, to a section of the hub.
        /// </summary>
        /// <param name="section">The section the switch lives in.</param>
        /// <param name="group">The heading the switch is listed under within its section.</param>
        /// <param name="label">The switch name.</param>
        /// <param name="scene">The scene the switch needs before it can be used.</param>
        /// <param name="isOn">Returns whether the switch is on.</param>
        /// <param name="setOn">Turns the switch on or off.</param>
        /// <param name="note">A short line under the name, or <c>null</c> for none.</param>
        /// <param name="keywords">Extra words search matches the switch by, or <c>null</c> for none.</param>
        /// <param name="isLegacy">True for a stock tool not yet moved into the Dev Tools look.</param>
        public void RegisterSwitch(Section section, string group, string label, ToolScene scene, Func<bool> isOn,
            Action<bool> setOn, string note = null, string keywords = null, bool isLegacy = false)
        {
            AddEntry(new HubEntry
            {
                Section = section,
                Group = group,
                Label = label,
                Note = note,
                Scene = scene,
                IsLegacy = isLegacy,
                IsSwitch = true,
                IsActive = isOn,
                SetActive = setOn
            }, keywords);
        }

        private void AddEntry(HubEntry entry, string keywords)
        {
            string sectionName = SECTION_NAMES[(int)entry.Section];
            string legacy = entry.IsLegacy ? "legacy" : string.Empty;
            entry.SearchText = $"{entry.Label} {entry.Group} {sectionName} {keywords} {legacy}".ToLowerInvariant();
            _entries.Add(entry);
            // A pane built before this entry existed is rebuilt the next time its section is shown.
            _sectionPanes[(int)entry.Section] = null;
            // A section shows in the rail once its first tool is registered.
            if (_railItems[(int)entry.Section] != null)
            {
                _railItems[(int)entry.Section].style.display = DisplayStyle.Flex;
            }
        }

        private void Awake()
        {
            RegisterCheats();
        }

        private void OnDestroy()
        {
            UnsubscribeFromGameState();
        }

        private void OnApplicationQuit()
        {
            // The position is otherwise saved only when the hub is closed, which quitting the game skips.
            if (_isWindowOpen)
            {
                SaveWindowRect();
            }
        }

        /// <summary>
        /// Runs when the window is first created, and every time the hub is opened.
        /// </summary>
        private void OnEnable()
        {
            _window = GetComponent<PanelRenderer>();

            // Since we're cloning the UXML tree from a VisualTreeAsset, the actual root element is a TemplateContainer,
            // so we need to get the first child of the TemplateContainer to get our actual root VisualElement.
            VisualElement root = _window.GetWindowRoot();
            if (ReferenceEquals(_rootElement, root))
                return;

            _rootElement = root;
            BindRoot();
            // A rebuilt panel has none of the rows, so they are built again on it.
            _isBuilt = false;
        }

        private void Update()
        {
            // Only runs while the hub is open, since closing it switches this component off.
            if (Time.unscaledTime < _nextRefresh)
                return;

            _nextRefresh = Time.unscaledTime + REFRESH_INTERVAL;
            RefreshEntries();
            RefreshStatusStrip();
        }

        private void BindRoot()
        {
            RestoreWindowRect();

            _rootElement.Q<Button>("close-button").clicked += () => IsWindowOpen = false;

            _search = _rootElement.Q<TextField>("search");
            _search.RegisterValueChangedCallback(_ => ShowSearchResults());
            _search.RegisterCallback<KeyDownEvent>(OnSearchKeyDown, TrickleDown.TrickleDown);

            _rail = _rootElement.Q<VisualElement>("rail");
            _paneTitle = _rootElement.Q<Label>("pane-title");
            _panePurpose = _rootElement.Q<Label>("pane-purpose");
            _paneNotice = _rootElement.Q<Label>("pane-notice");
            _paneNotice.style.display = DisplayStyle.None;
            _content = _rootElement.Q<ScrollView>("pane-content");

            _statusStrip = _rootElement.Q<VisualElement>("status-strip");
            _statusSummary = _rootElement.Q<Label>("status-summary");
            _statusExpand = _rootElement.Q<Button>("status-expand");
            _statusExpand.clicked += ToggleStatusList;
            _revertAll = _rootElement.Q<Button>("status-revert-all");
            _revertAll.clicked += OnRevertAllClicked;
            _statusList = _rootElement.Q<VisualElement>("status-list");
            _statusStrip.style.display = DisplayStyle.None;
        }

        private void BuildHub()
        {
            _isBuilt = true;
            _rail.Clear();
            for (int index = 0; index < SECTION_COUNT; index++)
            {
                var section = (Section)index;
                var item = new VisualElement();
                item.AddToClassList("dt-rail__item");
                // A section with no tools yet stays out of the rail until its first one is registered.
                item.style.display = HasEntries(section) ? DisplayStyle.Flex : DisplayStyle.None;
                // Capitals, as in the Settings menu's section list.
                var label = new Label(SECTION_NAMES[index].ToUpperInvariant());
                label.AddToClassList("dt-rail__label");
                item.Add(label);
                var badge = new Label();
                badge.AddToClassList("dt-rail__badge");
                badge.style.display = DisplayStyle.None;
                item.Add(badge);
                item.AddManipulator(new Clickable(() => SelectSection(section)));
                _rail.Add(item);

                _railItems[index] = item;
                _railBadges[index] = badge;
                _sectionPanes[index] = null;
                _shownOpenCounts[index] = 0;
            }

            foreach (HubEntry entry in _entries)
            {
                entry.SectionView = default;
                entry.SearchView = default;
            }

            _searchResults = new VisualElement();
            _overrideMask = -1;
            // TryParse also takes a number outside the enum, and a section can lose its tools between versions.
            bool isSavedUsable = Enum.TryParse(Configuration.HubLastSection?.Value, out Section saved) &&
                Enum.IsDefined(typeof(Section), saved) && HasEntries(saved);
            SelectSection(isSavedUsable ? saved : Section.Cheats);
        }

        private bool HasEntries(Section section)
        {
            foreach (HubEntry entry in _entries)
            {
                if (entry.Section == section)
                    return true;
            }

            return false;
        }

        private void OnOpened()
        {
            GameInstance game = Game;
            _gameState = game != null && game.GlobalGameState != null
                ? game.GlobalGameState.GetGameState().GameState
                : GameState.Invalid;
            if (_messageCenter == null && game != null)
            {
                // The hub can stay open while a campaign closes, so its subscription must outlive the campaign (#1427).
                _messageCenter = game.Messages;
                _messageCenter.PersistentSubscribe<GameStateEnteredMessage>(OnGameStateEntered);
            }

            _overrideMask = -1;
            _nextRefresh = 0f;
        }

        private void OnClosed()
        {
            UnsubscribeFromGameState();
            SaveWindowRect();
            CancelConfirm();
            if (!string.IsNullOrEmpty(_search.value))
            {
                _search.SetValueWithoutNotify(string.Empty);
                ShowSection();
            }
        }

        private void UnsubscribeFromGameState()
        {
            if (_messageCenter == null)
                return;

            _messageCenter.Unsubscribe<GameStateEnteredMessage>(OnGameStateEntered);
            _messageCenter = null;
        }

        private void OnGameStateEntered(MessageCenterMessage msg)
        {
            if (msg is not GameStateEnteredMessage message)
                return;

            _gameState = message.StateBeingEntered;
            _nextRefresh = 0f;
        }

        #region Sections and search

        private void SelectSection(Section section)
        {
            _section = section;
            if (!string.IsNullOrEmpty(_search.value))
            {
                _search.SetValueWithoutNotify(string.Empty);
            }

            ShowSection();

            string sectionName = section.ToString();
            if (Configuration.HubLastSection != null && Configuration.HubLastSection.Value != sectionName)
            {
                Configuration.HubLastSection.Value = sectionName;
            }
        }

        private void ShowSection()
        {
            SetRailSelection((int)_section);
            foreach (HubEntry entry in _entries)
            {
                entry.SearchView = default;
            }

            // Enter acts on a search result only while the results are shown.
            _firstSearchResult = null;
            _paneTitle.text = SECTION_NAMES[(int)_section].ToUpperInvariant();
            _panePurpose.text = SECTION_PURPOSES[(int)_section];
            _sectionPanes[(int)_section] ??= BuildSectionPane(_section);
            _isViewListingEntries = true;
            _content.Clear();
            _content.Add(_sectionPanes[(int)_section]);
            _content.scrollOffset = Vector2.zero;
            _nextRefresh = 0f;
        }

        private void SetRailSelection(int selectedIndex)
        {
            for (int index = 0; index < SECTION_COUNT; index++)
            {
                _railItems[index].EnableInClassList("dt-rail__item--selected", index == selectedIndex);
            }
        }

        private VisualElement BuildSectionPane(Section section)
        {
            var pane = new VisualElement();
            var groups = new List<string>();
            foreach (HubEntry entry in _entries)
            {
                if (entry.Section == section && !groups.Contains(entry.Group))
                {
                    groups.Add(entry.Group);
                }
            }

            foreach (string group in groups)
            {
                var groupElement = new VisualElement();
                groupElement.AddToClassList("dt-group");
                // The "//" sub-header of the game's own Redux windows.
                var header = new VisualElement();
                header.AddToClassList("dt-group__header");
                var slashes = new Label("//");
                slashes.AddToClassList("dt-group__slashes");
                header.Add(slashes);
                var title = new Label(group.ToUpperInvariant());
                title.AddToClassList("dt-group__title");
                header.Add(title);
                groupElement.Add(header);
                // Stock windows that still have their old look come last, so each group starts with its own tools.
                AddGroupRows(groupElement, section, group, false);
                AddGroupRows(groupElement, section, group, true);
                pane.Add(groupElement);
            }

            return pane;
        }

        private void AddGroupRows(VisualElement groupElement, Section section, string group, bool isLegacy)
        {
            foreach (HubEntry entry in _entries)
            {
                if (entry.Section != section || entry.Group != group || entry.IsLegacy != isLegacy)
                    continue;

                entry.SectionView = BuildRow(entry, false);
                groupElement.Add(entry.SectionView.Row);
            }
        }

        private EntryRow BuildRow(HubEntry entry, bool isSearchResult)
        {
            var view = new EntryRow { Row = new VisualElement() };
            view.Row.AddToClassList("dt-row");

            var text = new VisualElement();
            text.AddToClassList("dt-row__text");
            var titleLine = new VisualElement();
            titleLine.AddToClassList("dt-row__title-line");
            var name = new Label(entry.Label);
            name.AddToClassList("dt-row__name");
            titleLine.Add(name);
            if (isSearchResult)
            {
                // A search result names where it lives, so search also teaches the layout.
                titleLine.Add(CreateTag(SECTION_NAMES[(int)entry.Section], "dt-tag--section"));
            }

            if (entry.IsLegacy)
            {
                titleLine.Add(CreateTag("legacy", "dt-tag--legacy"));
            }

            text.Add(titleLine);
            if (!string.IsNullOrEmpty(entry.Note))
            {
                var note = new Label(entry.Note);
                note.AddToClassList("dt-row__note");
                text.Add(note);
            }

            view.Reason = new Label();
            view.Reason.AddToClassList("dt-row__reason");
            view.Reason.style.display = DisplayStyle.None;
            text.Add(view.Reason);
            view.Row.Add(text);

            if (entry.IsSwitch)
            {
                view.Switch = new Toggle();
                view.Switch.AddToClassList("switch");
                view.Switch.AddToClassList("dt-row__switch");
                view.Switch.RegisterValueChangedCallback(evt =>
                {
                    entry.SetActive(evt.newValue);
                    _nextRefresh = 0f;
                });
                view.Row.Add(view.Switch);
            }
            else
            {
                view.Button = new OabButton(() => ActivateEntry(entry)) { text = "Open" };
                view.Button.AddToClassList("dt-button");
                view.Button.AddToClassList("dt-row__button");
                view.Row.Add(view.Button);
            }

            return view;
        }

        private static Label CreateTag(string text, string modifier)
        {
            var tag = new Label(text.ToUpperInvariant());
            tag.AddToClassList("dt-tag");
            tag.AddToClassList(modifier);
            return tag;
        }

        private void ActivateEntry(HubEntry entry)
        {
            bool isActive = entry.IsActive();
            // An open tool can always be closed, even after its scene has gone.
            if (!isActive && GetUnavailableReason(entry) != null)
                return;

            if (entry.IsSwitch)
            {
                entry.SetActive(!isActive);
            }
            else
            {
                entry.Toggle();
            }

            _nextRefresh = 0f;
        }

        private void ShowSearchResults()
        {
            string query = _search.value?.Trim();
            if (string.IsNullOrEmpty(query))
            {
                ShowSection();
                return;
            }

            // No rail item is selected while results from every section are shown.
            SetRailSelection(-1);
            string[] terms = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            _searchResults.Clear();
            _firstSearchResult = null;
            int count = 0;
            foreach (HubEntry entry in _entries)
            {
                entry.SearchView = default;
                if (!MatchesAll(entry.SearchText, terms))
                    continue;

                entry.SearchView = BuildRow(entry, true);
                _searchResults.Add(entry.SearchView.Row);
                _firstSearchResult ??= entry;
                count++;
            }

            if (count == 0)
            {
                var nothing = new Label($"Nothing matches \"{query}\".");
                nothing.AddToClassList("dt-coming");
                _searchResults.Add(nothing);
            }

            _paneTitle.text = "SEARCH";
            string enterAction = _firstSearchResult is { IsSwitch: true } ? "switches" : "opens";
            _panePurpose.text = count switch
            {
                0 => "Tools and cheats from every section.",
                1 => $"1 match. Enter {enterAction} it.",
                _ => $"{count} matches. Enter {enterAction} the first."
            };
            _isViewListingEntries = count > 0;
            _content.Clear();
            _content.Add(_searchResults);
            _content.scrollOffset = Vector2.zero;
            _nextRefresh = 0f;
        }

        private static bool MatchesAll(string text, string[] terms)
        {
            foreach (string term in terms)
            {
                if (!text.Contains(term))
                    return false;
            }

            return true;
        }

        private void OnSearchKeyDown(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.Escape:
                    _search.value = string.Empty;
                    evt.StopPropagation();
                    break;
                case KeyCode.Return or KeyCode.KeypadEnter when _firstSearchResult != null &&
                    !string.IsNullOrWhiteSpace(_search.value):
                    ActivateEntry(_firstSearchResult);
                    evt.StopPropagation();
                    break;
            }
        }

        #endregion

        #region Rows and scene awareness

        private void RefreshEntries()
        {
            // Every tool needs a campaign, so that reason is said once for the whole pane instead of on every row.
            string campaignReason = GetSceneReason(ToolScene.Campaign, _gameState);
            _paneNotice.text = campaignReason == null ? string.Empty : $"{campaignReason}.";
            _paneNotice.style.display = campaignReason != null && _isViewListingEntries
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            Array.Clear(_openCounts, 0, SECTION_COUNT);
            foreach (HubEntry entry in _entries)
            {
                string reason = GetUnavailableReason(entry);
                bool isReasonShown = reason != null && reason != campaignReason;
                bool isActive = entry.IsActive();
                UpdateRow(entry.SectionView, reason, isReasonShown, isActive);
                UpdateRow(entry.SearchView, reason, isReasonShown, isActive);
                // Cheats are counted by the status strip, so the rail counts open tools and overlays only.
                if (isActive && !entry.IsOverride)
                {
                    _openCounts[(int)entry.Section]++;
                }
            }

            for (int index = 0; index < SECTION_COUNT; index++)
            {
                int count = _openCounts[index];
                if (count == _shownOpenCounts[index])
                    continue;

                _shownOpenCounts[index] = count;
                _railBadges[index].text = count.ToString(CultureInfo.InvariantCulture);
                _railBadges[index].style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private static void UpdateRow(EntryRow view, string reason, bool isReasonShown, bool isActive)
        {
            if (view.Row == null)
                return;

            bool isAvailable = reason == null;
            view.Row.EnableInClassList("dt-row--unavailable", !isAvailable);
            view.Row.EnableInClassList("dt-row--open", isActive && view.Button != null);
            view.Reason.text = isReasonShown ? reason : string.Empty;
            view.Reason.style.display = isReasonShown ? DisplayStyle.Flex : DisplayStyle.None;
            if (view.Switch != null)
            {
                view.Switch.SetValueWithoutNotify(isActive);
                view.Switch.SetEnabled(isAvailable || isActive);
                return;
            }

            view.Button.text = isActive ? "Close" : "Open";
            // The theme's active OabButton, as on the game's own toggled buttons.
            view.Button.EnableInClassList("active", isActive);
            view.Button.SetEnabled(isAvailable || isActive);
        }

        private string GetUnavailableReason(HubEntry entry)
        {
            string reason = GetSceneReason(entry.Scene, _gameState);
            if (reason == null && entry.IsSupported != null && !entry.IsSupported())
            {
                reason = NOT_IN_THIS_CAMPAIGN;
            }

            return reason;
        }

        /// <summary>
        /// Returns why a tool needing the given scene cannot be opened in the given game state.
        /// </summary>
        /// <param name="scene">The scene the tool needs.</param>
        /// <param name="state">The current game state.</param>
        /// <returns>A one-line reason, or <c>null</c> when the tool can be opened.</returns>
        internal static string GetSceneReason(ToolScene scene, GameState state) => state switch
        {
            GameState.Invalid or GameState.WarmUpLoading or GameState.MainMenu => LOAD_A_CAMPAIGN,
            GameState.Loading => WAIT_FOR_LOADING,
            not (GameState.FlightView or GameState.Map3DView) when scene == ToolScene.Flight => "Needs a vessel in flight",
            not (GameState.VehicleAssemblyBuilder or GameState.BaseAssemblyEditor) when scene == ToolScene.Assembly =>
                "Needs the VAB",
            _ => null
        };

        /// <summary>
        /// Returns where a newly created tool window goes: beside the hub where it fits, one step lower for each tool
        /// window open from the hub.
        /// </summary>
        /// <remarks>
        /// It runs once the new window has its first layout, by which time that window counts as open, so the first
        /// window already sits one step below the top of the hub.
        /// </remarks>
        /// <param name="windowSize">The size of the tool window.</param>
        /// <returns>The position of the tool window's top left corner.</returns>
        public Vector2 GetToolWindowPosition(Vector2 windowSize)
        {
            Rect bounds = _rootElement.panel?.visualTree?.contentRect ??
                new Rect(0, 0, ReferenceResolution.Width, ReferenceResolution.Height);
            Rect hub = _rootElement.layout;

            float x = hub.xMax + TOOL_WINDOW_GAP;
            if (x + windowSize.x > bounds.width - TOOL_WINDOW_GAP)
            {
                // No room on the right, so try the left side, and as a last resort the right screen edge.
                float left = hub.xMin - TOOL_WINDOW_GAP - windowSize.x;
                x = left >= TOOL_WINDOW_GAP ? left : bounds.width - TOOL_WINDOW_GAP - windowSize.x;
            }

            int openWindows = 0;
            foreach (HubEntry entry in _entries)
            {
                if (!entry.IsSwitch && entry.IsActive())
                {
                    openWindows++;
                }
            }

            // Each further window steps down, so the title bars of the ones below stay visible.
            float y = hub.yMin + TOOL_WINDOW_CASCADE * openWindows;
            return new Vector2(
                Mathf.Max(TOOL_WINDOW_GAP, x),
                Mathf.Clamp(y, TOOL_WINDOW_GAP, Mathf.Max(TOOL_WINDOW_GAP, bounds.height - windowSize.y - TOOL_WINDOW_GAP)));
        }

        #endregion

        #region Cheats

        private void RegisterCheats()
        {
            RegisterCheat("Damage", CheatSystemItemID.UnbreakableParts, "Unbreakable Parts",
                "Impacts, splashdowns, overheating and aero stress break no part", "crash damage invincible");
            // Unbreakable joints and infinite resources are difficulty options, saved with the campaign.
            RegisterDifficultyOption("Damage", DifficultyOptionsUtils.UNBREAKABLE_JOINTS, "Unbreakable Joints", "joint");
            RegisterDifficultyOption("Resources", DifficultyOptionsUtils.INFINITE_FUEL, "Infinite Propellant", "fuel");
            RegisterDifficultyOption("Resources", DifficultyOptionsUtils.INFINITE_POWER, "Infinite Electricity",
                "power ec battery");
            RegisterCheat("Physics", CheatSystemItemID.DisableGravity, "Disable Gravity", keywords: "gravity");
            RegisterCheat("Physics", CheatSystemItemID.DisableAerodynamics, "Disable Aerodynamics",
                keywords: "aero drag lift");
            RegisterCheat("Physics", CheatSystemItemID.DisableThermodynamics, "Disable Thermodynamics",
                keywords: "heat thermal temperature");
            // The status strip lists only these switches. Other cheat system values are either unused by the game or,
            // like Part Overheating, gameplay settings that belong to the Settings menu.
        }

        private void RegisterCheat(string group, CheatSystemItemID cheat, string label, string note = null,
            string keywords = null)
        {
            AddEntry(new HubEntry
            {
                Section = Section.Cheats,
                Group = group,
                Label = label,
                Note = note,
                Scene = ToolScene.Campaign,
                IsSwitch = true,
                IsOverride = true,
                NormalValue = GetCheatDefault(cheat),
                IsActive = () => Game.CheatSystem != null && Game.CheatSystem.Get(cheat),
                SetActive = value =>
                {
                    if (Game.CheatSystem != null)
                    {
                        Game.CheatSystem.Set(cheat, value);
                    }
                }
            }, $"cheat {keywords}");
        }

        private void RegisterDifficultyOption(string group, string optionId, string label, string keywords)
        {
            AddEntry(new HubEntry
            {
                Section = Section.Cheats,
                Group = group,
                Label = label,
                Scene = ToolScene.Campaign,
                IsSwitch = true,
                IsOverride = true,
                // Saved with the campaign, so reverting it asks first and says so.
                IsCampaignSetting = true,
                NormalValue = false,
                IsActive = () => Game.SessionManager != null &&
                    Game.SessionManager.TryGetDifficultyOptionState(optionId, out bool isEnabled) && isEnabled,
                // A campaign whose rules lack the option cannot turn it on.
                IsSupported = () => Game.SessionManager != null &&
                    Game.SessionManager.TryGetDifficultyOptionState(optionId, out bool _),
                SetActive = value => Game.SessionManager?.TrySetDifficultyOptionEnabled(optionId, value)
            }, $"cheat {keywords}");
        }

        private static bool GetCheatDefault(CheatSystemItemID cheat)
        {
            foreach (CheatSystem.ItemInfo info in CheatSystem.gItemInfoArray)
            {
                if (info.CheatSystemItemID == cheat)
                    return info.DefaultValue;
            }

            return false;
        }

        #endregion

        #region Status strip

        private void RefreshStatusStrip()
        {
            if (_confirmUntil > 0f && Time.unscaledTime > _confirmUntil)
            {
                CancelConfirm();
            }

            int mask = 0;
            int bit = 1;
            foreach (HubEntry entry in _entries)
            {
                if (!entry.IsOverride)
                    continue;

                if (IsOverridden(entry))
                {
                    mask |= bit;
                }

                bit <<= 1;
            }

            if (mask == _overrideMask)
                return;

            _overrideMask = mask;
            RebuildStatusStrip();
        }

        private static bool IsOverridden(HubEntry entry) => entry.IsActive() != entry.NormalValue;

        private void RebuildStatusStrip()
        {
            _statusList.Clear();
            _overrideNames.Clear();
            foreach (HubEntry entry in _entries)
            {
                if (!entry.IsOverride || !IsOverridden(entry))
                    continue;

                // A cheat that is on in normal play shows as switched off.
                string name = entry.NormalValue ? $"{entry.Label} off" : entry.Label;
                _overrideNames.Add(name);

                var row = new VisualElement();
                row.AddToClassList("dt-override");
                var label = new Label(name);
                label.AddToClassList("dt-override__name");
                row.Add(label);
                var revert = new OabButton { text = "Revert" };
                revert.clicked += () => OnRevertClicked(revert, entry);
                revert.AddToClassList("dt-button");
                row.Add(revert);
                _statusList.Add(row);
            }

            _overrideCount = _overrideNames.Count;
            _statusStrip.style.display = _overrideCount == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            if (_overrideCount == 0)
            {
                _isStatusExpanded = false;
            }

            CancelConfirm();
            ShowStatusList();
        }

        private void OnRevertClicked(Button button, HubEntry entry)
        {
            // A cheat reverts at once. A campaign setting asks first, since the change is saved with the campaign.
            if (entry.IsCampaignSetting && _confirmButton != button)
            {
                StartConfirm(button, entry, $"{GetCampaignChange(entry)} for this campaign. Click Confirm to do it.");
                return;
            }

            entry.SetActive(entry.NormalValue);
            CancelConfirm();
            _nextRefresh = 0f;
        }

        private static string GetCampaignChange(HubEntry entry) =>
            $"Turns {(entry.NormalValue ? "on" : "off")} {entry.Label}";

        private void ToggleStatusList()
        {
            _isStatusExpanded = !_isStatusExpanded;
            ShowStatusList();
        }

        private void ShowStatusList()
        {
            _statusList.style.display = _isStatusExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            _statusExpand.text = _isStatusExpanded ? "Hide" : "Show";
            if (_confirmButton == null)
            {
                ShowStatusSummary();
            }
        }

        private void ShowStatusSummary()
        {
            // The expanded list names every override, so the summary line only counts them then.
            string noun = _overrideCount == 1 ? "override" : "overrides";
            _statusSummary.text = _isStatusExpanded
                ? $"{_overrideCount} {noun} active"
                : $"{_overrideCount} {noun} active: {string.Join(", ", _overrideNames)}";
        }

        private void OnRevertAllClicked()
        {
            // Reverting everything asks once, saying what it will do.
            if (_confirmButton != _revertAll)
            {
                var campaignChanges = new List<string>();
                foreach (HubEntry entry in _entries)
                {
                    if (entry.IsCampaignSetting && IsOverridden(entry))
                    {
                        campaignChanges.Add(entry.Label);
                    }
                }

                string message = _overrideCount == 1
                    ? "Click Confirm to set it back to normal play."
                    : $"Click Confirm to set all {_overrideCount} back to normal play.";
                if (campaignChanges.Count > 0)
                {
                    message += $" Turns off {string.Join(" and ", campaignChanges)} for this campaign.";
                }

                StartConfirm(_revertAll, null, message);
                return;
            }

            foreach (HubEntry entry in _entries)
            {
                if (entry.IsOverride && IsOverridden(entry))
                {
                    entry.SetActive(entry.NormalValue);
                }
            }

            CancelConfirm();
            _nextRefresh = 0f;
        }

        private void StartConfirm(Button button, HubEntry entry, string message)
        {
            CancelConfirm();
            _confirmUntil = Time.unscaledTime + CONFIRM_SECONDS;
            _confirmButton = button;
            _confirmEntry = entry;
            button.text = "Confirm";
            // The theme's destructive OabButton marks the click that changes the game.
            button.AddToClassList("destructive");
            _statusSummary.text = message;
            _statusSummary.AddToClassList("dt-strip__summary--confirm");
        }

        private void CancelConfirm()
        {
            _confirmUntil = 0f;
            if (_confirmButton != null)
            {
                _confirmButton.text = _confirmEntry == null ? "Revert all" : "Revert";
                _confirmButton.RemoveFromClassList("destructive");
                _confirmButton = null;
                _confirmEntry = null;
            }

            if (_statusSummary == null)
                return;

            _statusSummary.RemoveFromClassList("dt-strip__summary--confirm");
            ShowStatusSummary();
        }

        #endregion

        #region Window position

        private void RestoreWindowRect()
        {
            if (!TryParseWindowRect(Configuration.HubWindowRect?.Value, out Vector2 position, out Vector2 size))
            {
                _rootElement.CenterByDefault();
                return;
            }

            if (size.x > 0f && size.y > 0f)
            {
                _rootElement.style.width = size.x;
                _rootElement.style.height = size.y;
            }

            _rootElement.SetDefaultPosition(windowSize =>
            {
                // The screen may be smaller than when the position and size were saved.
                Rect bounds = _rootElement.panel?.visualTree?.contentRect ??
                    new Rect(0, 0, ReferenceResolution.Width, ReferenceResolution.Height);
                if (windowSize.x > bounds.width || windowSize.y > bounds.height)
                {
                    windowSize = Vector2.Min(windowSize, bounds.size);
                    _rootElement.style.width = windowSize.x;
                    _rootElement.style.height = windowSize.y;
                }

                return new Vector2(
                    Mathf.Clamp(position.x, 0f, Mathf.Max(0f, bounds.width - windowSize.x)),
                    Mathf.Clamp(position.y, 0f, Mathf.Max(0f, bounds.height - windowSize.y)));
            });
        }

        private void SaveWindowRect()
        {
            if (Configuration.HubWindowRect == null)
                return;

            Rect layout = _rootElement.layout;
            if (float.IsNaN(layout.width) || layout.width <= 0f)
                return;

            // The size is kept only after the window was resized, so the stylesheet size applies until then.
            bool isResized = _rootElement.style.width.keyword == StyleKeyword.Undefined;
            string value = isResized
                ? string.Format(CultureInfo.InvariantCulture, "{0:0},{1:0},{2:0},{3:0}", layout.x, layout.y,
                    layout.width, layout.height)
                : string.Format(CultureInfo.InvariantCulture, "{0:0},{1:0}", layout.x, layout.y);
            if (Configuration.HubWindowRect.Value != value)
            {
                Configuration.HubWindowRect.Value = value;
            }
        }

        /// <summary>
        /// Reads a window position, and optionally a size, saved as "left,top" or "left,top,width,height".
        /// </summary>
        /// <param name="value">The saved text.</param>
        /// <param name="position">The saved position.</param>
        /// <param name="size">The saved size, or zero when none was saved.</param>
        /// <returns>True if a position was read, false otherwise.</returns>
        internal static bool TryParseWindowRect(string value, out Vector2 position, out Vector2 size)
        {
            position = Vector2.zero;
            size = Vector2.zero;
            if (string.IsNullOrEmpty(value))
                return false;

            string[] parts = value.Split(',');
            if (parts.Length != 2 && parts.Length != 4)
                return false;

            var numbers = new float[parts.Length];
            for (int index = 0; index < parts.Length; index++)
            {
                if (!float.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[index]))
                    return false;
            }

            position = new Vector2(numbers[0], numbers[1]);
            if (parts.Length == 4)
            {
                size = new Vector2(numbers[2], numbers[3]);
            }

            return true;
        }

        #endregion
    }
}

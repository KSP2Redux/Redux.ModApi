using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DebugTools.Runtime.UI;
using KSP.Game.Missions;
using KSP.Game.Missions.Definitions;
using KSP.Game.Missions.State;
using KSP.Messages;
using KSP.Messages.PropertyWatchers;
using KSP.Sim.impl;
using Redux.Missions;
using UitkForKsp2.Controls;
using UnityEngine.UIElements;

namespace DebugTools.Runtime.Controllers
{
    public class MissionsDebugWindowController : BaseWindowController
    {
        private const string FilterAll = "All";
        private const string FilterActive = "Active Only";
        private const string FilterCompleted = "Completed";
        private const string FilterPrimary = "Primary";
        private const string FilterSecondary = "Secondary";
        private const string FilterTutorial = "Tutorial";
        private const string FilterFTUE = "FTUE";

        private static readonly List<string> FilterChoices = new()
        {
            FilterAll, FilterActive, FilterCompleted,
            FilterPrimary, FilterSecondary, FilterTutorial, FilterFTUE
        };

        private DropdownField _typeFilter;
        private TextField _search;
        private Button _refresh;
        private ScrollView _missionsList;
        private VisualElement _details;

        private Label _headerId;
        private Label _headerName;
        private Label _headerType;
        private Label _headerOwner;
        private Label _headerState;

        private Button _btnActivate;
        private Button _btnDeactivate;
        private Button _btnComplete;
        private Button _btnFail;
        private Button _btnReset;
        private Button _btnRemove;

        private Foldout _statusDump;
        private TextField _statusText;
        private ScrollView _stagesList;
        private Label _conditionSummary;
        private VisualElement _conditionTree;
        private int _selectedStageIndex = -1;
        private MissionStage _inspectedStage;
        private MissionData _inspectedMission;
        private Condition _inspectedCondition;
        private readonly Dictionary<Condition, (Foldout group, Label detail)> _conditionViews = new();
        private readonly Dictionary<PropertyCondition, Action<object, object, bool>> _sampleHandlers = new();
        private readonly Dictionary<PropertyCondition, (object current, object target, bool result, DateTime utc, string vessel, float sampledAt, DateTime changedUtc)> _samples = new();

        private readonly List<MissionRow> _rows = new();
        private string _selectedMissionId;
        private bool _selectedIsActive;

        private bool _dirty = true;
        private float _nextRefreshTime;
        private const float RefreshThrottle = 0.5f;

        private void Awake()
        {
            // Persistent, so the window keeps working after a campaign is closed and another one loaded.
            Game.Messages.PersistentSubscribe<NewMissionAvailableMessage>(OnMissionMessage);
            Game.Messages.PersistentSubscribe<MissionCompleteMessage>(OnMissionMessage);
            Game.Messages.PersistentSubscribe<MissionStageCompleted>(OnMissionMessage);
            Game.Messages.PersistentSubscribe<OnMissionStageCompleted>(OnMissionMessage);
            Game.Messages.PersistentSubscribe<GameLoadFinishedMessage>(OnMissionMessage);
            Game.Messages.PersistentSubscribe<GameStateEnteredMessage>(OnMissionMessage);
        }

        private void OnDestroy()
        {
            StopConditionObservation();
            if (Game == null)
                return;

            Game.Messages.Unsubscribe<NewMissionAvailableMessage>(OnMissionMessage);
            Game.Messages.Unsubscribe<MissionCompleteMessage>(OnMissionMessage);
            Game.Messages.Unsubscribe<MissionStageCompleted>(OnMissionMessage);
            Game.Messages.Unsubscribe<OnMissionStageCompleted>(OnMissionMessage);
            Game.Messages.Unsubscribe<GameLoadFinishedMessage>(OnMissionMessage);
            Game.Messages.Unsubscribe<GameStateEnteredMessage>(OnMissionMessage);
        }

        private void OnEnable()
        {
            _typeFilter = RootElement.Q<DropdownField>("type-filter");
            _typeFilter.choices = FilterChoices;
            _typeFilter.value = FilterAll;
            _typeFilter.RegisterValueChangedCallback(_ => _dirty = true);

            _search = RootElement.Q<TextField>("search");
            _search.RegisterValueChangedCallback(_ => _dirty = true);

            _refresh = RootElement.Q<Button>("refresh");
            _refresh.clicked += () => _dirty = true;

            _missionsList = RootElement.Q<ScrollView>("missions-list");
            _missionsList.Clear();

            _details = RootElement.Q<VisualElement>("details");

            _headerId = RootElement.Q<Label>("header-id");
            _headerName = RootElement.Q<Label>("header-name");
            _headerType = RootElement.Q<Label>("header-type");
            _headerOwner = RootElement.Q<Label>("header-owner");
            _headerState = RootElement.Q<Label>("header-state");

            _btnActivate = RootElement.Q<Button>("btn-activate");
            _btnDeactivate = RootElement.Q<Button>("btn-deactivate");
            _btnComplete = RootElement.Q<Button>("btn-complete");
            _btnFail = RootElement.Q<Button>("btn-fail");
            _btnReset = RootElement.Q<Button>("btn-reset");
            _btnRemove = RootElement.Q<Button>("btn-remove");

            _btnActivate.clicked += OnActivateClicked;
            _btnDeactivate.clicked += () => SetSelectedState(MissionState.Inactive);
            _btnComplete.clicked += () => SetSelectedState(MissionState.Complete);
            _btnFail.clicked += () => SetSelectedState(MissionState.Failed);
            _btnReset.clicked += OnResetClicked;
            _btnRemove.clicked += OnRemoveClicked;

            _statusDump = RootElement.Q<Foldout>("status-dump");
            _statusText = RootElement.Q<TextField>("status-text");
            _stagesList = RootElement.Q<ScrollView>("stages-list");
            _conditionSummary = RootElement.Q<Label>("condition-summary");
            _conditionTree = RootElement.Q<VisualElement>("condition-tree");

            ClearDetails();
        }

        private void OnMissionMessage(MessageCenterMessage message)
        {
            if (message is GameLoadFinishedMessage ||
                message is GameStateEnteredMessage { StateBeingEntered: KSP.Game.GameState.MainMenu or KSP.Game.GameState.Loading })
            {
                StopConditionObservation();
                _selectedMissionId = null;
                _rows.Clear();
                if (_conditionTree != null)
                    ClearDetails();
            }
            _dirty = true;
        }

        private void LateUpdate()
        {
            if (!IsWindowOpen)
            {
                StopConditionObservation();
                return;
            }
            if (Game.GlobalGameState.GetState() is KSP.Game.GameState.MainMenu or KSP.Game.GameState.Loading)
            {
                StopConditionObservation();
                _selectedMissionId = null;
                _rows.Clear();
                _missionsList.Clear();
                _dirty = true;
                return;
            }
            if (UnityEngine.Time.unscaledTime < _nextRefreshTime && !_dirty) return;
            _nextRefreshTime = UnityEngine.Time.unscaledTime + RefreshThrottle;

            if (_dirty)
            {
                RebuildMissionList();
                _dirty = false;
            }

            if (!string.IsNullOrEmpty(_selectedMissionId))
                RefreshSelectedDetails();
        }

        private KSP2MissionManager Manager => Game?.KSP2MissionManager;

        private IEnumerable<(MissionData data, bool isActive)> EnumerateMissions()
        {
            var manager = Manager;
            if (manager == null) yield break;

            var activeIds = new HashSet<string>();
            foreach (var active in manager.ActiveMissions)
            {
                if (active?.MissionDatas == null) continue;
                foreach (var md in active.MissionDatas)
                {
                    if (md == null) continue;
                    activeIds.Add(md.ID);
                    yield return (md, true);
                }
            }

            foreach (var def in manager.GetMissionDefinitions())
            {
                if (def == null) continue;
                if (activeIds.Contains(def.ID)) continue;
                yield return (def, false);
            }
        }

        private bool PassesFilter(MissionData data, bool isActive)
        {
            var filter = _typeFilter?.value ?? FilterAll;
            switch (filter)
            {
                case FilterActive:
                    if (!isActive) return false;
                    break;
                case FilterCompleted:
                    if (Manager == null || !Manager.CompletedMissionIDs.Contains(data.ID)) return false;
                    break;
                case FilterPrimary:
                    if (data.type != MissionType.Primary) return false;
                    break;
                case FilterSecondary:
                    if (data.type != MissionType.Secondary) return false;
                    break;
                case FilterTutorial:
                    if (data.type != MissionType.Tutorial) return false;
                    break;
                case FilterFTUE:
                    if (data.type != MissionType.FTUE) return false;
                    break;
            }

            var query = _search?.value;
            if (!string.IsNullOrWhiteSpace(query))
            {
                var q = query.Trim();
                if ((data.ID == null || data.ID.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) < 0) &&
                    (data.name == null || data.name.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) < 0))
                {
                    return false;
                }
            }

            return true;
        }

        private void RebuildMissionList()
        {
            if (_missionsList == null) return;

            _missionsList.Clear();
            _rows.Clear();

            var entries = EnumerateMissions()
                .Where(e => PassesFilter(e.data, e.isActive))
                .OrderBy(e => e.data.type)
                .ThenBy(e => e.data.ID);

            var foundSelected = false;
            foreach (var (data, isActive) in entries)
            {
                var row = new MissionRow();
                BindRow(row, data, isActive);

                var capturedId = data.ID;
                var capturedActive = isActive;
                row.OnSelect = () => SelectMission(capturedId, capturedActive);

                if (capturedId == _selectedMissionId)
                {
                    row.SetSelected(true);
                    foundSelected = true;
                }

                _rows.Add(row);
                _missionsList.Add(row);
            }

            if (!foundSelected)
            {
                _selectedMissionId = null;
                ClearDetails();
            }
        }

        private static void BindRow(MissionRow row, MissionData data, bool isActive)
        {
            row.Id.text = data.ID;
            row.Name.text = string.IsNullOrEmpty(data.name) ? "-" : data.name;
            row.Type.text = data.type.ToString();
            row.Owner.text = $"{data.Owner}";
            var stateText = isActive ? data.state.ToString() : "Inactive";
            row.State.text = stateText;
            row.StageInfo.text = data.missionStages == null || data.missionStages.Count == 0
                ? "-"
                : $"{data.currentStageIndex + 1}/{data.missionStages.Count}";
            row.SetStateClass(stateText);
        }

        private void SelectMission(string id, bool isActive)
        {
            if (_selectedMissionId != id)
            {
                _selectedStageIndex = -1;
                StopConditionObservation();
            }
            _selectedMissionId = id;
            _selectedIsActive = isActive;

            foreach (var row in _rows)
                row.SetSelected(row.Id.text == id);

            RefreshSelectedDetails();
        }

        private bool TryGetSelected(out MissionData data, out bool isActive)
        {
            data = null;
            isActive = false;
            if (string.IsNullOrEmpty(_selectedMissionId) || Manager == null) return false;

            foreach (var am in Manager.ActiveMissions)
            {
                if (am?.MissionDatas == null) continue;
                foreach (var md in am.MissionDatas)
                {
                    if (md?.ID == _selectedMissionId)
                    {
                        data = md;
                        isActive = true;
                        return true;
                    }
                }
            }

            foreach (var def in Manager.GetMissionDefinitions())
            {
                if (def?.ID == _selectedMissionId)
                {
                    data = def;
                    isActive = false;
                    return true;
                }
            }

            return false;
        }

        private void RefreshSelectedDetails()
        {
            if (!TryGetSelected(out var data, out var isActive))
            {
                ClearDetails();
                return;
            }

            _selectedIsActive = isActive;

            _headerId.text = data.ID;
            _headerName.text = string.IsNullOrEmpty(data.name) ? "-" : data.name;
            _headerType.text = data.type.ToString();
            _headerOwner.text = $"{data.Owner}";
            _headerState.text = isActive ? data.state.ToString() : "Not Active";

            _btnActivate.SetEnabled(!isActive || data.state != MissionState.Active);
            _btnDeactivate.SetEnabled(isActive && data.state == MissionState.Active);
            _btnComplete.SetEnabled(isActive && data.state != MissionState.Complete);
            _btnFail.SetEnabled(isActive && data.state != MissionState.Failed);
            _btnReset.SetEnabled(isActive);
            _btnRemove.SetEnabled(isActive);

            if (_statusDump?.value == true)
                _statusText.value = data.GenerateMissionStatus();

            BuildStagesList(data, isActive);
            RefreshConditionInspector(data);
        }

        private void BuildStagesList(MissionData data, bool isActive)
        {
            _stagesList.Clear();
            if (data.missionStages == null) return;

            for (var i = 0; i < data.missionStages.Count; i++)
            {
                var stage = data.missionStages[i];
                var isCurrent = isActive && i == data.currentStageIndex && data.state == MissionState.Active;
                _stagesList.Add(BuildStageRow(data, i, stage, isCurrent, isActive));
            }
        }

        private VisualElement BuildStageRow(MissionData data, int index, MissionStage stage, bool isCurrent, bool isActive)
        {
            var row = new VisualElement();
            row.AddToClassList("stage-row");
            if (isCurrent) row.AddToClassList("stage-row__current");

            if (stage.completed) row.AddToClassList("stage-row__completed");
            else if (stage.active) row.AddToClassList("stage-row__active");
            else row.AddToClassList("stage-row__inactive");

            var indexLabel = new Label($"#{index}");
            indexLabel.AddToClassList("stage-row__index");
            row.Add(indexLabel);

            var nameLabel = new Label(string.IsNullOrEmpty(stage.name) ? $"Stage {stage.StageID}" : stage.name);
            nameLabel.AddToClassList("stage-row__name");
            row.Add(nameLabel);

            var objLabel = new Label(string.IsNullOrEmpty(stage.Objective) ? "-" : stage.Objective);
            objLabel.AddToClassList("stage-row__objective");
            objLabel.tooltip = stage.Objective;
            row.Add(objLabel);

            string statusText;
            if (stage.completed) statusText = "Completed";
            else if (stage.active) statusText = "Active";
            else statusText = "Inactive";
            var statusLabel = new Label(statusText);
            statusLabel.AddToClassList("stage-row__status");
            row.Add(statusLabel);

            var inspectButton = new Button { text = "Inspect" };
            inspectButton.AddToClassList("kui-button");
            inspectButton.AddToClassList("stage-row__btn");
            inspectButton.clicked += () =>
            {
                _selectedStageIndex = index;
                RefreshConditionInspector(data);
            };
            row.Add(inspectButton);

            var activateBtn = new Button { text = "Jump" };
            activateBtn.AddToClassList("kui-button");
            activateBtn.AddToClassList("stage-row__btn");
            activateBtn.AddToClassList("stage-row__activate");
            activateBtn.tooltip = "Jump mission to this stage";
            activateBtn.SetEnabled(isActive);
            activateBtn.clicked += () => OnJumpToStage(data, index);
            row.Add(activateBtn);

            var completeBtn = new Button { text = "Complete" };
            completeBtn.AddToClassList("kui-button");
            completeBtn.AddToClassList("stage-row__btn");
            completeBtn.AddToClassList("stage-row__complete");
            completeBtn.tooltip = isCurrent
                ? "Complete this stage (fires events, advances mission)"
                : "Flag this stage's completed=true (does not fire events)";
            completeBtn.SetEnabled(isActive && !stage.completed);
            completeBtn.clicked += () => OnCompleteStage(data, index, isCurrent);
            row.Add(completeBtn);

            return row;
        }

        private void OnDisable() => StopConditionObservation();

        private void StopConditionObservation()
        {
            foreach (var entry in _sampleHandlers)
            {
                entry.Key.OnDiagnosticSample -= entry.Value;
            }
            _sampleHandlers.Clear();
            _samples.Clear();
            _conditionViews.Clear();
            _inspectedStage = null;
            _inspectedMission = null;
            _inspectedCondition = null;
        }

        private string GetDiagnosticVessel()
        {
            if (Game == null || Game.ViewController == null ||
                !Game.ViewController.TryGetActiveSimVessel(out VesselComponent vessel))
                return "No active vessel";

            return $"{vessel.Name} | {vessel.GlobalId}";
        }

        private void RefreshConditionInspector(MissionData data)
        {
            if (data.missionStages == null || data.missionStages.Count == 0)
                return;

            if (_selectedStageIndex < 0 || _selectedStageIndex >= data.missionStages.Count)
                _selectedStageIndex = Math.Max(0, Math.Min(data.currentStageIndex, data.missionStages.Count - 1));

            MissionStage stage = data.missionStages[_selectedStageIndex];
            if (_inspectedStage != stage || _inspectedCondition != stage.condition)
            {
                StopConditionObservation();
                _inspectedStage = stage;
                _inspectedMission = data;
                _inspectedCondition = stage.condition;
                _conditionTree.Clear();
                BuildConditionTree(stage.condition, _conditionTree, "root");
            }

            foreach (var entry in _conditionViews)
            {
                Condition condition = entry.Key;
                (bool? result, string reason) = ReadConditionResult(condition);
                string state = result.HasValue ? result.Value.ToString() : "Unknown";
                string latch = condition.HasLatchedProgress ? condition.IsLatched.ToString() : "Not applicable";
                entry.Value.detail.text = $"Result: {state} | Latched: {latch}\n{ReadConditionDetail(condition)}";
            }

            (bool? met, string why) = ReadConditionResult(stage.condition);
            string summary = stage.completed ? "Stage already completed" : !stage.active
                ? "Stage inactive. Values are not being evaluated"
                : met == true ? "Condition met. Awaiting mission progression or branch handling"
                : $"Why not complete: {why}";
            if (stage.ProgressScope == MissionProgressScope.Vessel && GetDiagnosticVessel() == "No active vessel")
                summary = "Why not complete: vessel-scoped stage has no active vessel";

            _conditionSummary.text = $"Stage #{_selectedStageIndex} (ID {stage.StageID}) | {stage.ProgressScope}\n{summary}\nActive vessel: {GetDiagnosticVessel()}";
        }

        private void BuildConditionTree(Condition condition, VisualElement parent, string path)
        {
            if (condition == null)
                return;

            string title = condition switch
            {
                ConditionSet set => $"{path}: {set.ConditionMode}",
                PropertyCondition property => $"{path}: {property.PropertyTypeAQNAbbrev} {property.propOperator}",
                EventCondition eventCondition => $"{path}: event {eventCondition.EventTypeAQNAbbrev}",
                _ => $"{path}: {condition.GetType().Name}"
            };
            var group = new Foldout { text = title, value = true };
            group.AddToClassList("kui-group");
            var detail = new Label { enableRichText = false };
            detail.AddToClassList("dt-missions-condition-detail");
            group.Add(detail);
            parent.Add(group);
            _conditionViews[condition] = (group, detail);

            if (condition is PropertyCondition watched)
            {
                Action<object, object, bool> handler = (current, target, result) =>
                {
                    if (!IsWindowOpen)
                        return;

                    float now = UnityEngine.Time.unscaledTime;
                    if (_samples.TryGetValue(watched, out var previous) && now < previous.sampledAt + RefreshThrottle)
                        return;

                    DateTime utc = DateTime.UtcNow;
                    string vessel = GetDiagnosticVessel();
                    bool unchanged = _samples.ContainsKey(watched) && Equals(current, previous.current) &&
                        Equals(target, previous.target) && result == previous.result && vessel == previous.vessel;
                    _samples[watched] = (current, target, result, utc, vessel, now, unchanged ? previous.changedUtc : utc);
                };
                _sampleHandlers[watched] = handler;
                watched.OnDiagnosticSample += handler;
            }

            if (condition is not ConditionSet children)
                return;

            for (int i = 0; i < children.Children.Count; i++)
            {
                BuildConditionTree(children.Children[i], group, $"{path}.{i}");
            }
        }

        private (bool? result, string reason) ReadConditionResult(Condition condition)
        {
            if (condition == null)
                return (true, "No condition");

            if (condition is PropertyCondition property)
            {
                if (property.IsLatched && !property.RequireCurrentValue)
                    return (true, "Latched");

                return _samples.TryGetValue(property, out var sample)
                    ? (sample.result, $"{property.PropertyTypeAQNAbbrev} {property.propOperator} {FormatOperand(sample.target)}")
                    : (null, $"{property.PropertyTypeAQNAbbrev}: waiting for normal evaluation");
            }
            if (condition is EventCondition eventCondition)
                return (condition.IsLatched, $"{eventCondition.EventTypeAQNAbbrev}: waiting for accepted event");
            if (condition is ScriptCondition scriptCondition)
                return (condition.IsLatched, $"{scriptCondition.triggerEventTypeAQN}: waiting for script trigger");

            if (condition is not ConditionSet set)
                return (null, $"{condition.GetType().Name}: unsupported condition");

            int trueCount = 0;
            int unknownCount = 0;
            string firstUnmet = null;
            string firstMet = null;
            foreach (Condition child in set.Children)
            {
                (bool? result, string reason) = ReadConditionResult(child);
                if (result == true)
                {
                    trueCount++;
                    firstMet ??= child is PropertyCondition metProperty ? metProperty.PropertyTypeAQNAbbrev : child?.GetType().Name;
                }
                else
                    firstUnmet ??= reason;
                if (!result.HasValue)
                    unknownCount++;
            }
            bool? combined = set.ConditionMode switch
            {
                LogicalOperator.AND => trueCount + unknownCount < set.Children.Count ? false : unknownCount > 0 ? null : true,
                LogicalOperator.OR => trueCount > 0 ? true : unknownCount > 0 ? null : false,
                LogicalOperator.XOR => trueCount > 1 ? false : unknownCount > 0 ? null : trueCount == 1,
                LogicalOperator.NOT => trueCount > 0 ? false : unknownCount > 0 ? null : true,
                _ => null
            };
            string failure = set.ConditionMode switch
            {
                LogicalOperator.NOT when trueCount > 0 => $"NOT requires {firstMet} to be unmet",
                LogicalOperator.XOR when trueCount > 1 => $"XOR has {trueCount} met children, including {firstMet}. Needs exactly one",
                _ => firstUnmet ?? "No child is met"
            };
            return (combined, failure);
        }

        private string ReadConditionDetail(Condition condition)
        {
            if (condition is EventCondition eventCondition)
                return $"Needs accepted event: {eventCondition.EventTypeAQNAbbrev}\nInput filter: {eventCondition.DiagnosticInput}\nLast accepted event (UTC): {FormatEventTime(eventCondition.LastDiagnosticEventUtc)}";
            if (condition is ScriptCondition script)
                return $"Trigger: {script.triggerEventTypeAQN}\nLast trigger (UTC): {FormatEventTime(script.LastDiagnosticEventUtc)}\nRuntime script handler currently marks success on receipt without executing the script.";
            if (condition is not PropertyCondition property)
                return "Derived from children without calling Evaluate or resetting latches.";

            Type watcherType = property.property?.GetType();
            var metadata = watcherType == null ? null : (PropertyWatcherInfo)Attribute.GetCustomAttribute(watcherType, typeof(PropertyWatcherInfo));
            string target = watcherType != null && typeof(VehiclePropertyWatcher).IsAssignableFrom(watcherType)
                ? "Active vessel at sample time" : "Watcher-defined campaign, UI or input context";
            string threshold = !string.IsNullOrWhiteSpace(property.TestWatchedStringVariable)
                ? $"mission variable {property.TestWatchedStringVariable}" : "configured threshold";
            Type outputType = metadata?.OutputType ?? property.property?.baseType();
            object required = outputType == typeof(string) ? property.TestWatchedstring
                : outputType == typeof(bool) ? true
                : outputType == typeof(double) ? property.TestWatchedValue : (object)property.TestWatchedInt;
            if (!string.IsNullOrWhiteSpace(property.TestWatchedStringVariable))
                required = _inspectedMission.TryGetStringVariable(property.TestWatchedStringVariable, out string variable) ? variable : null;
            string values = _samples.TryGetValue(property, out var sample)
                ? $"Current sampled operand: {FormatOperand(sample.current)} {metadata?.Units}\nNeeds: {FormatOperand(sample.target)} {metadata?.Units}\nSample UTC: {sample.utc:O}\nLast observed operand or result change UTC: {sample.changedUtc:O}\nSample vessel: {sample.vessel}"
                : $"Current operand: unavailable until normal evaluation. A latched condition may no longer poll.\nNeeds: {FormatOperand(required)} {metadata?.Units}";
            return $"{values}\nInput: {(property.isInput ? property.Inputstring : "none")} | Target source: {threshold}\nContext: {target} | Require current: {property.RequireCurrentValue}\nLast event: not applicable (polled property). Samples do not invoke watchers.";
        }

        private static string FormatOperand(object value) => value == null
            ? "Unresolved" : Convert.ToString(value, CultureInfo.InvariantCulture);

        private static string FormatEventTime(DateTime? time) => time.HasValue
            ? time.Value.ToString("O", CultureInfo.InvariantCulture) : "Not recorded this session";

        private void ClearDetails()
        {
            StopConditionObservation();
            _selectedStageIndex = -1;
            _conditionTree.Clear();
            _conditionSummary.text = "Select a mission and inspect a stage.";
            _headerId.text = "-";
            _headerName.text = "-";
            _headerType.text = "-";
            _headerOwner.text = "-";
            _headerState.text = "-";
            _btnActivate.SetEnabled(false);
            _btnDeactivate.SetEnabled(false);
            _btnComplete.SetEnabled(false);
            _btnFail.SetEnabled(false);
            _btnReset.SetEnabled(false);
            _btnRemove.SetEnabled(false);
            _statusText.value = "";
            _stagesList.Clear();
        }

        private void OnActivateClicked()
        {
            if (Manager == null || string.IsNullOrEmpty(_selectedMissionId)) return;

            if (!TryGetSelected(out var data, out var isActive))
            {
                Manager.AddNewActiveMission(_selectedMissionId);
                _dirty = true;
                return;
            }

            if (!isActive)
            {
                Manager.AddNewActiveMission(_selectedMissionId);
            }
            else if (data.state != MissionState.Active)
            {
                Manager.SetMissionState(_selectedMissionId, MissionState.Active);
            }

            _dirty = true;
        }

        private void SetSelectedState(MissionState state)
        {
            if (Manager == null || string.IsNullOrEmpty(_selectedMissionId)) return;
            if (!TryGetSelected(out _, out var isActive) || !isActive) return;

            Manager.SetMissionState(_selectedMissionId, state);
            _dirty = true;
        }

        private void OnResetClicked()
        {
            if (Manager == null || !TryGetSelected(out var data, out var isActive) || !isActive) return;
            Manager.ResetMissionState(data.Owner, data.GetOwnerId(), data.ID);
            _dirty = true;
        }

        private void OnRemoveClicked()
        {
            if (Manager == null || !TryGetSelected(out var data, out var isActive) || !isActive) return;
            Manager.RemoveActiveMission(data);
            _dirty = true;
        }

        private void OnJumpToStage(MissionData data, int targetIndex)
        {
            if (data?.missionStages == null || targetIndex < 0 || targetIndex >= data.missionStages.Count) return;

            var current = data.currentStageIndex;
            if (current >= 0 && current < data.missionStages.Count && data.missionStages[current].active)
                data.missionStages[current].Deactivate();

            data.currentStageIndex = targetIndex;
            data.ReactivateCurrentStage();
            _dirty = true;
        }

        private void OnCompleteStage(MissionData data, int index, bool isCurrent)
        {
            if (data?.missionStages == null || index < 0 || index >= data.missionStages.Count) return;

            if (isCurrent)
            {
                data.OnStageComplete();
            }
            else
            {
                data.missionStages[index].completed = true;
            }

            _dirty = true;
        }
    }
}

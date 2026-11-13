using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KSP.Game;
using KSP.Modules;
using KSP.Sim;
using KSP.Sim.impl;
using KSP.Utilities;
using DebugTools.Utils;
using Redux.Ecs.Structural;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace DebugTools.Runtime.Controllers
{
    public class JointsToolsWindowController : BaseWindowController
    {
        private const float RefreshInterval = 0.15f;

        private Label _vesselName;
        private Label _statPhysics;
        private Label _statSituation;
        private Label _statParts;
        private Label _statMode;
        private Label _statAnalytical;
        private Label _statLegacy;
        private Label _statPhysx;
        private Label _statRigidbodies;
        private Label _statMax;
        private Label _statSolved;
        private Label _statOverride;
        private Label _statGraph;
        private Label _statSelected;
        private Label _mode;
        private Label _rigidityValue;
        private Label _stackRigidityValue;
        private Label _surfaceRigidityValue;

        // The stat values that read "n/a" without an active vessel
        private Label[] _statLabels;

        private Toggle _activeVesselOnly;
        private Toggle _showAnalytical;
        private Toggle _showLegacy;
        private Toggle _showMarkers;
        private Toggle _showStressGauges;
        private Toggle _multiJoints;
        private Toggle _inertiaTensorFix;
        private Toggle _dynamicTensorSolution;
        private Toggle _scaledSolverIteration;
        private Toggle _jointsEnabled;
        private Toggle _extraJoints;

        private VisualElement _headerGauges;

        private DropdownField _sortMode;
        private DropdownField _extraJointsMode;

        private TextField _jointRigidityInput;
        private TextField _stackRigidityInput;
        private TextField _surfaceRigidityInput;
        private Slider _stressOverrideValue;

        private ScrollView _rowsView;
        private Button _setPacked;
        private Button _setUnpacked;
        private Button _setJointRigidity;
        private Button _setStackRigidity;
        private Button _setSurfaceRigidity;
        private Button _clearSelection;
        private Button _setSelectedStress;
        private Button _setAllStress;
        private Button _clearSelectedStress;
        private Button _clearAllStress;

        private readonly List<JointRowController> _rows = new();
        private readonly List<JointRowModel> _models = new();

        private bool _ignoreControlEvents;
        private bool _forceRebuild = true;
        private bool _hasVessel;
        private bool _wasWindowOpen;
        private float _refreshTimer;
        private string _lastRowsSignature = string.Empty;
        private string _lastHighlightedId;

        private VesselComponent _activeVessel;
        private VesselBehavior _activeBehavior;

        private void OnEnable()
        {
            _vesselName = RootElement.Q<Label>("vessel-name");
            _statPhysics = RootElement.Q<Label>("stat-physics");
            _statSituation = RootElement.Q<Label>("stat-situation");
            _statParts = RootElement.Q<Label>("stat-parts");
            _statMode = RootElement.Q<Label>("stat-mode");
            _statAnalytical = RootElement.Q<Label>("stat-analytical");
            _statLegacy = RootElement.Q<Label>("stat-legacy");
            _statPhysx = RootElement.Q<Label>("stat-physx");
            _statRigidbodies = RootElement.Q<Label>("stat-rigidbodies");
            _statMax = RootElement.Q<Label>("stat-max");
            _statSolved = RootElement.Q<Label>("stat-solved");
            _statOverride = RootElement.Q<Label>("stat-override");
            _statGraph = RootElement.Q<Label>("stat-graph");
            _statSelected = RootElement.Q<Label>("stat-selected");
            _mode = RootElement.Q<Label>("mode-value");
            _rigidityValue = RootElement.Q<Label>("rigidity-value");
            _stackRigidityValue = RootElement.Q<Label>("stack-rigidity-value");
            _surfaceRigidityValue = RootElement.Q<Label>("surface-rigidity-value");
            _statLabels = new[]
            {
                _statPhysics, _statSituation, _statParts, _statMode, _statAnalytical, _statLegacy, _statPhysx,
                _statRigidbodies, _statMax, _statSolved, _statOverride, _statGraph
            };
            _rowsView = RootElement.Q<ScrollView>("joint-rows");

            _activeVesselOnly = RootElement.Q<Toggle>("active-vessel-only");
            _showAnalytical = RootElement.Q<Toggle>("show-analytical");
            _showLegacy = RootElement.Q<Toggle>("show-legacy");
            _showMarkers = RootElement.Q<Toggle>("show-markers");
            _showStressGauges = RootElement.Q<Toggle>("show-stress-gauges");
            _multiJoints = RootElement.Q<Toggle>("multi-joints");
            _inertiaTensorFix = RootElement.Q<Toggle>("inertia-tensor-fix");
            _dynamicTensorSolution = RootElement.Q<Toggle>("dynamic-tensor-solution");
            _scaledSolverIteration = RootElement.Q<Toggle>("scaled-solver-iteration");
            _jointsEnabled = RootElement.Q<Toggle>("joints-enabled");
            _extraJoints = RootElement.Q<Toggle>("extra-joints");

            _headerGauges = RootElement.Q<VisualElement>("header-gauges");

            _sortMode = RootElement.Q<DropdownField>("sort-mode");
            _extraJointsMode = RootElement.Q<DropdownField>("extra-joints-mode");

            _jointRigidityInput = RootElement.Q<TextField>("joint-rigidity-input");
            _stackRigidityInput = RootElement.Q<TextField>("stack-rigidity-input");
            _surfaceRigidityInput = RootElement.Q<TextField>("surface-rigidity-input");
            _stressOverrideValue = RootElement.Q<Slider>("stress-override-value");

            _setPacked = RootElement.Q<Button>("set-packed");
            _setUnpacked = RootElement.Q<Button>("set-unpacked");
            _setJointRigidity = RootElement.Q<Button>("set-joint-rigidity");
            _setStackRigidity = RootElement.Q<Button>("set-stack-rigidity");
            _setSurfaceRigidity = RootElement.Q<Button>("set-surface-rigidity");
            _clearSelection = RootElement.Q<Button>("clear-selection");
            _setSelectedStress = RootElement.Q<Button>("set-selected-stress");
            _setAllStress = RootElement.Q<Button>("set-all-stress");
            _clearSelectedStress = RootElement.Q<Button>("clear-selected-stress");
            _clearAllStress = RootElement.Q<Button>("clear-all-stress");

            _activeVesselOnly.RegisterValueChangedCallback(OnActiveVesselOnlyChanged);
            _showAnalytical.RegisterValueChangedCallback(OnShowAnalyticalChanged);
            _showLegacy.RegisterValueChangedCallback(OnShowLegacyChanged);
            _showMarkers.RegisterValueChangedCallback(OnShowMarkersChanged);
            _showStressGauges.RegisterValueChangedCallback(OnShowStressGaugesChanged);
            _multiJoints.RegisterValueChangedCallback(MultiJointsChanged);
            _inertiaTensorFix.RegisterValueChangedCallback(InertiaTensorFixChanged);
            _dynamicTensorSolution.RegisterValueChangedCallback(DynamicTensorSolutionChanged);
            _scaledSolverIteration.RegisterValueChangedCallback(ScaledSolverIterationChanged);
            _jointsEnabled.RegisterValueChangedCallback(JointsEnabledChanged);
            _extraJoints.RegisterValueChangedCallback(ExtraJointsChanged);

            _sortMode.choices = Enum.GetNames(typeof(JointDebugSortMode)).ToList();
            _sortMode.RegisterValueChangedCallback(OnSortModeChanged);

            _extraJointsMode.choices = Enum.GetNames(typeof(Data_ReinforcedConnection.ConnectionType)).ToList();
            _extraJointsMode.RegisterValueChangedCallback(ExtraJointsModeChanged);

            _setPacked.clicked += SetPacked;
            _setUnpacked.clicked += SetUnpacked;
            _setJointRigidity.clicked += SetJointRigidity;
            _setStackRigidity.clicked += SetStackJointRigidity;
            _setSurfaceRigidity.clicked += SetSurfaceJointRigidity;
            _clearSelection.clicked += JointDebugState.ClearSelection;
            _setSelectedStress.clicked += SetSelectedStressOverride;
            _setAllStress.clicked += SetAllStressOverride;
            _clearSelectedStress.clicked += ClearSelectedStressOverride;
            _clearAllStress.clicked += ClearAllStressOverrides;

            RootElement.RegisterCallback<PointerEnterEvent>(OnWindowPointerEnter);
            RootElement.RegisterCallback<PointerLeaveEvent>(OnWindowPointerLeave);
            UITKHelper.AttachTooltip(RootElement.Q<VisualElement>("stress-override"));
            JointDebugState.Changed -= OnJointDebugStateChanged;
            JointDebugState.Changed += OnJointDebugStateChanged;

            // Drop the design-time sample rows
            _rowsView.Clear();
            _rows.Clear();
            _lastRowsSignature = string.Empty;

            SyncControlsFromState();
            _forceRebuild = true;
        }

        private void OnDisable()
        {
            JointDebugState.PointerOverJointsWindow = false;
            JointDebugState.Changed -= OnJointDebugStateChanged;
        }

        private void LateUpdate()
        {
            if (!IsWindowOpen)
            {
                JointDebugState.PointerOverJointsWindow = false;
                // Hiding the game UI with F2 keeps the selection. Only a close drops it.
                if (_wasWindowOpen && !IsRequestedOpen)
                {
                    JointDebugState.ClearSelection();
                    Game?.UI?.OutlineManager?.ClearOutlines();
                    _lastHighlightedId = null;
                    _wasWindowOpen = false;
                }

                return;
            }

            _wasWindowOpen = true;
            UpdateVesselState();
            UpdateSettingsLabels();

            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer <= 0f || _forceRebuild)
            {
                _refreshTimer = RefreshInterval;
                RefreshRows();
            }

            ApplySelectionAndHover();
            SyncControlsFromState();
        }

        private void UpdateVesselState()
        {
            _hasVessel = Game?.ViewController != null &&
                         Game.ViewController.TryGetActiveSimVessel(out _activeVessel);
            _activeBehavior = _hasVessel ? Game.ViewController.GetBehaviorIfLoaded(_activeVessel) : null;

            if (!_hasVessel || _activeVessel == null || _activeBehavior == null)
            {
                _vesselName!.text = "No active vessel";
                foreach (Label stat in _statLabels)
                {
                    stat.text = "n/a";
                }

                _statSelected!.text = "n/a";
                _mode!.text = "n/a";
                return;
            }

            PartOwnerBehavior partOwner = _activeBehavior.PartOwner;
            JointDebugState.EnsureStructuralGraphReady(partOwner);
            StructuralPhysicsService.TryGetSnapshot(partOwner, out StructuralVesselSnapshot snapshot);

            int ownerRb = _activeBehavior.GetComponent<Rigidbody>() == null ? 0 : 1;
            int partRootRb = JointDebugState.CountPartRootRigidbodies(partOwner);
            int configJoints = _activeBehavior.GetComponentsInChildren<ConfigurableJoint>(true).Length;
            int legacyConnections = partOwner.JointConnections.Count();

            _vesselName!.text = _activeVessel.DisplayName;
            _statPhysics!.text = _activeVessel.Physics.ToString();
            _statSituation!.text = _activeVessel.Situation.ToString();
            _statParts!.text = $"{partOwner.Model.PartCount:0}";
            _statMode!.text = snapshot?.Mode.ToString() ?? "Unavailable";
            _statAnalytical!.text = $"{(snapshot?.ConnectionCount ?? 0):0}";
            _statLegacy!.text = $"{legacyConnections:0}";
            _statPhysx!.text = $"{configJoints:0}";
            _statRigidbodies!.text = $"{ownerRb:0}/{partRootRb:0}";
            _statMax!.text = JointDebugState.FormatPercent(snapshot?.MaximumUtilization ?? 0f);
            _statSolved!.text = JointDebugState.FormatPercent(snapshot?.SolvedMaximumUtilization ?? 0f);
            _statOverride!.text = snapshot?.PresentationStressOverrideActive == true ? "active" : "off";
            _statGraph!.text = snapshot?.GraphValid == true ? "valid" : "rebuilding";
            _statSelected!.text = JointDebugState.SelectedConnectionId ?? "none";
            string packing = _activeBehavior.IsUnpacked() ? "Unpacked" : "Packed";
            _mode!.text = _activeVessel.Physics == PhysicsMode.RigidBody ? packing : "n/a";
        }

        private void UpdateSettingsLabels()
        {
            _rigidityValue!.text = $"Rigidity: {PhysicsSettings.JOINT_RIGIDITY:0.0}";
            _stackRigidityValue!.text = $"Stack: {PhysicsSettings.JOINT_STACK_NODE_FACTOR:0.0}";
            _surfaceRigidityValue!.text = $"Surface: {PhysicsSettings.JOINT_SURFACE_NODE_FACTOR:0.0}";
        }

        private void RefreshRows()
        {
            _forceRebuild = false;
            _models.Clear();

            foreach (VesselComponent vessel in JointDebugState.GetTargetVessels())
            {
                VesselBehavior behavior = JointDebugState.GetLoadedBehavior(vessel);
                PartOwnerBehavior partOwner = behavior?.PartOwner;
                if (partOwner == null) continue;

                if (JointDebugState.ShowAnalytical)
                {
                    JointDebugState.EnsureStructuralGraphReady(partOwner);
                    if (!StructuralPhysicsService.TryGetSnapshot(partOwner, out StructuralVesselSnapshot snapshot))
                    {
                        continue;
                    }
                    int order = 0;
                    foreach (StructuralConnectionSnapshot connection in snapshot.Connections)
                    {
                        _models.Add(JointRowModel.FromAnalytical(vessel, connection, order++));
                    }
                }

                if (JointDebugState.ShowLegacy)
                {
                    int order = 0;
                    foreach (PartOwnerBehavior.JointConnection connection in partOwner.JointConnections)
                    {
                        _models.Add(JointRowModel.FromLegacy(vessel, connection, order++));
                    }
                }
            }

            SortModels(_models);
            SyncRows(_models);
        }

        private JointRowModel GetSelectedAnalyticalRow()
        {
            return _models.FirstOrDefault(model => model.IsAnalytical &&
                model.Id == JointDebugState.SelectedConnectionId);
        }

        private void SetSelectedStressOverride()
        {
            JointRowModel selected = GetSelectedAnalyticalRow();
            if (_activeBehavior?.PartOwner == null || selected == null)
                return;

            StructuralPhysicsService.SetPresentationStressOverride(_activeBehavior.PartOwner,
                selected.StructuralConnectionId, Mathf.Clamp01(_stressOverrideValue.value), out _);
            _forceRebuild = true;
        }

        private void SetAllStressOverride()
        {
            if (_activeBehavior?.PartOwner == null)
                return;

            StructuralPhysicsService.SetPresentationStressOverride(_activeBehavior.PartOwner,
                null, Mathf.Clamp01(_stressOverrideValue.value), out _);
            _forceRebuild = true;
        }

        private void ClearSelectedStressOverride()
        {
            JointRowModel selected = GetSelectedAnalyticalRow();
            if (_activeBehavior?.PartOwner == null || selected == null)
                return;

            StructuralPhysicsService.ClearPresentationStressOverride(_activeBehavior.PartOwner,
                selected.StructuralConnectionId, out _);
            _forceRebuild = true;
        }

        private void ClearAllStressOverrides()
        {
            if (_activeBehavior?.PartOwner == null)
                return;

            StructuralPhysicsService.ClearPresentationStressOverride(_activeBehavior.PartOwner, null, out _);
            _forceRebuild = true;
        }

        private static void SortModels(List<JointRowModel> models)
        {
            if (JointDebugState.SortMode == JointDebugSortMode.TreeOrder)
            {
                models.Sort((a, b) =>
                {
                    int vesselCompare = string.CompareOrdinal(a.VesselName, b.VesselName);
                    return vesselCompare != 0 ? vesselCompare : a.TreeOrder.CompareTo(b.TreeOrder);
                });
                return;
            }

            models.Sort((a, b) =>
            {
                int utilCompare = b.Utilization.CompareTo(a.Utilization);
                return utilCompare != 0 ? utilCompare : a.TreeOrder.CompareTo(b.TreeOrder);
            });
        }

        private void SyncRows(List<JointRowModel> models)
        {
            string signature = string.Join("|", models.Select(model => model.Id));
            if (signature != _lastRowsSignature)
            {
                _lastRowsSignature = signature;
                _rowsView!.Clear();
                _rows.Clear();

                foreach (JointRowModel model in models)
                {
                    JointRowController row = new(model);
                    row.Root.RegisterCallback<PointerEnterEvent>(_ => OnRowPointerEnter(row));
                    row.Root.RegisterCallback<PointerLeaveEvent>(_ => OnRowPointerLeave(row));
                    row.Root.RegisterCallback<PointerDownEvent>(_ => OnRowPointerDown(row));
                    _rows.Add(row);
                    _rowsView.Add(row.Root);
                }
            }

            _headerGauges!.style.display = JointDebugState.ShowStressGauges ? DisplayStyle.Flex : DisplayStyle.None;
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].SyncTo(models[i], JointDebugState.ShowStressGauges);
            }
        }

        private void ApplySelectionAndHover()
        {
            string activeId = JointDebugState.SelectedConnectionId ?? JointDebugState.HoveredConnectionId;
            JointRowController activeRow = null;

            foreach (JointRowController row in _rows)
            {
                bool selected = row.Model.Id == JointDebugState.SelectedConnectionId;
                bool hovered = row.Model.Id == JointDebugState.HoveredConnectionId;
                row.SetState(selected, hovered);
                if ((selected || hovered) && activeRow == null) activeRow = row;
            }

            if (activeRow != null)
            {
                HighlightParts(activeRow.Model.Parent, activeRow.Model.Child);
            }
            else if (_lastHighlightedId != null)
            {
                Game?.UI?.OutlineManager?.ClearOutlines();
            }

            if (activeId != null && activeId != _lastHighlightedId && activeRow != null)
            {
                _rowsView?.ScrollTo(activeRow.Root);
            }

            _lastHighlightedId = activeId;
        }

        private static void HighlightParts(PartBehavior parent, PartBehavior child)
        {
            ObjectOutlinesManager outlineManager = GameManager.Instance?.Game?.UI?.OutlineManager;
            if (outlineManager == null)
                return;

            outlineManager.ClearOutlines();
            if (parent != null) outlineManager.AddOutlineToPart(parent, 0);
            if (child != null)
            {
                int colorIndex = Mathf.Min(1, outlineManager.lineColors.Length - 1);
                outlineManager.AddOutlineToPart(child, colorIndex);
            }
        }

        private void SyncControlsFromState()
        {
            _ignoreControlEvents = true;
            _activeVesselOnly!.SetValueWithoutNotify(JointDebugState.ActiveVesselOnly);
            _showAnalytical!.SetValueWithoutNotify(JointDebugState.ShowAnalytical);
            _showLegacy!.SetValueWithoutNotify(JointDebugState.ShowLegacy);
            _showMarkers!.SetValueWithoutNotify(JointDebugState.ShowMarkers);
            _showStressGauges!.SetValueWithoutNotify(JointDebugState.ShowStressGauges);
            _sortMode!.SetValueWithoutNotify(JointDebugState.SortMode.ToString());
            _multiJoints!.SetValueWithoutNotify(PersistentProfileManager.MultiJointsEnabled);
            _inertiaTensorFix!.SetValueWithoutNotify(PhysicsSettings.ENABLE_INERTIA_TENSOR_SCALING);
            _dynamicTensorSolution!.SetValueWithoutNotify(PhysicsSettings.ENABLE_DYNAMIC_TENSOR_SOLUTION);
            _scaledSolverIteration!.SetValueWithoutNotify(PhysicsSettings.ENABLE_SCALED_SOLVER_ITERATION);
            _jointsEnabled!.SetValueWithoutNotify(!PhysicsSettings.DEBUG_DISABLE_JOINTS);
            _extraJoints!.SetValueWithoutNotify(PersistentProfileManager.EnhancedJointsEnabled);
            _extraJointsMode!.SetValueWithoutNotify(PersistentProfileManager.EnhancedJointsMode.ToString());
            _ignoreControlEvents = false;
        }

        private void OnJointDebugStateChanged()
        {
            _forceRebuild = true;
        }

        private void OnWindowPointerEnter(PointerEnterEvent evt)
        {
            JointDebugState.PointerOverJointsWindow = true;
        }

        private void OnWindowPointerLeave(PointerLeaveEvent evt)
        {
            JointDebugState.PointerOverJointsWindow = false;
        }

        private void OnRowPointerEnter(JointRowController row)
        {
            JointDebugState.SetHoveredConnection(row.Model.Id);
            UITKHelper.FindTooltip(RootElement)?.Show(row.Root);
        }

        private void OnRowPointerLeave(JointRowController row)
        {
            UITKHelper.FindTooltip(RootElement)?.Close(row.Root);
            if (JointDebugState.HoveredConnectionId == row.Model.Id)
            {
                JointDebugState.SetHoveredConnection(null);
            }
        }

        private void OnRowPointerDown(JointRowController row)
        {
            JointDebugState.SetSelectedConnection(row.Model.Id);
        }

        private void OnActiveVesselOnlyChanged(ChangeEvent<bool> evt)
        {
            if (!_ignoreControlEvents) JointDebugState.SetActiveVesselOnly(evt.newValue);
        }

        private void OnShowAnalyticalChanged(ChangeEvent<bool> evt)
        {
            if (!_ignoreControlEvents) JointDebugState.SetShowAnalytical(evt.newValue);
        }

        private void OnShowLegacyChanged(ChangeEvent<bool> evt)
        {
            if (!_ignoreControlEvents) JointDebugState.SetShowLegacy(evt.newValue);
        }

        private void OnShowMarkersChanged(ChangeEvent<bool> evt)
        {
            if (!_ignoreControlEvents) JointDebugState.SetShowMarkers(evt.newValue);
        }

        private void OnShowStressGaugesChanged(ChangeEvent<bool> evt)
        {
            if (!_ignoreControlEvents) JointDebugState.SetShowStressGauges(evt.newValue);
        }

        private void OnSortModeChanged(ChangeEvent<string> evt)
        {
            if (_ignoreControlEvents)
                return;

            if (Enum.TryParse(evt.newValue, out JointDebugSortMode sortMode))
            {
                JointDebugState.SetSortMode(sortMode);
            }
        }

        private void SetPacked()
        {
            if (_hasVessel) _activeBehavior?.DebugForcePackVessel();
        }

        private void SetUnpacked()
        {
            if (_hasVessel) _activeBehavior?.DebugForceUnpackVessel();
        }

        private void SetJointRigidity()
        {
            if (float.TryParse(_jointRigidityInput?.text, out float result)) PhysicsSettings.JOINT_RIGIDITY = result;
        }

        private void SetStackJointRigidity()
        {
            if (float.TryParse(_stackRigidityInput?.text, out float result))
                PhysicsSettings.JOINT_STACK_NODE_FACTOR = result;
        }

        private void SetSurfaceJointRigidity()
        {
            if (float.TryParse(_surfaceRigidityInput?.text, out float result))
                PhysicsSettings.JOINT_SURFACE_NODE_FACTOR = result;
        }

        private static void ExtraJointsChanged(ChangeEvent<bool> evt)
        {
            PersistentProfileManager.EnhancedJointsEnabled = evt.newValue;
        }

        private static void ExtraJointsModeChanged(ChangeEvent<string> evt)
        {
            PersistentProfileManager.EnhancedJointsMode =
                (Data_ReinforcedConnection.ConnectionType)Enum.Parse(typeof(Data_ReinforcedConnection.ConnectionType),
                    evt.newValue);
        }

        // Saves Multi Joints in the player profile, then rebuilds the unpacked vessels.
        private void MultiJointsChanged(ChangeEvent<bool> evt)
        {
            if (_ignoreControlEvents)
                return;

            PersistentProfileManager.MultiJointsEnabled = evt.newValue;
            if (Game?.ViewController == null)
                return;

            RebuildUnpackedVessels();
        }

        private static void InertiaTensorFixChanged(ChangeEvent<bool> evt)
        {
            PhysicsSettings.ENABLE_INERTIA_TENSOR_SCALING = evt.newValue;
        }

        // Sets the physics setting only. No vessel is rebuilt.
        private void DynamicTensorSolutionChanged(ChangeEvent<bool> evt)
        {
            if (!_ignoreControlEvents)
                PhysicsSettings.ENABLE_DYNAMIC_TENSOR_SOLUTION = evt.newValue;
        }

        // Sets the physics setting, then rebuilds the unpacked vessels.
        private void ScaledSolverIterationChanged(ChangeEvent<bool> evt)
        {
            if (_ignoreControlEvents || Game?.ViewController == null)
                return;

            PhysicsSettings.ENABLE_SCALED_SOLVER_ITERATION = evt.newValue;
            RebuildUnpackedVessels();
        }

        // Sets the debug switch for disabled joints, repacks the unpacked vessels and asks the floating origin to snap.
        private void JointsEnabledChanged(ChangeEvent<bool> evt)
        {
            if (_ignoreControlEvents || Game?.ViewController == null)
                return;

            PhysicsSettings.DEBUG_DISABLE_JOINTS = !evt.newValue;

            foreach (VesselComponent vessel in Game.ViewController.Universe.GetAllVessels())
            {
                if (Game.SpaceSimulation.TryGetViewObjectComponent<PartOwnerBehavior>(vessel.SimulationObject,
                        out var partOwner) &&
                    partOwner.ViewObject.Vessel.IsUnpacked())
                {
                    partOwner.ViewObject.Vessel.DebugForcePackVessel();
                }
            }

            Game.UniverseView.PhysicsSpace.FloatingOrigin.IsPendingForceSnap = true;
        }

        private void RebuildUnpackedVessels()
        {
            foreach (VesselComponent vessel in Game.ViewController.Universe.GetAllVessels())
            {
                if (Game.SpaceSimulation.TryGetViewObjectComponent<PartOwnerBehavior>(vessel.SimulationObject,
                        out var partOwner) &&
                    partOwner.ViewObject.Vessel.IsUnpacked())
                {
                    StartCoroutine(CoroutineRebuildVessel(partOwner.ViewObject.Vessel));
                }
            }
        }

        private static IEnumerator CoroutineRebuildVessel(VesselBehavior vessel)
        {
            vessel.DebugForcePackVessel();
            yield return null;
            vessel.DebugForceUnpackVessel();
        }

        private sealed class JointRowModel
        {
            public string Id;
            public string StructuralConnectionId;
            public string Kind;
            public string VesselName;
            public string ParentName;
            public string ChildName;
            public string NodeType;
            public string LegacyType;
            public int TreeOrder;
            public int JointCount;
            public bool IsAnalytical;
            public PartBehavior Parent;
            public PartBehavior Child;
            public float BaseBreakForce;
            public float BaseBreakTorque;
            public float BreakForce;
            public float BreakTorque;
            public float ReinforcementFactor;
            public int ReinforcementSourceCount;
            public float ForceUtilization;
            public float TorqueUtilization;
            public float Utilization;
            public float ForceMagnitude;
            public float TorqueMagnitude;

            public static JointRowModel FromAnalytical(
                VesselComponent vessel,
                StructuralConnectionSnapshot connection,
                int order
            )
            {
                return new JointRowModel
                {
                    Id = JointDebugState.BuildAnalyticalId(vessel, connection),
                    StructuralConnectionId = connection.Id,
                    Kind = "Analytical",
                    VesselName = vessel.DisplayName,
                    ParentName = string.IsNullOrEmpty(connection.ParentName) ? "<none>" : connection.ParentName,
                    ChildName = string.IsNullOrEmpty(connection.ChildName) ? "<none>" : connection.ChildName,
                    NodeType = connection.NodeType.ToString(),
                    LegacyType = string.Empty,
                    TreeOrder = connection.TreeOrder,
                    JointCount = 0,
                    IsAnalytical = true,
                    Parent = JointDebugState.ResolvePart(
                        GameManager.Instance.Game.ViewController.GetBehaviorIfLoaded(vessel)?.PartOwner,
                        connection.ParentGuid),
                    Child = JointDebugState.ResolvePart(
                        GameManager.Instance.Game.ViewController.GetBehaviorIfLoaded(vessel)?.PartOwner,
                        connection.ChildGuid),
                    BaseBreakForce = connection.BaseBreakForce,
                    BaseBreakTorque = connection.BaseBreakTorque,
                    BreakForce = connection.BreakForce,
                    BreakTorque = connection.BreakTorque,
                    ReinforcementFactor = connection.ReinforcementFactor,
                    ReinforcementSourceCount = connection.ReinforcementSourceCount,
                    ForceUtilization = connection.ForceUtilization,
                    TorqueUtilization = connection.TorqueUtilization,
                    Utilization = connection.Utilization,
                    ForceMagnitude = connection.ReactionForce.magnitude,
                    TorqueMagnitude = connection.ReactionTorque.magnitude
                };
            }

            public static JointRowModel FromLegacy(
                VesselComponent vessel,
                PartOwnerBehavior.JointConnection connection,
                int order
            )
            {
                int jointCount = 0;
                if (connection.Joints != null)
                {
                    jointCount = connection.Joints.Count(joint => joint != null && joint.connectedBody != null);
                }

                return new JointRowModel
                {
                    Id = JointDebugState.BuildLegacyId(vessel, connection, order),
                    Kind = "Legacy",
                    VesselName = vessel.DisplayName,
                    ParentName = JointDebugState.GetPartLabel(connection.host),
                    ChildName = JointDebugState.GetPartLabel(connection.target),
                    NodeType = connection.nodeType.ToString(),
                    LegacyType = connection.connectionType.ToString(),
                    TreeOrder = order,
                    JointCount = jointCount,
                    IsAnalytical = false,
                    Parent = connection.host,
                    Child = connection.target,
                    BaseBreakForce = connection.BreakForce,
                    BaseBreakTorque = connection.BreakTorque,
                    BreakForce = connection.BreakForce,
                    BreakTorque = connection.BreakTorque,
                    ReinforcementFactor = 0f,
                    ReinforcementSourceCount = 0,
                    ForceUtilization = 0f,
                    TorqueUtilization = 0f,
                    Utilization = 0f,
                    ForceMagnitude = 0f,
                    TorqueMagnitude = 0f
                };
            }
        }

        private sealed class JointRowController
        {
            public readonly VisualElement Root = new();
            private readonly Label _kind = new();
            private readonly Label _name = new();
            private readonly Label _node = new();
            private readonly Label _magnitudes = new();
            private readonly VisualElement _gauges = new();
            private readonly Gauge _forceGauge = new("F");
            private readonly Gauge _torqueGauge = new("T");
            private readonly Gauge _maxGauge = new("U");

            public JointRowModel Model { get; private set; }

            // One 20px dt-table line. The limits and the full load text sit in the row tooltip.
            public JointRowController(JointRowModel model)
            {
                Model = model;
                Root.AddToClassList("joint-row");
                Root.AddToClassList("dt-table__row");

                AddCell(_kind, "joint-kind");
                AddCell(_name, "joint-name");
                AddCell(_node, "joint-node");
                AddCell(_magnitudes, "joint-magnitudes");
                _gauges.AddToClassList("joint-gauges");
                _gauges.Add(_forceGauge.Root);
                _gauges.Add(_torqueGauge.Root);
                _gauges.Add(_maxGauge.Root);
                Root.Add(_gauges);
                SyncTo(model, true);
            }

            private void AddCell(Label cell, string className)
            {
                cell.AddToClassList("dt-table__cell");
                cell.AddToClassList(className);
                Root.Add(cell);
            }

            public void SyncTo(JointRowModel model, bool showGauges)
            {
                Model = model;
                _kind.text = model.IsAnalytical ? "A" : $"L:{model.LegacyType}";
                _name.text = $"{model.ParentName} -> {model.ChildName}";
                _node.text = $"{model.NodeType}  #{model.TreeOrder:0}";
                if (!model.IsAnalytical) _node.text += $"  joints:{model.JointCount:0}";
                string load = $"F:{model.ForceMagnitude:0}N T:{model.TorqueMagnitude:0}Nm";
                string max = $"Max:{JointDebugState.FormatPercent(model.Utilization)}";
                _magnitudes.text = showGauges ? load : $"{load} {max}";
                // The B: prefix keeps the tooltip from reading a part name as a position hint
                Root.tooltip = $"B:{_name.text}\n{GetLimitText(model)}\nLoad {load} {max}";
                _gauges.style.display = showGauges ? DisplayStyle.Flex : DisplayStyle.None;
                _forceGauge.Set(model.ForceUtilization);
                _torqueGauge.Set(model.TorqueUtilization);
                _maxGauge.Set(model.Utilization);
            }

            public void SetState(bool selected, bool hovered)
            {
                Root.EnableInClassList("selected", selected);
                Root.EnableInClassList("hovered", hovered);
            }

            private static string GetLimitText(JointRowModel model)
            {
                if (model.IsAnalytical && model.ReinforcementSourceCount > 0)
                {
                    return $"Limits F:{JointDebugState.FormatLimit(model.BaseBreakForce)}->{JointDebugState.FormatLimit(model.BreakForce)}N " +
                        $"T:{JointDebugState.FormatLimit(model.BaseBreakTorque)}->{JointDebugState.FormatLimit(model.BreakTorque)}Nm " +
                        $"Reinf:+{JointDebugState.FormatPercent(model.ReinforcementFactor)} src:{model.ReinforcementSourceCount:0}";
                }

                return
                    $"Limits F:{JointDebugState.FormatLimit(model.BreakForce)}N T:{JointDebugState.FormatLimit(model.BreakTorque)}Nm";
            }
        }

        private sealed class Gauge
        {
            public readonly VisualElement Root = new();
            private readonly Label _label = new();
            private readonly VisualElement _bar = new();
            private readonly VisualElement _fill = new();
            private readonly Label _value = new();

            public Gauge(string label)
            {
                Root.AddToClassList("joint-gauge");
                _label.AddToClassList("joint-gauge-label");
                _bar.AddToClassList("joint-gauge-bar");
                _fill.AddToClassList("joint-gauge-fill");
                _value.AddToClassList("joint-gauge-value");
                _label.text = label;
                _bar.Add(_fill);
                Root.Add(_label);
                Root.Add(_bar);
                Root.Add(_value);
            }

            public void Set(float utilization)
            {
                float clamped = Mathf.Clamp01(utilization);
                _fill.style.width = Length.Percent(clamped * 100f);
                _fill.style.backgroundColor = JointDebugState.GetStressColor(utilization);
                _value.text = JointDebugState.FormatPercent(utilization);
            }
        }
    }
}

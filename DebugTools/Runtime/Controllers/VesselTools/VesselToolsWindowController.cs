using System;
using System.Collections.Generic;
using System.Linq;
using DebugShapes;
using KSP.Game;
using KSP.Messages;
using KSP.Modules;
using KSP.Sim;
using KSP.Sim.impl;
using UnityEngine;
using UnityEngine.UIElements;

namespace DebugTools.Runtime.Controllers.VesselTools
{
    public class VesselToolsWindowController : BaseWindowController
    {
        private bool _initialized;

        // Flight axes
        private Toggle? _showControlPoints;
        private readonly List<DebugAxes> _vesselControlAxes = new();
        private bool _isControlPointsShowing;

        private Toggle? _cpNavballRotation;
        private readonly Quaternion _navballRotation = Quaternion.Euler(-90f, 0f, 0f);
        private bool _isControlPointsRotated = true;

        private Toggle? _showSASTargets;
        private readonly List<DebugArrow> _vesselSASArrows = new();
        private readonly Color _sasActiveColor = new(0.6f, 0f, 0.6f);
        private bool _isSASTargetsShowing;

        private Toggle? _showOrbitPoints;
        private readonly List<DebugAxes> _vesselOrbitAxes = new();
        private bool _isOrbitPointsShowing;

        private Toggle? _showJoints;

        // Buoyancy
        private Toggle? _showPartsBounds;
        private Toggle? _showCoB;

        // Misc
        private Toggle? _showCoMMarkers;
        private readonly List<DebugSphere> _comMarkers = new();
        private Slider? _markerSize;

        private Toggle? _showPhysicsForce;
        private bool _isPhysicsForceShowing;

        private GameState _state;
        private ViewController? _view;

        private List<VesselComponent> _vessels = new();


        private MessageCenter _messageCenter;

        private void Awake()
        {
            _messageCenter = Game.Messages;
            // Refresh overlay state after campaign transitions.
            _messageCenter.PersistentSubscribe<GameStateChangedMessage>(OnStateChanged);
            _messageCenter.PersistentSubscribe<GameLoadFinishedMessage>(OnStateChanged);
            Refresh();
        }

        private void OnDestroy()
        {
            if (_messageCenter != null)
            {
                _messageCenter.Unsubscribe<GameStateChangedMessage>(OnStateChanged);
                _messageCenter.Unsubscribe<GameLoadFinishedMessage>(OnStateChanged);
            }
        }

        private void OnStateChanged(MessageCenterMessage msg)
        {
            Refresh();
        }

        /// <inheritdoc />
        public override void CatchUpWithLoadedGame() => Refresh();

        private void Refresh()
        {
            if (!_initialized) return;

            _state = Game.GlobalGameState.GetGameState().GameState;
            _view = Game.ViewController;

            _showJoints?.SetValueWithoutNotify(JointDebugState.ShowMarkers);

            _isPhysicsForceShowing = Game.PhysicsForceDisplaySystem.IsDisplayed;
            _showPhysicsForce!.value = _isPhysicsForceShowing;

            if (_state == GameState.FlightView || _state == GameState.Map3DView)
            {
                return;
            }

            // Cleanup stuff when not in flight
            DestroyControlOrientations();
            DestroySASTargets();
            DestroyOrbitOrientations();

            _showControlPoints!.value = false;
            _showSASTargets!.value = false;
            _showJoints!.value = false;
            _showCoMMarkers!.value = false;

            Module_Drag.ShowDragDebug = _isPhysicsForceShowing;
            Module_LiftingSurface.ShowPAMDebug = _isPhysicsForceShowing;
        }

        private void OnEnable()
        {
            Enable();

            // Flight axes
            _showControlPoints = RootElement.Q<Toggle>("show-control-points");
            _showControlPoints.RegisterValueChangedCallback(OnShowControlPointsChanged);

            _cpNavballRotation = RootElement.Q<Toggle>("cp-follow-navball");
            _cpNavballRotation.value = _isControlPointsRotated;
            _cpNavballRotation.RegisterValueChangedCallback(OnCPNavballRotationChanged);

            _showSASTargets = RootElement.Q<Toggle>("show-sas");
            _showSASTargets.RegisterValueChangedCallback(OnShowSASTargetsChanged);

            _showOrbitPoints = RootElement.Q<Toggle>("show-orbit-points");
            _showOrbitPoints.RegisterValueChangedCallback(OnShowOrbitPointsChanged);

            _showJoints = RootElement.Q<Toggle>("show-joints");
            _showJoints.SetValueWithoutNotify(JointDebugState.ShowMarkers);
            _showJoints.RegisterValueChangedCallback(OnShowJointsChanged);
            JointDebugState.Changed -= OnJointDebugStateChanged;
            JointDebugState.Changed += OnJointDebugStateChanged;

            // Buoyancy
            _showPartsBounds = RootElement.Q<Toggle>("parts-bounds");
            _showPartsBounds.RegisterValueChangedCallback(OnShowPartsBoundsChanged);

            _showCoB = RootElement.Q<Toggle>("show-cob");
            _showCoB.RegisterValueChangedCallback(OnShowCoBChanged);

            // Misc
            _showCoMMarkers = RootElement.Q<Toggle>("show-com-markers");

            _markerSize = RootElement.Q<Slider>("com-markers-size");

            _showPhysicsForce = RootElement.Q<Toggle>("show-physics-force");
            _showPhysicsForce.RegisterValueChangedCallback(OnShowPhysicsForceChanged);

            _initialized = true;
            Refresh();
        }

        private void OnDisable()
        {
            JointDebugState.Changed -= OnJointDebugStateChanged;
        }

        private void LateUpdate()
        {
            if (!IsWindowOpen)
                return;

            UpdateVesselsCoM();
        }

        private void OnShowControlPointsChanged(ChangeEvent<bool> evt)
        {
            if (evt.newValue == _isControlPointsShowing) return;

            if (evt.newValue)
                CreateControlOrientations();
            else
                DestroyControlOrientations();
        }

        private void CreateControlOrientations()
        {
            DestroyControlOrientations();
            _vesselControlAxes.Clear();

            if (_view == null) return;

            _isControlPointsShowing = true;

            _vessels = _view.Universe.GetAllVessels();
            foreach (var vessel in _vessels)
            {
                var behavior = _view.GetBehaviorIfLoaded(vessel);
                if (behavior == null) continue;

                var axes = new DebugAxes(vessel.DisplayName + "_Ctrl", behavior.transform, Vector3.zero, 2f, 0.15f,
                    Color.blue, Color.green, Color.red);
                axes.SetupTracker(vessel.SimulationObject, UpdateVesselControl, true);
                axes.Tracker.RotationOffset = _isControlPointsRotated ? _navballRotation : Quaternion.identity;
                _vesselControlAxes.Add(axes);
            }
        }

        private void DestroyControlOrientations()
        {
            var count = _vesselControlAxes.Count;
            while (count-- > 0)
            {
                _vesselControlAxes[count].Destroy(UpdateVesselControl);
                _vesselControlAxes.RemoveAt(count);
            }

            _isControlPointsShowing = false;
        }

        private static void UpdateVesselControl(ITransformModel t, SimulationObjectModel o)
        {
            if (o?.Vessel != null)
            {
                t.UpdatePosition(o.Vessel.ControlTransform.Position);
                t.UpdateRotation(o.Vessel.ControlTransform.Rotation);
            }
        }

        private void OnCPNavballRotationChanged(ChangeEvent<bool> evt)
        {
            if (evt.newValue == _isControlPointsRotated) return;

            foreach (var axis in _vesselControlAxes)
                axis.Tracker.RotationOffset = evt.newValue ? _navballRotation : Quaternion.identity;

            _isControlPointsRotated = evt.newValue;
        }

        private void OnShowSASTargetsChanged(ChangeEvent<bool> evt)
        {
            if (evt.newValue == _isSASTargetsShowing) return;

            if (evt.newValue)
                CreateSASTargets();
            else
                DestroySASTargets();
        }

        private void CreateSASTargets()
        {
            DestroySASTargets();
            _vesselSASArrows.Clear();

            if (_view == null) return;

            _isSASTargetsShowing = true;

            _vessels = _view.Universe.GetAllVessels();
            foreach (var vessel in _vessels)
            {
                var behavior = _view.GetBehaviorIfLoaded(vessel);
                if (behavior == null) continue;

                var arrow = new DebugArrow(vessel.DisplayName + "_SAS", behavior.transform, _sasActiveColor,
                    Vector3.zero, Vector3.forward, 4f, 0.15f);
                arrow.UseWorldSpace = false;
                arrow.SetupTracker(vessel.SimulationObject, UpdateSASVectors, true);
                arrow.Tracker.RotationOffset = _navballRotation;
                _vesselSASArrows.Add(arrow);
            }
        }

        private void DestroySASTargets()
        {
            var count = _vesselSASArrows.Count;
            while (count-- > 0)
            {
                _vesselSASArrows[count].Destroy(UpdateSASVectors);
                _vesselSASArrows.RemoveAt(count);
            }

            _isSASTargetsShowing = false;
        }

        private void UpdateSASVectors(ITransformModel t, SimulationObjectModel o)
        {
            var vessel = o.Vessel;
            if (vessel?.Autopilot?.SAS == null) return;

            t.UpdatePosition(vessel.ControlTransform.Position);

            switch (vessel.speedMode)
            {
                case SpeedDisplayMode.Orbit:
                    UpdateSASOrbit(t, vessel, o.Telemetry);
                    break;
                case SpeedDisplayMode.Surface:
                    UpdateSASSurface(t, vessel, o.Telemetry);
                    break;
                case SpeedDisplayMode.Target:
                    UpdateSASTarget(t, vessel, o.Telemetry);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void UpdateSASOrbit(ITransformModel t, VesselComponent vessel, TelemetryComponent telemetry)
        {
            switch (vessel.Autopilot.AutopilotMode)
            {
                case AutopilotMode.StabilityAssist:
                    t.UpdateRotation(
                        Game.UniverseView.PhysicsSpace.PhysicsToRotation(vessel.Autopilot.SAS.LockedRotation));
                    break;
                case AutopilotMode.Prograde:
                    t.UpdateRotation(Rotation.LookRotation(telemetry.OrbitMovementNormal,
                        telemetry.OrbitMovementPrograde));
                    break;
                case AutopilotMode.Retrograde:
                    t.UpdateRotation(Rotation.LookRotation(telemetry.OrbitMovementAntiNormal,
                        telemetry.OrbitMovementRetrograde));
                    break;
                case AutopilotMode.Normal:
                    t.UpdateRotation(Rotation.LookRotation(telemetry.OrbitMovementRetrograde,
                        telemetry.OrbitMovementNormal));
                    break;
                case AutopilotMode.Antinormal:
                    t.UpdateRotation(Rotation.LookRotation(telemetry.OrbitMovementPrograde,
                        telemetry.OrbitMovementAntiNormal));
                    break;
                case AutopilotMode.RadialIn:
                    t.UpdateRotation(Rotation.LookRotation(telemetry.OrbitMovementPrograde,
                        telemetry.OrbitMovementRadialIn));
                    break;
                case AutopilotMode.RadialOut:
                    t.UpdateRotation(Rotation.LookRotation(telemetry.OrbitMovementRetrograde,
                        telemetry.OrbitMovementRadialOut));
                    break;
                case AutopilotMode.Target:
                    t.UpdateRotation(Rotation.LookRotation(telemetry.OrbitMovementNormal,
                        telemetry.TargetDirection));
                    break;
                case AutopilotMode.AntiTarget:
                    t.UpdateRotation(Rotation.LookRotation(telemetry.OrbitMovementNormal,
                        telemetry.AntiTargetDirection));
                    break;
                case AutopilotMode.Maneuver:
                    t.UpdateRotation(Rotation.LookRotation(telemetry.OrbitMovementNormal,
                        telemetry.ManeuverDirection));
                    break;
                case AutopilotMode.Navigation:
                case AutopilotMode.Autopilot:
                default:
                    break;
            }
        }

        private void UpdateSASSurface(ITransformModel t, VesselComponent vessel, TelemetryComponent telemetry)
        {
            switch (vessel.Autopilot.AutopilotMode)
            {
                case AutopilotMode.StabilityAssist:
                    t.UpdateRotation(
                        Game.UniverseView.PhysicsSpace.PhysicsToRotation(vessel.Autopilot.SAS.LockedRotation));
                    break;
                case AutopilotMode.Prograde:
                    t.UpdateRotation(Rotation.LookRotation(telemetry.OrbitMovementNormal,
                        telemetry.SurfaceMovementPrograde));
                    break;
                case AutopilotMode.Retrograde:
                    t.UpdateRotation(Rotation.LookRotation(telemetry.OrbitMovementAntiNormal,
                        telemetry.SurfaceMovementRetrograde));
                    break;
                case AutopilotMode.Normal:
                    t.UpdateRotation(Rotation.LookRotation(telemetry.HorizonDown,
                        telemetry.HorizonNorth));
                    break;
                case AutopilotMode.Antinormal:
                    t.UpdateRotation(Rotation.LookRotation(telemetry.HorizonUp,
                        telemetry.HorizonSouth));
                    break;
                case AutopilotMode.RadialIn:
                    t.UpdateRotation(Rotation.LookRotation(telemetry.HorizonNorth,
                        telemetry.HorizonDown));
                    break;
                case AutopilotMode.RadialOut:
                    t.UpdateRotation(Rotation.LookRotation(telemetry.HorizonSouth,
                        telemetry.HorizonUp));
                    break;
                case AutopilotMode.Target:
                    t.UpdateRotation(Rotation.LookRotation(telemetry.HorizonNorth,
                        telemetry.TargetDirection));
                    break;
                case AutopilotMode.AntiTarget:
                    t.UpdateRotation(Rotation.LookRotation(telemetry.HorizonNorth,
                        telemetry.AntiTargetDirection));
                    break;
                case AutopilotMode.Maneuver:
                    t.UpdateRotation(Rotation.LookRotation(telemetry.HorizonNorth,
                        telemetry.ManeuverDirection));
                    break;
                case AutopilotMode.Navigation:
                case AutopilotMode.Autopilot:
                default:
                    break;
            }
        }

        private void UpdateSASTarget(ITransformModel t, VesselComponent vessel, TelemetryComponent telemetry)
        {
            switch (vessel.Autopilot.AutopilotMode)
            {
                case AutopilotMode.StabilityAssist:
                    t.UpdateRotation(
                        Game.UniverseView.PhysicsSpace.PhysicsToRotation(vessel.Autopilot.SAS.LockedRotation));
                    break;
                case AutopilotMode.Prograde:
                    t.UpdateRotation(Rotation.LookRotation(telemetry.OrbitMovementNormal,
                        telemetry.TargetPrograde));
                    break;
                case AutopilotMode.Retrograde:
                    t.UpdateRotation(Rotation.LookRotation(telemetry.OrbitMovementAntiNormal,
                        telemetry.TargetRetrograde));
                    break;
                case AutopilotMode.Normal:
                    t.UpdateRotation(Rotation.LookRotation(telemetry.TargetRetrograde,
                        telemetry.HorizonNorth));
                    break;
                case AutopilotMode.Antinormal:
                    t.UpdateRotation(Rotation.LookRotation(telemetry.TargetPrograde,
                        telemetry.HorizonSouth));
                    break;
                case AutopilotMode.RadialIn:
                    t.UpdateRotation(Rotation.LookRotation(telemetry.TargetPrograde,
                        telemetry.HorizonDown));
                    break;
                case AutopilotMode.RadialOut:
                    t.UpdateRotation(Rotation.LookRotation(telemetry.TargetRetrograde,
                        telemetry.HorizonUp));
                    break;
                case AutopilotMode.Target:
                    t.UpdateRotation(Rotation.LookRotation(telemetry.HorizonNorth,
                        telemetry.TargetDirection));
                    break;
                case AutopilotMode.AntiTarget:
                    t.UpdateRotation(Rotation.LookRotation(telemetry.HorizonNorth,
                        telemetry.AntiTargetDirection));
                    break;
                case AutopilotMode.Maneuver:
                    t.UpdateRotation(Rotation.LookRotation(telemetry.HorizonNorth,
                        telemetry.ManeuverDirection));
                    break;
                case AutopilotMode.Navigation:
                case AutopilotMode.Autopilot:
                default:
                    break;
            }
        }

        private void OnShowOrbitPointsChanged(ChangeEvent<bool> evt)
        {
            if (evt.newValue == _isOrbitPointsShowing) return;

            if (evt.newValue)
                CreateOrbitOrientations();
            else
                DestroyOrbitOrientations();
        }

        private void CreateOrbitOrientations()
        {
            DestroyOrbitOrientations();
            _vesselOrbitAxes.Clear();

            if (_view == null) return;

            _isOrbitPointsShowing = true;

            _vessels = _view.Universe.GetAllVessels();
            foreach (var vessel in _vessels)
            {
                var behavior = _view.GetBehaviorIfLoaded(vessel);
                if (!(behavior != null)) continue;

                var axes = new DebugAxes(vessel.DisplayName + "_Orbit", behavior.transform, Vector3.zero, 2f, 0.15f,
                    Color.green, Color.magenta, Color.cyan);
                axes.SetupTracker(vessel.SimulationObject, Tracker_OnUpdate, true);
                _vesselOrbitAxes.Add(axes);
            }
        }

        private void DestroyOrbitOrientations()
        {
            var count = _vesselOrbitAxes.Count;
            while (count-- > 0)
            {
                _vesselOrbitAxes[count].Destroy(Tracker_OnUpdate);
                _vesselOrbitAxes.RemoveAt(count);
            }

            _isOrbitPointsShowing = false;
        }

        private static void Tracker_OnUpdate(ITransformModel t, SimulationObjectModel o)
        {
            var vessel = o?.Vessel;
            if (vessel != null)
                t.UpdatePosition(vessel.CenterOfMass);

            var telemetry = o?.Telemetry;
            if (telemetry != null)
                t.UpdateRotation(telemetry.OrbitMovementRotation);
        }

        private void OnShowJointsChanged(ChangeEvent<bool> evt)
        {
            JointDebugState.SetShowMarkers(evt.newValue);
        }

        private void OnJointDebugStateChanged()
        {
            _showJoints?.SetValueWithoutNotify(JointDebugState.ShowMarkers);
        }

        private void UpdateJointVisualizations()
        {
            if (_view == null || _showJoints == null || !_showJoints.value || _state != GameState.FlightView) return;

            _vessels = _view.Universe.GetAllVessels();
            foreach (var vessel in _vessels)
            {
                if (Game.SpaceSimulation.TryGetViewObjectComponent<PartOwnerBehavior>(vessel.SimulationObject,
                        out var behavior))
                {
                    UpdateVesselJointsVisualizations(behavior);
                }
            }
        }

        private void UpdateVesselJointsVisualizations(PartOwnerBehavior behavior)
        {
            var connections = behavior.JointConnections.ToList();

            foreach (var connection in connections)
            {
                if (connection?.visualJoints == null || connection.visualJoints.Length == 0 ||
                    connection?.Joints == null)
                    continue;

                var num = 0;
                foreach (var joint in connection.Joints)
                {
                    var sphere = connection.visualJoints[num++];
                    if (sphere == null) continue;

                    sphere.Enabled = _showJoints!.value;
                    if (sphere.Enabled)
                        sphere.Center = joint.connectedBody.transform.TransformPoint(joint.connectedAnchor);
                }
            }
        }

        private static void OnShowPartsBoundsChanged(ChangeEvent<bool> evt)
        {
            DebugVisualizer.RenderPartDragBounds = evt.newValue;
        }

        private void OnShowCoBChanged(ChangeEvent<bool> evt)
        {
            if (_view == null) return;

            _vessels = _view.Universe.GetAllVessels();
            foreach (var vessel in _vessels)
            foreach (var part in _view.GetBehaviorIfLoaded(vessel).parts)
                part.SetBuoyancyDebugForcePos(evt.newValue);
        }

        private void CreateCoMMarkers()
        {
            _comMarkers.Clear();

            if (_view == null) return;

            _vessels = _view.Universe.GetAllVessels();
            foreach (var vessel in _vessels)
            {
                var marker = new DebugSphere(vessel.Name + "_CoM", transform, Color.yellow, Vector3.zero, 1f)
                {
                    Enabled = _showCoMMarkers?.value ?? false
                };

                _comMarkers.Add(marker);
            }
        }

        private void UpdateVesselsCoM()
        {
            if (_view == null || _showCoMMarkers == null || _markerSize == null) return;

            if (_state != GameState.FlightView && _state != GameState.Launchpad && _state != GameState.Runway) return;

            _vessels = _view.Universe.GetAllVessels();

            if (_vessels.Count != _comMarkers.Count)
                CreateCoMMarkers();


            var i = 0;
            foreach (var vessel in _vessels)
            {
                _comMarkers[i].Enabled = _showCoMMarkers.value;
                _comMarkers[i].Center = Game.UniverseView.PhysicsSpace.PositionToPhysics(vessel.CenterOfMass);
                _comMarkers[i].Radius = _markerSize.value;
                ++i;
            }
        }

        private void OnShowPhysicsForceChanged(ChangeEvent<bool> evt)
        {
            if (_isPhysicsForceShowing == evt.newValue) return;

            Game.PhysicsForceDisplaySystem.TogglePhysicsForceDisplay();
            Module_Drag.ShowDragDebug = evt.newValue;
            Module_LiftingSurface.ShowPAMDebug = evt.newValue;
            _isPhysicsForceShowing = Game.PhysicsForceDisplaySystem.IsDisplayed;
        }
    }
}
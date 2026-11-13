using KSP.Sim.impl;
using UnityEngine;
using UnityEngine.UIElements;

namespace DebugTools.Runtime.Controllers.VesselTools
{
    public class VesselCoordinatesWindowController : BaseWindowController
    {
        private const string FP6Places = "F6";

        private Label? _pitch;
        private Label? _yaw;
        private Label? _roll;
        private Label? _controlState;

        private Toggle? _toggleHandOfKraken;

        private Label? _latitude;
        private Label? _longitude;
        private Label? _heading;
        private Label? _zenith;
        private Label? _horizonPitch;

        private Label? _celestialBody;
        private Label? _originDistance;
        private Label? _situation;
        private Label? _landed;
        private Label? _splashed;

        private Label? _altitudeRadius;
        private Label? _altitudeTerrain;
        private Label? _altitudeSeaLevel;
        private Label? _altitudeScenery;
        private Label? _altitudeSurface;

        private Label? _semiMajorAxis;
        private Label? _eccentricity;
        private Label? _inclination;
        private Label? _longitudeAscendingNode;
        private Label? _periapsis;
        private Label? _epoch;
        private Label? _meanAnomaly;

        private Label? _hokPhysicsMode;
        private Label? _hokCorrectingOrbit;
        private Label? _hokGotExpectedOrbit;
        private Label? _hokUnderAccel;
        private Label? _hokOrbitStandoff;

        private float _refreshRemaining;

        private void LateUpdate()
        {
            if (!IsWindowOpen)
                return;

            _refreshRemaining -= Time.unscaledDeltaTime;
            if (_refreshRemaining > 0f)
                return;
            _refreshRemaining = 0.5f;

            var view = Game.ViewController;
            if (view == null)
                return;

            var vessel = view.GetActiveSimVessel();
            if (vessel == null)
                return;

            var behavior = view.GetBehaviorIfLoaded(vessel);
            if (behavior == null)
                return;

            SyncTo(vessel, behavior);
        }

        private void OnEnable()
        {
            Enable();

            _pitch = RootElement.Q<Label>("pitch");
            _yaw = RootElement.Q<Label>("yaw");
            _roll = RootElement.Q<Label>("roll");
            _controlState = RootElement.Q<Label>("control-state");

            _toggleHandOfKraken = RootElement.Q<Toggle>("toggle-hok");
            _toggleHandOfKraken.RegisterValueChangedCallback(ToggleHandOfKraken);

            _latitude = RootElement.Q<Label>("latitude");
            _longitude = RootElement.Q<Label>("longitude");
            _heading = RootElement.Q<Label>("heading");
            _zenith = RootElement.Q<Label>("zenith");
            _horizonPitch = RootElement.Q<Label>("horizon-pitch");

            _celestialBody = RootElement.Q<Label>("celestial-body");
            _originDistance = RootElement.Q<Label>("origin-distance");
            _situation = RootElement.Q<Label>("situation");
            _landed = RootElement.Q<Label>("landed");
            _splashed = RootElement.Q<Label>("splashed");

            _altitudeRadius = RootElement.Q<Label>("alt-radius");
            _altitudeTerrain = RootElement.Q<Label>("alt-terrain");
            _altitudeSeaLevel = RootElement.Q<Label>("alt-sl");
            _altitudeScenery = RootElement.Q<Label>("alt-scenery");
            _altitudeSurface = RootElement.Q<Label>("alt-surface");

            _semiMajorAxis = RootElement.Q<Label>("semi-major-axis");
            _eccentricity = RootElement.Q<Label>("eccentricity");
            _inclination = RootElement.Q<Label>("inclination");
            _longitudeAscendingNode = RootElement.Q<Label>("long-asc-node");
            _periapsis = RootElement.Q<Label>("periapsis");
            _epoch = RootElement.Q<Label>("epoch");
            _meanAnomaly = RootElement.Q<Label>("epoch-anomaly");

            _hokPhysicsMode = RootElement.Q<Label>("hok-phys-mode");
            _hokCorrectingOrbit = RootElement.Q<Label>("hok-correcting");
            _hokGotExpectedOrbit = RootElement.Q<Label>("hok-got-pos");
            _hokUnderAccel = RootElement.Q<Label>("hok-under-accel");
            _hokOrbitStandoff = RootElement.Q<Label>("hok-orbit-standoff");
        }

        public void SyncTo(VesselComponent vessel, VesselBehavior behavior)
        {
            _pitch!.text = behavior.flightCtrlState.pitch.ToString("0.00");
            _yaw!.text = behavior.flightCtrlState.yaw.ToString("0.00");
            _roll!.text = behavior.flightCtrlState.roll.ToString("0.00");
            _controlState!.text = vessel.ControlStatus.ToString();

            _latitude!.text = $"{vessel.Latitude.ToString(FP6Places)}°";
            _longitude!.text = $"{vessel.Longitude.ToString(FP6Places)}°";
            _heading!.text = $"{vessel.Heading.ToString(FP6Places)}°";
            _zenith!.text = $"{vessel.Zenith.ToString(FP6Places)}°";
            _horizonPitch!.text = $"{vessel.Pitch_HorizonRelative.ToString(FP6Places)}°";

            _celestialBody!.text = $"{vessel.mainBody.bodyName}";
            _originDistance!.text =
                $"{Game.UniverseView.PhysicsSpace.FloatingOrigin.distanceFromOrigin.ToString(FP6Places)}";
            _situation!.text = $"{vessel.Situation}";
            _landed!.text = $"{vessel.Landed}";
            _splashed!.text = $"{vessel.Splashed}";

            _altitudeRadius!.text = $"{vessel.AltitudeFromRadius.ToString(FP6Places)} m";
            _altitudeTerrain!.text = $"{vessel.AltitudeFromTerrain.ToString(FP6Places)} m";
            _altitudeSeaLevel!.text = $"{vessel.AltitudeFromSeaLevel.ToString(FP6Places)} m";
            _altitudeScenery!.text = $"{vessel.AltitudeFromScenery.ToString(FP6Places)} m";
            _altitudeSurface!.text = $"{vessel.AltitudeFromSurface.ToString(FP6Places)} m";

            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (vessel.Orbiter != null)
            {
                _semiMajorAxis!.text = $"{vessel.Orbiter.PatchedConicsOrbit.OrbitalElements.SemiMajorAxis.ToString(FP6Places)} m";
                _eccentricity!.text = $"{vessel.Orbiter.PatchedConicsOrbit.OrbitalElements.Eccentricity.ToString(FP6Places)}";
                _inclination!.text = $"{vessel.Orbiter.PatchedConicsOrbit.OrbitalElements.Inclination.ToString(FP6Places)}";
                _longitudeAscendingNode!.text = $"{vessel.Orbiter.PatchedConicsOrbit.OrbitalElements.LongitudeOfAscendingNode.ToString(FP6Places)}";
                _periapsis!.text = $"{vessel.Orbiter.PatchedConicsOrbit.OrbitalElements.ArgumentOfPeriapsis.ToString(FP6Places)}";
                _epoch!.text = $"{vessel.Orbiter.PatchedConicsOrbit.OrbitalElements.Epoch.ToString(FP6Places)}";
                _meanAnomaly!.text = $"{vessel.Orbiter.PatchedConicsOrbit.OrbitalElements.MeanAnomalyAtEpoch.ToString(FP6Places)}";
            }

            _toggleHandOfKraken!.SetValueWithoutNotify(behavior.PartOwner.IsHandOfKrakenEnabled);
            _hokPhysicsMode!.text = $"{vessel.Physics}";
            _hokCorrectingOrbit!.text = $"{behavior.PartOwner.IsHandOfKrakenTryingToCorrect}";
            _hokGotExpectedOrbit!.text =
                $"{behavior.PartOwner.HasHandOfKrakenGotExpectedOrbit}";
            _hokUnderAccel!.text =
                $"{behavior.PartOwner.IsHandOfKrakenOrbitUnderAcceleration}";
            _hokOrbitStandoff!.text =
                $"{behavior.PartOwner.IsHandOfKrakenOrbitCorrectInStandOff}";
        }

        private void ToggleHandOfKraken(ChangeEvent<bool> evt)
        {
            var activeVessel = Game.ViewController.GetActiveSimVessel();
            if (activeVessel == null)
                return;

            var behavior = Game.ViewController.GetBehaviorIfLoaded(activeVessel);
            if (behavior == null)
                return;
            
            switch (behavior.PartOwner.IsHandOfKrakenEnabled)
            {
                case true when !evt.newValue:
                    behavior.PartOwner.DisableHandOfKraken();
                    break;
                case false when evt.newValue:
                    behavior.PartOwner.EnableHandOfKraken();
                    break;
            }
        }
    }
}
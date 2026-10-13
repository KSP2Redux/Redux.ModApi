---@meta
-- AUTO-GENERATED from analysis of codebase and assets - do not edit by hand.
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/VesselStateConverter.cs
-- Source: Assets/Code/KSP/Sim/State/VesselState.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/VesselDefinitionConverter.cs
-- Source: Assets/Code/KSP/Sim/Definitions/AssemblyDefinition.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/OrbitDefinitionConverter.cs
-- Source: Assets/Code/KSP/Sim/State/KeplerOrbitState.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/OrbiterDefinitionConverter.cs
-- Source: Assets/Code/KSP/Sim/Definitions/OrbiterDefinition.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/RigidbodyStateConverter.cs
-- Source: Assets/Code/KSP/Sim/State/RigidbodyState.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/RigidbodyDefinitionConverter.cs
-- Source: Assets/Code/KSP/Sim/Definitions/RigidbodyDefinition.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/NullableConverter.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/SimulationObjectStateConverter.cs
-- Source: Assets/Code/KSP/Sim/State/SimulationObjectState.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/SimulationObjectStateContainerConverter.cs
-- Source: Assets/Code/KSP/Sim/State/SimulationObjectStateContainer.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/SimulationObjectDefinitionConverter.cs
-- Source: Assets/Code/KSP/Sim/Definitions/SimulationObjectDefinition.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/SimulationObjectDefinitionContainerConverter.cs
-- Source: Assets/Code/KSP/Sim/Definitions/SimulationObjectDefinitionContainer.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/DictionaryConverter.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/TypeConverter.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/PhysicsOwnerStateConverter.cs
-- Source: Assets/Code/KSP/Sim/State/PhysicsOwnerState.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/PhysicsOwnerDefinitionConverter.cs
-- Source: Assets/Code/KSP/Sim/Definitions/PhysicsOwnerDefinition.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/UniverseStateConverter.cs
-- Source: Assets/Code/KSP/Sim/State/UniverseState.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/MotionHierarchyConverter.cs
-- Source: Assets/Code/KSP/Sim/State/MotionHierarchy.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/TransformHierarchyConverter.cs
-- Source: Assets/Code/KSP/Sim/State/TransformHierarchy.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/FramePositionStateConverter.cs
-- Source: Assets/Code/KSP/Sim/State/FramePositionState.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/GeographicPositionStateConverter.cs
-- Source: Assets/Code/KSP/Sim/State/GeographicPositionState.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/ForceStateConverter.cs
-- Source: Assets/Code/KSP/Sim/State/ForceState.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/FlightCtrlStateConverter.cs
-- Source: Assets/Code/KSP/Sim/State/FlightCtrlStateIncremental.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/TelemetryStateConverter.cs
-- Source: Assets/Code/KSP/Sim/State/TelemetryState.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/TelemetryDefinitionConverter.cs
-- Source: Assets/Code/KSP/Sim/Definitions/TelemetryDefinition.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/StagingStateConverter.cs
-- Source: Assets/Code/KSP/Sim/State/StagingState.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/StagingDefinitionConverter.cs
-- Source: Assets/Code/KSP/Sim/Definitions/StagingDefinition.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/ManeuverPlanDefinitionConverter.cs
-- Source: Assets/Code/KSP/Sim/Definitions/ManeuverPlanDefinition.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/SerializedVesselConverter.cs
-- Source: Assets/Code/KSP/Sim/LegacySerializedVessel.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/SerializedVesselSaveConverter.cs
-- Source: Assets/Code/KSP/Sim/SerializedVesselSave.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/SerializedSituationConverter.cs
-- Source: Assets/Code/KSP/Sim/SerializedSituation.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/SerializedSurfaceLocationConverter.cs
-- Source: Assets/Code/KSP/Sim/SerializedSurfaceLocation.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/SerializedRigidbodyStateConverter.cs
-- Source: Assets/Code/KSP/Sim/SerializedRigidbodyState.cs
-- Source: Assets/Code/KSP/ScriptInterop/impl/moonsharp/GameModeDataConverter.cs
-- Source: Assets/Code/KSP/Contexts/Game/GameModeData.cs

---Represents the serializable state of a vessel, including flight controls, display modes, autopilot configuration, physics mode, and situational data.
---@class VesselState
-- alt: KSP.Sim.State.VesselState
---@field launchTime? number The universal time at which the vessel was launched.
---@field landedAtBase string The name of the KSC facility at which the vessel is currently landed.
---@field landedAtBaseTime? number The universal time at which the vessel landed at the current base.
---@field FlightCtrlState? FlightCtrlState The incremental flight control state snapshot for the vessel.
---@field speedMode? SpeedDisplayMode The active speed display mode shown in the navball readout.
---@field altimeterMode? AltimeterDisplayMode The active altimeter display mode indicating the altitude reference used.
---@field autopilotMode? AutopilotMode The active autopilot mode controlling vessel attitude hold behavior.
---@field flightControlsMode? any The active flight controls mode governing which inputs the vessel responds to.
---@field actionGroupStates? ActionGroupStates The snapshot of all action group toggle states for the vessel.
---@field currentTargetID any The globally unique identifier of the vessel or object currently set as this vessel's target.
---@field currentControlOwnerPart? any The globally unique identifier of the part currently acting as the primary control point.

---Represents the serializable definition of a game assembly, including its name, simulation object type, version, and ground offset.
---@class Vessel
-- alt: VesselDefinition, KSP.Sim.Definitions.VesselDefinition
---@field vesselName string Name of the assembly.
---@field version string Version string of the assembly.
---@field description string Description text for the assembly.
---@field simulationObjectType string Simulation object type identifier for this assembly.
---@field offsetToGround? number Vertical offset from the ground applied to the assembly's simulation object.

---Represents the Keplerian orbital elements for a simulated orbit.
---@class Orbit
-- alt: OrbitState, KSP.Sim.State.OrbitState
---@field referenceBodyGuid string The name identifier of the celestial body that this orbit centres around.
---@field inclination number The orbital inclination, measured as the angle between the orbital plane and the reference plane.
---@field eccentricity number The orbital eccentricity, defining the shape of the orbit.
---@field semiMajorAxis number The semi-major axis of the orbit, determining its overall size.
---@field longitudeOfAscendingNode number The longitude of the ascending node, defining the orbit's orientation about the reference direction.
---@field argumentOfPeriapsis number The argument of periapsis, defining the orientation of the ellipse within its orbital plane.
---@field meanAnomalyAtEpoch number The mean anomaly at the reference epoch, defining the object's position in the orbit at epoch.
---@field epoch number The reference time at which meanAnomalyAtEpoch is defined.

---Represents the visual display settings for an orbital path, including orbit and node colors and camera distance thresholds.
---@class Orbiter
-- alt: KSP.Sim.Definitions.OrbiterDefinition
---@field orbitColor? ColorInterop The color used to render the orbital path line.
---@field nodeColor? ColorInterop The color used to render orbital node markers.
---@field lowerCamVsSmaRatio? number The lower camera distance to semi-major axis ratio threshold for orbit line visibility.
---@field upperCamVsSmaRatio? number The upper camera distance to semi-major axis ratio threshold for orbit line visibility.

---Represents the physical state of a rigidbody within a reference frame, including local position, rotation, velocity, angular velocity, and mass.
---@class RigidbodyState
-- alt: KSP.Sim.State.RigidbodyState
---@field referenceTransformGuid string GUID string identifying the reference transform associated with this rigidbody state.
---@field referenceFrameType TransformFrameTypeInterop The type of reference frame used to express the kinematic quantities of this rigidbody.
---@field localPosition Vec3d Position of the rigidbody expressed in the local reference frame, in meters.
---@field localRotation Quatd Orientation of the rigidbody expressed as a double-precision quaternion in the local reference frame.
---@field localVelocity Vec3d Linear velocity of the rigidbody expressed in the local reference frame, in meters per second.
---@field localAngularVelocity Vec3d Angular velocity of the rigidbody expressed in the local reference frame, in radians per second.

---Represents the definition of a rigidbody in the physics simulation.
---No fields are exposed to Lua.
---@class Rigidbody
-- alt: RigidbodyDefinition, KSP.Sim.Definitions.RigidbodyDefinition

---Represents the state snapshot of a simulation object, containing its frame-relative position.
---@class State
-- alt: SimulationObjectState, KSP.Sim.State.SimulationObjectState
---@field position FramePositionState The frame-relative position state of the simulation object.

---Represents the state snapshot of a simulation object, bundling an optional object-level state and a per-component state dictionary.
---@class StateContainer
-- alt: SimulationObjectStateContainer, KSP.Sim.State.SimulationObjectStateContainer
---@field state? State The optional SimulationObjectState snapshot for this simulation object.
---@field componentStates table<string, any> A dictionary mapping component types to their corresponding state objects.

---Represents the definition data for a simulation object.
---No fields are exposed to Lua.
---@class SimulationObjectDefinition
-- alt: KSP.Sim.Definitions.SimulationObjectDefinition

---Represents a container pairing a SimulationObjectDefinition with its associated component definitions.
---@class DefinitionContainer
-- alt: SimulationObjectDefinitionContainer, KSP.Sim.Definitions.SimulationObjectDefinitionContainer
---@field definition? SimulationObjectDefinition The simulation object definition paired with this container, or nil if none is assigned.
---@field componentDefinitions table<string, any> A dictionary mapping each component type to its corresponding definition object.

---Represents the physics ownership state of a simulated object, including its total mass and physics mode.
---@class PhysicsOwnerState
-- alt: KSP.Sim.State.PhysicsOwnerState
---@field mass? number The total mass of the simulated object, in kilograms, or nil if not yet assigned.
---@field physicsMode? PhysicsMode The PhysicsMode applied to the simulated object, or nil if not yet assigned.

---Represents a definition component that marks a simulation entity as a physics owner.
---No fields are exposed to Lua.
---@class PhysicsOwnerDefinition
-- alt: KSP.Sim.Definitions.PhysicsOwnerDefinition

---Represents the current state of the universe, storing the universal time.
---@class UniverseState
-- alt: KSP.Sim.State.UniverseState
---@field universalTime number The current universal time, in seconds elapsed since the epoch.

---Represents a node in the simulation motion hierarchy, storing the frame type, relative velocities, and child nodes.
---@class MotionHierarchy
-- alt: KSP.Sim.State.MotionHierarchy
---@field motionGuid string The GUID of the transform associated with this motion hierarchy node.
---@field motionFrameType TransformFrameTypeInterop The reference frame type of the transform for this motion hierarchy node.
---@field relativeVelocity Vec3d The relative linear velocity of the simulation object in its parent frame.
---@field relativeAngularVelocity Vec3d The relative angular velocity of the simulation object in its parent frame.
---@field children? MotionHierarchy[] The child motion hierarchy nodes beneath this node, or nil when no children are present.

---Represents a snapshot of a transform node's position, rotation, reference frame type, and children within the simulation transform hierarchy.
---@class TransformHierarchy
-- alt: KSP.Sim.State.TransformHierarchy
---@field transformGuid string The GUID string that uniquely identifies this transform node within the simulation.
---@field referenceFrameType TransformFrameTypeInterop The reference frame type that describes the coordinate frame of this transform node.
---@field localPosition Vec3d The position of this transform relative to its parent, expressed in double-precision coordinates.
---@field localRotation Quatd The rotation of this transform relative to its parent, expressed as a double-precision quaternion.
---@field children? TransformHierarchy[] The child transform hierarchy nodes nested directly under this node.

---Represents the position and rotation of a transform within a reference frame.
---@class FramePositionState
-- alt: KSP.Sim.State.FramePositionState
---@field referenceTransformGuid string The GUID identifying the parent reference transform.
---@field referenceFrameType TransformFrameTypeInterop The type of reference frame associated with the parent transform.
---@field localPosition Vec3d The position of the transform relative to the reference frame.
---@field localRotation Quatd The rotation of the transform relative to the reference frame.

---Represents the geographic position of an entity relative to a reference body, capturing latitude, longitude, altitude from radius, and heading.
---@class GeographicPositionState
-- alt: KSP.Sim.State.GeographicPositionState
---@field referenceBodyGuid string GUID identifying the reference body that defines the coordinate frame for this position.
---@field latitude number Latitude of the position in degrees.
---@field longitude number Longitude of the position in degrees.
---@field altitude number Altitude above the reference body's mean radius in meters.
---@field heading number Heading direction in degrees, measured clockwise from north.

---Represents the state of a force applied in local space, including its ForceType, value, and position.
---@class ForceState
-- alt: KSP.Sim.State.ForceState
---@field mode ForceType The ForceType that classifies how this force is applied.
---@field localPosition Vec3d The position in local space at which this force is applied.
---@field localValue Vec3d The force vector expressed in local space.

---Represents a partial set of flight control inputs that can be selectively applied to a FlightCtrlState.
---@class FlightCtrlState
-- alt: FlightCtrlStateIncremental, KSP.Sim.State.FlightCtrlStateIncremental
---@field mainThrottle? number Optional main throttle override value in the range [0, 1].
---@field roll? number Optional roll control axis override.
---@field yaw? number Optional yaw control axis override.
---@field pitch? number Optional pitch control axis override.
---@field rollTrim? number Optional roll trim override.
---@field yawTrim? number Optional yaw trim override.
---@field pitchTrim? number Optional pitch trim override.
---@field inputRoll? number Optional raw roll input axis override.
---@field inputYaw? number Optional raw yaw input axis override.
---@field inputPitch? number Optional raw pitch input axis override.
---@field wheelSteer? number Optional wheel steering axis override.
---@field wheelSteerTrim? number Optional wheel steering trim override.
---@field wheelThrottle? number Optional wheel throttle axis override.
---@field wheelThrottleTrim? number Optional wheel throttle trim override.
---@field X? number Optional translation X axis override.
---@field Y? number Optional translation Y axis override.
---@field Z? number Optional translation Z axis override.
---@field killRot? boolean Optional kill-rotation flag override.
---@field gearUp? boolean Optional gear-up command override.
---@field gearDown? boolean Optional gear-down command override.
---@field headlight? boolean Optional headlight toggle override.

---Represents the telemetry state.
---No fields are exposed to Lua.
---@class TelemetryState
-- alt: KSP.Sim.State.TelemetryState

---Represents a marker struct identifying a telemetry definition in the simulation.
---No fields are exposed to Lua.
---@class Telemetry
-- alt: TelemetryDefinition, KSP.Sim.Definitions.TelemetryDefinition

---Represents the staging configuration of a vessel, including available stages and unassigned parts.
---@class StagingState
-- alt: KSP.Sim.State.StagingState
---@field unassignedParts? StageParts The set of parts not yet assigned to any stage, or nil if all parts are assigned.
---@field availableStages StageParts[] The ordered list of stages available on the vessel, each containing its assigned part IDs.

---Represents the staging definition for a simulated part.
---No fields are exposed to Lua.
---@class Staging
-- alt: StagingDefinition, KSP.Sim.Definitions.StagingDefinition

---Represents a maneuver plan definition.
---No fields are exposed to Lua.
---@class ManeuverPlan
-- alt: ManeuverPlanDefinition, KSP.Sim.Definitions.ManeuverPlanDefinition

---Represents a legacy serialized vessel. Superseded by SerializedAssembly.
---@class Vehicle
-- alt: SerializedVessel, KSP.Sim.SerializedVessel
---@field vesselDefinition Vessel The assembly definition that describes the vessel's structure and configuration.
---@field partDefinitions partcontainer[] The collection of part definition containers describing each part belonging to the vessel.
---@field stagingState? StagingState The optional staging state of the vessel, or nil if no staging data is present.
---@field partOwnerState? PartOwnerState The optional part owner state of the vessel, or nil if no part owner data is present.

---Represents a serialized vessel save, pairing a vessel definition with its associated situation.
---@class VesselSave
-- alt: VehicleSave, KSP.Sim.SerializedVesselSave
---@field vessel Vehicle The legacy serialized vessel definition paired with this save.
---@field situation Situation The serialized situation associated with this vessel save.

---Represents the serialized state of a simulation situation, including component definitions and states.
---@class Situation
-- alt: KSP.Sim.SerializedSituation
---@field simulationObjectState? State The optional state snapshot of the simulation object associated with this situation.
---@field componentDefinitions table<string, any> A dictionary mapping component type names to their definition data.
---@field componentStates table<string, any> A dictionary mapping component type names to their serialized runtime state data.

---Represents a serialized surface location, identified by a parent object GUID and an object name.
---@class SerializedSurfaceLocation
-- alt: KSP.Sim.SerializedSurfaceLocation
---@field parentGuid string The GUID identifying the parent object.
---@field objectName string The name of the object at this surface location.

---Represents the serialized physics state of a rigidbody, including position, rotation, velocity, and reference frame.
---@class SerializedRigidbodyState
-- alt: KSP.Sim.SerializedRigidbodyState
---@field referenceTransformGuid string GUID of the reference transform against which the local-space values are expressed.
---@field referenceFrameType TransformFrameTypeInterop Type of reference frame used to express the local-space values.
---@field localVelocity Vec3d Linear velocity of the rigidbody expressed in the local reference frame, in meters per second.
---@field localAngularVelocity Vec3d Angular velocity of the rigidbody expressed in the local reference frame, in radians per second.
---@field localPosition Vec3d Position of the rigidbody expressed in the local reference frame, in meters.
---@field localRotation Quatd Rotation of the rigidbody expressed in the local reference frame.

---Represents the configuration data for a game mode, including its name, description, and default save key.
---@class GameMode
-- alt: KSP.Contexts.Game.GameModeData
---@field DefaultStatePath string The default save key associated with this game mode.
---@field Name string The display name of the game mode.
---@field Description string The human-readable description of the game mode.

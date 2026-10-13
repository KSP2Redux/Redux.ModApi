---@meta
-- AUTO-GENERATED from analysis of codebase and assets - do not edit by hand.
-- Source: Assets/Code/KSP/Sim/impl/lua/ViewControllerLua.cs

---Represents a Lua scripting bridge for ViewController, exposing view-layer functionality to a Lua script environment.
---@class View
---@field ActiveVehicle ViewActiveVehicle
---@field FlightCamera ViewFlightCamera
local View = {}

---@param instanceGuid string
---@param controllable? boolean
---@return boolean result
function View.SetActiveVessel(instanceGuid, controllable) end

---@return string? guid
function View.GetActiveVessel() end

---@param childName string
---@param tag string
---@return string result
function View.ReParentToActiveVessel(childName, tag) end

---@return boolean result
function View.MapIsEnabled() end

---@param languageKey string
---@return boolean result
function View.SetLanguage(languageKey) end

---@param gameObjectName string
---@param shotList any
---@return boolean result
function View.StartCutscene(gameObjectName, shotList) end

---@param guid string
---@return ViewVesselHandle? vessel
function View.Vessel(guid) end

---@return ViewVesselHandle? vessel
function View.ActiveVessel() end

---@return ViewVesselHandle[] vessels
function View.NearbyVessels() end

---@param guid string
---@return table<string, any> info
function View._VesselGetInfo(guid) end

---@param guid string
---@return table<string, any> telemetry
function View._VesselGetTelemetry(guid) end

---@param guid string
---@return table<string, any> orbit
function View._VesselGetOrbit(guid) end

---@param guid string
---@return table<string, any> aero
function View._VesselGetAero(guid) end

---@param guid string
---@return table<string, any> clearance
function View._VesselGetGroundClearance(guid) end

---@param guid string
---@return table<string, any> controls
function View._VesselGetControls(guid) end

---@param guid string
---@return table<string, any> controlPoint
function View._VesselGetControlPoint(guid) end

---@param guid string
---@return table<string, any>[] vessels
function View._VesselGetNearbyVessels(guid) end

---@param guid string
---@param values table<string, any>
---@return boolean result
function View._VesselSetControls(guid, values) end

---@param guid string
---@param value number
---@return boolean result
function View._VesselSetThrottle(guid, value) end

---@param guid string
---@param values table<string, any>
---@return table<string, any> result
function View._VesselSetAttitude(guid, values) end

---@param guid string
---@param values table<string, any>
---@return table<string, any> result
function View._VesselSetSAS(guid, values) end

---@param guid string
---@return boolean result
function View._VesselStage(guid) end

---@param guid string
---@return number fuel
function View._VesselGetStageFuel(guid) end

---@param guid string
---@return ActionGroupStates? states
function View._VesselGetActionGroups(guid) end

---@param guid string
---@param group KSPActionGroup
---@return boolean? state
function View._VesselGetActionGroup(guid, group) end

---@param guid string
---@param group KSPActionGroup
---@param value boolean
---@param callback? fun(guid?: string)
---@return boolean result
function View._VesselSetActionGroup(guid, group, value, callback) end

---@param guid string
---@param options? table<string, any>
---@return table<string, any>[] resources
function View._VesselGetResources(guid, options) end

---@param guid string
---@param options? table<string, any>
---@return table<string, any>[] parts
function View._VesselGetParts(guid, options) end

---@param guid string
---@param ut number
---@param vector table<string, any>
---@return string nodeId
function View._VesselCreateManeuverNode(guid, ut, vector) end

---@param guid string
---@param nodeId string
---@return table<string, any> node
function View._VesselGetManeuverNode(guid, nodeId) end

---@param guid string
---@return table<string, any>[] nodes
function View._VesselGetManeuverNodes(guid) end

---@param guid string
---@return table<string, any> node
function View._VesselGetNextManeuverNode(guid) end

---@param guid string
---@param nodeId string
---@param ut number
---@return table<string, any> node
function View._VesselSetManeuverNodeTime(guid, nodeId, ut) end

---@param guid string
---@param nodeId string
---@param vector table<string, any>
---@return table<string, any> node
function View._VesselSetManeuverNodeVector(guid, nodeId, vector) end

---@param guid string
---@param nodeId string
---@param vector table<string, any>
---@return table<string, any> node
function View._VesselAddManeuverNodeVector(guid, nodeId, vector) end

---@param guid string
---@param nodeId string
---@return boolean result
function View._VesselWarpToManeuverNode(guid, nodeId) end

---@param guid string
---@param nodeId string
---@return boolean result
function View._VesselRemoveManeuverNode(guid, nodeId) end

---@param guid string
---@return SpeedDisplayMode? mode
function View._VesselGetSpeedDisplayMode(guid) end

---@param guid string
---@param speedMode SpeedDisplayMode
---@param callback? fun(guid?: string)
---@return boolean result
function View._VesselSetSpeedDisplayMode(guid, speedMode, callback) end

---@param guid string
---@return AltimeterDisplayMode? mode
function View._VesselGetAltimeterDisplayMode(guid) end

---@param guid string
---@param altimeterMode AltimeterDisplayMode
---@param callback? fun(guid?: string)
---@return boolean result
function View._VesselSetAltimeterDisplayMode(guid, altimeterMode, callback) end

---@param guid string
---@return any mode
function View._VesselGetFlightControlsMode(guid) end

---@param guid string
---@param controlsMode any
---@param callback? fun(guid?: string)
---@return boolean result
function View._VesselSetFlightControlsMode(guid, controlsMode, callback) end

---@class ViewActiveVehicle
local ViewActiveVehicle = {}

---@return string? guid
function ViewActiveVehicle.GetGuid() end

---@return ActionGroupStates? states
function ViewActiveVehicle.GetActionGroups() end

---@param group KSPActionGroup
---@return boolean? state
function ViewActiveVehicle.GetActionGroup(group) end

---@param group KSPActionGroup
---@param value boolean
---@param callback? fun(guid?: string)
---@return boolean result
function ViewActiveVehicle.SetActionGroup(group, value, callback) end

---@return SpeedDisplayMode? mode
function ViewActiveVehicle.GetSpeedDisplayMode() end

---@param speedMode SpeedDisplayMode
---@param callback? fun(guid?: string)
---@return boolean result
function ViewActiveVehicle.SetSpeedDisplayMode(speedMode, callback) end

---@return AltimeterDisplayMode? mode
function ViewActiveVehicle.GetAltimeterDisplayMode() end

---@param altimeterMode AltimeterDisplayMode
---@param callback? fun(guid?: string)
---@return boolean result
function ViewActiveVehicle.SetAltimeterDisplayMode(altimeterMode, callback) end

---@return any mode
function ViewActiveVehicle.GetFlightControlsMode() end

---@param controlsMode any
---@param callback? fun(guid?: string)
---@return boolean result
function ViewActiveVehicle.SetFlightControlsMode(controlsMode, callback) end

---@return table<string, any> info
function ViewActiveVehicle.GetInfo() end

---@return table<string, any> telemetry
function ViewActiveVehicle.GetTelemetry() end

---@return table<string, any> orbit
function ViewActiveVehicle.GetOrbit() end

---@return table<string, any> aero
function ViewActiveVehicle.GetAero() end

---@return table<string, any> clearance
function ViewActiveVehicle.GetGroundClearance() end

---@return table<string, any> controls
function ViewActiveVehicle.GetControls() end

---@return table<string, any> controlPoint
function ViewActiveVehicle.GetControlPoint() end

---@param values table<string, any>
---@return boolean result
function ViewActiveVehicle.SetControls(values) end

---@param value number
---@return boolean result
function ViewActiveVehicle.SetThrottle(value) end

---@param values table<string, any>
---@return table<string, any> result
function ViewActiveVehicle.SetAttitude(values) end

---@return boolean result
function ViewActiveVehicle.Stage() end

---@param values table<string, any>
---@return table<string, any> result
function ViewActiveVehicle.SetSAS(values) end

---@return number fuel
function ViewActiveVehicle.GetStageFuel() end

---@param options? table<string, any>
---@return table<string, any>[] resources
function ViewActiveVehicle.GetResources(options) end

---@param options? table<string, any>
---@return table<string, any>[] parts
function ViewActiveVehicle.GetParts(options) end

---@param ut number
---@param vector table<string, any>
---@return string nodeId
function ViewActiveVehicle.CreateManeuverNode(ut, vector) end

---@param nodeId string
---@return table<string, any> node
function ViewActiveVehicle.GetManeuverNode(nodeId) end

---@return table<string, any>[] nodes
function ViewActiveVehicle.GetManeuverNodes() end

---@return table<string, any> node
function ViewActiveVehicle.GetNextManeuverNode() end

---@param nodeId string
---@param ut number
---@return table<string, any> node
function ViewActiveVehicle.SetManeuverNodeTime(nodeId, ut) end

---@param nodeId string
---@param vector table<string, any>
---@return table<string, any> node
function ViewActiveVehicle.SetManeuverNodeVector(nodeId, vector) end

---@param nodeId string
---@param vector table<string, any>
---@return table<string, any> node
function ViewActiveVehicle.AddManeuverNodeVector(nodeId, vector) end

---@param nodeId string
---@return boolean result
function ViewActiveVehicle.WarpToManeuverNode(nodeId) end

---@param nodeId string
---@return boolean result
function ViewActiveVehicle.RemoveManeuverNode(nodeId) end

---@class ViewFlightCamera
local ViewFlightCamera = {}

---@return CameraMode mode
function ViewFlightCamera.GetMode() end

---@param mode CameraMode
---@return CameraMode mode
function ViewFlightCamera.SetMode(mode) end

---@return CameraMode[] modes
function ViewFlightCamera.GetAvailableModes() end

---@param distance number
---@return number distance
function ViewFlightCamera.SetDistance(distance) end

---@return number distance
function ViewFlightCamera.GetDistance() end

---@param heading number
---@return number heading
function ViewFlightCamera.SetHeading(heading) end

---@return number heading
function ViewFlightCamera.GetHeading() end

---@param pitch number
---@return number pitch
function ViewFlightCamera.SetPitch(pitch) end

---@return number pitch
function ViewFlightCamera.GetPitch() end

---@param fieldOfView number
---@return number fieldOfView
function ViewFlightCamera.SetFieldOfView(fieldOfView) end

---@return number fieldOfView
function ViewFlightCamera.GetFieldOfView() end

function ViewFlightCamera.RevertToDefaults() end

---@class ViewVesselHandle
---@field guid string
---@field id string
local ViewVesselHandle = {}

---@return string guid
function ViewVesselHandle.GetGuid() end

---@return table<string, any> info
function ViewVesselHandle.GetInfo() end

---@return table<string, any> telemetry
function ViewVesselHandle.GetTelemetry() end

---@return table<string, any> orbit
function ViewVesselHandle.GetOrbit() end

---@return table<string, any> aero
function ViewVesselHandle.GetAero() end

---@return table<string, any> clearance
function ViewVesselHandle.GetGroundClearance() end

---@return table<string, any> controls
function ViewVesselHandle.GetControls() end

---@return table<string, any> controlPoint
function ViewVesselHandle.GetControlPoint() end

---@return ViewVesselHandle[] vessels
function ViewVesselHandle.NearbyVessels() end

---@param values table<string, any>
---@return boolean result
function ViewVesselHandle.SetControls(values) end

---@param value number
---@return boolean result
function ViewVesselHandle.SetThrottle(value) end

---@param values table<string, any>
---@return table<string, any> result
function ViewVesselHandle.SetAttitude(values) end

---@param values table<string, any>
---@return table<string, any> result
function ViewVesselHandle.SetSAS(values) end

---@return boolean result
function ViewVesselHandle.Stage() end

---@return number fuel
function ViewVesselHandle.GetStageFuel() end

---@return ActionGroupStates? states
function ViewVesselHandle.GetActionGroups() end

---@param group KSPActionGroup
---@return boolean? state
function ViewVesselHandle.GetActionGroup(group) end

---@param group KSPActionGroup
---@param value boolean
---@param callback? fun(guid?: string)
---@return boolean result
function ViewVesselHandle.SetActionGroup(group, value, callback) end

---@param options? table<string, any>
---@return table<string, any>[] resources
function ViewVesselHandle.GetResources(options) end

---@param options? table<string, any>
---@return table<string, any>[] parts
function ViewVesselHandle.GetParts(options) end

---@param ut number
---@param vector table<string, any>
---@return string nodeId
function ViewVesselHandle.CreateManeuverNode(ut, vector) end

---@param nodeId string
---@return table<string, any> node
function ViewVesselHandle.GetManeuverNode(nodeId) end

---@return table<string, any>[] nodes
function ViewVesselHandle.GetManeuverNodes() end

---@return table<string, any> node
function ViewVesselHandle.GetNextManeuverNode() end

---@param nodeId string
---@param ut number
---@return table<string, any> node
function ViewVesselHandle.SetManeuverNodeTime(nodeId, ut) end

---@param nodeId string
---@param vector table<string, any>
---@return table<string, any> node
function ViewVesselHandle.SetManeuverNodeVector(nodeId, vector) end

---@param nodeId string
---@param vector table<string, any>
---@return table<string, any> node
function ViewVesselHandle.AddManeuverNodeVector(nodeId, vector) end

---@param nodeId string
---@return boolean result
function ViewVesselHandle.WarpToManeuverNode(nodeId) end

---@param nodeId string
---@return boolean result
function ViewVesselHandle.RemoveManeuverNode(nodeId) end

---@return SpeedDisplayMode? mode
function ViewVesselHandle.GetSpeedDisplayMode() end

---@param mode SpeedDisplayMode
---@param callback? fun(guid?: string)
---@return boolean result
function ViewVesselHandle.SetSpeedDisplayMode(mode, callback) end

---@return AltimeterDisplayMode? mode
function ViewVesselHandle.GetAltimeterDisplayMode() end

---@param mode AltimeterDisplayMode
---@param callback? fun(guid?: string)
---@return boolean result
function ViewVesselHandle.SetAltimeterDisplayMode(mode, callback) end

---@return any mode
function ViewVesselHandle.GetFlightControlsMode() end

---@param mode any
---@param callback? fun(guid?: string)
---@return boolean result
function ViewVesselHandle.SetFlightControlsMode(mode, callback) end

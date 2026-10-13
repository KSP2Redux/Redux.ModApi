---@meta
-- AUTO-GENERATED from analysis of codebase and assets - do not edit by hand.
-- Source: Assets/Code/KSP/Sim/impl/lua/SpaceSimulationLua.cs

---Lua scripting bridge that registers SpaceSimulation teleport, debug, and Hand of God force callbacks on an IScriptEnvironment.
---@class SpaceSimulation
---@field Debug SpaceSimulationDebug
---@field HOG SpaceSimulationHOG
local SpaceSimulation = {}

---Teleports the simulation object identified by guid to a rendezvous position relative to a target object.
---@param guid string The GUID of the simulation object to teleport.
---@param target string The GUID of the target simulation object to rendezvous with.
---@param distanceX number The local X-axis offset from the target, in meters.
---@param distanceY number The local Y-axis offset from the target, in meters.
---@param distanceZ number The local Z-axis offset from the target, in meters.
---@param pitchDeg number The pitch component of the local rotation, in degrees.
---@param yawDeg number The yaw component of the local rotation, in degrees.
---@param rollDeg number The roll component of the local rotation, in degrees.
---@return boolean result true if the teleport succeeded. Otherwise false.
function SpaceSimulation.TeleportToRendezvous(guid, target, distanceX, distanceY, distanceZ, pitchDeg, yawDeg, rollDeg) end

---Teleports the simulation object identified by guid to the specified Keplerian orbit around a celestial body.
---@param guid string The GUID of the simulation object to teleport.
---@param celestialBodyGuid string The GUID of the celestial body that serves as the orbit reference.
---@param inclination number The orbital inclination, in degrees.
---@param eccentricity number The orbital eccentricity.
---@param semiMajorAxis number The semi-major axis of the orbit, in meters.
---@param longitudeOfAscendingNode number The longitude of the ascending node, in degrees.
---@param argumentOfPeriapsis number The argument of periapsis, in degrees.
---@param meanAnomalyAtEpoch number The mean anomaly at the reference epoch, in radians.
---@param epoch number The universal time epoch at which meanAnomalyAtEpoch is defined, in seconds.
---@return boolean result true if the teleport succeeded. Otherwise false.
function SpaceSimulation.TeleportToOrbit(guid, celestialBodyGuid, inclination, eccentricity, semiMajorAxis, longitudeOfAscendingNode, argumentOfPeriapsis, meanAnomalyAtEpoch, epoch) end

---Teleports the simulation object identified by guid to a surface location on a celestial body.
---@param guid string The GUID of the simulation object to teleport.
---@param celestialBodyGuid string The GUID of the target celestial body.
---@param altitude number The altitude above the surface, in meters.
---@param latitude number The geodetic latitude of the surface position, in degrees.
---@param longitude number The geodetic longitude of the surface position, in degrees.
---@param verticalSpeed number The initial vertical speed upon arrival, in meters per second.
---@return boolean result true if the teleport succeeded. Otherwise false.
function SpaceSimulation.TeleportToSurface(guid, celestialBodyGuid, altitude, latitude, longitude, verticalSpeed) end

---@param launchSite string
---@return boolean result
function SpaceSimulation.SetLaunchSite(launchSite) end

---@class SpaceSimulationDebug
local SpaceSimulationDebug = {}

---@param msg string
function SpaceSimulationDebug.Log(msg) end

---@param seconds number
---@param callback fun()
function SpaceSimulationDebug.WaitFor(seconds, callback) end

---@param simObjectGuid string
---@param vesselPhysics string
---@param enablePhysX boolean
---@param enablePartUnpack boolean
---@return boolean result
function SpaceSimulationDebug.SetPhysicsMode(simObjectGuid, vesselPhysics, enablePhysX, enablePartUnpack) end

function SpaceSimulationDebug.Break() end

---@return number universalTime
function SpaceSimulationDebug.GetUniversalTime() end

---@param timeScale number
function SpaceSimulationDebug.SetTimeScale(timeScale) end

---@return number timeScale
function SpaceSimulationDebug.GetTimeScale() end

---@return table<string, string> types
function SpaceSimulationDebug.GetTypes() end

---@class SpaceSimulationHOG
local SpaceSimulationHOG = {}

---@param forceGuid string
---@param simObjGuid string
---@param forceState ForceState
---@return string forceGuid
function SpaceSimulationHOG.CreateForce(forceGuid, simObjGuid, forceState) end

---@param forceGuid string
---@param simObjGuid string
---@param forceState ForceState
---@return string forceGuid
function SpaceSimulationHOG.CreateTorque(forceGuid, simObjGuid, forceState) end

---@param forceGuid string
---@param forceValue Vec3
function SpaceSimulationHOG.SetLocalForce(forceGuid, forceValue) end

---@param forceGuid string
---@param torqueValue Vec3
function SpaceSimulationHOG.SetLocalTorque(forceGuid, torqueValue) end

---@param forceGuid string
function SpaceSimulationHOG.Destroy(forceGuid) end

---@param forceGuid string
function SpaceSimulationHOG.DestroyForce(forceGuid) end

---@param forceGuid string
function SpaceSimulationHOG.DestroyTorque(forceGuid) end

---@param simObjGuid string
---@param forceState ForceState
---@return boolean result
function SpaceSimulationHOG.ApplyImpulse(simObjGuid, forceState) end

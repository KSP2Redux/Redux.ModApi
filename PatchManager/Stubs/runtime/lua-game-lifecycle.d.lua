---@meta
-- AUTO-GENERATED from analysis of codebase and assets - do not edit by hand.
-- Source: Assets/Code/Redux/Lua/GameLifecycleLibrary.cs
-- Source: Assets/Code/Redux/Lua/GameLifecycleManager.cs
-- Source: Assets/Code/Redux/Lua/LuaCoroutineHandle.cs
-- Source: Assets/Code/KSP/game/GameState.cs

---The Lua `Game` global through which mod scripts register live-game lifecycle closures.
---@class GameLifecycleLibrary
---@field State string The name of the current game state (for example "FlightView").
---@field Time number The current universe time in seconds.
---@field IsPaused boolean Whether the game is paused.
local GameLifecycleLibrary = {}

---Registers a closure to fire every frame for the calling mod.
---@param fn fun(deltaTime: number) The closure to register.
function GameLifecycleLibrary:Update(fn) end

---Registers a closure to fire every physics step for the calling mod.
---@param fn fun(deltaTime: number) The closure to register.
function GameLifecycleLibrary:FixedUpdate(fn) end

---Registers a closure to fire whenever any game state is entered, receiving the state as an argument.
---@param fn fun(state: integer) The closure to register.
function GameLifecycleLibrary:SceneEnter(fn) end

---Registers a closure to fire whenever any game state is left, receiving the state as an argument.
---@param fn fun(state: integer) The closure to register.
function GameLifecycleLibrary:SceneExit(fn) end

---Registers a closure to fire when the active vessel changes, receiving the new vessel as an argument.
---@param fn fun(vessel: any) The closure to register.
function GameLifecycleLibrary:OnActiveVesselChanged(fn) end

---Registers a closure to fire only when the flight view is entered.
---@param fn fun() The closure to register.
function GameLifecycleLibrary:OnFlight(fn) end

---Registers a closure to fire only when the Kerbal Space Center is entered.
---@param fn fun() The closure to register.
function GameLifecycleLibrary:OnKSC(fn) end

---Registers a closure to fire only when the vehicle assembly editor is entered.
---@param fn fun() The closure to register.
function GameLifecycleLibrary:OnEditor(fn) end

---Registers a closure to fire only when the main menu is entered.
---@param fn fun() The closure to register.
function GameLifecycleLibrary:OnMainMenu(fn) end

---Starts a coroutine over the given function, resumed every frame until it finishes or is stopped.
---@param fn fun() The function to run as a coroutine.
---@return LuaCoroutineHandle handle A handle for stopping or polling the coroutine.
function GameLifecycleLibrary:StartCoroutine(fn) end

---Sets the paused state of the game.
---@param paused boolean True to pause, false to resume.
---@return boolean ok True if the game's paused state matches the requested value afterwards.
function GameLifecycleLibrary:SetPaused(paused) end

---Warps to the given time-warp rate, snapping a rate factor to the nearest available rate.
---@param rateOrIndex number A warp-rate index, or a rate factor matched to the nearest available rate.
---@return boolean ok True if the warp rate was set, false if time warp is unavailable.
function GameLifecycleLibrary:WarpTo(rateOrIndex) end

---Warps forward to the given universal time.
---@param universalTime number The universe time to warp to.
---@return boolean ok True if the warp was started, false if time warp is unavailable.
function GameLifecycleLibrary:WarpToTime(universalTime) end

---Registers a closure to run whenever a feature set becomes active, for data a mod keeps per set.
---@param fn fun(set: table<string, boolean>) The closure, handed the set just activated as a table of each id mapped to true.
---@return FeatureGatedAction action The registered action, for gating it with features.
function GameLifecycleLibrary:OnSetActivated(fn) end

---Registers a closure to run each time a campaign the calling mod is enabled in finishes loading.
---@param fn fun() The closure to register.
---@return FeatureGatedAction action The registered action, for gating it with features.
function GameLifecycleLibrary:OnCampaignLoaded(fn) end

---Registers a closure to run before a campaign the calling mod was enabled in unloads.
---@param fn fun() The closure to register.
---@return FeatureGatedAction action The registered action, for gating it with features.
function GameLifecycleLibrary:OnCampaignUnloading(fn) end

---A running Lua coroutine, returned to the script that started it.
---@class LuaCoroutineHandle
---@field IsDone boolean Whether the coroutine has finished or been stopped.
---@field IsRunning boolean Whether the coroutine is still running.
local LuaCoroutineHandle = {}

---Stops the coroutine so the runner drops it on the next frame.
function LuaCoroutineHandle:Stop() end

---Represents the active high-level game state, identifying which scene or view is currently running.
---@class GameState
---@field Invalid 0 A sentinel value indicating that no valid game state has been set.
---@field WarmUpLoading 1 The initial warm-up loading phase shown when the game first starts.
---@field MainMenu 2 The main menu scene.
---@field KerbalSpaceCenter 3 The Kerbal Space Center overview scene.
---@field VehicleAssemblyBuilder 10 The Vehicle Assembly Builder editor scene.
---@field BaseAssemblyEditor 11 The Base Assembly Editor scene for constructing surface bases.
---@field FlightView 20 The active flight view scene.
---@field ColonyView 21 The colony management view scene.
---@field Map3DView 22 The three-dimensional orbital map view scene.
---@field PhotoMode 30 The photo mode scene for in-game photography.
---@field MetricsMode 31 The metrics and performance diagnostics mode scene.
---@field PlanetViewer 32 The planet viewer scene.
---@field Loading 33 A general-purpose loading screen scene.
---@field TrainingCenter 40 The Training Center facility scene.
---@field MissionControl 41 The Mission Control facility scene.
---@field TrackingStation 42 The Tracking Station facility scene.
---@field ResearchAndDevelopment 43 The Research and Development facility scene.
---@field Launchpad 44 The launchpad facility scene.
---@field Runway 45 The runway facility scene.
---@field Flag 46 The flag planting selection scene.

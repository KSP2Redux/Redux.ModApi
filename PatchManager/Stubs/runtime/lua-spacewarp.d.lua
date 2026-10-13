---@meta
-- AUTO-GENERATED from analysis of codebase and assets - do not edit by hand.
-- Source: Assets/Modules/Redux.ModApi/SpaceWarp/Runtime/Core/API/Lifecycle/SwLibrary.cs
-- Source: Assets/Modules/Redux.ModApi/SpaceWarp/Runtime/Core/API/Lifecycle/ModLogger.cs
-- Source: Assets/Modules/Redux.ModApi/SpaceWarp/Runtime/Core/API/Lifecycle/ConsoleLibrary.cs
-- Source: Assets/Modules/Redux.ModApi/SpaceWarp/Runtime/Core/API/Lifecycle/FeaturesLibrary.cs
-- Source: Assets/Modules/Redux.ModApi/SpaceWarp/Runtime/Core/API/Lifecycle/ModRequire.cs

---The Lua `SW` global. Mod scripts register lifecycle closures through it (for example
---`SW:Init(fn)`), and those closures fire at the matching SpaceWarp loading phase, before the
---mod's C# lifecycle method. The closures are wired onto the mod's SpaceWarpPluginDescriptor
---as plain Actions, so the descriptor never has to name a game-side script type.
---@class SwLibrary
local SwLibrary = {}

---Registers a closure to fire during the Init phase, before the mod's C# OnInitialized.
---@param fn fun() The closure to fire.
function SwLibrary:Init(fn) end

---Registers a closure to fire during the PostInit phase, before the mod's C# OnPostInitialized.
---@param fn fun() The closure to fire.
function SwLibrary:PostInit(fn) end

---Returns an extra named logger, for a mod that wants one beyond its default `Log` global.
---@param name string The logger name.
---@return ModLogger logger A logger wrapping the named LogManager logger.
function SwLibrary:GetLogger(name) end

---The Lua `Log` global.
---@class ModLogger
local ModLogger = {}

---Logs an info-level message.
---@param message string The message to log.
function ModLogger:Info(message) end

---Logs a warning-level message.
---@param message string The message to log.
function ModLogger:Warning(message) end

---Logs an error-level message.
---@param message string The message to log.
function ModLogger:Error(message) end

---Logs a debug-level message.
---@param message string The message to log.
function ModLogger:Debug(message) end

---The Lua `Console` global, through which a mod exposes values and functions to the in-game console.
---@class ConsoleLibrary
local ConsoleLibrary = {}

---Registers a value or function under `name` as a global on the console environment.
---@param name string The global name console scripts use to reach the value.
---@param value any The value or function to expose.
function ConsoleLibrary:Register(name, value) end

---The `Features` global: a read-only view of the active feature set.
---@class FeaturesLibrary
local FeaturesLibrary = {}

---Checks whether a feature is on.
---@param featureId string The feature id, bare or qualified.
---@return boolean on True if the feature is on.
function FeaturesLibrary:Has(featureId) end

---Calls a function after each feature set activates, with the new set as All gives it.
---@param fn fun(enabled: table<string, boolean>?) The function to call.
---@error Thrown when fn is not a function.
function FeaturesLibrary:OnChanged(fn) end

---Lists the enabled features as a set.
---@return table<string, boolean>? set A new table with each enabled id as a key mapped to true, or nil before any set is active.
function FeaturesLibrary:All() end

---A `require` implementation that resolves and runs modules against the calling mod's forked
---environment instead of the shared root globals.
---@param modname string
---@return any
function require(modname) end

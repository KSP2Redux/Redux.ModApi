---@meta
-- AUTO-GENERATED from analysis of codebase and assets - do not edit by hand.
-- Source: Assets/Code/Redux/Lua/ReduxContributors.cs
-- Source: Assets/Code/Redux/Lua/InputLibrary.cs
-- Source: Assets/Code/Redux/Lua/FilesLibrary.cs
-- Source: Assets/Code/Redux/Lua/WaitLibrary.cs
-- Source: Assets/Code/Redux/Lua/Scripting/ScriptContextLibrary.cs

---The Lua `Input` global for querying keyboard state by key name (for example `Input.GetKeyDown("Space")`).
---@class InputLibrary
local InputLibrary = {}

---Whether the named key is currently held down.
---@param key string The key name to query.
---@return boolean held True while the key is held, false if held nothing or the name does not resolve.
function InputLibrary:GetKey(key) end

---Whether the named key went down this frame.
---@param key string The key name to query.
---@return boolean down True on the frame the key is pressed, false otherwise or if the name does not resolve.
function InputLibrary:GetKeyDown(key) end

---Whether the named key came up this frame.
---@param key string The key name to query.
---@return boolean up True on the frame the key is released, false otherwise or if the name does not resolve.
function InputLibrary:GetKeyUp(key) end

---The Lua `Files` global for reading and writing files under the calling environment's own folder.
---@class FilesLibrary
local FilesLibrary = {}

---Lists the files directly in a folder under the root.
---@param relativePath? string The folder relative to the root, or nil/blank for the root itself.
---@return string[] files Root-relative paths with forward slashes, sorted case-insensitively, empty when the folder is missing.
function FilesLibrary:List(relativePath) end

---Reads a file under the root.
---@param relativePath string The file relative to the root.
---@return string? text The file text, or nil when the path is invalid or the file is missing.
function FilesLibrary:Read(relativePath) end

---Writes a file under the root, creating parent folders as needed.
---@param relativePath string The file relative to the root.
---@param text string? The text to write. Nil is written as empty.
---@return boolean ok True on success, false when the path is invalid.
function FilesLibrary:Write(relativePath, text) end

---@class Wait
local Wait = {}

---@param seconds number
---@param callback? fun()
---@return LuaCoroutineHandle? handle
function Wait.Seconds(seconds, callback) end

---@param predicate fun(): boolean
---@param timeoutSeconds? number
---@param callback? fun(satisfied: boolean)
---@return boolean|LuaCoroutineHandle result
function Wait.Until(predicate, timeoutSeconds, callback) end

---@param key string
---@param callback? fun()
---@return LuaCoroutineHandle? handle
function Wait.InputDown(key, callback) end

---@param key string
---@param callback? fun()
---@return LuaCoroutineHandle? handle
function Wait.Input(key, callback) end

---@param key string
---@param callback? fun()
---@return LuaCoroutineHandle? handle
function Wait.InputUp(key, callback) end

---@param key string
---@param holdSeconds? number
---@param callback? fun()
---@return LuaCoroutineHandle? handle
function Wait.InputHeld(key, holdSeconds, callback) end

---@param eventName string
---@param timeoutSeconds? number
---@param callback? fun()
---@return (LuaCoroutineHandle|boolean)? result
function Wait.Event(eventName, timeoutSeconds, callback) end

---The Lua `Script` global, exposing the running console/CLI script's own identity and stop state.
---@class ScriptContextLibrary
local ScriptContextLibrary = {}

---The current run's id.
---@return string id The run id.
function ScriptContextLibrary:Id() end

---The current run's display name.
---@return string name The run name.
function ScriptContextLibrary:Name() end

---The current run's kind.
---@return string kind The run kind.
function ScriptContextLibrary:Kind() end

---Whether the host asked this run to stop, for a long-running script to poll and exit cooperatively.
---@return boolean stop True when a stop has been requested.
function ScriptContextLibrary:Stop() end

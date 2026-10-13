---@meta
-- AUTO-GENERATED from analysis of codebase and assets - do not edit by hand.
-- Source: Assets/Code/KSP/game/Missions/KSP2MissionLuaInterface.cs

---Represents the Lua scripting interface for KSP2 missions, exposing mission activation and state-management callbacks to the game script environment.
---@class KSP2MissionLuaInterface
local KSP2MissionLuaInterface = {}

---@param missionID string
function KSP2MissionLuaInterface.ActivateMission(missionID) end

---@param missionID string
---@param state any
function KSP2MissionLuaInterface.SetMissionState(missionID, state) end

---@meta
-- AUTO-GENERATED from analysis of codebase and assets - do not edit by hand.
-- Source: Assets/Modules/Redux.ModApi/PatchManager/Runtime/Parts/AudioLuaModule.cs
-- Source: Assets/Code/Redux/Modules/Data_PartAudioPreset.cs
-- Source: Assets/Code/KSP/Sim/Definitions/ModuleData.cs

---Lua module exposed under the global `PM.Audio`, for binding registered audio presets to parts.
---@class AudioLuaModule
local AudioLuaModule = {}

---Binds a registered audio preset to a transform of a part, adding `Module_PartAudioPreset` when the part
---has none. A binding for the same preset and transform is updated rather than duplicated.
---@param part PartUserData The part to bind the preset to.
---@param presetId string The registered preset ID, matching the PresetId of a PartAudioPresetDefinition.
---@param targetTransformPath? string The transform path within the part, or nil for the part root.
---@param overrideExistingAudio? boolean Whether the preset replaces existing audio components at the transform rather than adding alongside them.
---@error Thrown when part is nil or presetId is blank.
function AudioLuaModule:EnsurePreset(part, presetId, targetTransformPath, overrideExistingAudio) end

---@class _Data_PartAudioPreset : _JsonUserDataBase
---@field ["$type"] "Redux.Modules.Data_PartAudioPreset, Assembly-CSharp" | "Redux.Modules.Data_PartAudioPreset, Ksp2Redux"
---@field Presets JsonList<AudioPresetBinding> The list of audio preset bindings to apply to specific transforms within this part.
---@field ModuleType string Gets the Type of the part behaviour module that owns this data definition.
---@field DataType string Gets the runtime Type of this module data instance.
---@field IsActiveInStagingProp ModuleProperty<boolean> Gets the module property that tracks whether this module is active for staging purposes.

---@alias Data_PartAudioPreset _Data_PartAudioPreset | { ["$type"]: "Redux.Modules.Data_PartAudioPreset, Assembly-CSharp" | "Redux.Modules.Data_PartAudioPreset, Ksp2Redux", Presets: JsonList<AudioPresetBinding>, ModuleType: string, DataType: string, IsActiveInStagingProp: ModuleProperty<boolean> }

---@class _AudioPresetBinding : _JsonUserDataBase
---@field PresetId string The registered preset ID, which must match the PresetId on a PartAudioPresetDefinition asset in the project's audio preset folder.
---@field TargetTransformPath string The transform path within the part at which the preset is applied. An empty value applies the preset at the part root.
---@field OverrideExistingAudio boolean Whether this preset replaces any existing audio components at the target transform, rather than adding new components alongside them.

---@alias AudioPresetBinding _AudioPresetBinding | { PresetId: string, TargetTransformPath: string, OverrideExistingAudio: boolean }

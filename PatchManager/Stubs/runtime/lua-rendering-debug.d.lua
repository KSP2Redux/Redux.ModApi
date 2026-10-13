---@meta
-- AUTO-GENERATED from analysis of codebase and assets - do not edit by hand.
-- Source: Assets/Code/KSP/Rendering/LightingSettingsLua.cs
-- Source: Assets/Code/KSP/Rendering/PlanetSettingsLua.cs
-- Source: Assets/Code/KSP/Rendering/PostProcessingSettingsLua.cs
-- Source: Assets/Code/KSP/VolumeCloud/lua/VolumeCloudLua.cs

---Exposes lighting graphics settings to a Lua script environment by registering callbacks for atmospheric scattering, global illumination, and reflection parameters.
---@class LightingDebug
local LightingDebug = {}

---Sets the atmospheric scattering enabled state from a Lua call.
---@param enabled boolean Non-zero to enable atmospheric scattering. Zero to disable it.
---@return integer result 0 on success.
function LightingDebug.SetAtmosphericScatteringEnabled(enabled) end

---Returns the current atmospheric scattering enabled state to a Lua caller.
---@return boolean enabled True if atmospheric scattering is enabled. Otherwise, false.
function LightingDebug.GetAtmosphericScatteringEnabled() end

---Sets the global illumination enabled state from a Lua call.
---@param enabled boolean Non-zero to enable global illumination. Zero to disable it.
---@return integer result 0 on success.
function LightingDebug.SetGlobalIlluminationEnabled(enabled) end

---Returns the current global illumination enabled state to a Lua caller.
---@return boolean enabled True if global illumination is enabled. Otherwise, false.
function LightingDebug.GetGlobalIlluminationEnabled() end

---Sets the global illumination observer enabled state from a Lua call.
---@param enabled boolean Non-zero to enable the GI observer. Zero to disable it.
---@return integer result 0 on success.
function LightingDebug.SetGlobalIlluminationObserverEnabled(enabled) end

---Returns the current global illumination observer enabled state to a Lua caller.
---@return boolean enabled True if the GI observer is enabled. Otherwise, false.
function LightingDebug.GetGlobalIlluminationObserverEnabled() end

---Sets the reflections enabled state from a Lua call.
---@param enabled boolean Non-zero to enable reflections. Zero to disable them.
---@return integer result 0 on success.
function LightingDebug.SetReflectionsEnabled(enabled) end

---Returns the current reflections enabled state to a Lua caller.
---@return boolean enabled True if reflections are enabled. Otherwise, false.
function LightingDebug.GetReflectionsEnabled() end

---@param intensity integer
---@return integer result
function LightingDebug.SetReflectionIntensity(intensity) end

---@return integer intensity
function LightingDebug.GetReflectionIntensity() end

---@param maxSteps integer
---@return integer result
function LightingDebug.SetReflectionMipBiasMaxSteps(maxSteps) end

---@return integer maxSteps
function LightingDebug.GetReflectionMipBiasMaxSteps() end

---@param rate integer
---@return integer result
function LightingDebug.SetReflectionMipBiasGrowthRate(rate) end

---@return integer rate
function LightingDebug.GetReflectionMipBiasGrowthRate() end

---@param offset integer
---@return integer result
function LightingDebug.SetReflectionMipBiasOffset(offset) end

---@return integer offset
function LightingDebug.GetReflectionMipBiasOffset() end

---Registers planet rendering settings (scatter, collider visibility, and low-quality mode) as callbacks on a Lua script environment.
---@class PlanetDebug
local PlanetDebug = {}

---Sets whether planet scatter rendering is enabled.
---@param enabled boolean True to enable planet scatter rendering, false to disable it.
---@return integer result True if planet scatter rendering is enabled, false otherwise.
function PlanetDebug.SetPlanetScatterEnabled(enabled) end

---Gets whether planet scatter rendering is currently enabled.
---@return boolean enabled True if planet scatter rendering is enabled, false otherwise.
function PlanetDebug.GetPlanetScatterEnabled() end

---Sets whether planet collider rendering is enabled.
---@param enabled boolean True to enable rendering of planet colliders, false to disable it.
---@return integer result True if planet collider rendering is enabled, false otherwise.
function PlanetDebug.SetPlanetRenderCollidersEnabled(enabled) end

---Gets whether planet collider rendering is currently enabled.
---@return boolean enabled True if planet collider rendering is enabled, false otherwise.
function PlanetDebug.GetRenderPlanetCollidersEnabled() end

---Sets whether planet low-quality rendering mode is enabled.
---@param enabled boolean True to enable low-quality planet rendering, false to disable it.
---@return integer result True if low-quality planet rendering is enabled, false otherwise.
function PlanetDebug.SetPlanetLowQualityEnabled(enabled) end

---Gets whether planet low-quality rendering mode is currently enabled.
---@return boolean enabled True if low-quality planet rendering is enabled, false otherwise.
function PlanetDebug.GetPlanetLowQualityEnabled() end

---Represents the Lua scripting bridge for post-processing and tutorial overlay graphics settings.
---@class PostProcessingDebug
local PostProcessingDebug = {}

---Sets whether post-processing effects are enabled.
---@param enabled boolean True to enable post-processing effects, false to disable them.
---@return integer result Zero upon completion.
function PostProcessingDebug.SetPostProcessingEnabled(enabled) end

---Gets whether post-processing effects are currently enabled.
---@return boolean enabled True if post-processing effects are enabled, false otherwise.
function PostProcessingDebug.GetPostProcessingEnabled() end

---Sets whether the tutorial overlay is enabled.
---@param enabled boolean True to enable the tutorial overlay, false to disable it.
---@return integer result Zero upon completion.
function PostProcessingDebug.SetTutorialOverlayEnabled(enabled) end

---Gets whether the tutorial overlay is currently enabled.
---@return boolean enabled True if the tutorial overlay is enabled, false otherwise.
function PostProcessingDebug.GetTutorialOverlayEnabled() end

---Lua binding for VolumeCloudRenderer, registering cloud enable state and raymarch step-size callbacks in a script environment.
---@class VolumeCloud
local VolumeCloud = {}

---@param enableClouds boolean
---@return integer result
function VolumeCloud.EnableClouds(enableClouds) end

---@return boolean enabled
function VolumeCloud.IsEnabledClouds() end

---@param stepSize integer
---@return integer result
function VolumeCloud.SetRaymarchStepSize(stepSize) end

---@return integer stepSize
function VolumeCloud.GetRaymarchStepSize() end

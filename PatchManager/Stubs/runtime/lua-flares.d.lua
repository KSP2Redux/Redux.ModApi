---@meta
-- AUTO-GENERATED from analysis of codebase and assets - do not edit by hand.
-- Source: Assets/Code/Redux/Lua/SunFlareLibrary.cs
-- Source: Assets/Code/Redux/Rendering/SunFlareProfile.cs
-- Source: Assets/Code/Redux/Rendering/SunFlareLayer.cs
-- Source: Assets/Code/Redux/Rendering/SunFlareSource.cs

---The Lua Flares surface for editing named profiles and attaching them to lights.
---@class SunFlareLibrary
local SunFlareLibrary = {}

---Gets or creates an editable named profile.
---@param name string The body name or custom profile identifier.
---@return SunFlareProfile The profile shared by sources using this name.
function SunFlareLibrary:Profile(name) end

---Attaches a profile to a directional or local light.
---@param light userdata The source light.
---@param profile string The profile identifier.
---@return SunFlareSource The configurable source.
function SunFlareLibrary:Attach(light, profile) end

---A named, Lua-editable optical profile shared by light sources.
---@class SunFlareProfile
---@field OffscreenFade number Gets or sets the viewport distance over which an offscreen source fades. Zero hides it immediately.
---@field ShaderName string Gets or sets the loaded shader name. Changing it clears shader parameter overrides and invalidates layer references.
local SunFlareProfile = {}

---Gets an editable layer declared by the selected shader as a vector named _FlareLayer_Name.
---@param name string The case-sensitive layer name, without the _FlareLayer_ prefix.
---@return SunFlareLayer The shared width, height, brightness and rotation parameters for this layer.
function SunFlareProfile:Layer(name) end

---Sets the profile tint using linear RGB values.
---@param red number The red multiplier.
---@param green number The green multiplier.
---@param blue number The blue multiplier.
function SunFlareProfile:SetTint(red, green, blue) end

---Overrides a float or range declared in the shader's Properties block. Range values are clamped to its limits.
---@param name string The case-sensitive shader property name.
---@param value number The finite property value.
function SunFlareProfile:SetFloat(name, value) end

---Overrides a vector declared in the shader's Properties block.
---Named layer width, height and brightness must be nonnegative.
---@param name string The case-sensitive shader property name.
---@param x number The finite first component.
---@param y number The finite second component.
---@param z number The finite third component.
---@param w number The finite fourth component.
function SunFlareProfile:SetVector(name, x, y, z, w) end

---Restores the selected shader's defaults, white tint and default offscreen fade. Existing layer references remain valid.
function SunFlareProfile:Reset() end

---The size, intensity and rotation of a shader-declared flare layer, editable from Lua.
---@class SunFlareLayer
---@field Width number Gets or sets the width as a fraction of image height before the source's scale is applied.
---@field Height number Gets or sets the height as a fraction of image height before the source's scale is applied.
---@field Brightness number Gets or sets the layer intensity. Zero hides this layer.
---@field Rotation number Gets or sets the counterclockwise rotation in degrees.

---A light source rendered with a named optical profile, independently of ProFlares.
---@class SunFlareSource
---@field ProfileName string Gets or sets the named profile selected by this source.
---@field Scale number Gets or sets the multiplier for all profile layer sizes. Zero hides the source.
---@field Brightness number Gets or sets optical intensity, independent of Light.intensity. Zero hides the source.
---@field AngularRadius number Gets or sets the angular radius in radians used to average visibility across the source disc.
---@field IsVisible boolean Whether the source is allowed to render.
---@field enabled boolean The inherited Unity component enable switch.

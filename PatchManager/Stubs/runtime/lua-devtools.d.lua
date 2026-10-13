---@meta
-- AUTO-GENERATED from analysis of codebase and assets - do not edit by hand.
-- Source: Assets/Code/Root/CheatSystem.cs
-- Source: Assets/Code/Root/DebugVisualizer.cs
-- Source: Assets/Code/KSP/DebugTools/QuickActionDebugTools.cs
-- Source: Assets/Code/KSP/DebugTools/FXDebugTools.cs
-- Source: Assets/Code/Root/KSPAudioDebug.cs
-- Source: Assets/Code/KSP/Audio/Ambience/AmbienceManager.cs

---MonoBehaviour that manages runtime cheat and debug options, exposing toggles for physics, aerodynamics, thermodynamics, part unlocking, and resource cheats.
---@class Cheats
local Cheats = {}

---Enables all cheat items marked with the quick-debug flag.
function Cheats.QuickDebugEnable() end

---Restores all quick-debug cheat items to their default values.
function Cheats.QuickDebugRestore() end

---Sets the no-crash-damage cheat to the specified value.
---@param noCrashDamage boolean Pass `true` to disable crash damage. Pass `false` to re-enable it.
---@return boolean result `true` if the value was applied. Otherwise, `false`.
function Cheats.SetNoCrashDamage(noCrashDamage) end

---Returns a value indicating whether the no-crash-damage cheat is currently enabled.
---@return boolean enabled `true` if no-crash-damage is active. Otherwise, `false`.
function Cheats.GetNoCrashDamage() end

---Sets the infinite-propellant difficulty option to the specified value.
---@param infinitePropellant boolean Pass `true` to enable infinite propellant. Pass `false` to disable it.
---@return boolean result `true` if the option was applied, `false` if the session is unavailable.
function Cheats.SetInfinitePropellant(infinitePropellant) end

---Returns a value indicating whether the infinite-propellant difficulty option is currently enabled.
---@return boolean enabled `true` if infinite propellant is active, `false` if it is inactive or the session is unavailable.
function Cheats.GetInfinitePropellant() end

---Sets the infinite-electricity difficulty option to the specified value.
---@param infiniteElectricity boolean Pass `true` to enable infinite electricity. Pass `false` to disable it.
---@return boolean result `true` if the option was applied, `false` if the session is unavailable.
function Cheats.SetInfiniteElectricity(infiniteElectricity) end

---Returns a value indicating whether the infinite-electricity difficulty option is currently enabled.
---@return boolean enabled `true` if infinite electricity is active, `false` if it is inactive or the session is unavailable.
function Cheats.GetInfiniteElectricity() end

---Sets the unlock-all-parts cheat to the specified value.
---@param unlockAllParts boolean Pass `true` to unlock all parts. Pass `false` to restore normal unlock rules.
---@return boolean result `true` if the value was applied. Otherwise, `false`.
function Cheats.SetUnlockAllParts(unlockAllParts) end

---Returns a value indicating whether the unlock-all-parts cheat is currently enabled.
---@return boolean enabled `true` if all parts are unlocked. Otherwise, `false`.
function Cheats.GetUnlockAllParts() end

---Sets the disable-gravity cheat to the specified value.
---@param disableGravity boolean Pass `true` to disable gravity. Pass `false` to restore it.
---@return boolean result `true` if the value was applied. Otherwise, `false`.
function Cheats.SetDisableGravity(disableGravity) end

---Returns a value indicating whether the disable-gravity cheat is currently enabled.
---@return boolean enabled `true` if gravity is disabled. Otherwise, `false`.
function Cheats.GetDisableGravity() end

---Sets the disable-aerodynamics cheat to the specified value.
---@param disableAerodynamics boolean Pass `true` to disable aerodynamics. Pass `false` to restore them.
---@return boolean result `true` if the value was applied. Otherwise, `false`.
function Cheats.SetDisableAerodynamics(disableAerodynamics) end

---Returns a value indicating whether the disable-aerodynamics cheat is currently enabled.
---@return boolean enabled `true` if aerodynamics are disabled. Otherwise, `false`.
function Cheats.GetDisableAerodynamics() end

---Sets the disable-thermodynamics cheat to the specified value.
---@param disableThermodynamics boolean Pass `true` to disable thermodynamics. Pass `false` to restore them.
---@return boolean result `true` if the value was applied. Otherwise, `false`.
function Cheats.SetDisableThermodynamics(disableThermodynamics) end

---Returns a value indicating whether the disable-thermodynamics cheat is currently enabled.
---@return boolean enabled `true` if thermodynamics are disabled. Otherwise, `false`.
function Cheats.GetDisableThermodynamics() end

---Resets all cheat item values to their defaults.
function Cheats.ResetAllValues() end

---Sets the heat-part-destruction difficulty option to the specified value.
---@param ignoreMaxTemp boolean Pass `true` to enable heat-part-destruction. Pass `false` to disable it.
---@return boolean result `true` if the option was applied, `false` if the session is unavailable.
function Cheats.SetIgnoreMaxTemp(ignoreMaxTemp) end

---Returns a value indicating whether heat-part-destruction is currently enabled via the session difficulty option.
---@return boolean enabled `true` if heat-part-destruction is active, `false` if it is inactive or the session is unavailable.
function Cheats.GetIgnoreMaxTemp() end

---MonoBehaviour that manages in-game debug visualizations and exposes debug tool toggles to the script environment.
---@class DebugVisualizer
local DebugVisualizer = {}

---Sets whether the FPS frame-time overlay is visible.
---@param show boolean Whether to show the frame-time display.
---@return integer result Zero on completion.
function DebugVisualizer.ShowFrameTimes(show) end

---@return boolean show
function DebugVisualizer.GetShowFrameTimes() end

---@param textPos string
---@return boolean result
function DebugVisualizer.SetTextPosition(textPos) end

---@return boolean result
function DebugVisualizer.ToggleLocalizationDebugMode() end

---@return boolean result
function DebugVisualizer.QAPrintPartsInBuildersCSVs() end

function DebugVisualizer.ToggleVesselScience() end

function DebugVisualizer.ToggleKerbalTools() end

function DebugVisualizer.ToggleScienceTools() end

function DebugVisualizer.ToggleScienceExperimentResearchReportTools() end

function DebugVisualizer.ToggleSimObjectTools() end

function DebugVisualizer.ToggleOABAssemblyOverlay() end

function DebugVisualizer.ToggleOABSizeLimits() end

function DebugVisualizer.ToggleFXTools() end

function DebugVisualizer.ToggleRenderingTools() end

function DebugVisualizer.ToggleTerrainTools() end

function DebugVisualizer.TogglePlanetViewer() end

function DebugVisualizer.ToggleTeleportWindow() end

function DebugVisualizer.ToggleTeleportBookmarkWalkWindow() end

function DebugVisualizer.ToggleLogConsole() end

function DebugVisualizer.ToggleThermalDebugTool() end

function DebugVisualizer.ToggleVesselCoordinateLocation() end

---@param text string
---@param isTextOnly boolean
function DebugVisualizer.PassiveTierNotification(text, isTextOnly) end

---@param titleText string
---@param firstLineItemtext string
---@param secondLineItemtext string
---@param thirdLineItemtext string
---@param timerDuration number
---@param isHighLevelOfImportance boolean
---@param isMediumLevelOfImportance boolean
function DebugVisualizer.AlertTierNotification(titleText, firstLineItemtext, secondLineItemtext, thirdLineItemtext, timerDuration, isHighLevelOfImportance, isMediumLevelOfImportance) end

---@param titleContentText string
---@param bodyContentText string
---@param isTextOnly boolean
---@param isTextAndIcon boolean
function DebugVisualizer.AdminTierNotification(titleContentText, bodyContentText, isTextOnly, isTextAndIcon) end

---@param enableTimer boolean
function DebugVisualizer.EnableNotificationTimer(enableTimer) end

function DebugVisualizer.ToggleShowDebugLoginButtons() end

function DebugVisualizer.PlaceKerbal() end

function DebugVisualizer.RestartLegalAcceptance() end

---@param hidden boolean
function DebugVisualizer.ToggleNotificationsCanvasVisibility(hidden) end

function DebugVisualizer.ToggleAuthenticationInfo() end

function DebugVisualizer.ToggleVesselJointsAndModes() end

function DebugVisualizer.ToggleRenderDragBounds() end

---@param show boolean
---@return integer result
function DebugVisualizer.ShowTextureStreamingStats(show) end

---@return boolean show
function DebugVisualizer.GetShowTextureStreamingStats() end

---@param enabled boolean
---@return integer result
function DebugVisualizer.SetTextureStreaming(enabled) end

---@return boolean enabled
function DebugVisualizer.GetTextureStreaming() end

---@param newValue integer
---@return integer budget
function DebugVisualizer.SetTextureStreamingBudget(newValue) end

---@return integer budget
function DebugVisualizer.GetTextureStreamingBudget() end

---API for registering quick-action debug script callbacks that spawn vessels in various game states.
---@class QuickActions
local QuickActions = {}

function QuickActions.ShipInVAB() end

function QuickActions.ShipOnLaunchpad() end

function QuickActions.ShipInOrbit() end

function QuickActions.PlaneInVAB() end

function QuickActions.PlaneOnRunway() end

function QuickActions.VABSurface() end

function QuickActions.VABOrbit() end

function QuickActions.BAESurface() end

function QuickActions.BAEOrbit() end

---Represents a debug window for inspecting and controlling visual effects on simulation parts and vessels.
---@class FXDebugTools
local FXDebugTools = {}

---Makes the VFX test suite dialog visible.
function FXDebugTools.ShowVFXTestSuite() end

---Sets whether the alternate reentry intensity calculation method is active.
---@param show boolean True to enable the alternate calculation. False to use the default.
function FXDebugTools.SetShowAlternateReentryCalc(show) end

---Returns a value indicating whether the alternate reentry intensity calculation is currently enabled.
---@return boolean enabled True if the alternate calculation is active. Otherwise, false.
function FXDebugTools.GetShowAlternateReentryCalc() end

---Sets whether explosion visual effects are enabled.
---@param show boolean True to enable explosion FX. False to disable.
function FXDebugTools.SetShowExplosionFX(show) end

---Returns a value indicating whether explosion visual effects are currently enabled.
---@return boolean enabled True if explosion FX are enabled. Otherwise, false.
function FXDebugTools.GetShowExplosionFX() end

---Sets whether engine visual effects are enabled and publishes a visibility state change message.
---@param show boolean True to enable engine FX. False to disable.
function FXDebugTools.SetShowEngineFX(show) end

---Returns a value indicating whether engine visual effects are currently enabled.
---@return boolean enabled True if engine FX are enabled. Otherwise, false.
function FXDebugTools.GetShowEngineFX() end

---Sets whether part detachment visual effects are enabled.
---@param show boolean True to enable detach FX. False to disable.
function FXDebugTools.SetShowDetachFX(show) end

---Returns a value indicating whether part detachment visual effects are currently enabled.
---@return boolean enabled True if detach FX are enabled. Otherwise, false.
function FXDebugTools.GetShowDetachFX() end

---Sets whether surface impact visual effects are enabled.
---@param show boolean True to enable surface impact FX. False to disable.
function FXDebugTools.SetShowSurfaceImpactFX(show) end

---Returns a value indicating whether surface impact visual effects are currently enabled.
---@return boolean enabled True if surface impact FX are enabled. Otherwise, false.
function FXDebugTools.GetShowSurfaceImpactFX() end

---Sets whether ground blast visual effects are enabled.
---@param show boolean True to enable ground blast FX. False to disable.
function FXDebugTools.SetShowGroundBlastFX(show) end

---Returns a value indicating whether ground blast visual effects are currently enabled.
---@return boolean enabled True if ground blast FX are enabled. Otherwise, false.
function FXDebugTools.GetShowGroundBlastFX() end

---Sets whether shock cone visual effects are enabled.
---@param show boolean True to enable shock cone FX. False to disable.
function FXDebugTools.SetShowShockConeFX(show) end

---Returns a value indicating whether shock cone visual effects are currently enabled.
---@return boolean enabled True if shock cone FX are enabled. Otherwise, false.
function FXDebugTools.GetShowShockConeFX() end

---Sets whether contrail visual effects are enabled.
---@param show boolean True to enable contrail FX. False to disable.
function FXDebugTools.SetShowContrailsFX(show) end

---Returns a value indicating whether contrail visual effects are currently enabled.
---@return boolean enabled True if contrail FX are enabled. Otherwise, false.
function FXDebugTools.GetShowContrailsFX() end

---Sets whether wingtip vortex visual effects are enabled.
---@param show boolean True to enable wingtip vortex FX. False to disable.
function FXDebugTools.SetShowWingtipVorticesFX(show) end

---Returns a value indicating whether wingtip vortex visual effects are currently enabled.
---@return boolean enabled True if wingtip vortex FX are enabled. Otherwise, false.
function FXDebugTools.GetShowWingtipVorticesFX() end

---Sets whether reentry visual effects are enabled.
---@param show boolean True to enable reentry FX. False to disable.
function FXDebugTools.SetShowReentryFX(show) end

---Returns a value indicating whether reentry visual effects are currently enabled.
---@return boolean enabled True if reentry FX are enabled. Otherwise, false.
function FXDebugTools.GetShowReentryFX() end

---Sets whether persistent surface contact visual effects are enabled.
---@param show boolean True to enable persistent surface contact FX. False to disable.
function FXDebugTools.SetShowPersistantSurfaceContactFX(show) end

---Returns a value indicating whether persistent surface contact visual effects are currently enabled.
---@return boolean enabled True if persistent surface contact FX are enabled. Otherwise, false.
function FXDebugTools.GetShowPersistantSurfaceContactFX() end

---Sets whether wheel surface contact visual effects are enabled.
---@param show boolean True to enable wheel surface FX. False to disable.
function FXDebugTools.SetShowWheelSurfaceFX(show) end

---Returns a value indicating whether wheel surface contact visual effects are currently enabled.
---@return boolean enabled True if wheel surface FX are enabled. Otherwise, false.
function FXDebugTools.GetShowWheelSurfaceFX() end

---Sets whether Kerbal poof visual effects are enabled.
---@param show boolean True to enable Kerbal poof FX. False to disable.
function FXDebugTools.SetShowKerbalPoofFX(show) end

---Returns a value indicating whether Kerbal poof visual effects are currently enabled.
---@return boolean enabled True if Kerbal poof FX are enabled. Otherwise, false.
function FXDebugTools.GetShowKerbalPoofFX() end

---Sets whether Kerbal footstep visual effects are enabled.
---@param show boolean True to enable Kerbal footstep FX. False to disable.
function FXDebugTools.SetShowKerbalFootstepFX(show) end

---Returns a value indicating whether Kerbal footstep visual effects are currently enabled.
---@return boolean enabled True if Kerbal footstep FX are enabled. Otherwise, false.
function FXDebugTools.GetShowKerbalFootstepFX() end

---Sets whether launch smoke visual effects are enabled.
---@param show boolean True to enable launch smoke FX. False to disable.
function FXDebugTools.SetShowLaunchSmokeFX(show) end

---Returns a value indicating whether launch smoke visual effects are currently enabled.
---@return boolean enabled True if launch smoke FX are enabled. Otherwise, false.
function FXDebugTools.GetShowLaunchSmokeFX() end

---Sets whether launch fire visual effects are enabled.
---@param show boolean True to enable launch fire FX. False to disable.
function FXDebugTools.SetShowLaunchFireFX(show) end

---Returns a value indicating whether launch fire visual effects are currently enabled.
---@return boolean enabled True if launch fire FX are enabled. Otherwise, false.
function FXDebugTools.GetShowLaunchFireFX() end

---Sets whether launch fuel tank ice visual effects are enabled.
---@param show boolean True to enable launch fuel tank ice FX. False to disable.
function FXDebugTools.SetShowLaunchFuelTankIceFX(show) end

---Returns a value indicating whether launch fuel tank ice visual effects are currently enabled.
---@return boolean enabled True if launch fuel tank ice FX are enabled. Otherwise, false.
function FXDebugTools.GetShowLaunchFuelTankIceFX() end

---Registers audio debug script callbacks into the game environment for testing audio events and ambience.
---@class AudioDebug
local AudioDebug = {}

---@param enable boolean
function AudioDebug.ToggleRealSpace(enable) end

---@param eventName string
function AudioDebug.TriggerDebugAudioEvent(eventName) end

---Fires the debug ambience event tree for diagnostic purposes.
function AudioDebug.AmbienceFireDebugEvent() end

---Rebuilds the ambience tree data by invoking the build operation on the current instance.
function AudioDebug.AmbienceRebuildTrees() end

---Logs the recorded construction history of ambience tree items to the debug output.
function AudioDebug.AmbiencePrintLog() end

---Stops all active ambience events on the current instance.
function AudioDebug.AmbienceStopAll() end

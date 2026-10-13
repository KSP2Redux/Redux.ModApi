---@meta
-- AUTO-GENERATED from analysis of codebase and assets - do not edit by hand.
-- Source: Assets/Modules/Redux.ModApi/PatchManager/Runtime/CampaignPacks/CampaignPacksLuaModule.cs
-- Source: Assets/Modules/Redux.ModApi/PatchManager/Runtime/Generic/JsonConverter.cs
-- Source: Assets/Code/Redux/Packs/CampaignPackManager.cs
-- Source: Assets/Code/Redux/Packs/CampaignPack.cs

---Lua submodule exposed as `PM.CampaignPacks`, providing patches and creation for campaign packs.
---@class CampaignPacksLuaModule
local CampaignPacksLuaModule = {}

---Registers a campaign pack patch with the given namespaced patch name.
---@param name string The patch's local name, namespaced with the host mod's ID.
---@return PatchDefinition<CampaignPackUserData, any> patch The registered patch.
function CampaignPacksLuaModule:Patch(name) end

---Registers a patch that copies each campaign pack it matches under a new name, then runs patchMethod
---on the copy.
---@param source string The name of the campaign pack to copy. Supports `*` and `?` wildcards.
---@param newName string The name of the copy, with `{name}` standing in for the source's name.
---@param patchMethod? fun(copy: CampaignPackUserData): string? The callback to run on each copy, or `nil` to copy without changes.
---@return PatchDefinition<CampaignPackUserData, any> patch The registered patch.
function CampaignPacksLuaModule:Duplicate(source, newName, patchMethod) end

---Creates a new campaign pack with the given ID and runs callback against it for further
---configuration.
---@param id string The campaign pack ID, which also names the created asset.
---@param callback fun(pack: CampaignPackUserData) Callback that receives the new campaign pack for further configuration.
---@return LuaAsset asset The queued asset, for gating it with features.
function CampaignPacksLuaModule:CreateCampaignPack(id, callback) end

---@class CampaignPackUserData : _CampaignPack, JsonUserData

---The data model for a single campaign pack. That can be tied to a campaign.
---@class _CampaignPack : _JsonUserDataBase
---@field CampaignPackId string This is the internal identifier for the campaign pack
---@field CampaignPackLocalizationKey string This is the localization key for the campaign pack
---@field CampaignPackDescriptionLocalizationKey? string The localization key for the description shown on the campaign pack's card, or nil for none
---@field ImageKey? string The addressable key of the sprite shown on the campaign pack's card, or nil to show the title and description only
---@field Galaxy string The key of the galaxy definition this campaign pack uses
---@field Features JsonList<string> The features a campaign started from this pack is played with, besides every installed mod's default.
---@field GameModes JsonList<string> The ids of the game modes a campaign can be started from this pack in, or empty for every game mode.

---@alias CampaignPack _CampaignPack | { CampaignPackId: string, CampaignPackLocalizationKey: string, CampaignPackDescriptionLocalizationKey?: string, ImageKey?: string, Galaxy: string, Features: JsonList<string>, GameModes: JsonList<string> }

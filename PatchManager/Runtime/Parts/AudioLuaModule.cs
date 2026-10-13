using MoonSharp.Interpreter;
using Newtonsoft.Json.Linq;
using PatchManager.LuaPatching;
using PatchManager.LuaPatching.Attributes;
using PatchManager.LuaPatching.Builtin;
using PatchManager.Parts.UserData;

namespace PatchManager.Parts
{
    /// <summary>
    /// Lua module exposed under the global <c>PM.Audio</c>, for binding registered audio presets to parts.
    /// </summary>
    [PatchManagerModule("Audio")]
    [MoonSharpUserData]
    public class AudioLuaModule
    {
        /// <summary>
        /// Creates the module. It keeps no state, so both arguments are unused.
        /// </summary>
        /// <param name="pmc">The <c>PM</c> instance the module is exposed under.</param>
        /// <param name="universe">The patching universe.</param>
        public AudioLuaModule(PatchManagerCore pmc, Universe universe)
        {
        }

        /// <summary>
        /// Binds a registered audio preset to a transform of a part, adding <c>Module_PartAudioPreset</c> when the part
        /// has none. A binding for the same preset and transform is updated rather than duplicated.
        /// </summary>
        /// <param name="part">The part to bind the preset to.</param>
        /// <param name="presetId">The registered preset ID, matching the PresetId of a PartAudioPresetDefinition.</param>
        /// <param name="targetTransformPath">The transform path within the part, or nil for the part root.</param>
        /// <param name="overrideExistingAudio">
        /// Whether the preset replaces existing audio components at the transform rather than adding alongside them.
        /// </param>
        /// <exception cref="ScriptRuntimeException">Thrown when <paramref name="part" /> is nil or <paramref name="presetId" /> is blank.</exception>
        public void EnsurePreset(
            PartUserData part,
            string presetId,
            string targetTransformPath = null,
            bool overrideExistingAudio = false)
        {
            if (part == null)
                throw new ScriptRuntimeException("PM.Audio:EnsurePreset requires a part.");
            if (string.IsNullOrWhiteSpace(presetId))
                throw new ScriptRuntimeException("PM.Audio:EnsurePreset requires a preset ID.");

            part.EnsureModule("Module_PartAudioPreset", module =>
            {
                module.EnsureDataObject("PartAudioPreset", dataObject =>
                {
                    if (dataObject["Presets"] is not JArray presets)
                    {
                        presets = new JArray();
                        dataObject["Presets"] = presets;
                    }

                    string normalizedTarget = targetTransformPath ?? string.Empty;
                    foreach (JToken preset in presets)
                    {
                        if (preset is not JObject presetObject)
                            continue;

                        string existingId = presetObject.Value<string>("PresetId");
                        string existingTarget = presetObject.Value<string>("TargetTransformPath") ?? string.Empty;
                        if (existingId == presetId && existingTarget == normalizedTarget)
                        {
                            presetObject["OverrideExistingAudio"] = overrideExistingAudio;
                            return;
                        }
                    }

                    presets.Add(new JObject
                    {
                        ["PresetId"] = presetId,
                        ["TargetTransformPath"] = targetTransformPath,
                        ["OverrideExistingAudio"] = overrideExistingAudio
                    });
                });
            });
        }
    }
}

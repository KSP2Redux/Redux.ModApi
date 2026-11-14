using System;
using System.Collections.Generic;
using System.Text;
using KSP.Game;
using KSP.OAB;
using KSP.Sim.impl;
using Redux.QA;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UIElements;
using VSwift.Modules.Behaviours;

namespace DebugTools.Runtime.Controllers
{
    /// <summary>
    /// Material inspection and reversible VAB appearance previews for one part.
    /// </summary>
    public class PartAppearanceWindowController : BaseWindowController
    {
        private const string OVERRIDE_ID = "qa:part-appearance";
        private const string SHADER_KEY = "QA/PartChannel.shader";
        private readonly List<(string Id, string Name, Transform Root)> _parts = new();
        private readonly List<(Renderer Renderer, Material[] Materials)> _materials = new();
        private readonly List<Material> _temporary = new();
        private readonly List<Action> _visualRestore = new();
        private readonly List<(Module_PartSwitch Module, string[] Variants)> _variants = new();
        private readonly List<Action> _variantResourceRestore = new();
        private readonly Dictionary<Animator, (AnimationClip Clip, float Position)> _clips = new();
        private readonly Dictionary<Animator, GameObject> _animationProxies = new();
        private readonly Dictionary<Transform, Transform> _poseTargets = new();
        private readonly Dictionary<Light, float> _lightPreview = new();
        private AsyncOperationHandle<Shader> _shader;
        private DropdownField _subject;
        private DropdownField _view;
        private Transform _root;
        private Label _status;
        private Label _inventory;
        private bool _previewing;
        private bool _restoring;
        private GameState _scene;
        private IVisualElementScheduledItem _previewTick;
        private ObjectAssemblyBuilderEvents _previewEvents;

        private void OnEnable()
        {
            _subject = RootElement.Q<DropdownField>("subject");
            _view = RootElement.Q<DropdownField>("view");
            _status = RootElement.Q<Label>("status");
            _inventory = RootElement.Q<Label>("inventory");
            _view.choices = new List<string> { "Lit", "Albedo", "Normal (tangent)", "Smoothness", "Metallic", "Paint mask (RG, dirt B)", "Emissive", "AO" };
            _view.SetValueWithoutNotify("Lit");
            _subject.RegisterValueChangedCallback(_ => Select());
            _view.RegisterValueChangedCallback(_ => ApplyView());
            RootElement.Q<Button>("refresh").clicked += Refresh;
            RootElement.Q<Button>("selected").clicked += UseSelected;
            RootElement.Q<Button>("stop").clicked += Stop;
            RootElement.Q<Button>("capture").clicked += Capture;
            Refresh();
        }

        /// <inheritdoc />
        protected override void OnRequestedOpenChanged(bool isOpen)
        {
            if (isOpen)
                Refresh();
            else
                Stop();
        }

        private void OnDisable() => Stop();
        private void OnDestroy() => Stop();

        private void Refresh()
        {
            Stop();
            _parts.Clear();
            var names = new List<string>();
            foreach (var part in PartDiagnostics.Subjects(Game))
            {
                Transform root = null;
                if (part is IObjectAssemblyPart assembly)
                    root = assembly.PartTransform;
                else if (part is PartComponent flight)
                {
                    var behaviour = Game.ViewController.GetBehaviorIfLoaded(flight);
                    if (behaviour != null)
                        root = behaviour.transform;
                }
                if (root == null)
                    continue;
                _parts.Add((part.GlobalId.ToString(), part.Name, root));
                names.Add($"{part.Name} | {part.GlobalId}");
            }
            _subject.choices = names;
            _subject.SetValueWithoutNotify(names.Count > 0 ? names[0] : "");
            Select();
        }

        private void UseSelected()
        {
            var builder = Game.OAB.Current;
            var part = builder == null ? null : builder.ActivePartTracker.PartBeingManipulated ?? builder.ActivePartTracker.partGrabbed;
            Refresh();
            if (part == null)
                return;
            int index = _parts.FindIndex(candidate => candidate.Root == part.PartTransform);
            if (index >= 0)
                _subject.index = index;
        }

        private bool Owns(Component component)
        {
            if (_root == null || component == null)
                return false;
            // Attached parts are descendants too. Stop at the nearest current part root.
            for (Transform cursor = component.transform; cursor != null; cursor = cursor.parent)
            {
                if (cursor == _root)
                    return true;
                foreach (var part in _parts)
                {
                    if (cursor == part.Root)
                        return false;
                }
            }
            return false;
        }

        private bool OwnsMaterialRenderer(Renderer renderer)
        {
            if (!Owns(renderer))
                return false;
            for (Transform cursor = renderer.transform; cursor != null && cursor != _root; cursor = cursor.parent)
            {
                if (cursor.GetComponent<ObjectAssemblyPartNode>() != null)
                    return false;
            }
            // FinalizeLoad places the model first. Builder markers and highlight helpers are siblings.
            // An Outline component on a real part renderer is retained, since it draws via the camera.
            return _scene != GameState.VehicleAssemblyBuilder ||
                (_root.childCount > 0 && renderer.transform.IsChildOf(_root.GetChild(0)));
        }

        private void Select()
        {
            Stop();
            _root = _subject.index >= 0 && _subject.index < _parts.Count ? _parts[_subject.index].Root : null;
            _scene = Game.GlobalGameState.GetState();
            Inspect();
            BuildScrubbers();
        }

        private void Inspect()
        {
            var text = new StringBuilder();
            if (_root != null)
                foreach (Renderer renderer in _root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!OwnsMaterialRenderer(renderer))
                        continue;
                    Material[] materials = renderer.sharedMaterials;
                    for (int slot = 0; slot < materials.Length; slot++)
                    {
                        Material material = materials[slot];
                        if (material == null || material.shader == null)
                            continue;
                        text.AppendLine($"{renderer.name} / slot {slot}: {material.name}\nShader: {material.shader.name}");
                        foreach (string property in material.GetTexturePropertyNames())
                        {
                            Texture texture = material.GetTexture(property);
                            text.AppendLine(texture == null ? $"  {property}: none" :
                                FormattableString.Invariant($"  {property}: {texture.name}, {texture.width} x {texture.height}, {texture.graphicsFormat}"));
                        }
                    }
                }
            _inventory.text = text.Length == 0 ? "No loaded materials on this part." : text.ToString();
            _status.text = "Views show texture channels, before lighting. Paint uses UV1. Stop and close revert all previews. Refresh after craft edits.";
        }

        private void Register()
        {
            if (_previewEvents == null && Game.OAB.Current != null)
            {
                _previewEvents = Game.OAB.Current.eventsBuilder;
                _previewEvents.PreOnLaunchAssemblyAtLocation += BeforeLaunch;
                _previewEvents.PreOnSaveAssembly += Stop;
                _previewEvents.PreOnSaveSubAssembly += Stop;
            }
            DebugOverrides.Report(OVERRIDE_ID, "Part material / VAB appearance preview", Stop);
            if (_previewTick == null)
                _previewTick = RootElement.schedule.Execute(WatchPreview).Every(16);
            else
                _previewTick.Resume();
        }

        private void ApplyView()
        {
            if (_restoring)
                return;
            string requested = _view.value;
            if (_previewing)
            {
                StopVisuals();
                _view.SetValueWithoutNotify(requested);
            }
            RestoreMaterials();
            if (_view.index <= 0 || _root == null)
            {
                if (!_previewing && _variants.Count == 0)
                {
                    DebugOverrides.Clear(OVERRIDE_ID);
                    _previewTick?.Pause();
                }
                return;
            }
            if (!_shader.IsValid())
            {
                _shader = Addressables.LoadAssetAsync<Shader>(SHADER_KEY);
                _shader.Completed += _ =>
                {
                    if (this != null && IsRequestedOpen && _shader.IsValid())
                        ApplyView();
                };
                return;
            }
            if (!_shader.IsDone)
                return;
            if (_shader.Status != AsyncOperationStatus.Succeeded || _shader.Result == null)
            {
                _status.text = "Channel shader unavailable. Lit view retained.";
                return;
            }
            Register();
            foreach (Renderer renderer in _root.GetComponentsInChildren<Renderer>(true))
            {
                if (!OwnsMaterialRenderer(renderer))
                    continue;
                Material[] original = renderer.sharedMaterials;
                var replacement = (Material[])original.Clone();
                for (int slot = 0; slot < original.Length; slot++)
                {
                    Material source = original[slot];
                    if (source == null || source.shader == null ||
                        (source.shader.name != "KSP2/Scenery/Standard (Opaque)" &&
                         source.shader.name != "KSP2/Parts/Paintable" && source.shader.name != "KSP2/Parts/Standard Damaged"))
                        continue;
                    var material = new Material(_shader.Result) { name = "QA channel preview", hideFlags = HideFlags.DontSave };
                    material.CopyPropertiesFromMaterial(source);
                    material.SetFloat("_SmoothnessOverride", source.shader.name == "KSP2/Parts/Standard Damaged" ||
                        source.IsKeywordEnabled("_SMOOTHNESSOVERRIDE_ON") ? 1f : 0f);
                    material.SetFloat("_QaChannel", _view.index);
                    _temporary.Add(material);
                    replacement[slot] = material;
                }
                _materials.Add((renderer, original));
                renderer.sharedMaterials = replacement;
            }
            _status.text = "Channel preview active. Unsupported non-paintable slots retain Lit. Raw maps exclude detail normals, damage, heating and sun fade.";
        }

        private void RestoreMaterials()
        {
            foreach (var entry in _materials)
            {
                if (entry.Renderer != null)
                    entry.Renderer.sharedMaterials = entry.Materials;
            }
            _materials.Clear();
            foreach (Material material in _temporary)
            {
                if (material != null)
                    Destroy(material);
            }
            _temporary.Clear();
        }

        private void AddSlider(VisualElement parent, string title, float maximum, Action<float> changed)
        {
            var label = new Label(title);
            label.AddToClassList("appearance-wrap");
            parent.Add(label);
            var slider = new Slider(0f, maximum);
            slider.AddToClassList("kui-field");
            slider.RegisterValueChangedCallback(evt =>
            {
                try
                {
                    changed(evt.newValue);
                    label.text = FormattableString.Invariant($"{title}: {evt.newValue:0.000}");
                }
                catch (Exception exception)
                {
                    Stop();
                    _status.text = exception.Message;
                }
            });
            parent.Add(slider);
        }

        private void BuildScrubbers()
        {
            VisualElement controls = RootElement.Q("scrubbers");
            controls.Clear();
            if (_root == null || Game.GlobalGameState.GetState() != GameState.VehicleAssemblyBuilder)
            {
                controls.Add(new Label("Animation, variant and light previews need the VAB."));
                return;
            }
            foreach (var module in _root.GetComponentsInChildren<KSP.Sim.Definitions.PartBehaviourModule>(true))
            {
                if (Owns(module))
                    controls.Add(new Label(module.GetModuleDisplayName()));
            }
            foreach (Animator animator in _root.GetComponentsInChildren<Animator>(true))
            {
                if (!Owns(animator) || animator.runtimeAnimatorController == null)
                    continue;
                var seen = new HashSet<AnimationClip>();
                foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                {
                    if (clip == null || !seen.Add(clip))
                        continue;
                    AddSlider(controls, $"{animator.name} / {clip.name} (fraction)", 1f, value =>
                    {
                        BeginVisualPreview();
                        _clips[animator] = (clip, value);
                        SampleClips();
                    });
                }
            }
            foreach (Light light in _root.GetComponentsInChildren<Light>(true))
            {
                if (!Owns(light))
                    continue;
                AddSlider(controls, $"{light.name} light intensity", Mathf.Max(8f, light.intensity), value =>
                {
                    BeginVisualPreview();
                    _lightPreview[light] = value;
                    light.enabled = value > 0f;
                    light.intensity = value;
                });
            }
            foreach (Renderer renderer in _root.GetComponentsInChildren<Renderer>(true))
            {
                if (!OwnsMaterialRenderer(renderer))
                    continue;
                Material[] materials = renderer.sharedMaterials;
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    Material material = materials[slot];
                    if (material == null || !material.HasProperty("_EmissionColor"))
                        continue;
                    int index = slot;
                    Color color = material.GetColor("_EmissionColor");
                    if (color.maxColorComponent <= 0f)
                        color = Color.white;
                    Color baseColor = color;
                    AddSlider(controls, $"{renderer.name} slot {slot} emission multiplier", 8f, value =>
                    {
                        BeginVisualPreview();
                        var block = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(block, index);
                        block.SetColor("_EmissionColor", baseColor * value);
                        renderer.SetPropertyBlock(block, index);
                    });
                }
            }
            foreach (Module_PartSwitch module in _root.GetComponentsInChildren<Module_PartSwitch>(true))
            {
                if (!Owns(module) || module.DataPartSwitch == null)
                    continue;
                for (int index = 0; index < module.DataPartSwitch.VariantSets.Count; index++)
                {
                    int setIndex = index;
                    var set = module.DataPartSwitch.VariantSets[index];
                    controls.Add(new Label($"Variant set: {set.VariantSetId} (reverted on Stop / close)"));
                    foreach (var variant in set.Variants)
                    {
                        string id = variant.VariantId;
                        var button = new Button(() => SwitchVariant(module, setIndex, id)) { text = id };
                        button.AddToClassList("kui-button");
                        controls.Add(button);
                    }
                }
            }
        }

        private void BeginVisualPreview()
        {
            if (_previewing)
                return;
            RestoreMaterials();
            _view.SetValueWithoutNotify("Lit");
            Register();
            _previewing = true;
            foreach (Transform transform in _root.GetComponentsInChildren<Transform>(true))
            {
                if (transform == _root || !Owns(transform))
                    continue;
                Vector3 position = transform.localPosition;
                Quaternion rotation = transform.localRotation;
                Vector3 scale = transform.localScale;
                bool active = transform.gameObject.activeSelf;
                _visualRestore.Add(() =>
                {
                    if (transform == null)
                        return;
                    transform.SetLocalPositionAndRotation(position, rotation);
                    transform.localScale = scale;
                    transform.gameObject.SetActive(active);
                });
            }
            foreach (Animator animator in _root.GetComponentsInChildren<Animator>(true))
            {
                if (!Owns(animator))
                    continue;
                bool enabled = animator.enabled;
                _visualRestore.Add(() => { if (animator != null) animator.enabled = enabled; });
                animator.enabled = false;
            }
            foreach (Light light in _root.GetComponentsInChildren<Light>(true))
            {
                if (!Owns(light))
                    continue;
                float intensity = light.intensity;
                bool enabled = light.enabled;
                Color color = light.color;
                float range = light.range;
                float spotAngle = light.spotAngle;
                _visualRestore.Add(() =>
                {
                    if (light == null)
                        return;
                    light.intensity = intensity;
                    light.enabled = enabled;
                    light.color = color;
                    light.range = range;
                    light.spotAngle = spotAngle;
                });
            }
            foreach (Renderer renderer in _root.GetComponentsInChildren<Renderer>(true))
            {
                if (!OwnsMaterialRenderer(renderer))
                    continue;
                Material[] original = renderer.sharedMaterials;
                for (int slot = 0; slot < original.Length; slot++)
                {
                    int index = slot;
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block, index);
                    _visualRestore.Add(() => { if (renderer != null) renderer.SetPropertyBlock(block.isEmpty ? null : block, index); });
                }
            }
        }

        private void SampleClips()
        {
            foreach (var entry in _clips)
            {
                if (entry.Key == null || entry.Value.Clip == null)
                    continue;
                if (!_animationProxies.TryGetValue(entry.Key, out GameObject proxy))
                {
                    proxy = ClonePose(entry.Key.transform, null).gameObject;
                    _animationProxies.Add(entry.Key, proxy);
                }
                // Sample only a transform hierarchy. Curves cannot touch modules, colliders or material assets.
                entry.Value.Clip.SampleAnimation(proxy, entry.Value.Position * entry.Value.Clip.length);
            }
            foreach (var pair in _poseTargets)
            {
                if (pair.Key == null || pair.Value == null)
                    continue;
                pair.Value.SetLocalPositionAndRotation(pair.Key.localPosition, pair.Key.localRotation);
                pair.Value.localScale = pair.Key.localScale;
            }
        }

        private Transform ClonePose(Transform source, Transform parent)
        {
            var proxy = new GameObject(source.name) { hideFlags = HideFlags.HideAndDontSave };
            proxy.transform.SetParent(parent, false);
            proxy.transform.SetLocalPositionAndRotation(source.localPosition, source.localRotation);
            proxy.transform.localScale = source.localScale;
            if (source != _root)
                _poseTargets.Add(proxy.transform, source);
            foreach (Transform child in source)
            {
                if (Owns(child))
                    ClonePose(child, proxy.transform);
            }
            return proxy.transform;
        }

        private void WatchPreview()
        {
            if (!IsRequestedOpen)
                return;
            if (_root == null || Game.GlobalGameState.GetState() != _scene)
            {
                Stop();
                return;
            }
            if (_previewing)
            {
                SampleClips();
                foreach (var entry in _lightPreview)
                {
                    if (entry.Key == null)
                        continue;
                    entry.Key.enabled = entry.Value > 0f;
                    entry.Key.intensity = entry.Value;
                }
            }
        }

        private void SwitchVariant(Module_PartSwitch module, int setIndex, string id)
        {
            try
            {
                // Release geometry and material snapshots before the VAB replaces visual state.
                StopVisuals();
                if (_variants.Count == 0)
                {
                    var affected = new List<IObjectAssemblyPart> { module.OABPart };
                    if (module.OABPart?.SymmetrySet?.Parts != null)
                        affected.AddRange(module.OABPart.SymmetrySet.Parts);
                    var seen = new HashSet<Module_PartSwitch>();
                    foreach (var part in affected)
                    {
                        if (part == null || part.PartTransform == null)
                            continue;
                        foreach (Module_PartSwitch candidate in part.PartTransform.GetComponentsInChildren<Module_PartSwitch>(true))
                        {
                            if (candidate.OABPart == part && candidate.DataPartSwitch != null && seen.Add(candidate))
                            {
                                var data = candidate.DataPartSwitch;
                                var selections = new string[data.VariantSets.Count];
                                for (int index = 0; index < selections.Length; index++)
                                {
                                    selections[index] = index < data.ActiveVariants.Count ? data.ActiveVariants[index] : data.GetDefaultVariant(index);
                                }
                                _variants.Add((candidate, selections));
                                if (part is ObjectAssemblyPart { Container: { } container })
                                {
                                    foreach (var resourceId in container)
                                    {
                                        var savedId = resourceId;
                                        double stored = container.GetResourceStoredUnits(resourceId);
                                        _variantResourceRestore.Add(() =>
                                        {
                                            if (candidate != null && candidate.OABPart is ObjectAssemblyPart { Container: { } restored })
                                                restored.SetResourceStoredUnits(savedId, stored);
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
                if (!_variants.Exists(entry => entry.Module == module))
                    throw new InvalidOperationException("Cannot preview a variant without its original selection snapshot.");
                Register();
                module.SelectVariantInOab(setIndex, id);
                Inspect();
                BuildScrubbers();
                _status.text = "Variant applied through the VAB, including symmetry. Reverted on Stop / close. VAB undo history still records these edits.";
            }
            catch (Exception exception)
            {
                Stop();
                _status.text = exception.Message;
            }
        }

        private void StopVisuals()
        {
            _clips.Clear();
            foreach (GameObject proxy in _animationProxies.Values)
            {
                if (proxy != null)
                    Destroy(proxy);
            }
            _animationProxies.Clear();
            _poseTargets.Clear();
            _lightPreview.Clear();
            RestoreMaterials();
            for (int index = _visualRestore.Count - 1; index >= 0; index--)
            {
                _visualRestore[index]();
            }
            _visualRestore.Clear();
            _previewing = false;
            if (_view != null)
                _view.SetValueWithoutNotify("Lit");
        }

        private void BeforeLaunch(KSP.Sim.SerializedLocation location) => Stop();

        private void Stop()
        {
            if (_restoring)
                return;
            _restoring = true;
            try
            {
                if (_previewEvents != null)
                {
                    _previewEvents.PreOnLaunchAssemblyAtLocation -= BeforeLaunch;
                    _previewEvents.PreOnSaveAssembly -= Stop;
                    _previewEvents.PreOnSaveSubAssembly -= Stop;
                    _previewEvents = null;
                }
                StopVisuals();
                // Apply each saved selection locally to avoid fanning restoration out to a changed symmetry set.
                foreach (var entry in _variants)
                {
                    if (entry.Module == null || entry.Module.DataPartSwitch == null)
                        continue;
                    entry.Module.RestoreVariantSelectionInOab(entry.Variants);
                }
                _variants.Clear();
                foreach (Action restore in _variantResourceRestore)
                {
                    restore();
                }
                _variantResourceRestore.Clear();
                DebugOverrides.Clear(OVERRIDE_ID);
                _previewTick?.Pause();
                if (_shader.IsValid())
                    Addressables.Release(_shader);
                _shader = default;
                if (_status != null)
                    _status.text = "Preview stopped. Material assignments, visual state and variant selections restored.";
            }
            finally
            {
                _restoring = false;
            }
        }

        private void Capture()
        {
            if (_subject.index < 0 || _subject.index >= _parts.Count)
                return;
            var part = _parts[_subject.index];
            var report = new QaReport("Part appearance", "bench-run");
            var result = new QaReportResult(part.Id, part.Name, "Material and appearance", "snapshot", QaVerdict.NeedsReview, "Visual review required.");
            result.Evidence.Add(new Newtonsoft.Json.Linq.JObject { ["type"] = "log", ["caption"] = _view.value, ["text"] = _inventory.text });
            report.Add(result);
            _status.text = report.Write();
        }
    }
}

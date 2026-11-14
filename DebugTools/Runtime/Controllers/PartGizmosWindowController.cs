using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using KSP.Game;
using KSP.Modules;
using KSP.OAB;
using KSP.Sim;
using KSP.Sim.Definitions;
using KSP.Sim.impl;
using Redux.QA;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace DebugTools.Runtime.Controllers
{
    /// <summary>
    /// Read-only runtime geometry inspection for the current VAB craft or flight vessel.
    /// </summary>
    public class PartGizmosWindowController : BaseWindowController
    {
        private const string AID_ID = "visual:part-gizmos";
        private readonly List<(Transform Root, PartData Data, string Id)> _parts = new();
        private readonly List<(int Part, IObjectAssemblyPartNode Vab, AttachNodeData Flight, Label Tag)> _nodes = new();
        private readonly List<(int Part, Collider Collider)> _colliders = new();
        private readonly List<(int Part, Module_Drag Module)> _drag = new();
        private readonly List<(int Part, Transform Transform)> _thrust = new();
        private readonly List<(int Part, Collider Collider)> _hatches = new();
        private readonly List<Vector3> _vertices = new(16384);
        private readonly List<Color32> _colors = new(16384);
        private readonly List<int> _indices = new(16384);
        private readonly Vector3[] _corners = new Vector3[8];
        private readonly Toggle[] _layers = new Toggle[6];
        private readonly WaitForEndOfFrame _endOfFrame = new();
        private Toggle _draw;
        private Toggle _whole;
        private DropdownField _subject;
        private Label _status;
        private VisualElement _tags;
        private GameObject _geometry;
        private Mesh _mesh;
        private Material _material;
        private Coroutine _drawing;
        private GameState _scene;
        private VesselComponent _vessel;
        private int _partCount;
        private int _selectedIndex = -1;
        private bool _includeWhole;
        private bool _isTruncated;

        private void OnEnable()
        {
            _draw = RootElement.Q<Toggle>("draw");
            _whole = RootElement.Q<Toggle>("whole");
            _subject = RootElement.Q<DropdownField>("subject");
            _status = RootElement.Q<Label>("status");
            string[] names = { "nodes", "colliders", "drag", "thrust", "hatches", "com" };
            for (int i = 0; i < names.Length; i++)
            {
                _layers[i] = RootElement.Q<Toggle>(names[i]);
                _layers[i].RegisterValueChangedCallback(_ => SetDrawing());
            }
            _subject.RegisterValueChangedCallback(_ => _selectedIndex = _subject.index);
            _whole.RegisterValueChangedCallback(evt => _includeWhole = evt.newValue);
            _includeWhole = _whole.value;
            _draw.RegisterValueChangedCallback(_ => SetDrawing());
            RootElement.Q<Button>("refresh").clicked += Refresh;
            RootElement.Q<Button>("selected").clicked += UseSelected;
            RootElement.Q<Button>("clear").clicked += ClearAll;
            RootElement.Q<Button>("capture").clicked += Capture;
        }

        /// <inheritdoc />
        protected override void OnRequestedOpenChanged(bool isOpen)
        {
            if (isOpen)
                Refresh();
            else
                StopDrawing();
        }

        private void Refresh()
        {
            StopDrawing();
            _parts.Clear();
            _nodes.Clear();
            _colliders.Clear();
            _drag.Clear();
            _thrust.Clear();
            _hatches.Clear();
            _tags = new VisualElement { pickingMode = PickingMode.Ignore };
            _tags.style.position = UnityEngine.UIElements.Position.Absolute;
            _tags.StretchToParentSize();
            _scene = Game.GlobalGameState.GetState();
            _vessel = Game.ViewController.GetActiveSimVessel();
            var subjects = _scene is GameState.VehicleAssemblyBuilder or GameState.FlightView
                ? PartDiagnostics.Subjects(Game) : new List<KSP.Sim.ResourceSystem.IFlowNode>();
            _partCount = subjects.Count;
            var choices = new List<string>();
            foreach (var subject in subjects)
            {
                Transform root = null;
                PartData data = null;
                if (subject is IObjectAssemblyPart vab)
                {
                    root = vab.PartTransform;
                    data = vab.AvailablePart?.PartData;
                }
                else if (subject is PartComponent flight)
                {
                    PartBehavior view = Game.ViewController.GetBehaviorIfLoaded(flight);
                    if (view != null)
                        root = view.transform;
                    data = flight.PartData;
                }
                if (root == null || data == null)
                    continue;
                _parts.Add((root, data, subject.GlobalId.ToString()));
                choices.Add($"{subject.Name} | {subject.GlobalId}");
            }
            _subject.choices = choices;
            _subject.SetValueWithoutNotify(choices.Count > 0 ? choices[0] : "");
            _selectedIndex = choices.Count > 0 ? 0 : -1;
            for (int index = 0; index < _parts.Count; index++)
            {
                var part = _parts[index];
                foreach (var subject in subjects)
                {
                    if (subject.GlobalId.ToString() != part.Id)
                        continue;
                    if (subject is IObjectAssemblyPart vab)
                    {
                        foreach (IObjectAssemblyPartNode node in vab.Nodes)
                        {
                            _nodes.Add((index, node, null, MakeTag(node.NodeTag, node.SizeKey, node.Diameter * 0.5f)));
                        }
                    }
                    else if (subject is PartComponent flight)
                    {
                        foreach (AttachNodeData node in flight.GetAttachments())
                        {
                            _nodes.Add((index, null, node, MakeTag(node.NodeId, node.SizeKey, node.Diameter * 0.5f)));
                        }
                    }
                }
                foreach (Collider collider in part.Root.GetComponentsInChildren<Collider>(true))
                {
                    if (Owns(index, collider.transform))
                        _colliders.Add((index, collider));
                }
                foreach (Module_Drag module in part.Root.GetComponentsInChildren<Module_Drag>(true))
                {
                    if (Owns(index, module.gameObject.transform))
                        _drag.Add((index, module));
                }
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (Module_Engine engine in part.Root.GetComponentsInChildren<Module_Engine>(true))
                {
                    if (!Owns(index, engine.gameObject.transform) || engine.DataEngine?.engineModes == null)
                        continue;
                    foreach (var mode in engine.DataEngine.engineModes)
                    {
                        if (mode == null)
                            continue;
                        if (mode.ThrustTransformNamesMultipliers != null && mode.ThrustTransformNamesMultipliers.Length > 0)
                        {
                            foreach (var group in mode.ThrustTransformNamesMultipliers)
                            {
                                if (group != null && !string.IsNullOrEmpty(group.ThrustTransformName))
                                    names.Add(group.ThrustTransformName);
                            }
                        }
                        else if (!string.IsNullOrEmpty(mode.thrustVectorTransformName))
                            names.Add(mode.thrustVectorTransformName);
                    }
                }
                foreach (Transform candidate in part.Root.GetComponentsInChildren<Transform>(true))
                {
                    if (Owns(index, candidate) && names.Contains(candidate.name))
                        _thrust.Add((index, candidate));
                }
                foreach (ClimbBehavior climb in part.Root.GetComponentsInChildren<ClimbBehavior>(true))
                {
                    if (!Owns(index, climb.transform))
                        continue;
                    // The public API exposes only the first instance. Read the serialized list once per refresh.
                    var instances = typeof(ClimbBehavior).GetField("_instances", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(climb) as List<ClimbInstance>;
                    if (instances == null)
                        continue;
                    foreach (ClimbInstance airlock in instances)
                    {
                        ClimbPreset preset = ClimbPresetAssetReference.GetLoadedAsset(airlock.ClimbPreset);
                        if (preset != null && preset.Type == ClimbType.Airlock && airlock.TriggerCollider != null)
                            _hatches.Add((index, airlock.TriggerCollider));
                    }
                }
            }
            int budget = _nodes.Count * 64 + _colliders.Count * 256 + _thrust.Count * 6 + _hatches.Count * 64 + _parts.Count * 144;
            foreach (var entry in _drag)
            {
                budget += (entry.Module.DataDrag?.cubes.Count ?? 0) * 72;
            }
            // Reserve outside the frame loop. Later topology growth requires an explicit refresh.
            _vertices.Capacity = Math.Max(_vertices.Capacity, budget);
            _indices.Capacity = Math.Max(_indices.Capacity, budget);
            _colors.Capacity = Math.Max(_colors.Capacity, budget);
            _isTruncated = false;
            _status.text = FormattableString.Invariant($"Cached {_parts.Count} loaded parts, {_nodes.Count} nodes, {_colliders.Count} colliders, {_thrust.Count} thrust transforms and {_hatches.Count} airlocks. Refresh after topology, variant or node-size edits. VAB and flight view only.");
        }

        private Label MakeTag(string id, string sizeKey, float radius)
        {
            var label = new Label(FormattableString.Invariant($"{id} | {sizeKey} | r {radius:F3} m")) { pickingMode = PickingMode.Ignore, enableRichText = false };
            label.AddToClassList("kui-value");
            label.style.position = UnityEngine.UIElements.Position.Absolute;
            label.style.backgroundColor = new Color(0.05f, 0.06f, 0.08f, 0.85f);
            _tags.Add(label);
            return label;
        }

        private bool Owns(int index, Transform candidate)
        {
            for (Transform cursor = candidate; cursor != null; cursor = cursor.parent)
            {
                if (cursor == _parts[index].Root)
                    return true;
                for (int other = 0; other < _parts.Count; other++)
                {
                    if (cursor == _parts[other].Root)
                        return false;
                }
            }
            return false;
        }

        private void UseSelected()
        {
            var builder = Game.OAB.Current;
            var selected = builder == null ? null : builder.ActivePartTracker.PartBeingManipulated ?? builder.ActivePartTracker.partGrabbed;
            if (selected == null)
            {
                _status.text = "Grab a VAB part, then Use VAB selection. In flight choose a loaded part from the list.";
                return;
            }
            Refresh();
            for (int i = 0; i < _parts.Count; i++)
            {
                if (_parts[i].Root == selected.PartTransform)
                    _subject.index = i;
            }
        }

        private bool Includes(int index) => (_includeWhole || _selectedIndex == index) && _parts[index].Root != null;

        private void ClearAll()
        {
            foreach (Toggle layer in _layers)
            {
                layer.SetValueWithoutNotify(false);
            }
            StopDrawing();
        }

        private void SetDrawing()
        {
            bool any = false;
            foreach (Toggle layer in _layers)
            {
                any |= layer.value;
            }
            if (!_draw.value || !any || !IsRequestedOpen)
            {
                StopDrawing();
                return;
            }
            if (_geometry == null && !CreateGeometry())
            {
                StopDrawing();
                return;
            }
            if (_drawing != null)
                return;
            RootElement.panel.visualTree.Add(_tags);
            DebugOverrides.Report(AID_ID, "Part geometry gizmos (visual aid)", StopDrawing, false);
            _drawing = StartCoroutine(DrawWhileEnabled());
        }

        private bool CreateGeometry()
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                _status.text = "Overlay shader unavailable. Drawing disabled.";
                return false;
            }
            _geometry = new GameObject("QA part gizmos");
            _geometry.transform.SetParent(transform, false);
            _mesh = new Mesh { name = "QA part gizmo lines", indexFormat = IndexFormat.UInt32 };
            _mesh.MarkDynamic();
            _geometry.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _material = new Material(shader) { color = Color.white, renderQueue = (int)RenderQueue.Overlay };
            _material.SetInt("_ZTest", (int)CompareFunction.Always);
            _material.SetInt("_ZWrite", 0);
            _material.SetInt("_Cull", (int)CullMode.Off);
            MeshRenderer renderer = _geometry.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _geometry.SetActive(false);
            return true;
        }

        private IEnumerator DrawWhileEnabled()
        {
            while (true)
            {
                yield return _endOfFrame;
                if (!IsRequestedOpen || Game.GlobalGameState.GetState() != _scene || !SubjectUnchanged())
                {
                    _status.text = "Craft or scene changed. Refresh and enable drawing again.";
                    StopDrawing();
                    yield break;
                }
                Camera camera = GetOverlayCamera();
                int layer = GetOverlayLayer(camera);
                bool visible = IsRequestedOpen && layer >= 0;
                _geometry.SetActive(visible);
                // Tags belong to the UI, but world geometry remains visible for F2 screenshots.
                _tags.style.display = visible && IsWindowOpen ? DisplayStyle.Flex : DisplayStyle.None;
                if (visible)
                {
                    _geometry.layer = layer;
                    DrawFrame(camera);
                }
            }
        }

        private bool SubjectUnchanged()
        {
            foreach (var part in _parts)
            {
                if (part.Root == null)
                    return false;
            }
            if (_scene == GameState.VehicleAssemblyBuilder)
                return Game.OAB.Current != null && Game.OAB.Current.ActivePartTracker.PartsMasterList.Count == _partCount;
            return _scene == GameState.FlightView && Game.ViewController.GetActiveSimVessel() == _vessel &&
                _vessel?.SimulationObject.PartOwner != null && _vessel.SimulationObject.PartOwner.Parts.Count == _partCount;
        }

        private void StopDrawing()
        {
            if (_draw != null)
                _draw.SetValueWithoutNotify(false);
            if (_drawing != null)
            {
                StopCoroutine(_drawing);
                _drawing = null;
            }
            if (_geometry != null)
                _geometry.SetActive(false);
            _tags?.RemoveFromHierarchy();
            DebugOverrides.Clear(AID_ID);
        }

        private void DrawFrame(Camera camera)
        {
            _vertices.Clear();
            _indices.Clear();
            _colors.Clear();
            foreach (var entry in _nodes)
            {
                bool visible = _layers[0].value && Includes(entry.Part);
                if ((entry.Vab is UnityEngine.Object nodeObject && nodeObject == null) ||
                    (entry.Vab != null && entry.Vab.NodeTransform == null))
                    visible = false;
                entry.Tag.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (!visible)
                    continue;
                Transform root = _parts[entry.Part].Root;
                Vector3 position = entry.Vab != null ? entry.Vab.WorldPosition : root.TransformPoint((Vector3)entry.Flight.Position);
                Vector3 direction = entry.Vab != null ? entry.Vab.WorldRotation * Vector3.forward : root.TransformDirection((Vector3)entry.Flight.Orientation);
                float radius = (entry.Vab != null ? entry.Vab.Diameter : entry.Flight.Diameter) * 0.5f;
                AttachNodeType type = entry.Vab != null ? entry.Vab.NodeType : entry.Flight.NodeType;
                Color color = type switch
                {
                    AttachNodeType.Stack => new Color(0.4f, 0.8f, 0.95f),
                    AttachNodeType.Surface => new Color(0.95f, 0.4f, 0.8f),
                    AttachNodeType.Dock => new Color(0.95f, 0.85f, 0.4f),
                    _ => Color.white
                };
                Ring(position, direction, radius, color);
                Arrow(position, direction.normalized * Mathf.Max(radius * 0.3f, 0.05f), color);
                if (camera == null || RootElement.panel == null || camera.WorldToScreenPoint(position).z <= 0)
                {
                    entry.Tag.style.display = DisplayStyle.None;
                    continue;
                }
                Vector2 panelPosition = RuntimePanelUtils.CameraTransformWorldToPanel(RootElement.panel, position, camera);
                entry.Tag.style.left = panelPosition.x;
                entry.Tag.style.top = panelPosition.y;
                entry.Tag.style.color = color;
            }
            if (_layers[1].value)
            {
                foreach (var entry in _colliders)
                {
                    if (Includes(entry.Part) && entry.Collider != null && entry.Collider.gameObject.activeInHierarchy)
                        DrawCollider(entry.Collider);
                }
            }
            if (_layers[2].value)
            {
                foreach (var entry in _drag)
                {
                    if (!Includes(entry.Part) || entry.Module == null || entry.Module.DataDrag == null)
                        continue;
                    foreach (DragCube cube in entry.Module.DataDrag.cubes)
                    {
                        DrawCube(entry.Module.gameObject.transform, cube);
                    }
                }
            }
            if (_layers[3].value)
            {
                foreach (var entry in _thrust)
                {
                    if (Includes(entry.Part) && entry.Transform != null)
                        Arrow(entry.Transform.position, -entry.Transform.forward * 0.5f, Color.green);
                }
            }
            if (_layers[4].value)
            {
                foreach (var entry in _hatches)
                {
                    if (!Includes(entry.Part) || entry.Collider == null)
                        continue;
                    Transform hatch = entry.Collider.transform;
                    Ring(hatch.position, hatch.forward, 0.15f, Color.yellow);
                    Arrow(hatch.position, hatch.forward * 0.4f, Color.yellow);
                }
            }
            if (_layers[5].value)
            {
                for (int index = 0; index < _parts.Count; index++)
                {
                    if (!Includes(index))
                        continue;
                    Vector3 center = _parts[index].Root.TransformPoint(_parts[index].Data.coMassOffset);
                    Ring(center, Vector3.right, 0.12f, Color.white);
                    Ring(center, Vector3.up, 0.12f, Color.white);
                    Ring(center, Vector3.forward, 0.12f, Color.white);
                }
            }
            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colors);
            _mesh.SetIndices(_indices, MeshTopology.Lines, 0);
        }

        private void Line(Vector3 start, Vector3 end, Color color)
        {
            if (!float.IsFinite(start.sqrMagnitude) || !float.IsFinite(end.sqrMagnitude))
                return;
            if (_vertices.Count + 2 > _vertices.Capacity)
            {
                if (!_isTruncated)
                {
                    _isTruncated = true;
                    _status.text = "Geometry exceeded the cached budget. Some lines are hidden. Refresh craft to resize buffers.";
                }
                return;
            }
            int index = _vertices.Count;
            _vertices.Add(_geometry.transform.InverseTransformPoint(start));
            _vertices.Add(_geometry.transform.InverseTransformPoint(end));
            _colors.Add(color);
            _colors.Add(color);
            _indices.Add(index);
            _indices.Add(index + 1);
        }

        private void Arrow(Vector3 origin, Vector3 vector, Color color)
        {
            Vector3 direction = vector.normalized;
            Vector3 side = Vector3.Cross(direction, Mathf.Abs(direction.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 tip = origin + vector;
            float head = vector.magnitude * 0.2f;
            Line(origin, tip, color);
            Line(tip, tip - direction * head + side * head * 0.5f, color);
            Line(tip, tip - direction * head - side * head * 0.5f, color);
        }

        private void Ring(Vector3 center, Vector3 normal, float radius, Color color)
        {
            if (normal.sqrMagnitude < 0.000001f || !float.IsFinite(radius) || radius <= 0)
                return;
            Vector3 first = Vector3.Cross(normal.normalized, Mathf.Abs(normal.normalized.y) < 0.9f ? Vector3.up : Vector3.right).normalized * radius;
            Vector3 second = Vector3.Cross(normal.normalized, first);
            Vector3 previous = center + first;
            for (int i = 1; i <= 24; i++)
            {
                float angle = i * (2f * Mathf.PI / 24f);
                Vector3 next = center + first * Mathf.Cos(angle) + second * Mathf.Sin(angle);
                Line(previous, next, color);
                previous = next;
            }
        }

        private void Box(Matrix4x4 matrix, Bounds bounds, Color color)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector3 offset = Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                _corners[i] = matrix.MultiplyPoint3x4(bounds.center + offset);
            }
            for (int i = 0; i < 8; i++)
            {
                for (int bit = 1; bit <= 4; bit *= 2)
                {
                    if ((i & bit) == 0)
                        Line(_corners[i], _corners[i | bit], color);
                }
            }
        }

        private void DrawCollider(Collider collider)
        {
            Transform root = collider.transform;
            Color color = collider.isTrigger ? new Color(1f, 0.6f, 0.1f) : Color.cyan;
            if (collider is BoxCollider box)
            {
                Box(root.localToWorldMatrix, new Bounds(box.center, box.size), color);
            }
            else if (collider is SphereCollider sphere)
            {
                Vector3 scale = root.lossyScale;
                float radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                Vector3 center = root.TransformPoint(sphere.center);
                Ring(center, Vector3.right, radius, color);
                Ring(center, Vector3.up, radius, color);
                Ring(center, Vector3.forward, radius, color);
            }
            else if (collider is CapsuleCollider capsule)
            {
                Vector3 scale = root.lossyScale;
                int axis = capsule.direction;
                float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale[(axis + 1) % 3]), Mathf.Abs(scale[(axis + 2) % 3]));
                float half = Mathf.Max(0, capsule.height * Mathf.Abs(scale[axis]) * 0.5f - radius);
                Vector3 localAxis = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
                Vector3 direction = root.TransformDirection(localAxis).normalized;
                Vector3 center = root.TransformPoint(capsule.center);
                Vector3 tangent = Vector3.Cross(direction, Mathf.Abs(direction.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 other = Vector3.Cross(direction, tangent);
                Ring(center + direction * half, direction, radius, color);
                Ring(center - direction * half, direction, radius, color);
                for (int side = 0; side < 4; side++)
                {
                    Vector3 radial = side < 2 ? tangent * (side == 0 ? 1 : -1) : other * (side == 2 ? 1 : -1);
                    Line(center + direction * half + radial * radius, center - direction * half + radial * radius, color);
                    for (int cap = -1; cap <= 1; cap += 2)
                    {
                        Vector3 previous = center + direction * (half * cap) + radial * radius;
                        for (int segment = 1; segment <= 6; segment++)
                        {
                            float angle = segment * Mathf.PI / 12f;
                            Vector3 next = center + direction * (half * cap + Mathf.Sin(angle) * radius * cap) + radial * (Mathf.Cos(angle) * radius);
                            Line(previous, next, color);
                            previous = next;
                        }
                    }
                }
            }
            else if (collider is MeshCollider mesh && mesh.sharedMesh != null)
            {
                Box(root.localToWorldMatrix, mesh.sharedMesh.bounds, color);
            }
        }

        private void DrawCube(Transform root, DragCube cube)
        {
            if (cube == null || cube.Area == null || cube.Drag == null || cube.Area.Length < 6 || cube.Drag.Length < 6)
                return;
            // Match SDK face order XP, XN, YP, YN, ZP, ZN. Area sets panel size independently of drag color.
            for (int face = 0; face < 6; face++)
            {
                int axis = face / 2;
                Vector3 normal = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
                normal *= face % 2 == 0 ? 1 : -1;
                Vector3 first = axis == 0 ? Vector3.up : Vector3.right;
                Vector3 second = axis == 2 ? Vector3.up : Vector3.forward;
                float boundA = Mathf.Abs(axis == 0 ? cube.Size.y : cube.Size.x);
                float boundB = Mathf.Abs(axis == 2 ? cube.Size.y : cube.Size.z);
                float areaScale = Mathf.Sqrt(Mathf.Clamp01(cube.Area[face] / Mathf.Max(0.0001f, boundA * boundB)));
                Vector3 center = cube.Center + normal * (Mathf.Abs(cube.Size[axis]) * 0.5f + 0.015f);
                Vector3 halfA = first * (boundA * areaScale * 0.5f);
                Vector3 halfB = second * (boundB * areaScale * 0.5f);
                Color color = Color.Lerp(new Color(0.1f, 0.75f, 1f), new Color(1f, 0.35f, 0.1f), Mathf.Clamp01(cube.Drag[face]));
                Vector3 a = root.TransformPoint(center - halfA - halfB);
                Vector3 b = root.TransformPoint(center - halfA + halfB);
                Vector3 c = root.TransformPoint(center + halfA + halfB);
                Vector3 d = root.TransformPoint(center + halfA - halfB);
                Line(a, b, color);
                Line(b, c, color);
                Line(c, d, color);
                Line(d, a, color);
                Line(a, c, color);
                Line(b, d, color);
            }
        }

        private void Capture()
        {
            try
            {
                var report = new QaReport("Part geometry gizmos", "bench-run");
                string layers = FormattableString.Invariant($"Nodes {_layers[0].value}, colliders {_layers[1].value}, drag cubes {_layers[2].value}, thrust {_layers[3].value}, airlocks {_layers[4].value}, authored CoM {_layers[5].value}.");
                report.Add(new QaReportResult(_whole.value ? "craft" : _subject.value, "Geometry overlay", "part-gizmos", "settings", QaVerdict.NeedsReview,
                    FormattableString.Invariant($"Draw {_draw.value}, whole craft {_whole.value}, scene {_scene}, cached parts {_parts.Count}. {layers} {_status.text} Visual inspection required. No gameplay state changed.")));
                _status.text = $"Captured: {report.Write()}";
            }
            catch (Exception exception)
            {
                _status.text = exception.Message;
            }
        }

        private void OnDisable() => StopDrawing();

        private void OnDestroy()
        {
            StopDrawing();
            if (_mesh != null)
                Destroy(_mesh);
            if (_material != null)
                Destroy(_material);
            if (_geometry != null)
                Destroy(_geometry);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using KSP.Game;
using KSP.Sim;
using KSP.Sim.impl;
using KSP.UI.Flight;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace DebugTools.Runtime.Controllers
{
    /// <summary>
    /// Read-only per-part force overlay with three reusable geometry batches.
    /// </summary>
    public class ForceArrowsWindowController : BaseWindowController
    {
        private const string AID_ID = "visual:part-forces";
        private readonly List<Vector3>[] _vertices = { new(3000), new(3000), new(3000) };
        private readonly List<int>[] _indices = { new(3000), new(3000), new(3000) };
        private readonly List<Color32>[] _colors = { new(3000), new(3000), new(3000) };
        private readonly Mesh[] _meshes = new Mesh[3];
        private readonly Material[] _materials = new Material[3];
        private readonly MeshRenderer[] _renderers = new MeshRenderer[3];
        private readonly Toggle[] _types = new Toggle[3];
        private GameObject _geometry;
        private Toggle _draw;
        private Slider _scale;
        private FloatField _minimum;
        private Label _readout;
        private float _knPerMetre = 10;
        private Coroutine _drawing;
        private readonly WaitForEndOfFrame _endOfFrame = new();

        private void OnEnable()
        {
            _draw = RootElement.Q<Toggle>("draw");
            _scale = RootElement.Q<Slider>("scale");
            _minimum = RootElement.Q<FloatField>("minimum");
            _readout = RootElement.Q<Label>("readout");
            _types[0] = RootElement.Q<Toggle>("drag");
            _types[1] = RootElement.Q<Toggle>("lift");
            _types[2] = RootElement.Q<Toggle>("thrust");
            _draw.RegisterValueChangedCallback(_ => SetDrawing());
            _scale.RegisterValueChangedCallback(_ => UpdateScale());
            RootElement.Q<Button>("capture").clicked += Capture;
            UpdateScale();
        }

        private void UpdateScale()
        {
            _knPerMetre = Mathf.Pow(10, _scale.value);
            _readout.text = FormattableString.Invariant($"Linear arrow length: {_knPerMetre:F3} kN per metre. Scale slider is logarithmic.");
        }

        private void SetDrawing()
        {
            if (!_draw.value)
            {
                if (_drawing != null)
                {
                    StopCoroutine(_drawing);
                    _drawing = null;
                }
                if (_geometry != null)
                    _geometry.SetActive(false);
                DebugOverrides.Clear(AID_ID);
                return;
            }
            if (_geometry == null)
                CreateGeometry();
            if (!_draw.value)
                return;
            DebugOverrides.Report(AID_ID, "Part force arrows (visual aid)", StopDrawing, false);
            if (_drawing == null)
                _drawing = StartCoroutine(DrawWhileEnabled());
        }

        private IEnumerator DrawWhileEnabled()
        {
            while (_draw.value)
            {
                yield return _endOfFrame;
                DrawFrame();
            }
        }

        /// <inheritdoc />
        protected override void OnRequestedOpenChanged(bool isOpen)
        {
            if (!isOpen)
                StopDrawing();
        }

        private void StopDrawing()
        {
            if (_draw == null)
                return;
            _draw.SetValueWithoutNotify(false);
            SetDrawing();
        }

        private void CreateGeometry()
        {
            Color[] colors = { Color.red, Color.cyan, Color.green };
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                _readout.text = "Force overlay shader unavailable. Arrows disabled.";
                StopDrawing();
                return;
            }
            _geometry = new GameObject("QA force arrows");
            _geometry.transform.SetParent(transform, false);
            for (int type = 0; type < 3; type++)
            {
                var batch = new GameObject("Force batch");
                batch.transform.SetParent(_geometry.transform, false);
                _meshes[type] = new Mesh { name = "QA force lines", indexFormat = IndexFormat.UInt32 };
                _meshes[type].MarkDynamic();
                batch.AddComponent<MeshFilter>().sharedMesh = _meshes[type];
                _materials[type] = new Material(shader) { color = colors[type], renderQueue = (int)RenderQueue.Overlay };
                _materials[type].SetInt("_ZTest", (int)CompareFunction.Always);
                _materials[type].SetInt("_ZWrite", 0);
                _materials[type].SetInt("_Cull", (int)CullMode.Off);
                _renderers[type] = batch.AddComponent<MeshRenderer>();
                _renderers[type].sharedMaterial = _materials[type];
                _renderers[type].shadowCastingMode = ShadowCastingMode.Off;
                _renderers[type].receiveShadows = false;
            }
        }

        private void DrawFrame()
        {
            if (!_draw.value)
                return;
            if (!IsRequestedOpen || Game.GlobalGameState.GetState() != GameState.FlightView)
            {
                StopDrawing();
                return;
            }
            int layer = GetOverlayLayer(GetOverlayCamera());
            bool visible = IsRequestedOpen && layer >= 0 && Game.ViewController.TryGetActiveSimVessel(out _);
            _geometry.SetActive(visible);
            if (!visible || !Game.ViewController.TryGetActiveSimVessel(out VesselComponent vessel))
                return;
            foreach (MeshRenderer renderer in _renderers)
            {
                renderer.gameObject.layer = layer;
            }
            float minimum = _minimum.value;
            if (!float.IsFinite(minimum) || minimum < 0)
            {
                _geometry.SetActive(false);
                return;
            }
            for (int type = 0; type < 3; type++)
            {
                _vertices[type].Clear();
                _indices[type].Clear();
                _colors[type].Clear();
            }
            if (vessel.SimulationObject.PartOwner != null)
            {
                foreach (PartComponent part in vessel.SimulationObject.PartOwner.Parts)
                {
                    PartBehavior view = Game.ViewController.GetBehaviorIfLoaded(part);
                    if (view == null || part.SimulationObject.Rigidbody == null)
                        continue;
                    Vector3 drag = Vector3.zero;
                    Vector3 lift = Vector3.zero;
                    Vector3 thrust = Vector3.zero;
                    foreach (IForce force in part.SimulationObject.Rigidbody.Forces)
                    {
                        Type type = force.GetType();
                        Vector3 vector = (Vector3)force.RelativeForce;
                        if (type == PhysicsForceDisplaySystem.MODULE_DRAG_DRAG_TYPE || type == PhysicsForceDisplaySystem.MODULE_LIFTINGSURFACE_DRAG_TYPE)
                            drag += vector;
                        else if (type == PhysicsForceDisplaySystem.MODULE_DRAG_BODY_LIFT_TYPE || type == PhysicsForceDisplaySystem.MODULE_LIFTINGSURFACE_LIFT_TYPE)
                            lift += vector;
                        else if (type == PhysicsForceDisplaySystem.MODULE_ENGINE_ENGINE_FORCE)
                            thrust += vector;
                    }
                    AddArrow(0, view.transform, drag, minimum);
                    AddArrow(1, view.transform, lift, minimum);
                    AddArrow(2, view.transform, thrust, minimum);
                }
            }
            for (int type = 0; type < 3; type++)
            {
                _meshes[type].Clear();
                _meshes[type].SetVertices(_vertices[type]);
                _meshes[type].SetColors(_colors[type]);
                _meshes[type].SetIndices(_indices[type], MeshTopology.Lines, 0);
                _renderers[type].enabled = _vertices[type].Count > 0;
            }
        }

        private void AddArrow(int type, Transform part, Vector3 force, float minimum)
        {
            float magnitude = force.magnitude;
            if (!_types[type].value || !float.IsFinite(magnitude) || magnitude <= minimum || magnitude <= 0)
                return;
            Vector3 origin = _geometry.transform.InverseTransformPoint(part.position);
            Vector3 vector = _geometry.transform.InverseTransformDirection(part.TransformDirection(force)) / _knPerMetre;
            Vector3 tip = origin + vector;
            Vector3 direction = vector.normalized;
            Vector3 side = Vector3.Cross(direction, Mathf.Abs(direction.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 otherSide = Vector3.Cross(direction, side);
            float head = Mathf.Min(vector.magnitude * 0.2f, 1f);
            Line(type, origin, tip);
            Line(type, tip, tip - direction * head + side * head * 0.5f);
            Line(type, tip, tip - direction * head - side * head * 0.5f);
            Line(type, tip, tip - direction * head + otherSide * head * 0.5f);
            Line(type, tip, tip - direction * head - otherSide * head * 0.5f);
        }

        private void Line(int type, Vector3 start, Vector3 end)
        {
            int index = _vertices[type].Count;
            _vertices[type].Add(start);
            _vertices[type].Add(end);
            _colors[type].Add(new Color32(255, 255, 255, 255));
            _colors[type].Add(new Color32(255, 255, 255, 255));
            _indices[type].Add(index);
            _indices[type].Add(index + 1);
        }

        private void OnDisable() => StopDrawing();

        private void Capture()
        {
            try
            {
                var report = new Redux.QA.QaReport("Part force arrows", "bench-run");
                report.Add(new Redux.QA.QaReportResult("active-vessel", "Active vessel", "force-arrows", "settings",
                    Redux.QA.QaVerdict.NeedsReview,
                    FormattableString.Invariant($"Read-only visual aid. Draw {_draw.value}. Scale {_knPerMetre:F3} kN/m. Minimum {_minimum.value:F3} kN. Drag {_types[0].value}, lift {_types[1].value}, thrust {_types[2].value}. Source force categories, arrows at part origins. Visual acceptance and performance require review.")));
                _readout.text = $"Captured: {report.Write()}";
            }
            catch (Exception exception)
            {
                _readout.text = exception.Message;
            }
        }

        private void OnDestroy()
        {
            DebugOverrides.Clear(AID_ID);
            for (int type = 0; type < 3; type++)
            {
                if (_meshes[type] != null)
                    Destroy(_meshes[type]);
                if (_materials[type] != null)
                    Destroy(_materials[type]);
            }
            if (_geometry != null)
                Destroy(_geometry);
        }
    }
}

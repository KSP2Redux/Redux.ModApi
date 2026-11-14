using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using KSP.Game;
using KSP.Modules;
using KSP.OAB;
using KSP.VFX;
using Newtonsoft.Json.Linq;
using Redux.QA;
using Redux.UI.Screenshots;
using Redux.VFX.Plume.Volumetric;
using UnityEngine;
using UnityEngine.UIElements;

namespace DebugTools.Runtime.Controllers
{
    /// <summary>
    /// VAB bench owning temporary plume sources, a collision plate and fixed camera views.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class PlumeLabWindowController : BaseWindowController
    {
        private const string OVERRIDE_ID = "bench:plume-lab";
        private readonly List<VolumetricPlumeSource> _sources = new();
        private readonly List<VolumetricPlumeSource> _added = new();
        private readonly List<(IObjectAssemblyPart Part, Vector3 Position, Quaternion Rotation)> _parts = new();
        private ObjectAssemblyBuilder _builder;
        private Slider _throttle;
        private ConditionsPanel _conditions;
        private float _pressureAtm;
        private DropdownField _quality;
        private VisualElement _selection;
        private Label _status;
        private Label _results;
        private GameObject _plate;
        private Camera _camera;
        private Vector3 _cameraPosition;
        private Quaternion _cameraRotation;
        private bool _orthographic;
        private float _cameraFov;
        private Vector3 _viewPosition;
        private Quaternion _viewRotation;
        private float _viewFov;
        private string _viewNote = "";
        private int? _previousQuality;
        private bool _running;
        private bool _capturing;
        private int _version;
        private float _nextReadout;
        private float _changedAt;
        private string _viewName = "Free";

        private float PressureAtm => _pressureAtm;

        private void OnEnable()
        {
            _throttle = RootElement.Q<Slider>("throttle");
            _conditions = new ConditionsPanel(RootElement.Q("conditions"), Game);
            _quality = RootElement.Q<DropdownField>("quality");
            _quality.choices = new List<string> { "Off", "Low", "High" };
            _quality.index = PersistentProfileManager.PlumeQuality;
            _selection = RootElement.Q("engines");
            _status = RootElement.Q<Label>("status");
            _results = RootElement.Q<Label>("results");
            RootElement.Q<Button>("refresh").clicked += RefreshSubject;
            RootElement.Q<Button>("start").clicked += StartRun;
            RootElement.Q<Button>("stop").clicked += StopRun;
            RootElement.Q<Button>("side").clicked += () => SetView("Side", Vector3.right);
            RootElement.Q<Button>("below").clicked += () => SetView("Below", Vector3.down);
            RootElement.Q<Button>("quarter").clicked += () => SetView("Three-quarter", new Vector3(1f, -0.4f, -1f));
            RootElement.Q<Button>("free").clicked += RestoreCamera;
            RootElement.Q<Button>("capture").clicked += () =>
            {
                if (_running && !_capturing)
                    StartCoroutine(Capture());
            };
            // Bubble after the shared panel has derived its atmosphere and preset values.
            RootElement.RegisterCallback<ChangeEvent<float>>(_ => ApplyConditions());
            RootElement.RegisterCallback<ChangeEvent<string>>(_ => ApplyConditions());
            foreach (string field in new[] { "x", "y", "z", "pitch", "yaw", "roll", "width", "depth" })
            {
                RootElement.Q<FloatField>(field).RegisterValueChangedCallback(_ => MovePlate());
            }
            RootElement.Q<Toggle>("plate").RegisterValueChangedCallback(_ => MovePlate());
            RefreshSubject();
            ApplyConditions();
        }

        private void OnDisable() => StopRun();
        private void OnDestroy() => StopRun();

        private void RefreshSubject()
        {
            StopRun();
            _builder = Game.OAB.Current;
            _selection.Clear();
            if (_builder == null)
                return;

            foreach (IObjectAssemblyPart part in _builder.ActivePartTracker.PartsMasterList)
            {
                if (part.PartTransform == null)
                    continue;
                part.TryGetModule<Module_Engine>(out var engine);
                foreach (ThrottleVFXManager manager in part.PartTransform.GetComponentsInChildren<ThrottleVFXManager>(true))
                {
                    // Attached parts can be descendants. Only list managers owned by this part.
                    bool owned = true;
                    foreach (IObjectAssemblyPart other in _builder.ActivePartTracker.PartsMasterList)
                    {
                        if (other != part && other.PartTransform != null && other.PartTransform.IsChildOf(part.PartTransform) &&
                            manager.transform.IsChildOf(other.PartTransform))
                        {
                            owned = false;
                            break;
                        }
                    }
                    if (!owned || (engine == null && (manager.FXModeActionEvents == null || manager.FXModeActionEvents.Length == 0)))
                        continue;
                    string mode = engine != null ? engine.DataEngine?.currentEngineModeData?.engineID : null;
                    mode ??= FormattableString.Invariant($"mode {manager.CurrentEngineMode}");
                    var toggle = new Toggle($"{part.PartName} | {mode}") { value = true, userData = manager };
                    toggle.AddToClassList("kui-switch");
                    toggle.AddToClassList("plume-engine");
                    _selection.Add(toggle);
                }
            }
            _status.text = "Select engines, then start. Only visual exhaust is driven. Craft edits stop the run.";
        }

        private void StartRun()
        {
            if (_running || Game.GlobalGameState.GetState() != GameState.VehicleAssemblyBuilder || _builder == null || _builder != Game.OAB.Current)
                return;
            _running = true;
            _version++;
            _previousQuality = PersistentProfileManager.TestPlumeQuality;
            DebugOverrides.Report(OVERRIDE_ID, "VAB plume lab: visual throttle, pressure, quality, plate and camera", StopRun);
            try
            {
                foreach (IObjectAssemblyPart part in _builder.ActivePartTracker.PartsMasterList)
                {
                    _parts.Add((part, part.PartTransform.position, part.PartTransform.rotation));
                }
                _selection.Query<Toggle>().ForEach(toggle =>
                {
                    if (!toggle.value || toggle.userData is not ThrottleVFXManager manager || manager == null)
                        return;
                    VolumetricPlumeSource[] sources = manager.GetComponentsInChildren<VolumetricPlumeSource>(true);
                    if (sources.Length == 0)
                    {
                        var source = manager.gameObject.AddComponent<VolumetricPlumeSource>();
                        _added.Add(source);
                        sources = new[] { source };
                    }
                    foreach (VolumetricPlumeSource source in sources)
                    {
                        if (!_sources.Contains(source))
                            _sources.Add(source);
                    }
                });
                if (_sources.Count == 0)
                    throw new InvalidOperationException("No engine plume sources selected. Refresh the current craft.");
                _selection.SetEnabled(false);
                ApplyConditions();
                if (!_running)
                    return;
                MovePlate();
                _status.text = "Running. High quality enables collider impingement. Inspect merging in the game view.";
            }
            catch (Exception exception)
            {
                StopRun();
                _status.text = exception.Message;
            }
        }

        private void ApplyConditions()
        {
            JObject conditions;
            try
            {
                conditions = _conditions.Read();
            }
            catch (Exception exception)
            {
                StopRun();
                _status.text = exception.Message;
                return;
            }
            _pressureAtm = conditions.Value<float>("pressureKPa") / 101.325f;
            RootElement.Q<Label>("pressure-value").text = FormattableString.Invariant($"{PressureAtm:F4} atm ({PressureAtm * 101.325f:F3} kPa), plume only. Other conditions do not drive exhaust.");
            RootElement.Q<Label>("throttle-value").text = FormattableString.Invariant($"Visual throttle: {_throttle.value:F1} %");
            double pressure = conditions.Value<double>("pressureKPa");
            double density = conditions.Value<double>("density");
            double mach = conditions.Value<double>("mach");
            double sound = AeroUtilities.GetSoundSpeed(_conditions.Body.atmosphereAdiabaticIndex, pressure, density);
            RootElement.Q<Label>("condition-readout").text = FormattableString.Invariant($"{pressure:F4} kPa | {density:F6} kg/m3 | Mach {mach:F3} = {sound * mach:F2} m/s");
            _changedAt = Time.unscaledTime;
            if (!_running)
                return;
            PersistentProfileManager.TestPlumeQuality = _quality.index;
            foreach (VolumetricPlumeSource source in _sources)
            {
                if (source != null)
                    source.SetLabPreview(_throttle.value / 100f, PressureAtm, true);
            }
        }

        private bool SubjectUnchanged()
        {
            if (Game.GlobalGameState.GetState() != GameState.VehicleAssemblyBuilder || _builder == null || _builder != Game.OAB.Current ||
                _builder.ActivePartTracker.PartsMasterList.Count != _parts.Count)
                return false;
            for (int index = 0; index < _parts.Count; index++)
            {
                var snapshot = _parts[index];
                Transform part = snapshot.Part.PartTransform;
                if (part == null || _builder.ActivePartTracker.PartsMasterList[index] != snapshot.Part ||
                    (part.position - snapshot.Position).sqrMagnitude > 0.000001f || Quaternion.Angle(part.rotation, snapshot.Rotation) > 0.01f)
                    return false;
            }
            return true;
        }

        private void LateUpdate()
        {
            if (!_running)
                return;
            if (!IsRequestedOpen || !SubjectUnchanged())
            {
                StopRun();
                return;
            }
            if (_camera != null)
            {
                _camera.orthographic = false;
                _camera.fieldOfView = _viewFov;
                _camera.transform.SetPositionAndRotation(_viewPosition, _viewRotation);
            }
            if (Time.unscaledTime < _nextReadout)
                return;
            _nextReadout = Time.unscaledTime + 0.25f;
            _results.text = Readouts(out bool ready);
            if (!_capturing)
                _status.text = ready ? "Ready for capture." : "Waiting for visible plumes and computed flow. Keep the game view visible.";
        }

        private string Readouts(out bool ready)
        {
            // The renderer diagnostics also format numbers. Limit the culture change to this call.
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                return ReadInvariantReadouts(out ready);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        private string ReadInvariantReadouts(out bool ready)
        {
            var text = new StringBuilder(_viewNote);
            text.Append(VolumetricPlumeRenderer.ReadLabGroups(_sources, out ready));
            foreach (VolumetricPlumeSource source in _sources)
            {
                if (source == null || source.Nozzles == null)
                    continue;
                text.Append($"{source.name} measured bell exit radii (m): ");
                foreach (Transform nozzle in source.Nozzles)
                {
                    if (nozzle != null)
                        text.Append($"{source.GetNozzleExitRadius(nozzle):F4} ");
                }
                text.AppendLine("(zero means unavailable, renderer uses thrust fallback)");
            }
            return text.ToString();
        }

        private Vector3 CraftCenter()
        {
            Vector3 center = Vector3.zero;
            foreach (var snapshot in _parts)
            {
                center += snapshot.Position;
            }
            return center / Mathf.Max(1, _parts.Count);
        }

        private float Number(string name) => RootElement.Q<FloatField>(name).value;

        private void MovePlate()
        {
            _changedAt = Time.unscaledTime;
            if (!_running)
                return;
            foreach (string field in new[] { "x", "y", "z", "pitch", "yaw", "roll", "width", "depth" })
            {
                if (!float.IsFinite(Number(field)))
                    return;
            }
            if (_plate == null)
            {
                // A scene root stays outside the preview craft's collider exclusion hierarchy.
                _plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _plate.name = "QA plume collision plate";
                _plate.hideFlags = HideFlags.HideAndDontSave;
            }
            _plate.SetActive(RootElement.Q<Toggle>("plate").value);
            _plate.transform.SetPositionAndRotation(CraftCenter() + new Vector3(Number("x"), Number("y"), Number("z")),
                Quaternion.Euler(Number("pitch"), Number("yaw"), Number("roll")));
            _plate.transform.localScale = new Vector3(Mathf.Clamp(Number("width"), 0.1f, 100f), 0.1f, Mathf.Clamp(Number("depth"), 0.1f, 100f));
            Physics.SyncTransforms();
        }

        private void SetView(string name, Vector3 direction)
        {
            if (!_running || _builder.CameraManager == null)
                return;
            if (_camera == null)
            {
                _camera = _builder.CameraManager.Camera;
                if (_camera == null)
                    return;
                _cameraPosition = _camera.transform.position;
                _cameraRotation = _camera.transform.rotation;
                _orthographic = _camera.orthographic;
                _cameraFov = _camera.fieldOfView;
            }
            Bounds bounds = default;
            foreach (var snapshot in _parts)
            {
                if (snapshot.Part.PartTransform == null)
                    continue;
                // Use the OAB's collider bounds, excluding particles and other VFX renderers.
                snapshot.Part.GetPartBounds(ref bounds);
            }
            if (bounds.extents == Vector3.zero)
                bounds = new Bounds(CraftCenter(), Vector3.one * 4f);
            var nearby = new Bounds(bounds.center, bounds.size + Vector3.one * 24f);
            foreach (VolumetricPlumeSource source in _sources)
            {
                if (source != null && VolumetricPlumeRenderer.TryGetPreviewBounds(source, out Bounds plume))
                {
                    // Hot-cone bounds can extend far beyond the visible exhaust and the hall.
                    Vector3 minimum = Vector3.Max(nearby.min, plume.min);
                    Vector3 maximum = Vector3.Min(nearby.max, plume.max);
                    if (minimum.x <= maximum.x && minimum.y <= maximum.y && minimum.z <= maximum.z)
                    {
                        bounds.Encapsulate(minimum);
                        bounds.Encapsulate(maximum);
                    }
                }
            }
            if (_plate != null && _plate.activeSelf)
            {
                bounds.Encapsulate(nearby.ClosestPoint(_plate.GetComponent<Renderer>().bounds.min));
                bounds.Encapsulate(nearby.ClosestPoint(_plate.GetComponent<Renderer>().bounds.max));
            }
            Collider volume = _builder.BuilderAssets.GetCameraBoundsCollider();
            Vector3 target = bounds.center;
            if (!ObjectAssemblyPlacementTool.IsPointInCameraVolume(volume, target))
                target = CraftCenter();
            if (!ObjectAssemblyPlacementTool.IsPointInCameraVolume(volume, target))
            {
                RestoreCamera();
                _viewNote = "Fixed view unavailable: craft is outside the VAB camera volume.\n";
                return;
            }
            direction.Normalize();
            // Stop at the first wall along the ray, including the concave hall corners.
            float available = 0f;
            for (float step = 0.5f; step <= 150f; step += 0.5f)
            {
                if (!ObjectAssemblyPlacementTool.IsPointInCameraVolume(volume, target + direction * step))
                    break;
                available = step;
            }
            available = Mathf.Max(0f, available - 1f);
            if (available < 1f)
            {
                RestoreCamera();
                _viewNote = "Fixed view unavailable: insufficient clearance inside the hall.\n";
                return;
            }
            Quaternion rotation = Quaternion.LookRotation(-direction, name == "Below" ? Vector3.forward : Vector3.up);
            float distance = FitDistance(bounds, target, rotation, 45f);
            _viewFov = 45f;
            while (distance > available && _viewFov < 100f)
            {
                _viewFov += 5f;
                distance = FitDistance(bounds, target, rotation, _viewFov);
            }
            _viewNote = distance > available ? "Hall limits this view: some distant exhaust may be cropped.\n" : "";
            _camera.orthographic = false;
            _camera.fieldOfView = _viewFov;
            _viewPosition = target + direction * Mathf.Min(distance, available);
            _viewRotation = rotation;
            _viewName = name;
            _changedAt = Time.unscaledTime;
        }

        private float FitDistance(Bounds bounds, Vector3 target, Quaternion rotation, float fieldOfView)
        {
            float vertical = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            float horizontal = vertical * Mathf.Max(0.1f, _camera.aspect);
            float distance = 1f;
            Quaternion inverse = Quaternion.Inverse(rotation);
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 offset = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f)) - target;
                Vector3 point = inverse * offset;
                distance = Mathf.Max(distance, Mathf.Max(Mathf.Abs(point.x) / horizontal, Mathf.Abs(point.y) / vertical) * 1.15f - point.z);
            }
            return distance;
        }

        private void RestoreCamera()
        {
            if (_camera != null)
            {
                _camera.transform.SetPositionAndRotation(_cameraPosition, _cameraRotation);
                _camera.orthographic = _orthographic;
                _camera.fieldOfView = _cameraFov;
            }
            _camera = null;
            _viewName = "Free";
            _viewNote = "";
        }

        private void StopRun()
        {
            if (!_running)
                return;
            _running = false;
            _version++;
            foreach (VolumetricPlumeSource source in _sources)
            {
                if (source != null)
                    source.SetLabPreview(0f, 0f, false);
            }
            _sources.Clear();
            foreach (VolumetricPlumeSource source in _added)
            {
                if (source != null)
                {
                    source.enabled = false;
                    Destroy(source);
                }
            }
            _added.Clear();
            _parts.Clear();
            if (_plate != null)
            {
                _plate.SetActive(false);
                Destroy(_plate);
            }
            _plate = null;
            RestoreCamera();
            PersistentProfileManager.TestPlumeQuality = _previousQuality;
            DebugOverrides.Clear(OVERRIDE_ID);
            _selection.SetEnabled(true);
            _status.text = "Stopped and restored. Refresh after craft edits.";
        }

        private IEnumerator Capture()
        {
            _capturing = true;
            int version = _version;
            try
            {
                float deadline = Time.unscaledTime + 45f;
                bool ready = false;
                do
                {
                    yield return new WaitForEndOfFrame();
                    if (!_running || version != _version || !SubjectUnchanged())
                        yield break;
                    Readouts(out ready);
                    _status.text = "Capture waiting for flow readiness...";
                } while ((!ready || Time.unscaledTime - _changedAt < 1f) && Time.unscaledTime < deadline);
                if (!ready || Time.unscaledTime - _changedAt < 1f)
                {
                    _status.text = "Capture timed out. Use nonzero throttle and Low or High quality with visible plumes.";
                    yield break;
                }
                var report = new QaReport("VAB plume lab", "bench-run");
                var result = new QaReportResult("current-craft", "Current VAB craft", "plume-lab", "capture", QaVerdict.NeedsReview,
                    "Collider impingement uses the flight renderer at High quality. Depth clipping uses scene depth. No physical heating or force is applied.\n" + Readouts(out _));
                result.Measurements.Add(new JObject { ["name"] = "Conditions", ["pressureAtm"] = PressureAtm,
                    ["pressureKPa"] = PressureAtm * 101.325f, ["throttlePercent"] = _throttle.value,
                    ["quality"] = _quality.value, ["view"] = _viewName });
                foreach (var part in _parts)
                {
                    result.Measurements.Add(new JObject { ["name"] = "Craft part", ["partId"] = part.Part.PartName,
                        ["position"] = part.Position.ToString("F4", CultureInfo.InvariantCulture), ["rotation"] = part.Rotation.eulerAngles.ToString("F3", CultureInfo.InvariantCulture) });
                }
                if (_plate != null)
                    result.Measurements.Add(new JObject { ["name"] = "Plate", ["enabled"] = _plate.activeSelf,
                        ["positionMetres"] = _plate.transform.position.ToString("F4", CultureInfo.InvariantCulture), ["rotationDegrees"] = _plate.transform.eulerAngles.ToString("F3", CultureInfo.InvariantCulture),
                        ["sizeMetres"] = _plate.transform.localScale.ToString("F3", CultureInfo.InvariantCulture) });
                Directory.CreateDirectory(Path.Combine(report.DirectoryPath, "evidence"));
                bool completed = false;
                ScreenshotCapture.CaptureToFile(Path.Combine(report.DirectoryPath, "evidence", "plume-lab.png"), ScreenshotFormat.Png, completed: saved =>
                {
                    bool valid = this != null && _running && version == _version && SubjectUnchanged();
                    if (saved && valid)
                        result.Evidence.Add(new JObject { ["type"] = "image", ["path"] = "evidence/plume-lab.png", ["caption"] = "VAB plume lab" });
                    else
                        result.Message += " Screenshot failed or run changed during capture. Image is not accepted as evidence.";
                    report.Add(result);
                    string path = report.Write();
                    if (this != null)
                        _status.text = $"Report: {path}";
                    completed = true;
                });
                while (!completed)
                    yield return null;
            }
            finally
            {
                _capturing = false;
            }
        }
    }
}

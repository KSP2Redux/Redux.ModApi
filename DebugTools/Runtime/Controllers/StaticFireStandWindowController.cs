using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KSP.Game;
using KSP.Messages;
using KSP.Modules;
using KSP.Sim.impl;
using KSP.Sim.State;
using Newtonsoft.Json.Linq;
using Redux.CliIntegration;
using Redux.Messages;
using Redux.QA;
using Redux.UI.Screenshots;
using Redux.VFX.Plume.Volumetric;
using UnityEngine;
using UnityEngine.UIElements;

namespace DebugTools.Runtime.Controllers
{
    /// <summary>
    /// Flight bench controller owning a reversible vessel hold and engine recording session.
    /// </summary>
    public class StaticFireStandWindowController : BaseWindowController
    {
        private const string HOLD_ID = "bench:static-fire:hold";
        private const string PRESSURE_ID = "bench:static-fire:pressure";
        private const string THROTTLE_ID = "bench:static-fire:throttle";
        private readonly Dictionary<Rigidbody, RigidbodyConstraints> _held = new();
        private readonly List<(PartComponent Part, Data_Engine Data)> _engines = new();
        private readonly List<(Data_Engine Data, bool Independent, float Percent)> _throttleRestore = new();
        private readonly Dictionary<VolumetricPlumeSource, float?> _pressureRestore = new();
        private readonly List<VolumetricPlumeSource> _sources = new();
        private VesselComponent _vessel;
        private DropdownField _engine;
        private DropdownField _quality;
        private Slider _pressure;
        private Slider _throttle;
        private FloatField _duration;
        private Label _status;
        private Label _results;
        private bool _running;
        private bool _sweeping;
        private bool _capturing;
        private float _sweepStarted;
        private float _sweepDuration;
        private float _originalThrottle;
        private int? _originalQuality;
        private int _appliedQuality;
        private int _runVersion;
        private int _partCount;

        private void OnEnable()
        {
            _engine = RootElement.Q<DropdownField>("engine");
            _quality = RootElement.Q<DropdownField>("quality");
            _quality.choices = new List<string> { "Off", "Low", "High" };
            _quality.index = PersistentProfileManager.PlumeQuality;
            _pressure = RootElement.Q<Slider>("pressure");
            _throttle = RootElement.Q<Slider>("throttle");
            _duration = RootElement.Q<FloatField>("duration");
            _status = RootElement.Q<Label>("status");
            _results = RootElement.Q<Label>("results");
            RootElement.Q<Button>("refresh").clicked += RefreshSubject;
            RootElement.Q<Button>("vacuum").clicked += () => SetPressure(0f);
            RootElement.Q<Button>("kerbin").clicked += () => SetPressure(1f);
            RootElement.Q<Button>("eve").clicked += () => SetPressure(4f);
            RootElement.Q<Button>("start").clicked += StartRun;
            RootElement.Q<Button>("stop").clicked += StopRun;
            RootElement.Q<Button>("sweep").clicked += StartSweep;
            RootElement.Q<Button>("capture").clicked += () =>
            {
                if (_running && !_capturing)
                    StartCoroutine(Capture());
            };
            _pressure.RegisterValueChangedCallback(_ => ApplyPressure());
            _quality.RegisterValueChangedCallback(_ =>
            {
                if (_running)
                    ApplyQuality();
            });
            _throttle.RegisterValueChangedCallback(_ =>
            {
                _sweeping = false;
                if (_running)
                    ApplyThrottle(_throttle.value);
            });
            StateChanges.FlightViewLeft += OnFlightLeft;
            RefreshSubject();
            ApplyPressure();
        }

        private void OnFlightLeft(GameStateLeftMessage message) => StopRun();

        private void OnDisable() => StopRun();

        private void OnDestroy()
        {
            StopRun();
            StateChanges.FlightViewLeft -= OnFlightLeft;
        }

        private void RefreshSubject()
        {
            StopRun();
            _engines.Clear();
            _vessel = Game.ViewController.GetActiveSimVessel();
            var choices = new List<string> { "All engines" };
            if (_vessel != null)
            {
                foreach (PartComponent part in _vessel.SimulationObject.PartOwner.Parts)
                {
                    foreach (PartComponentModule module in part.Modules.ValuesList)
                    {
                        if (module is not PartComponentModule_Engine || !module.DataModules.TryGetByType(out Data_Engine data))
                            continue;

                        _engines.Add((part, data));
                        choices.Add($"{part.PartData.Title} | {part.PartName} | {part.GlobalId}");
                    }
                }
            }
            _engine.choices = choices;
            _engine.index = 0;
            RootElement.Q<Label>("subject").text = _vessel == null ? "No active vessel" : $"{_vessel.Name} | {_vessel.GlobalId}";
            _status.text = $"Stopped. {_engines.Count} engines. Stage or ignite engines normally before running.";
        }

        private bool IsSelected(int index) => _engine.index == 0 || _engine.index == index + 1;

        private float PressureAtm => _pressure.value <= -3.99f ? 0f : Mathf.Pow(10f, _pressure.value);

        private void SetPressure(float atmospheres)
        {
            _pressure.SetValueWithoutNotify(Mathf.Log10(Mathf.Max(0.0001f, atmospheres)));
            ApplyPressure();
        }

        private void ApplyPressure()
        {
            RootElement.Q<Label>("pressure-value").text = $"Plume: {PressureAtm:F4} atm ({PressureAtm * 101.325f:F3} kPa). Engine atmosphere unchanged.";
            if (!_running)
                return;

            foreach (VolumetricPlumeSource source in _sources)
            {
                if (source != null)
                    source.TestPressureAtm = PressureAtm;
            }
        }

        private void StartRun()
        {
            if (_running)
                return;
            if (_vessel == null || _vessel != Game.ViewController.GetActiveSimVessel() || _engines.Count == 0)
            {
                _status.text = "Refresh subject after selecting a loaded vessel with engines.";
                return;
            }
            if (Game.GlobalGameState.GetState() != GameState.FlightView || Game.ViewController.TimeWarp.CurrentRate != 1f ||
                DebugOverrides.IsReported("cli:vessel-hold"))
            {
                _status.text = "Needs flight at 1x time with the CLI vessel hold released.";
                return;
            }
            VesselBehavior view = _vessel.SimulationObject.objVesselBehavior;
            if (view == null)
                return;

            if (HasIgnitedLockedEngine())
            {
                _status.text = "An ignited throttle-locked engine cannot be swept or isolated. Shut it down before running.";
                return;
            }

            _originalThrottle = _vessel.flightCtrlState.mainThrottle;
            _partCount = _vessel.SimulationObject.PartOwner.Parts.Count;
            _originalQuality = PersistentProfileManager.TestPlumeQuality;
            _running = true;
            try
            {
                // Register before mutating so a partial start can use the same cleanup as a completed run.
                DebugOverrides.Report(HOLD_ID, "Static-fire vessel hold", StopRun);
                DebugOverrides.Report(PRESSURE_ID, "Static-fire plume pressure", StopRun);
                DebugOverrides.Report(THROTTLE_ID, "Static-fire throttle and plume quality", StopRun);
                CliIntegrationServer.HoldVfxVessel(view, _held);
                if (_held.Count == 0)
                    throw new InvalidOperationException("No dynamic rigidbodies. Leave rails warp before running.");

                for (int index = 0; index < _engines.Count; index++)
                {
                    Data_Engine data = _engines[index].Data;
                    _throttleRestore.Add((data, data.IndependentThrottle.GetValue(), data.IndependentThrottlePercentage.GetValue()));
                    data.IndependentThrottlePercentage.SetValue(0f);
                    data.IndependentThrottle.SetValue(!IsSelected(index));
                }
                foreach (VolumetricPlumeSource source in view.GetComponentsInChildren<VolumetricPlumeSource>(true))
                {
                    PartBehavior part = source.GetComponentInParent<PartBehavior>();
                    for (int index = 0; index < _engines.Count; index++)
                    {
                        if (!IsSelected(index) || part == null || part.Model != _engines[index].Part)
                            continue;

                        _pressureRestore[source] = source.TestPressureAtm;
                        _sources.Add(source);
                        break;
                    }
                }
                ApplyQuality();
                ApplyPressure();
                ApplyThrottle(_throttle.value);
                _engine.SetEnabled(false);
                _status.text = $"Running. {_sources.Count} volumetric plume sources. Pressure affects volumetric plumes only.";
                StartCoroutine(Run(++_runVersion));
            }
            catch (Exception exception)
            {
                StopRun();
                _status.text = exception.Message;
            }
        }

        private void ApplyThrottle(float percent)
        {
            Game.ViewController.flightInputHandler.SetMainThrottle(_vessel, percent / 100f);
        }

        private void ApplyQuality()
        {
            _appliedQuality = _quality.index;
            PersistentProfileManager.TestPlumeQuality = _appliedQuality;
        }

        private void StartSweep()
        {
            if (!float.IsFinite(_duration.value) || _duration.value <= 0f)
            {
                _status.text = "Sweep duration must be a positive number of seconds.";
                return;
            }
            StartRun();
            if (!_running)
                return;

            _sweepDuration = _duration.value;
            _sweepStarted = Time.time;
            _sweeping = true;
            _throttle.SetValueWithoutNotify(0f);
            ApplyThrottle(0f);
        }

        private IEnumerator Run(int version)
        {
            float nextReadout = 0f;
            try
            {
                while (_running && version == _runVersion)
                {
                    if (!IsRequestedOpen || _vessel != Game.ViewController.GetActiveSimVessel() ||
                        Game.ViewController.TimeWarp.CurrentRate != 1f || !HasDynamicHold() ||
                        _vessel.SimulationObject.PartOwner.Parts.Count != _partCount || HasIgnitedLockedEngine())
                        yield break;

                    if (_sweeping)
                    {
                        float fraction = Mathf.Clamp01((Time.time - _sweepStarted) / _sweepDuration);
                        _throttle.SetValueWithoutNotify(fraction * 100f);
                        ApplyThrottle(fraction * 100f);
                        _sweeping = fraction < 1f;
                    }
                    // The game sets its own throttle on the pad when the vessel stages out of prelaunch
                    // (FlightInputHandler.HandleLaunchStaging and the InitialThrottleInPreLaunch setting), so the
                    // stand keeps its value for the whole run instead of setting it once.
                    else if (Math.Abs(_vessel.flightCtrlState.mainThrottle - _throttle.value / 100f) > 0.001f)
                    {
                        ApplyThrottle(_throttle.value);
                    }
                    if (IsWindowOpen && Time.unscaledTime >= nextReadout)
                    {
                        _results.text = Readouts();
                        nextReadout = Time.unscaledTime + 0.2f;
                    }

                    yield return null;
                }
            }
            finally
            {
                if (version == _runVersion)
                    StopRun();
            }
        }

        private bool HasIgnitedLockedEngine()
        {
            foreach (var engine in _engines)
            {
                if (engine.Data.currentEngineModeData.throttleLocked && engine.Data.EngineIgnited)
                    return true;
            }
            return false;
        }

        private bool HasDynamicHold()
        {
            foreach (Rigidbody body in _held.Keys)
            {
                if (body == null || body.isKinematic || body.constraints != RigidbodyConstraints.FreezeAll)
                    return false;
            }
            return _held.Count > 0;
        }

        private void StopRun()
        {
            if (!_running)
                return;

            _running = false;
            _sweeping = false;
            try
            {
                try
                {
                    bool ignited = false;
                    foreach (var engine in _engines)
                    {
                        ignited |= engine.Data.EngineIgnited;
                    }
                    ApplyThrottle(ignited ? 0f : _originalThrottle * 100f);
                }
                finally
                {
                    foreach (var snapshot in _throttleRestore)
                    {
                        snapshot.Data.IndependentThrottlePercentage.SetValue(snapshot.Percent);
                        snapshot.Data.IndependentThrottle.SetValue(snapshot.Independent);
                    }
                    foreach (var engine in _engines)
                    {
                        Data_Engine data = engine.Data;
                        if (!data.EngineIgnited)
                            continue;

                        // Restored independent settings and locked engines can ignore main throttle zero.
                        // Shut these down before releasing physics, including normally unshuttable engines.
                        if (data.currentEngineModeData.throttleLocked ||
                            (data.IndependentThrottle.GetValue() && data.IndependentThrottlePercentage.GetValue() > 0f))
                        {
                            bool allowShutdown = data.currentEngineModeData.allowShutdown;
                            try
                            {
                                data.currentEngineModeData.allowShutdown = true;
                                foreach (PartComponentModule module in engine.Part.Modules.ValuesList)
                                {
                                    if (module is PartComponentModule_Engine engineModule &&
                                        module.DataModules.TryGetByType(out Data_Engine moduleData) && moduleData == data)
                                        engineModule.Shutdown();
                                }
                            }
                            finally
                            {
                                data.currentEngineModeData.allowShutdown = allowShutdown;
                            }
                        }
                        // Clear the last thrust sample as well as the command before the hold releases.
                        data.RequestedThrottle = 0f;
                        data.currentThrottle = 0f;
                        data.FinalThrustValue = 0f;
                    }
                }
            }
            finally
            {
                _throttleRestore.Clear();
                foreach (var snapshot in _pressureRestore)
                {
                    if (snapshot.Key != null)
                        snapshot.Key.TestPressureAtm = snapshot.Value;
                }
                _pressureRestore.Clear();
                _sources.Clear();
                if (PersistentProfileManager.TestPlumeQuality == _appliedQuality)
                    PersistentProfileManager.TestPlumeQuality = _originalQuality;
                CliIntegrationServer.ReleaseVfxBodies(_held);
                DebugOverrides.Clear(HOLD_ID);
                DebugOverrides.Clear(PRESSURE_ID);
                DebugOverrides.Clear(THROTTLE_ID);
                _engine.SetEnabled(true);
                _status.text = "Stopped. Hold released with lit engines at zero thrust. Independent settings and plume conditions restored.";
            }
        }

        private string Readouts()
        {
            var text = new StringBuilder();
            for (int index = 0; index < _engines.Count; index++)
            {
                if (!IsSelected(index))
                    continue;

                var engine = _engines[index];
                text.AppendLine($"{engine.Part.PartData.Title} | {engine.Part.PartName} | {engine.Part.GlobalId}");
                text.AppendLine($"Throttle {engine.Data.currentThrottle * 100f:F1} % | Thrust {engine.Data.FinalThrustValue:F3} kN | Isp {engine.Data.RealISPValue:F2} s");
                text.AppendLine($"Requested mass flow {engine.Data.RequestedMassFlow * 1000:F4} kg/s | Engine ambient {engine.Part.staticPressureAtm:F5} atm ({engine.Part.staticPressureAtm * 101.325:F3} kPa)");
                text.AppendLine($"Ignited {engine.Data.EngineIgnited} | Flameout {engine.Data.Flameout}");
            }
            return text.ToString();
        }

        private IEnumerator Capture()
        {
            _capturing = true;
            bool dispatched = false;
            try
            {
                yield return new WaitForEndOfFrame();
                if (!_running)
                    yield break;

                var report = new QaReport("Static-fire stand", "bench-run");
                var results = new List<QaReportResult>();
                for (int index = 0; index < _engines.Count; index++)
                {
                    if (!IsSelected(index))
                        continue;

                    var engine = _engines[index];
                    var result = new QaReportResult(engine.Part.GlobalId.ToString(), engine.Part.PartData.Title,
                        "static-fire", "capture", QaVerdict.NeedsReview,
                        "Recorded engine telemetry. Pressure override affects volumetric plumes only.");
                    result.Measurements.Add(new JObject { ["name"] = "Thrust", ["value"] = engine.Data.FinalThrustValue, ["unit"] = "kN" });
                    result.Measurements.Add(new JObject { ["name"] = "Isp", ["value"] = engine.Data.RealISPValue, ["unit"] = "s" });
                    result.Measurements.Add(new JObject { ["name"] = "Requested mass flow", ["value"] = engine.Data.RequestedMassFlow * 1000f, ["unit"] = "kg/s" });
                    result.Measurements.Add(new JObject { ["name"] = "Engine ambient pressure", ["value"] = engine.Part.staticPressureAtm, ["unit"] = "atm" });
                    result.Measurements.Add(new JObject { ["name"] = "Engine ambient pressure", ["value"] = engine.Part.staticPressureAtm * 101.325, ["unit"] = "kPa" });
                    result.Measurements.Add(new JObject { ["name"] = "Engine throttle", ["value"] = engine.Data.currentThrottle * 100f, ["unit"] = "%" });
                    result.Measurements.Add(new JObject { ["name"] = "Main throttle", ["value"] = _vessel.flightCtrlState.mainThrottle * 100f, ["unit"] = "%" });
                    result.Measurements.Add(new JObject { ["name"] = "Plume pressure", ["value"] = PressureAtm, ["unit"] = "atm" });
                    result.Measurements.Add(new JObject { ["name"] = "Plume quality", ["value"] = PersistentProfileManager.PlumeQuality, ["unit"] = "0 off, 1 low, 2 high" });
                    results.Add(result);
                }
                Directory.CreateDirectory(Path.Combine(report.DirectoryPath, "evidence"));
                string path = Path.Combine(report.DirectoryPath, "evidence", "static-fire.png");
                ScreenshotCapture.CaptureToFile(path, ScreenshotFormat.Png, completed: saved =>
                {
                    try
                    {
                        foreach (QaReportResult result in results)
                        {
                            if (saved)
                                result.Evidence.Add(new JObject { ["type"] = "image", ["path"] = "evidence/static-fire.png", ["caption"] = "Static-fire frame" });
                            else
                                result.Message += " Screenshot capture failed.";

                            report.Add(result);
                        }
                        string reportPath = report.Write();
                        if (this != null)
                            _status.text = $"Captured: {reportPath}";
                    }
                    finally
                    {
                        _capturing = false;
                    }
                });
                dispatched = true;
            }
            finally
            {
                if (!dispatched)
                    _capturing = false;
            }
        }
    }
}

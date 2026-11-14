using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KSP.Game;
using KSP.Sim.impl;
using Redux.Ecs.Structural;
using Redux.QA;
using UnityEngine;
using UnityEngine.UIElements;

namespace DebugTools.Runtime.Controllers
{
    /// <summary>
    /// Previews structural presentation at an intact vessel attachment.
    /// </summary>
    public class StructuralVfxWindowController : BaseWindowController
    {
        private const string OVERRIDE_ID = "bench:structural-vfx";
        private DropdownField _part;
        private DropdownField _connection;
        private DropdownField _medium;
        private FloatField _stress;
        private Label _status;
        private VesselComponent _vessel;
        private PartOwnerBehavior _owner;
        private StructuralStressVfxController _controller;
        private readonly List<StructuralConnectionSnapshot> _connections = new();
        private readonly List<string> _partIds = new();
        private bool _running;
        private bool _stressApplied;
        private string _connectionId;
        private double _nextRefresh;

        private void OnEnable()
        {
            _part = RootElement.Q<DropdownField>("part");
            _connection = RootElement.Q<DropdownField>("connection");
            _medium = RootElement.Q<DropdownField>("medium");
            _medium.choices = new List<string> { "Natural", "Vacuum", "Atmosphere", "Underwater" };
            _medium.index = 0;
            _stress = RootElement.Q<FloatField>("stress");
            _status = RootElement.Q<Label>("status");
            _part.RegisterValueChangedCallback(_ => SelectPart());
            _connection.RegisterValueChangedCallback(_ => Stop());
            RootElement.Q<Button>("refresh").clicked += Refresh;
            RootElement.Q<Button>("overstress").clicked += () => Play(false);
            RootElement.Q<Button>("burst").clicked += () => Play(true);
            RootElement.Q<Button>("stop").clicked += Stop;
            RootElement.Q<Button>("capture").clicked += () =>
            {
                var report = new QaReport("Structural failure VFX preview", "bench-run");
                report.Add(new QaReportResult(_connectionId ?? "none", _part.value ?? "No part", "structural-vfx", "capture",
                    QaVerdict.NeedsReview, _status.text));
                _status.text = $"Captured: {report.Write()}";
            };
        }

        /// <inheritdoc />
        protected override void OnRequestedOpenChanged(bool isOpen)
        {
            if (isOpen)
                Refresh();
            else
                Stop();
        }

        private void Refresh()
        {
            Stop();
            _nextRefresh = Time.realtimeSinceStartupAsDouble + 0.5;
            _vessel = Game.ViewController.GetActiveSimVessel();
            VesselBehavior view = _vessel == null ? null : Game.ViewController.GetBehaviorIfLoaded(_vessel);
            _owner = view == null ? null : view.PartOwner;
            _controller = view == null ? null : view.GetComponent<StructuralStressVfxController>();
            _partIds.Clear();
            var names = new List<string>();
            if (_vessel?.SimulationObject.PartOwner != null)
            {
                foreach (PartComponent part in _vessel.SimulationObject.PartOwner.Parts)
                {
                    _partIds.Add(part.GlobalId.ToString());
                    names.Add($"{part.PartName} | {part.GlobalId}");
                }
            }
            string previousPart = _part.index >= 0 && _part.index < _part.choices.Count ? _part.value : null;
            _part.choices = names;
            int selected = previousPart == null ? -1 : names.IndexOf(previousPart);
            _part.SetValueWithoutNotify(names.Count == 0 ? null : names[selected >= 0 ? selected : 0]);
            SelectPart();
        }

        private void SelectPart()
        {
            Stop();
            _connections.Clear();
            if (_owner != null && _part.index >= 0 && _part.index < _partIds.Count &&
                StructuralPhysicsService.TryGetSnapshot(_owner, out StructuralVesselSnapshot snapshot, true))
            {
                string id = _partIds[_part.index];
                _connections.AddRange(snapshot.Connections.Where(item => item.ParentGuid == id || item.ChildGuid == id));
            }
            _connection.choices = _connections.Select(item => $"{item.ParentName} / {item.ChildName} | {item.Id}").ToList();
            _connection.index = _connections.Count > 0 ? 0 : -1;
            if (_partIds.Count == 0)
            {
                _status.text = "Waiting for the active vessel parts. The list refreshes while open.";
                return;
            }
            _status.text = _connections.Count > 0 ? "Pick an attachment. Effects play at its anchor." :
                "No structural attachment for this part yet. Single-part craft have none. Retrying while open.";
        }

        private void Play(bool burst)
        {
            Stop();
            if (Game.ViewController.GetActiveSimVessel() != _vessel)
            {
                _status.text = "Active vessel changed. Refresh before previewing.";
                return;
            }
            if (_controller == null || _connection.index < 0 || _connection.index >= _connections.Count)
            {
                _status.text = "Needs a loaded vessel with a structural VFX controller and an intact attachment.";
                return;
            }
            float stress = _stress.value;
            if (!float.IsFinite(stress) || stress < 0f || stress > 1f)
            {
                _status.text = "Stress must be a finite number from 0 to 1.";
                return;
            }
            if (!StructuralPhysicsService.TryGetSnapshot(_owner, out StructuralVesselSnapshot snapshot, true) ||
                snapshot.PresentationStressOverrideActive)
            {
                _status.text = "Structural data unavailable or another stress override is active. Clear it before previewing.";
                return;
            }
            string id = _connections[_connection.index].Id;
            StructuralConnectionSnapshot connection = snapshot.Connections.FirstOrDefault(item => item.Id == id);
            if (connection == null)
            {
                _status.text = "Attachment changed. Refresh the active vessel.";
                return;
            }
            StructuralVfxMedium? medium = _medium.index <= 0 ? null : Enum.Parse<StructuralVfxMedium>(_medium.value);
            if (!_controller.Preview(connection, stress, burst, burst ? medium : null))
            {
                _status.text = "Preview unavailable: check assets, FX quality and camera culling.";
                return;
            }
            _running = true;
            _connectionId = id;
            DebugOverrides.Report(OVERRIDE_ID, "Structural failure VFX preview", Stop);
            if (!burst && !StructuralPhysicsService.SetPresentationStressOverride(_owner, id, stress, out string error))
            {
                Stop();
                _status.text = error;
                return;
            }
            _stressApplied = !burst;
            _status.text = $"{(burst ? "Break burst" : "Overstress running")} at {stress.ToString("F2", CultureInfo.InvariantCulture)}. Physics and attachments are unchanged by this tool.";
        }

        private void Stop()
        {
            if (!_running)
                return;
            _running = false;
            if (_owner != null && _stressApplied)
                StructuralPhysicsService.ClearPresentationStressOverride(_owner, _connectionId, out _);
            _stressApplied = false;
            if (_controller != null)
                _controller.ClearForReferenceFrameChange();
            StructuralStressVfxRuntime.ClearPreviewEffects(_connectionId);
            DebugOverrides.Clear(OVERRIDE_ID);
            _status.text = "Stopped. Preview particles and presentation override cleared.";
        }

        private void LateUpdate()
        {
            if (!IsRequestedOpen)
                return;
            // Tool creation can precede vessel loading and structural graph creation.
            // Reopening must also replace the subject from the previous flight.
            if (!_running && Time.realtimeSinceStartupAsDouble >= _nextRefresh &&
                (Game.ViewController.GetActiveSimVessel() != _vessel || _owner == null || _controller == null || _connections.Count == 0))
                Refresh();
            if (!_running)
                return;
            if (!IsRequestedOpen || Game.GlobalGameState.GetState() != GameState.FlightView ||
                Game.ViewController.GetActiveSimVessel() != _vessel || _owner == null ||
                !StructuralPhysicsService.TryGetSnapshot(_owner, out StructuralVesselSnapshot snapshot, true))
            {
                Stop();
                return;
            }
            if (_stressApplied && !snapshot.PresentationStressOverrideActive)
            {
                Stop();
                return;
            }
            foreach (StructuralConnectionSnapshot connection in snapshot.Connections)
            {
                if (connection.Id == _connectionId)
                    return;
            }
            Stop();
        }

        private void OnDisable() => Stop();
        private void OnDestroy() => Stop();
    }
}

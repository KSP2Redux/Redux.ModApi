using System;
using System.Collections.Generic;
using KSP.Game;
using KSP.Logging;
using KSP.Sim.impl;
using UnityEngine.UIElements;

namespace DebugTools.Runtime.Controllers
{
    /// <summary>
    /// Presents the existing Planet Viewer's free flight and return actions.
    /// </summary>
    public class FreeFlightWindowController : BaseWindowController
    {
        private VisualElement _boundRoot;
        private DropdownField _bodies;
        private Button _enable;
        private Button _teleport;
        private Button _returnVessel;
        private Button _returnKsc;
        private Label _status;
        private float _nextRefresh;
        private string _error;
        private (bool Active, bool Busy, string Unavailable) _statusState;

        private void OnEnable()
        {
            Enable();
            if (ReferenceEquals(_boundRoot, RootElement))
                return;
            _boundRoot = RootElement;
            _bodies = RootElement.Q<DropdownField>("body-dropdown");
            _enable = RootElement.Q<Button>("enable-button");
            _teleport = RootElement.Q<Button>("teleport-button");
            _returnVessel = RootElement.Q<Button>("return-vessel-button");
            _returnKsc = RootElement.Q<Button>("return-ksc-button");
            _status = RootElement.Q<Label>("status-label");
            _enable.clicked += () => RunAction(() =>
            {
                Game.CreatePlanetViewer();
                Game.PlanetViewer.RequestFreeFlight();
            });
            _teleport.clicked += () => RunAction(() => Game.PlanetViewer.TeleportFreeFlight(_bodies.value));
            _returnVessel.clicked += () => RunAction(() => Game.PlanetViewer.DisableFreeFlight());
            _returnKsc.clicked += () => RunAction(() => Game.PlanetViewer.DisableFreeFlight(false));
            _bodies.choices = new List<string>();
        }

        private void Update()
        {
            if (!IsWindowOpen)
            {
                _nextRefresh = 0;
                return;
            }

            if (Game == null)
            {
                _enable.SetEnabled(false);
                _teleport.SetEnabled(false);
                _returnVessel.SetEnabled(false);
                _returnKsc.SetEnabled(false);
                _bodies.SetEnabled(false);
                _bodies.choices.Clear();
                _bodies.SetValueWithoutNotify(null);
                _status.text = "Load a campaign to use free flight.";
                _error = null;
                _statusState = default;
                return;
            }

            if (UnityEngine.Time.unscaledTime >= _nextRefresh)
            {
                RefreshBodies();
                _nextRefresh = UnityEngine.Time.unscaledTime + 1;
            }

            // The viewer owns the session, so closing this window has no gameplay side effects.
            Game.CreatePlanetViewer();
            var viewer = Game.PlanetViewer;
            bool active = viewer.IsFreeFlightEnabled;
            bool busy = viewer.IsFreeFlightBusy;
            string unavailable = active ? null : viewer.FreeFlightUnavailableReason;
            var state = (active, busy, unavailable);
            if (_statusState != state)
            {
                _error = null;
                _statusState = state;
            }
            bool returning = active && Game.UI.GetCurtainVisibility();
            _enable.SetEnabled(!active && unavailable == null);
            _teleport.SetEnabled(active && !busy && _bodies.choices.Count > 0);
            _bodies.SetEnabled(!busy && _bodies.choices.Count > 0);
            VesselComponent vessel = Game.UniverseModel?.FindVesselComponent(viewer.ReturnVesselId);
            bool canReturn = vessel != null && !vessel.SimulationObject.IsDestroyedOrBeingDestroyed;
            _returnVessel.SetEnabled(active && !returning && canReturn);
            _returnKsc.SetEnabled(active && !returning);

            string message = unavailable ?? "Enable to fly from the current camera position.";
            if (active)
            {
                message = returning ? "Returning from free flight..." :
                    busy ? "Loading the selected body..." : "Free flight is active.";
                if (viewer.ReturnVesselId != IGGuid.Empty && !canReturn)
                    message = "The original vessel is no longer available. Return to KSC.";
            }
            _status.text = _error ?? (active && !busy ? viewer.FreeFlightError : null) ?? message;
        }

        private void RefreshBodies()
        {
            ICollection<string> names = Game != null ? Game.SpaceSimulation?.GetBodyNameKeys() : null;
            int index = 0;
            bool unchanged = (names?.Count ?? 0) == _bodies.choices.Count;
            if (unchanged && names != null)
            {
                foreach (string name in names)
                {
                    if (_bodies.choices[index++] != name)
                    {
                        unchanged = false;
                        break;
                    }
                }
            }
            if (unchanged)
                return;

            var choices = names != null ? new List<string>(names) : new List<string>();
            string selected = choices.Contains(_bodies.value) ? _bodies.value : choices.Find(name => name == "Kerbin");
            if (selected == null && choices.Count > 0)
                selected = choices[0];
            _bodies.choices = choices;
            _bodies.SetValueWithoutNotify(selected);
            _error = null;
        }

        private void RunAction(Action action)
        {
            _error = null;
            try
            {
                action();
            }
            catch (Exception exception)
            {
                _error = exception.Message;
                GlobalLog.Error(KSP.Logging.LogFilter.UI, $"Free flight action failed: {exception}");
            }
        }
    }
}

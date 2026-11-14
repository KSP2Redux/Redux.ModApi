using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using KSP.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace DebugTools.Runtime.Controllers
{
    /// <summary>
    /// Read-only lock snapshots and a non-consuming input delivery probe.
    /// </summary>
    public class InputLocksWindowController : BaseWindowController
    {
        private readonly Dictionary<string, double> _firstSeen = new(StringComparer.Ordinal);
        private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
        private readonly List<string> _expired = new();
        private readonly List<VisualElement> _panels = new();
        private readonly List<RaycastResult> _hits = new();
        private readonly StringBuilder _text = new();
        private readonly StringBuilder _probeText = new();
        private readonly FieldInfo _definitionOwners = typeof(InputDefinition).GetField("_suppressors", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly FieldInfo _actionOwners = typeof(ToggleableInputAction).GetField("_suppressors", BindingFlags.Instance | BindingFlags.NonPublic);
        private VisualElement _locks;
        private Label _result;
        private double _nextRefresh;
        private bool _armed;
        private int _armedFrame;
        private int _inputFrame = -1;
        private string _lockSnapshot;
        private Coroutine _observation;
        private readonly WaitForEndOfFrame _endOfFrame = new();

        private void OnEnable()
        {
            _locks = RootElement.Q<VisualElement>("locks");
            _result = RootElement.Q<Label>("result");
            RootElement.Q<Button>("probe").clicked += Arm;
            RootElement.Q<Button>("cancel").clicked += Cancel;
            RootElement.Q<Button>("capture").clicked += Capture;
        }

        /// <inheritdoc />
        protected override void OnRequestedOpenChanged(bool isOpen)
        {
            if (_observation != null)
            {
                StopCoroutine(_observation);
                _observation = null;
            }
            Cancel();
            _firstSeen.Clear();
            if (isOpen)
                _observation = StartCoroutine(ObserveWhileOpen());
        }

        private IEnumerator ObserveWhileOpen()
        {
            while (IsRequestedOpen)
            {
                Poll();
                yield return _endOfFrame;
                FinishProbe();
                yield return null;
            }
        }

        private void Poll()
        {
            if (!IsRequestedOpen)
            {
                Cancel();
                _firstSeen.Clear();
                return;
            }
            if (Time.realtimeSinceStartupAsDouble >= _nextRefresh)
            {
                _nextRefresh = Time.realtimeSinceStartupAsDouble + 0.25;
                RefreshLocks();
            }
            if (!_armed || Time.frameCount <= _armedFrame)
                return;
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            bool click = mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame);
            if (!click && !(Keyboard.current?.anyKey.wasPressedThisFrame ?? false))
                return;
            Observe(click ? "Raw mouse press" : "Raw keyboard press");
            if (!click || EventSystem.current == null)
                return;
            var pointer = new PointerEventData(EventSystem.current) { position = mouse.position.ReadValue() };
            _hits.Clear();
            EventSystem.current.RaycastAll(pointer, _hits);
            if (_hits.Count > 0)
                _probeText.AppendLine($"EventSystem top hit candidate: {_hits[0].gameObject.name} ({_hits[0].module.GetType().Name}). This is a raycast, not delivery confirmation.");
        }

        private void FinishProbe()
        {
            if (!_armed || _inputFrame < 0 || Time.frameCount <= _inputFrame)
                return;
            _result.text = $"{_probeText}\nLocks at first observation:\n{_lockSnapshot}\nNo game-handler delivery or per-receiver lock causality hook exists. Suppressed actions cannot perform. UITK targets confirm dispatch, not callback completion. UGUI delivery and manual input polling are unobserved.";
            Cancel();
        }

        private void RefreshLocks()
        {
            _text.Clear();
            _locks.Clear();
            _seen.Clear();
#pragma warning disable CS0618
            foreach (KeyValuePair<string, ulong> entry in InputLockManager.lockStack)
            {
                Row($"legacy:{entry.Key}", entry.Key, $"0x{entry.Value.ToString("X16", CultureInfo.InvariantCulture)} ({(ControlTypes)entry.Value})");
            }
#pragma warning restore CS0618
            if (Game != null && Game.InputManager != null)
            {
                foreach (InputDefinition definition in Game.InputManager.InputDefinitions.Values)
                {
                    Owners(_definitionOwners, definition, $"map {definition.InputSourceMap.name}");
                    if (!definition.Enabled)
                        Row($"state:{definition.InputSourceMap.name}", "Map state (owner unavailable)", $"map {definition.InputSourceMap.name} disabled, no ControlTypes mask");
                    foreach (KeyValuePair<string, ToggleableInputAction> action in definition.Actions)
                    {
                        Owners(_actionOwners, action.Value, $"action {definition.InputSourceMap.name}/{action.Key}");
                        if (!action.Value.Enabled)
                            Row($"state:{definition.InputSourceMap.name}/{action.Key}", "Action state (owner unavailable)", $"action {definition.InputSourceMap.name}/{action.Key} disabled, no ControlTypes mask");
                    }
                }
            }
            _expired.Clear();
            foreach (string key in _firstSeen.Keys)
            {
                if (!_seen.Contains(key))
                    _expired.Add(key);
            }
            foreach (string key in _expired)
            {
                _firstSeen.Remove(key);
            }
            if (_seen.Count == 0)
                _text.AppendLine("No observed input locks.");
            if (_definitionOwners == null || _actionOwners == null)
                _text.AppendLine("Suppression owner inspection unavailable in this build.");
            if (_seen.Count == 0 || _definitionOwners == null || _actionOwners == null)
            {
                var notice = new Label(_seen.Count == 0 ? "No observed input locks." : "Suppression owner inspection unavailable in this build.");
                notice.AddToClassList("input-wrap");
                _locks.Add(notice);
            }
        }

        private void Owners(FieldInfo field, object target, string scope)
        {
            if (field?.GetValue(target) is not HashSet<object> owners)
                return;
            foreach (object owner in owners)
            {
                string id = $"{owner.GetType().FullName}#{RuntimeHelpers.GetHashCode(owner).ToString("X8", CultureInfo.InvariantCulture)}";
                Row($"{scope}:{id}", id, $"{scope} (suppression, no ControlTypes mask)");
            }
        }

        private void Row(string key, string owner, string mask)
        {
            _seen.Add(key);
            double now = Time.realtimeSinceStartupAsDouble;
            if (!_firstSeen.TryGetValue(key, out double since))
            {
                since = now;
                _firstSeen[key] = since;
            }
            string age = (now - since).ToString("F1", CultureInfo.InvariantCulture);
            var row = new VisualElement();
            row.AddToClassList("input-row");
            var ownerCell = new Label(owner);
            ownerCell.AddToClassList("input-owner");
            var maskCell = new Label(mask);
            maskCell.AddToClassList("input-mask");
            var ageCell = new Label($">= {age}");
            ageCell.AddToClassList("input-age");
            row.Add(ownerCell);
            row.Add(maskCell);
            row.Add(ageCell);
            _locks.Add(row);
            _text.AppendLine($"Owner: {owner}\nMask/scope: {mask}\nObserved age: >= {age} s\n");
        }

        private void Arm()
        {
            Cancel();
            _probeText.Clear();
            _armed = true;
            _armedFrame = Time.frameCount;
            _inputFrame = -1;
            foreach (UIDocument document in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                VisualElement root = document.rootVisualElement?.panel?.visualTree;
                if (root == null || _panels.Contains(root))
                    continue;
                _panels.Add(root);
                root.RegisterCallback<PointerDownEvent>(OnPointer, TrickleDown.TrickleDown);
                root.RegisterCallback<KeyDownEvent>(OnKey, TrickleDown.TrickleDown);
            }
            InputSystem.onActionChange += OnAction;
            _result.text = "Armed: next click, key or performed button action. Input is never consumed. Panels opened after arming are not observed. Press Cancel to stop.";
        }

        private void Observe(string description)
        {
            if (!_armed || !IsRequestedOpen || Time.frameCount <= _armedFrame)
                return;
            if (_inputFrame >= 0 && Time.frameCount != _inputFrame)
                return;
            if (_inputFrame < 0)
            {
                _inputFrame = Time.frameCount;
                RefreshLocks();
                _lockSnapshot = _text.ToString();
            }
            _probeText.AppendLine(description);
        }

        private void OnPointer(PointerDownEvent evt)
        {
            if (evt.target is VisualElement element)
                Observe($"UITK pointer dispatch target: {ElementPath(element)}");
        }

        private void OnKey(KeyDownEvent evt)
        {
            if (evt.target is VisualElement element)
                Observe($"UITK key {evt.keyCode} dispatch target: {ElementPath(element)}");
        }

        private static string ElementPath(VisualElement element)
        {
            string path = "";
            for (VisualElement current = element; current != null; current = current.parent)
            {
                path = $"/{current.GetType().Name}#{current.name}{path}";
            }
            return path;
        }

        private void OnAction(object source, InputActionChange change)
        {
            if (change == InputActionChange.ActionPerformed && source is InputAction action && action.type == InputActionType.Button)
                Observe($"Input System performed: {action.actionMap?.name}/{action.name}, control {action.activeControl?.path}. Handler completion unobserved.");
        }

        private void Cancel()
        {
            if (!_armed)
                return;
            _armed = false;
            InputSystem.onActionChange -= OnAction;
            foreach (VisualElement panel in _panels)
            {
                panel.UnregisterCallback<PointerDownEvent>(OnPointer, TrickleDown.TrickleDown);
                panel.UnregisterCallback<KeyDownEvent>(OnKey, TrickleDown.TrickleDown);
            }
            _panels.Clear();
        }

        private void OnDisable() => OnRequestedOpenChanged(false);

        private void Capture()
        {
            try
            {
                RefreshLocks();
                var report = new Redux.QA.QaReport("Input locks", "bench-run");
                report.Add(new Redux.QA.QaReportResult("input", "Input routing", "input-locks", "snapshot",
                    Redux.QA.QaVerdict.NeedsReview, $"Observed lock ages are lower bounds.\n{_text}\nProbe:\n{_result.text}"));
                _result.text = $"Captured: {report.Write()}";
            }
            catch (Exception exception)
            {
                _result.text = exception.Message;
            }
        }

        private void OnDestroy() => Cancel();
    }
}

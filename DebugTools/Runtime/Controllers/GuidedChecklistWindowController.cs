using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using KSP.OAB;
using Newtonsoft.Json.Linq;
using Redux.QA;
using Redux.UI.BugReport;
using Redux.UI.Screenshots;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace DebugTools.Runtime.Controllers
{
    /// <summary>
    /// A hosted manual checklist with a pinned subject and persisted row evidence.
    /// </summary>
    public class GuidedChecklistWindowController : BaseWindowController
    {
        private const string ROW_ADDRESS = "Assets/ReduxAssets/Definitions/PartChecklist.json";
        private static int _attachmentNumber;
        private readonly List<InputAction> _actions = new();
        private readonly List<QaReportResult> _results = new();
        private JArray _rows;
        private QaReport _report;
        private TextField _subject;
        private TextField _note;
        private Label _status;
        private Label _progress;
        private Label _instruction;
        private int _index;
        private bool _busy;

        private void OnEnable()
        {
            _subject = RootElement.Q<TextField>("subject");
            _note = RootElement.Q<TextField>("note");
            _status = RootElement.Q<Label>("status");
            _progress = RootElement.Q<Label>("progress");
            _instruction = RootElement.Q<Label>("instruction");
            RootElement.Q<Button>("selected").clicked += UseSelected;
            RootElement.Q<Button>("start").clicked += StartSession;
            RootElement.Q<Button>("report").clicked += OpenReport;
            BindVerdict("pass", Game.Input.Global.ChecklistPass);
            BindVerdict("fail", Game.Input.Global.ChecklistFail);
            BindVerdict("review", Game.Input.Global.ChecklistReview);
            BindVerdict("na", Game.Input.Global.ChecklistNotApplicable);
            BindVerdict("skip", Game.Input.Global.ChecklistSkip);
            _status.text = "Loading checklist...";
            Refresh();
            KSP.Game.GameManager.Instance.Assets.Load<TextAsset>(ROW_ADDRESS, asset =>
            {
                if (this == null)
                    return;

                try
                {
                    if (asset == null)
                        throw new InvalidDataException("Checklist data is unavailable.");

                    var rows = JArray.Parse(asset.text);
                    var ids = new HashSet<string>(StringComparer.Ordinal);
                    foreach (JToken row in rows)
                    {
                        string id = row.Value<string>("id");
                        if (string.IsNullOrWhiteSpace(id) || !ids.Add(id) ||
                            string.IsNullOrWhiteSpace(row.Value<string>("title")) ||
                            string.IsNullOrWhiteSpace(row.Value<string>("instruction")))
                            throw new InvalidDataException("Each checklist row needs a unique ID, title and instruction.");
                    }
                    if (rows.Count == 0)
                        throw new InvalidDataException("The checklist has no rows.");

                    _rows = rows;
                    _status.text = "Choose a part ID or use the selected VAB part.";
                    Refresh();
                }
                catch (Exception exception)
                {
                    _status.text = $"Checklist data failed: {exception.Message}";
                }
            });
        }

        private void BindVerdict(string buttonName, InputAction action)
        {
            Button button = RootElement.Q<Button>(buttonName);
            int index = _actions.Count;
            button.text = $"{button.text} ({action.GetBindingDisplayString()})";
            button.clicked += () => Record(index);
            _actions.Add(action);
            action.performed += OnVerdict;
        }

        private void OnDestroy()
        {
            foreach (InputAction action in _actions)
            {
                action.performed -= OnVerdict;
            }
        }

        private void OnVerdict(InputAction.CallbackContext context)
        {
            Record(_actions.IndexOf(context.action));
        }

        private void UseSelected()
        {
            ObjectAssemblyBuilder builder = Game.OAB.Current;
            if (builder == null)
            {
                _status.text = "Select a part in the VAB or type its part ID.";
                return;
            }

            IObjectAssemblyPart part = builder.ActivePartTracker.PartBeingManipulated ??
                builder.ActivePartTracker.partGrabbed;
            if (part == null)
            {
                _status.text = "Select or grab a VAB part first.";
                return;
            }

            _subject.value = part.PartName;
        }

        private void StartSession()
        {
            if (_rows == null || _busy || (_report != null && _index < _rows.Count))
                return;

            string subject = _subject.value.Trim();
            if (string.IsNullOrWhiteSpace(subject))
            {
                _status.text = "Enter a part ID first.";
                return;
            }

            try
            {
                _report = new QaReport($"Part checklist: {subject}", "checklist");
                _results.Clear();
                _index = 0;
                foreach (JToken row in _rows)
                {
                    // The row title is the check, so the dossier names each checklist row. The row id stays the key.
                    var result = new QaReportResult(subject, subject, row.Value<string>("title"),
                        row.Value<string>("id"), QaVerdict.NotTested, "Not tested.");
                    result.Evidence.Add(new JObject
                    {
                        ["type"] = "log", ["caption"] = row.Value<string>("title"),
                        ["text"] = row.Value<string>("instruction")
                    });
                    _results.Add(result);
                    _report.Add(result);
                }
                _report.Write();
                _status.text = _report.DirectoryPath;
                Refresh();
            }
            catch (Exception exception)
            {
                _report = null;
                _status.text = $"Could not start checklist: {exception.Message}";
                Refresh();
            }
        }

        private void Record(int index)
        {
            if (!IsWindowOpen || _busy || _report == null || _index >= _results.Count || index < 0)
                return;

            // A draft stays above the overlay until the tester closes it.
            if (Game.UI.BugReportUITKDocument != null && Game.UI.BugReportUITKDocument.enabled)
                return;

            QaVerdict verdict = index switch
            {
                0 => QaVerdict.Passed,
                1 => QaVerdict.Failed,
                2 => QaVerdict.NeedsReview,
                3 => QaVerdict.NotApplicable,
                _ => QaVerdict.NotTested
            };
            _busy = true;
            Refresh();
            StartCoroutine(CaptureRow(verdict, _note.value));
        }

        private IEnumerator CaptureRow(QaVerdict verdict, string note)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(_report.DirectoryPath, $"capture-{Guid.NewGuid():N}.png");
            try
            {
                ScreenshotCapture.CaptureToFile(path, ScreenshotFormat.Png, completed: saved =>
                {
                    if (this == null)
                        return;

                    CompleteRow(verdict, note, path, saved);
                });
            }
            catch (Exception exception)
            {
                _busy = false;
                _status.text = $"Screenshot failed: {exception.Message}. Retry this row.";
                Refresh();
            }
        }

        private void CompleteRow(QaVerdict verdict, string note, string path, bool saved)
        {
            QaReportResult result = _results[_index];
            QaVerdict previousVerdict = result.Verdict;
            string previousMessage = result.Message;
            int previousEvidenceCount = result.Evidence.Count;
            bool recorded = false;
            try
            {
                if (!saved)
                    throw new IOException("Screenshot could not be saved");

                string title = _rows[_index].Value<string>("title");
                JObject evidence = _report.CopyImage(path, title);
                result.Verdict = verdict;
                result.Message = note;
                result.Evidence.Add(evidence);
                _report.RecordOverrides();
                _report.Write();
                recorded = true;
                _index++;
                _note.value = string.Empty;
                _status.text = _report.DirectoryPath;
                if (verdict == QaVerdict.Failed)
                {
                    BugReportUI reporter = Game.UI.BugReportUITKgo == null
                        ? null : Game.UI.BugReportUITKgo.GetComponent<BugReportUI>();
                    if (reporter == null)
                    {
                        _status.text = $"Failure saved. Bug reporter unavailable. Dossier: {_report.DirectoryPath}";
                        return;
                    }

                    string description = $"Part: {result.SubjectId}\nCheck: {title}\n{_rows[_index - 1].Value<string>("instruction")}\nNote: {note}\nDossier: {_report.DirectoryPath}";
                    reporter.OpenDraft($"{result.SubjectId}: {title}", description,
                        new Attachment($"checklist-{_rows[_index - 1].Value<string>("id")}-{++_attachmentNumber}.png", File.ReadAllBytes(path), "png"));
                }
            }
            catch (Exception exception)
            {
                if (!recorded)
                {
                    result.Verdict = previousVerdict;
                    result.Message = previousMessage;
                    while (result.Evidence.Count > previousEvidenceCount)
                    {
                        result.Evidence.RemoveAt(result.Evidence.Count - 1);
                    }
                }
                _status.text = $"Could not record row: {exception.Message}. Check the dossier before retrying.";
            }
            finally
            {
                _busy = false;
                Refresh();
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        private void Refresh()
        {
            bool running = _report != null && _index < _results.Count;
            RootElement.Q<Button>("start").SetEnabled(_rows != null && !running && !_busy);
            RootElement.Q<Button>("selected").SetEnabled(!running && !_busy);
            _subject.SetEnabled(!running && !_busy);
            _note.SetEnabled(running && !_busy);
            RootElement.Q<VisualElement>("verdicts").SetEnabled(running && !_busy);
            RootElement.Q<Button>("report").SetEnabled(_report != null && !_busy);
            _progress.text = running ? $"{_index + 1}/{_results.Count}: {_rows[_index].Value<string>("title")}" : "Checklist";
            _instruction.text = running ? _rows[_index].Value<string>("instruction") :
                (_report == null ? "Start a checklist to record manual checks." : "Checklist complete. Open the dossier to review results.");
        }

        private void OpenReport()
        {
            if (_report == null || _busy)
                return;

            Application.OpenURL(new Uri(Path.Combine(_report.DirectoryPath, "report.html")).AbsoluteUri);
        }
    }
}

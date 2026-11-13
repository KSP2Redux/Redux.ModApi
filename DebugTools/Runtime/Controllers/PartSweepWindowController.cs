using System;
using KSP.OAB;
using Newtonsoft.Json.Linq;
using Redux.CliIntegration;
using Redux.QA;
using UnityEngine;
using UnityEngine.UIElements;

namespace DebugTools.Runtime.Controllers
{
    /// <summary>
    /// The on-demand filter and report controls shared by the part sweep and dossier hub entries.
    /// </summary>
    public class PartSweepWindowController : BaseWindowController
    {
        private TextField _subject;
        private TextField _origin;
        private TextField _since;
        private TextField _previous;
        private Label _status;

        private void OnEnable()
        {
            _subject = RootElement.Q<TextField>("subject");
            _origin = RootElement.Q<TextField>("origin");
            _since = RootElement.Q<TextField>("since");
            _previous = RootElement.Q<TextField>("previous");
            _status = RootElement.Q<Label>("status");
            RootElement.Q<Button>("selected").clicked += UseSelected;
            RootElement.Q<Button>("start").clicked += StartSweep;
            RootElement.Q<Button>("dossier").clicked += OpenDossier;
            RootElement.Q<Button>("list").clicked += ListParts;
            _status.text = "Sweep needs an empty VAB and a current F3 index. Save the current craft first. Dossier accepts any part ID.";
        }

        private void UseSelected()
        {
            ObjectAssemblyBuilder builder = Game.OAB.Current;
            IObjectAssemblyPart part = builder == null ? null : builder.ActivePartTracker.PartBeingManipulated ?? builder.ActivePartTracker.partGrabbed;
            if (part == null)
            {
                _status.text = "Select a VAB part or enter its ID.";
                return;
            }
            _subject.value = part.PartName;
        }

        private void StartSweep()
        {
            try
            {
                var options = new JObject { ["origin"] = _origin.value.Trim(), ["since"] = _since.value.Trim() };
                if (!string.IsNullOrWhiteSpace(_previous.value))
                    options["previousJson"] = _previous.value.Trim();
                if (!string.IsNullOrWhiteSpace(_subject.value))
                    options["ids"] = new JArray(_subject.value.Trim());
                JObject result = CliIntegrationServer.EnsureStarted(Game).StartPartSweep(options);
                _status.text = $"Sweep started. Report: {result["reportPath"]}";
            }
            catch (Exception exception) { _status.text = exception.Message; }
        }

        private void ListParts()
        {
            try
            {
                var options = new JObject { ["origin"] = _origin.value.Trim(), ["since"] = _since.value.Trim(), ["listOnly"] = true };
                JObject result = CliIntegrationServer.EnsureStarted(Game).StartPartSweep(options);
                RootElement.Q<TextField>("parts").value = result.ToString();
            }
            catch (Exception exception) { _status.text = exception.Message; }
        }

        private void OpenDossier()
        {
            try
            {
                string path = QaReport.WritePartDossier(_subject.value.Trim());
                _status.text = path;
                Application.OpenURL(new Uri(path).AbsoluteUri);
            }
            catch (Exception exception) { _status.text = exception.Message; }
        }
    }
}

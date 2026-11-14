using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using KSP.OAB;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Redux.QA;
using UnityEngine.UIElements;

namespace DebugTools.Runtime.Controllers
{
    /// <summary>
    /// Read-only resource routing and module conditions for flight and VAB parts.
    /// </summary>
    public class PartDiagnosticsWindowController : BaseWindowController
    {
        private DropdownField _subject;
        private readonly List<string> _guids = new();
        private JObject _snapshot;
        private string _rendered;
        private bool _showConditions;

        private void OnEnable()
        {
            _subject = RootElement.Q<DropdownField>("subject");
            RootElement.Q<Button>("refresh").clicked += Refresh;
            RootElement.Q<Button>("selected").clicked += UseSelected;
            RootElement.Q<Button>("flow").clicked += () => Explain(false);
            RootElement.Q<Button>("why").clicked += () => Explain(true);
            RootElement.Q<Button>("capture").clicked += Capture;
            _subject.RegisterValueChangedCallback(_ => Clear());
            Refresh();
        }

        /// <inheritdoc />
        protected override void OnRequestedOpenChanged(bool isOpen)
        {
            if (isOpen)
                Refresh();
            else
                Clear();
        }

        private void Clear()
        {
            _snapshot = null;
            RootElement.Q<Label>("results").text = "";
        }

        private void Refresh()
        {
            Clear();
            _guids.Clear();
            var choices = new List<string>();
            foreach (var part in PartDiagnostics.Subjects(Game))
            {
                _guids.Add(part.GlobalId.ToString());
                choices.Add($"{part.Name} | {part.GlobalId}");
            }
            _subject.choices = choices;
            _subject.index = choices.Count > 0 ? 0 : -1;
            RootElement.Q<Label>("status").text = "Read-only. Refresh after staging or changing craft. VAB has routing and configuration, no measured delivery.";
        }

        private void UseSelected()
        {
            var builder = Game.OAB.Current;
            IObjectAssemblyPart part = builder == null ? null : builder.ActivePartTracker.PartBeingManipulated ?? builder.ActivePartTracker.partGrabbed;
            if (part == null)
            {
                RootElement.Q<Label>("status").text = "Select a VAB part. In flight, choose a part from the list.";
                return;
            }
            Refresh();
            _subject.index = part is KSP.Sim.DeltaV.IDeltaVPart deltaVPart ? _guids.IndexOf(deltaVPart.GlobalId.ToString()) : -1;
        }

        private static string Number(JToken value) => value == null || value.Type == JTokenType.Null
            ? "unavailable" : value.Value<double>().ToString("0.00000", CultureInfo.InvariantCulture);

        private void Explain(bool conditions)
        {
            Clear();
            try
            {
                if (_subject.index < 0 || _subject.index >= _guids.Count)
                    throw new InvalidOperationException("Pick a current part first.");
                _snapshot = PartDiagnostics.Explain(Game, _guids[_subject.index]);
                _showConditions = conditions;
                var text = new StringBuilder();
                text.AppendLine($"{_snapshot["partName"]} | {_snapshot["partGuid"]}");
                if (conditions)
                {
                    foreach (JObject check in (JArray)_snapshot["conditions"])
                    {
                        string value = check["value"].Type is JTokenType.Float or JTokenType.Integer ? Number(check["value"]) : check["value"].ToString();
                        text.AppendLine($"{check["module"]}: {check["condition"]} = {value} | {(check.Value<bool>("blocks") ? "BLOCKS" : "clear")}");
                        text.AppendLine($"  {check["source"]}");
                    }
                    text.AppendLine(_snapshot.Value<string>("conditionsNote"));
                }
                foreach (JObject request in (JArray)_snapshot["requests"])
                {
                    text.AppendLine($"Request {request["request"]} | active {request["active"]} | priority {Number(request["priority"])} | tick UT {Number(request["lastTickUT"])} s");
                    foreach (JObject row in (JArray)request["resources"])
                    {
                        text.AppendLine($"{row["resource"]} ({row["direction"]}, {row["flowMode"]}) | limiting resource last tick: {row["limitedLastTick"]}");
                        text.AppendLine($"  Compiled demand {Number(row["requested"])} {row["unit"]}, minimum {Number(row["minimum"])} {row["unit"]}");
                        text.AppendLine($"  Last solved requested {Number(row["lastSolvedRequestedRate"])} / received {Number(row["receivedRate"])} resource units/s");
                        foreach (JObject container in (JArray)row["containers"])
                        {
                            string route = container.Value<bool>("reachable") ? $"reachable, group {container["groupOrder"]}" : $"excluded: {container["exclusion"]}";
                            text.AppendLine($"  {container["name"]} | {container["partGuid"]}: {route}, {Number(container["storedUnits"])} / {Number(container["capacityUnits"])} units");
                        }
                        foreach (JObject other in (JArray)row["sharedConsumers"])
                        {
                            text.AppendLine($"  Shared consumer {other["partGuid"]} / {other["request"]}: {Number(other["demand"])} {other["unit"]}, priority {Number(other["priority"])}");
                        }
                    }
                }
                if (((JArray)_snapshot["requests"]).Count == 0)
                    text.AppendLine("No compiled requests on this part. Disabled modules may not have submitted a request.");
                text.AppendLine(_snapshot.Value<string>("note"));
                _rendered = text.ToString();
                RootElement.Q<Label>("results").text = _rendered;
                RootElement.Q<Label>("status").text = "Snapshot captured on demand. No simulation was run. Capture saves this displayed snapshot.";
            }
            catch (Exception exception)
            {
                RootElement.Q<Label>("status").text = exception.Message;
            }
        }

        private void Capture()
        {
            if (_snapshot == null)
                return;
            var report = new QaReport("Part resource and operation diagnostics", "bench-run");
            var result = new QaReportResult(_snapshot.Value<string>("partGuid"), _snapshot.Value<string>("partName"),
                _showConditions ? "Why not working" : "Resource flow", "snapshot", QaVerdict.NeedsReview, "Read-only snapshot. Runtime acceptance pending.");
            result.Evidence.Add(new JObject { ["type"] = "log", ["caption"] = "Displayed diagnostics", ["text"] = _rendered });
            result.Evidence.Add(new JObject { ["type"] = "log", ["caption"] = "Diagnostic JSON", ["text"] = _snapshot.ToString(Formatting.Indented) });
            report.Add(result);
            RootElement.Q<Label>("status").text = report.Write();
        }
    }
}

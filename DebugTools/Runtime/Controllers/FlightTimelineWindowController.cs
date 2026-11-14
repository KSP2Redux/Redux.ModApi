using System;
using System.Collections;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using Redux.QA;
using UnityEngine;
using UnityEngine.UIElements;

namespace DebugTools.Runtime.Controllers
{
    /// <summary>
    /// Live and frozen views over the bounded flight recorder.
    /// </summary>
    public class FlightTimelineWindowController : BaseWindowController
    {
        private JObject _snapshot;
        private Slider _zoom;
        private Slider _cursor;
        private Toggle _loss;
        private VisualElement _charts;
        private Label _values;
        private Label _events;
        private Label _status;
        private Coroutine _refresh;
        private static readonly string[] CHANNELS = { "Message", "CLI", "Assertion", "Event tap" };

        private void OnEnable()
        {
            _zoom = RootElement.Q<Slider>("zoom");
            _cursor = RootElement.Q<Slider>("cursor");
            _loss = RootElement.Q<Toggle>("loss");
            _charts = RootElement.Q<VisualElement>("charts");
            _values = RootElement.Q<Label>("values");
            _events = RootElement.Q<Label>("events");
            _status = RootElement.Q<Label>("status");
            _values.enableRichText = _events.enableRichText = _status.enableRichText = false;
            _loss.SetValueWithoutNotify(true);
            _zoom.RegisterValueChangedCallback(_ => Draw());
            _cursor.RegisterValueChangedCallback(_ => Draw());
            _loss.RegisterValueChangedCallback(_ => Refresh());
            RootElement.Q<Button>("capture").clicked += Capture;
        }

        /// <inheritdoc />
        protected override void OnRequestedOpenChanged(bool isOpen)
        {
            if (_refresh != null)
                StopCoroutine(_refresh);
            _refresh = isOpen ? StartCoroutine(Observe()) : null;
        }

        private IEnumerator Observe()
        {
            var wait = new WaitForSecondsRealtime(0.5f);
            while (IsRequestedOpen)
            {
                if (IsWindowOpen)
                    Refresh();
                yield return wait;
            }
        }

        private void Refresh()
        {
            _snapshot = FlightRecorder.Snapshot(_loss.value);
            Draw();
        }

        private void Draw()
        {
            _charts.Clear();
            if (_snapshot == null)
            {
                _status.text = "No vessel loss recorded in this session.";
                _values.text = _events.text = "";
                return;
            }
            double end = _snapshot.Value<double>("endRealSeconds");
            double start = end - _zoom.value;
            double cursor = start + _cursor.value * _zoom.value;
            JArray samples = (JArray)_snapshot["samples"];
            JArray selected = null;
            foreach (JArray sample in samples)
            {
                if (sample[0].Value<double>() <= cursor)
                    selected = sample;
            }
            _status.text = FormattableString.Invariant($"{_snapshot["vesselId"]} | {start:F2} to {end:F2} real s | UTC {_snapshot["capturedUtc"]} | overwritten events {_snapshot["overwrittenEvents"]}");
            var text = new StringBuilder();
            if (selected != null)
            {
                text.AppendLine(FormattableString.Invariant($"Cursor {cursor:F2} real s | sample {selected[0].Value<double>():F2} real s | UT {selected[1].Value<double>():F2} s | tick {selected[2]}"));
                for (int c = 3; c < ((JArray)_snapshot["columns"]).Count; c++)
                {
                    text.AppendLine(FormattableString.Invariant($"{_snapshot["columns"][c]}: {selected[c].Value<double>():F3}"));
                }
            }
            else
            {
                text.Append("No sample at or before cursor.");
            }
            _values.text = text.ToString();
            for (int c = 3; c < ((JArray)_snapshot["columns"]).Count; c++)
            {
                int column = c;
                double min = double.PositiveInfinity;
                double max = double.NegativeInfinity;
                foreach (JArray row in samples)
                {
                    if (row[0].Value<double>() < start)
                        continue;
                    min = Math.Min(min, row[c].Value<double>());
                    max = Math.Max(max, row[c].Value<double>());
                }
                if (double.IsInfinity(min))
                    continue;
                _charts.Add(new Label(FormattableString.Invariant($"{_snapshot["columns"][c]} | {min:F2} to {max:F2}")));
                var graph = new VisualElement();
                graph.AddToClassList("timeline-graph");
                graph.generateVisualContent += context =>
                {
                    var painter = context.painter2D;
                    painter.lineWidth = 1;
                    painter.strokeColor = Color.cyan;
                    painter.BeginPath();
                    bool first = true;
                    foreach (JArray row in samples)
                    {
                        double real = row[0].Value<double>();
                        if (real < start)
                            continue;
                        var point = new Vector2((float)((real - start) / (end - start) * graph.contentRect.width),
                            32 - (float)((row[column].Value<double>() - min) / Math.Max(0.000001, max - min) * 30));
                        if (first)
                            painter.MoveTo(point);
                        else
                            painter.LineTo(point);
                        first = false;
                    }
                    painter.Stroke();
                    painter.strokeColor = Color.white;
                    painter.BeginPath();
                    float x = (float)((cursor - start) / (end - start) * graph.contentRect.width);
                    painter.MoveTo(new Vector2(x, 0));
                    painter.LineTo(new Vector2(x, 34));
                    painter.Stroke();
                };
                _charts.Add(graph);
            }
            var markers = (JArray)_snapshot["events"];
            foreach (string channel in CHANNELS)
            {
                _charts.Add(new Label(channel));
                var graph = new VisualElement();
                graph.AddToClassList("timeline-markers");
                graph.generateVisualContent += context =>
                {
                    var painter = context.painter2D;
                    painter.lineWidth = 2;
                    painter.strokeColor = Color.yellow;
                    painter.BeginPath();
                    foreach (JObject marker in markers)
                    {
                        double real = marker.Value<double>("realSeconds");
                        if (marker.Value<string>("channel") != channel || real < start)
                            continue;
                        float x = (float)((real - start) / (end - start) * graph.contentRect.width);
                        painter.MoveTo(new Vector2(x, 0));
                        painter.LineTo(new Vector2(x, 16));
                    }
                    painter.Stroke();
                };
                _charts.Add(graph);
            }
            text.Clear();
            foreach (JObject marker in markers)
            {
                if (marker.Value<double>("realSeconds") < start)
                    continue;
                text.AppendLine(FormattableString.Invariant($"{marker.Value<double>("realSeconds"):F2} real s | UT {marker.Value<double>("utSeconds"):F2} s | tick {marker["tick"]} | {marker["channel"]}: {marker["text"]}"));
            }
            _events.text = text.ToString();
        }

        private void Capture()
        {
            _snapshot = FlightRecorder.Snapshot(_loss.value);
            if (_snapshot == null)
            {
                Draw();
                return;
            }
            var report = new QaReport("Flight timeline", "playtest");
            Directory.CreateDirectory(report.DirectoryPath);
            File.WriteAllText(Path.Combine(report.DirectoryPath, "timeline.json"), _snapshot.ToString());
            File.WriteAllText(Path.Combine(report.DirectoryPath, "timeline.html"), FlightRecorder.RenderHtml(_snapshot));
            var result = new QaReportResult("flight", "Flight", "Timeline", "timeline", QaVerdict.NeedsReview, "Flight recorder capture");
            result.Evidence.Add(new JObject { ["type"] = "html", ["path"] = "timeline.html", ["caption"] = "Flight timeline" });
            report.Add(result);
            _status.text = report.Write();
        }

        private void OnDisable()
        {
            if (_refresh != null)
                StopCoroutine(_refresh);
            _refresh = null;
        }
    }
}

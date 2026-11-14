using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;
using Redux.QA;
using UnityEngine.UIElements;

namespace DebugTools.Runtime.Controllers
{
    /// <summary>
    /// Thermal chamber window owning hypothetical conditions and captured predictions.
    /// </summary>
    public class ThermalBenchWindowController : BaseWindowController
    {
        private ConditionsPanel _conditions;
        private JObject _result;
        private JArray _sweep;
        private QaReport _report;
        private Label _status;
        private static readonly string[] PART_COLUMNS = { "part", "peakK", "limitK", "marginK", "failureS" };

        private void OnEnable()
        {
            _conditions = new ConditionsPanel(RootElement.Q("conditions"), Game);
            _status = RootElement.Q<Label>("status");
            RootElement.Q<Label>("validation").text = "Flight validated | VAB launch comparison pending";
            RootElement.Q<Button>("run").clicked += Run;
            RootElement.Q<Button>("capture").clicked += Capture;
            // Name the craft as soon as the window opens, and follow it when the assembly or vessel changes.
            UpdateSubject();
            RootElement.schedule.Execute(UpdateSubject).Every(1000);
        }

        private void UpdateSubject()
        {
            string text = "No main VAB assembly or loaded flight vessel";
            try
            {
                if (Game.GlobalGameState.GetState() == KSP.Game.GameState.VehicleAssemblyBuilder)
                    text = WindTunnel.GetAssembly(Game).Anchor.Name;
                else if (Game.ViewController.GetActiveSimVessel() is KSP.Sim.impl.VesselComponent vessel)
                    text = vessel.Name;
            }
            catch (InvalidOperationException) { }
            RootElement.Q<Label>("subject").text = text;
        }

        private void Run()
        {
            _result = null;
            _report = null;
            RootElement.Q("results").Clear();
            try
            {
                JObject conditions = _conditions.Read();
                if (Game.GlobalGameState.GetState() == KSP.Game.GameState.VehicleAssemblyBuilder)
                    conditions["initialK"] = RootElement.Q<FloatField>("initial").value;
                double duration = RootElement.Q<FloatField>("duration").value;
                double step = RootElement.Q<FloatField>("step").value;
                bool sweep = RootElement.Q<Toggle>("sweep").value;
                double from = RootElement.Q<FloatField>("from").value;
                double to = RootElement.Q<FloatField>("to").value;
                int count = RootElement.Q<IntegerField>("count").value;
                if (sweep && (!double.IsFinite(from) || !double.IsFinite(to) || Math.Abs(from) > 85 || Math.Abs(to) > 85 || count < 2 || count > 21 || duration / step * (count + 1) > 20000))
                    throw new ArgumentException("Use angles within 85 degrees, 2 to 21 samples and at most 20000 total ticks.");
                var report = new QaReport("Thermal chamber", "bench-run");
                JObject result = ThermalBench.Run(Game, _conditions.Body, conditions, duration, step);
                var angles = new JArray();
                if (sweep)
                {
                    for (int index = 0; index < count; index++)
                    {
                        double angle = from + (to - from) * index / (count - 1.0);
                        var point = (JObject)conditions.DeepClone();
                        point["aoa"] = angle;
                        JObject prediction = ThermalBench.Run(Game, _conditions.Body, point, duration, step);
                        double? failure = null;
                        foreach (JObject part in prediction["parts"])
                        {
                            double? time = part.Value<double?>("failureS");
                            if (time.HasValue && (!failure.HasValue || time < failure))
                                failure = time;
                        }
                        angles.Add(new JArray(angle, prediction["heatingKW"], failure));
                    }
                }
                _result = result;
                _report = report;
                _sweep = angles;
                RootElement.Q<Label>("subject").text = result.Value<string>("craft");
                VisualElement table = RootElement.Q("results");
                AddCells(table, new[] { "Part", "Peak K", "Limit K", "Margin K", "Failure s" });
                foreach (JObject part in result["parts"])
                {
                    var cells = new string[PART_COLUMNS.Length];
                    for (int column = 0; column < cells.Length; column++)
                    {
                        JToken value = part[PART_COLUMNS[column]];
                        cells[column] = value.Type == JTokenType.Null ? "> duration" : column == 0
                            ? value.Value<string>() : value.Value<double>().ToString("F2", CultureInfo.InvariantCulture);
                    }
                    AddCells(table, cells);
                }
                AddRow(table, FormattableString.Invariant($"Peak producer heat {result.Value<double>("producedKW"):F3} kW | Operational cooling {result.Value<double>("radiatorCapacityKW"):F3} kW | Installed {result.Value<double>("installedCapacityKW"):F3} kW"));
                AddRow(table, FormattableString.Invariant($"Peak holding demand {result.Value<double>("demandKW"):F3} kW | Surplus {result.Value<double>("surplusKW"):F3} kW (negative = deficit)"));
                AddRow(table, "AoA deg | Peak reentry kW | First failure s");
                foreach (JArray row in angles)
                {
                    string failure = row[2].Type == JTokenType.Null ? "> duration" : row[2].Value<double>().ToString("F2", CultureInfo.InvariantCulture);
                    AddRow(table, FormattableString.Invariant($"{row[0].Value<double>():F2} | {row[1].Value<double>():F3} | {failure}"));
                }
                _status.text = result.Value<string>("limits");
            }
            catch (Exception exception)
            {
                _result = null;
                _report = null;
                _status.text = exception.Message;
            }
        }

        private static void AddRow(VisualElement table, string text)
        {
            var label = new Label(text);
            label.AddToClassList("kui-value");
            table.Add(label);
        }

        private static void AddCells(VisualElement table, string[] cells)
        {
            var row = new VisualElement();
            row.AddToClassList("kui-row");
            for (int index = 0; index < cells.Length; index++)
            {
                var label = new Label(cells[index]);
                label.AddToClassList(index == 0 ? "thermal-part" : "thermal-number");
                row.Add(label);
            }
            table.Add(row);
        }

        private void Capture()
        {
            if (_report == null || _result == null)
                return;
            try
            {
                var result = new QaReportResult(_result.Value<string>("craftId"), _result.Value<string>("craft"),
                    "thermal-chamber", "prediction", QaVerdict.NeedsReview, "Not validated. Frozen-state thermal prediction.");
                result.Evidence.Add(new JObject { ["type"] = "log", ["caption"] = "Prediction and assumptions", ["text"] = _result.ToString() });
                var rows = new JArray();
                foreach (JObject part in _result["parts"])
                {
                    rows.Add(new JArray(part["part"], part["peakK"], part["limitK"], part["marginK"], part["failureS"]));
                }
                result.Evidence.Add(new JObject { ["type"] = "table", ["caption"] = "Part temperatures and failure time",
                    ["columns"] = new JArray(PART_COLUMNS), ["rows"] = rows });
                result.Evidence.Add(new JObject { ["type"] = "table", ["caption"] = "Orientation sweep",
                    ["columns"] = new JArray("AoA deg", "Peak reentry kW", "First failure s"), ["rows"] = _sweep.DeepClone() });
                result.Evidence.Add(new JObject { ["type"] = "html", ["path"] = "polars.html", ["caption"] = "Open thermal charts" });
                _report.Add(result);
                string path = _report.Write();
                var html = new StringBuilder("<!doctype html><meta charset='utf-8'><title>Thermal chamber</title><style>body{font:14px sans-serif;margin:24px}pre{white-space:pre-wrap}svg{max-width:100%}line{stroke:#ccc}circle{fill:#1679a8}text{font:12px sans-serif}</style><h1>Thermal chamber</h1><p>Not validated. A missing failure marker means the part survived the requested duration. Failure is resolved to the end of an integration tick.</p><pre>");
                html.Append(WebUtility.HtmlEncode(_result.ToString()));
                JObject fingerprint = JObject.Parse(File.ReadAllText(Path.Combine(_report.DirectoryPath, "report.json")));
                html.Append(WebUtility.HtmlEncode(fingerprint["fingerprint"].ToString())).Append("</pre>");
                Chart(html, _sweep, 1, "AoA (deg)", "Peak reentry (kW)");
                Chart(html, _sweep, 2, "AoA (deg)", "First failure (s)");
                File.WriteAllText(Path.Combine(_report.DirectoryPath, "polars.html"), html.ToString());
                _status.text = path;
            }
            catch (Exception exception)
            {
                _status.text = exception.Message;
            }
        }

        private static void Chart(StringBuilder html, JArray rows, int column, string xLabel, string yLabel)
        {
            if (rows.Count < 2)
                return;
            double xMin = double.PositiveInfinity;
            double xMax = double.NegativeInfinity;
            double yMax = 1;
            foreach (JArray row in rows)
            {
                xMin = Math.Min(xMin, row[0].Value<double>());
                xMax = Math.Max(xMax, row[0].Value<double>());
                if (row[column].Type != JTokenType.Null)
                    yMax = Math.Max(yMax, row[column].Value<double>());
            }
            html.Append($"<h2>{yLabel}</h2><svg viewBox='0 0 720 300' role='img' aria-label='{yLabel} versus {xLabel}'>");
            for (int tick = 0; tick <= 5; tick++)
            {
                double x = 80 + tick * 120;
                double y = 250 - tick * 44;
                html.Append(FormattableString.Invariant($"<line x1='{x}' y1='30' x2='{x}' y2='250'/><line x1='80' y1='{y}' x2='680' y2='{y}'/><text x='{x}' y='270'>{xMin + (xMax - xMin) * tick / 5:G4}</text><text x='5' y='{y}'>{yMax * tick / 5:G4}</text>"));
            }
            foreach (JArray row in rows)
            {
                if (row[column].Type == JTokenType.Null)
                    continue;
                double x = 80 + 600 * (row[0].Value<double>() - xMin) / Math.Max(1e-12, xMax - xMin);
                double y = 250 - 220 * row[column].Value<double>() / yMax;
                html.Append(FormattableString.Invariant($"<circle cx='{x:F2}' cy='{y:F2}' r='4'/>"));
            }
            html.Append($"<text x='320' y='295'>{xLabel}</text><text x='80' y='18'>{yLabel}</text></svg>");
        }
    }
}

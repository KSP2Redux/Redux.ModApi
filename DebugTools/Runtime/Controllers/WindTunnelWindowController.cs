using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using KSP.Sim.impl;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Redux.QA;
using UnityEngine;
using UnityEngine.UIElements;

namespace DebugTools.Runtime.Controllers
{
    /// <summary>
    /// Wind tunnel window owning its hypothetical conditions and captured sweep.
    /// </summary>
    public class WindTunnelWindowController : BaseWindowController
    {
        private ConditionsPanel _conditions;
        private DropdownField _variable;
        private QaReport _report;
        private JArray _rows;
        private JObject _stamp;
        private string _subject;
        private Label _status;
        private static readonly string[] COLUMNS = { "sample", "drag", "lift", "side", "pitch", "yaw", "roll", "heating" };

        private void OnEnable()
        {
            _conditions = new ConditionsPanel(RootElement.Q("conditions"), Game);
            _variable = RootElement.Q<DropdownField>("variable");
            _variable.choices = new List<string> { "AoA (deg)", "Sideslip (deg)", "Mach", "Altitude (m)" };
            _variable.index = 0;
            _variable.RegisterValueChangedCallback(_ => UpdateSweepLabels());
            UpdateSweepLabels();
            _status = RootElement.Q<Label>("status");
            RootElement.Q<Label>("validation").text = WindTunnel.IS_VALIDATED ? "Validated against flight" : "Not validated";
            RootElement.Q<Button>("run").clicked += Run;
            RootElement.Q<Button>("capture").clicked += Capture;
            UpdateResultsHeader();
        }

        private string SweepVariable => _variable.index switch
        {
            1 => "sideslip",
            2 => "mach",
            3 => "altitude",
            _ => "aoa"
        };

        private void UpdateSweepLabels()
        {
            string unit = SweepVariable switch { "mach" => "Mach", "altitude" => "m", _ => "deg" };
            RootElement.Q<FloatField>("from").label = $"From ({unit})";
            RootElement.Q<FloatField>("to").label = $"To ({unit})";
        }

        private void UpdateResultsHeader()
        {
            VisualElement header = RootElement.Q("results-header");
            header.Clear();
            AddTableRow(header, new[] { _variable.value, "Drag kN", "Lift kN", "Side kN", "Pitch kN m", "Yaw kN m", "Roll kN m", "Heat kW" });
        }

        private static string FormatNumber(double value)
        {
            // Five significant digits without exponent notation. Hide sub-resolution solver noise.
            if (Math.Abs(value) < 0.0001)
                return "0";
            int exponent = (int)Math.Floor(Math.Log10(Math.Abs(value)));
            double scale = Math.Pow(10, exponent - 4);
            double rounded = Math.Round(value / scale) * scale;
            int decimals = Math.Clamp(4 - (int)Math.Floor(Math.Log10(Math.Abs(rounded))), 0, 8);
            return rounded.ToString(FormattableString.Invariant($"F{decimals}"), CultureInfo.InvariantCulture);
        }

        private JArray DisplayRows()
        {
            var rows = new JArray();
            foreach (JArray row in _rows)
            {
                var cells = new JArray();
                foreach (JToken cell in row)
                {
                    cells.Add(FormatNumber(cell.Value<double>()));
                }
                rows.Add(cells);
            }
            return rows;
        }

        private void Run()
        {
            try
            {
                _rows = null;
                _report = null;
                RootElement.Q("results").Clear();
                VesselComponent vessel = Game.ViewController.GetActiveSimVessel();
                if (vessel == null)
                    throw new InvalidOperationException("Needs a loaded flight vessel. VAB sampling is not supported.");
                _subject = vessel.Name;
                RootElement.Q<Label>("subject").text = $"{vessel.Name} | {vessel.GlobalId}";
                JObject conditions = _conditions.Read();
                float start = RootElement.Q<FloatField>("from").value;
                float end = RootElement.Q<FloatField>("to").value;
                int count = RootElement.Q<IntegerField>("count").value;
                if (!float.IsFinite(start) || !float.IsFinite(end) || count < 2 || count > 101)
                    throw new ArgumentException("Use finite sweep limits and 2 to 101 samples.");
                var report = new QaReport("Wind tunnel", "bench-run");
                var rows = new JArray();
                var physics = Game.UniverseView.PhysicsSpace;
                Vector3 forward = physics.VectorToPhysics(vessel.ControlTransform.up);
                Vector3 right = physics.VectorToPhysics(vessel.ControlTransform.right);
                Vector3 up = Vector3.Cross(right, forward).normalized;
                for (int index = 0; index < count; index++)
                {
                    double value = start + (end - start) * index / (count - 1.0);
                    var point = (JObject)conditions.DeepClone();
                    point[SweepVariable] = value;
                    if (SweepVariable == "altitude")
                    {
                        point["pressureKPa"] = _conditions.Body.GetPressure(value);
                        point["temperature"] = _conditions.Body.GetTemperature(value);
                    }
                    double aoa = point.Value<double>("aoa");
                    double slip = point.Value<double>("sideslip");
                    double mach = point.Value<double>("mach");
                    if (Math.Abs(aoa) > 85 || Math.Abs(slip) > 85 || mach < 0)
                        throw new ArgumentException("Sweep angles must stay within 85 degrees and Mach must be nonnegative.");
                    double density = _conditions.Body.GetDensity(point.Value<double>("pressureKPa"), point.Value<double>("temperature"));
                    double sound = AeroUtilities.GetSoundSpeed(_conditions.Body.atmosphereAdiabaticIndex, point.Value<double>("pressureKPa"), density);
                    Vector3 direction = (forward - up * (float)Math.Tan(aoa * Math.PI / 180) +
                        right * (float)Math.Tan(slip * Math.PI / 180)).normalized;
                    JObject result = WindTunnel.Sample(Game, _conditions.Body, density, (float)(sound * mach), (float)mach, direction);
                    var row = new JArray(value);
                    for (int column = 1; column < COLUMNS.Length; column++)
                    {
                        if (!double.IsFinite(result.Value<double>(COLUMNS[column])))
                            throw new InvalidOperationException("The flight solver returned a nonfinite result. No report was captured.");
                        row.Add(result[COLUMNS[column]]);
                    }
                    rows.Add(row);
                }
                _stamp = new JObject { ["conditions"] = conditions, ["variable"] = SweepVariable,
                    ["craft"] = _subject, ["craftId"] = vessel.GlobalId.ToString(), ["validated"] = WindTunnel.IS_VALIDATED,
                    ["from"] = start, ["to"] = end, ["count"] = count };
                _stamp["thermalShielding"] = "Current flight exposure and shield fractions held fixed across the sweep";
                _rows = rows;
                _report = report;
                VisualElement table = RootElement.Q("results");
                table.Clear();
                UpdateResultsHeader();
                foreach (JArray row in rows)
                {
                    var cells = new string[row.Count];
                    for (int column = 0; column < cells.Length; column++)
                        cells[column] = FormatNumber(row[column].Value<double>());
                    AddTableRow(table, cells);
                }
                _status.text = FormattableString.Invariant($"{count} headless samples. Current geometry and shielding held fixed. Magnitudes below 0.0001 shown as 0. Capture to write reports.");
            }
            catch (Exception exception)
            {
                _status.text = exception.Message;
            }
        }

        private static void AddTableRow(VisualElement table, string[] cells)
        {
            var row = new VisualElement();
            row.AddToClassList("wind-tunnel-row");
            foreach (string cell in cells)
            {
                var label = new Label(cell);
                label.AddToClassList("wind-tunnel-cell");
                row.Add(label);
            }
            table.Add(row);
        }

        private static void AppendMetadata(StringBuilder html, JToken token, string path)
        {
            if (token is JObject properties)
            {
                foreach (JProperty property in properties.Properties())
                {
                    string label = property.Name switch
                    {
                        "altitude" => "Altitude (m ASL)", "temperature" => "Temperature (K)",
                        "pressureKPa" => "Pressure (kPa)", "density" => "Density (kg/m3)",
                        "mach" => "Mach", "aoa" => "AoA (deg)", "sideslip" => "Sideslip (deg)",
                        _ => property.Name
                    };
                    AppendMetadata(html, property.Value, path.Length == 0 ? label : $"{path} / {label}");
                }
                return;
            }
            if (token is JArray items && items.Count > 0)
            {
                for (int index = 0; index < items.Count; index++)
                {
                    AppendMetadata(html, items[index], FormattableString.Invariant($"{path} [{index + 1}]"));
                }
                return;
            }
            string value = token.Type == JTokenType.String ? token.Value<string>() : token.ToString(Formatting.None);
            html.Append($"<tr><th scope='row'>{WebUtility.HtmlEncode(path)}</th><td>{WebUtility.HtmlEncode(value)}</td></tr>");
        }

        private void AppendChart(StringBuilder html, int column)
        {
            string variable = _stamp.Value<string>("variable");
            string xLabel = variable switch
            {
                "aoa" => "AoA (deg)", "sideslip" => "Sideslip (deg)",
                "mach" => "Mach (dimensionless)", "altitude" => "Altitude (m)", _ => variable
            };
            string unit = column == 7 ? "kW" : column >= 4 ? "kN m" : "kN";
            string yLabel = $"{COLUMNS[column]} ({unit})";
            double xMinimum = double.PositiveInfinity;
            double xMaximum = double.NegativeInfinity;
            double minimum = double.PositiveInfinity;
            double maximum = double.NegativeInfinity;
            foreach (JArray row in _rows)
            {
                double x = row[0].Value<double>();
                double y = row[column].Value<double>();
                xMinimum = Math.Min(xMinimum, x);
                xMaximum = Math.Max(xMaximum, x);
                minimum = Math.Min(minimum, y);
                maximum = Math.Max(maximum, y);
            }
            html.Append($"<section><h2>{WebUtility.HtmlEncode(yLabel)}</h2><p>Range {minimum.ToString("G5", CultureInfo.InvariantCulture)} to {maximum.ToString("G5", CultureInfo.InvariantCulture)} {unit}</p>");
            // Pad flat ranges so zero heating and equal sweep limits still produce a readable plot.
            double yPadding = maximum == minimum ? Math.Max(1, Math.Abs(minimum) * 0.05) : (maximum - minimum) * 0.05;
            double yMinimum = minimum - yPadding;
            double yMaximum = maximum + yPadding;
            if (xMinimum == xMaximum)
            {
                double padding = Math.Max(1, Math.Abs(xMinimum) * 0.05);
                xMinimum -= padding;
                xMaximum += padding;
            }
            double PlotX(double value) => 110 + 720 * (value - xMinimum) / (xMaximum - xMinimum);
            double PlotY(double value) => 300 - 260 * (value - yMinimum) / (yMaximum - yMinimum);
            html.Append($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 880 375' role='img' aria-label='{WebUtility.HtmlEncode(yLabel)} by {WebUtility.HtmlEncode(xLabel)}'><title>{WebUtility.HtmlEncode(yLabel)} by {WebUtility.HtmlEncode(xLabel)}</title>");
            for (int tick = 0; tick <= 5; tick++)
            {
                double xValue = xMinimum + (xMaximum - xMinimum) * tick / 5;
                double yValue = yMinimum + (yMaximum - yMinimum) * tick / 5;
                double x = PlotX(xValue);
                double y = PlotY(yValue);
                html.Append(FormattableString.Invariant($"<line class='grid' x1='{x:F2}' x2='{x:F2}' y1='40' y2='300'/><line class='grid' x1='110' x2='830' y1='{y:F2}' y2='{y:F2}'/><text x='{x:F2}' y='323' text-anchor='middle'>{xValue.ToString("G5", CultureInfo.InvariantCulture)}</text><text x='100' y='{y + 4:F2}' text-anchor='end'>{yValue.ToString("G5", CultureInfo.InvariantCulture)}</text>"));
            }
            if (yMinimum <= 0 && yMaximum >= 0)
            {
                double zero = PlotY(0);
                html.Append(FormattableString.Invariant($"<line class='zero' x1='110' x2='830' y1='{zero:F2}' y2='{zero:F2}'/>"));
            }
            if (xMinimum <= 0 && xMaximum >= 0)
            {
                double zero = PlotX(0);
                html.Append(FormattableString.Invariant($"<line class='zero' x1='{zero:F2}' x2='{zero:F2}' y1='40' y2='300'/>"));
            }
            html.Append($"<path class='axis' fill='none' d='M110 40V300H830'/><text x='470' y='360' text-anchor='middle'>{WebUtility.HtmlEncode(xLabel)}</text><text transform='translate(20 170) rotate(-90)' text-anchor='middle'>{WebUtility.HtmlEncode(yLabel)}</text><polyline points='");
            foreach (JArray row in _rows)
            {
                html.Append(FormattableString.Invariant($"{PlotX(row[0].Value<double>()):F2},{PlotY(row[column].Value<double>()):F2} "));
            }
            html.Append("'/>");
            foreach (JArray row in _rows)
            {
                double x = row[0].Value<double>();
                double y = row[column].Value<double>();
                html.Append(FormattableString.Invariant($"<circle cx='{PlotX(x):F2}' cy='{PlotY(y):F2}' r='3'><title>{WebUtility.HtmlEncode(xLabel)}: {x.ToString("G5", CultureInfo.InvariantCulture)}, {WebUtility.HtmlEncode(yLabel)}: {y.ToString("G5", CultureInfo.InvariantCulture)}</title></circle>"));
            }
            html.Append("</svg></section>");
        }

        private void Capture()
        {
            if (_report == null || _rows == null)
                return;
            try
            {
                var result = new QaReportResult(_stamp.Value<string>("craftId"), _subject, "wind-tunnel", "polars",
                    QaVerdict.NeedsReview, "Headless sweep. Flight validation is required before trusting these values.");
                result.Evidence.Add(new JObject { ["type"] = "log", ["caption"] = "Conditions", ["text"] = _stamp.ToString() });
                result.Evidence.Add(new JObject { ["type"] = "table", ["caption"] = "Polars: forces kN, moments kN m, heating kW",
                    ["columns"] = new JArray(COLUMNS), ["rows"] = DisplayRows(), ["numericRows"] = _rows.DeepClone() });
                result.Evidence.Add(new JObject { ["type"] = "html", ["path"] = "polars.html", ["caption"] = "Open wind tunnel polars" });
                _report.Add(result);
                string path = _report.Write();
                JObject fingerprint = JObject.Parse(File.ReadAllText(Path.Combine(_report.DirectoryPath, "report.json")));
                var html = new StringBuilder("<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>Wind tunnel polars</title><style>body{font:14px sans-serif;margin:24px;color:#243444;background:#fff}main{max-width:1000px;margin:auto}svg{display:block;width:100%;height:auto}pre{white-space:pre-wrap;overflow-wrap:anywhere}polyline{fill:none;stroke:#1679a8;stroke-width:2}circle{fill:#1679a8}text{font:12px sans-serif;fill:#243444}.grid{stroke:#e0e5ea;stroke-width:1}.axis{stroke:#607080;stroke-width:1}.zero{stroke:#607080;stroke-width:1.5;stroke-dasharray:5 3}table{border-collapse:collapse;width:100%;margin-bottom:20px}th,td{border-bottom:1px solid #e0e5ea;text-align:left;padding:4px 8px;overflow-wrap:anywhere}th{width:28%}details{margin:16px 0}section{break-inside:avoid}</style></head><body><main><h1>Wind tunnel polars</h1><p>Not validated. Frozen craft geometry and current flight thermal exposure and shielding. Forces kN, moments kN m, heating kW.</p><h2>Conditions and sweep</h2><table>");
                AppendMetadata(html, _stamp, "");
                html.Append("</table><h2>Build fingerprint</h2><table>");
                AppendMetadata(html, fingerprint["fingerprint"], "");
                html.Append("</table><details><summary>Full conditions and fingerprint JSON</summary><pre>");
                html.Append(WebUtility.HtmlEncode(new JObject { ["sweep"] = _stamp.DeepClone(), ["fingerprint"] = fingerprint["fingerprint"].DeepClone() }.ToString(Formatting.Indented)));
                html.Append("</pre></details>");
                for (int column = 1; column < COLUMNS.Length; column++)
                {
                    AppendChart(html, column);
                }
                html.Append("</main></body></html>");
                File.WriteAllText(Path.Combine(_report.DirectoryPath, "polars.html"), html.ToString());
                _status.text = path;
            }
            catch (Exception exception)
            {
                _status.text = exception.Message;
            }
        }
    }
}

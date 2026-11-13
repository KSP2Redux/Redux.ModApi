using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using Redux.CliIntegration;
using UnityEngine;
using UnityEngine.UIElements;

namespace DebugTools.Runtime.Controllers
{
    /// <summary>
    /// Flight window owning the module bench's controls and visible observation lifetime.
    /// </summary>
    public class ModuleBenchWindowController : BaseWindowController
    {
        private DropdownField _module;
        private DropdownField _preset;
        private Label _status;
        private Label _results;
        private bool _running;

        private void OnEnable()
        {
            _module = RootElement.Q<DropdownField>("module");
            _module.choices = new List<string> { "all", "generator", "solar", "radiator", "converter", "heatshield", "antenna" };
            _module.index = 0;
            _preset = RootElement.Q<DropdownField>("preset");
            _preset.choices = new List<string> { "Ambient", "Sunlight", "Shade", "Thermal load", "Reentry" };
            _preset.index = 0;
            _status = RootElement.Q<Label>("status");
            _results = RootElement.Q<Label>("results");
            RootElement.Q<Button>("refresh").clicked += RefreshSubject;
            RootElement.Q<Button>("start").clicked += StartRun;
            RootElement.Q<Button>("stop").clicked += StopRun;
            RootElement.Q<Button>("capture").clicked += Capture;
            RefreshSubject();
        }

        private JObject Command(string action) => CliIntegrationServer.ModuleBench(Game, new JObject
        {
            ["action"] = action, ["module"] = _module.value, ["preset"] = _preset.value,
            ["duration"] = RootElement.Q<FloatField>("duration").value,
            ["power"] = RootElement.Q<FloatField>("power").value
        });

        private void RefreshSubject()
        {
            if (_running)
                StopRun();
            try
            {
                JObject response = Command("list");
                var text = new StringBuilder();
                foreach (JObject subject in (JArray)response["subjects"])
                {
                    text.AppendLine($"{subject["name"]} | {subject["partName"]} | {subject["id"]} | {subject["module"]}");
                }
                RootElement.Q<Label>("subject").text = text.ToString();
                _status.text = $"{response["count"]} supported modules. Enable and deploy modules normally before running.";
            }
            catch (Exception exception)
            {
                _status.text = exception.Message;
            }
        }

        private void StartRun()
        {
            if (_running)
                return;
            try
            {
                Command("start");
                _running = true;
                _module.SetEnabled(false);
                _preset.SetEnabled(false);
                _status.text = "Observing normal simulation ticks. Close or Stop cancels and restores the test load.";
                StartCoroutine(Observe());
            }
            catch (Exception exception)
            {
                _status.text = exception.Message;
            }
        }

        private IEnumerator Observe()
        {
            try
            {
                while (_running && IsRequestedOpen)
                {
                    JObject response = Command("status");
                    if (response.Value<bool>("running"))
                    {
                        yield return new WaitForSecondsRealtime(0.2f);
                        continue;
                    }
                    ShowResults(response["result"] as JObject);
                    yield break;
                }
            }
            finally
            {
                StopRun();
            }
        }

        private void ShowResults(JObject result)
        {
            if (result?.Value<bool>("completed") != true)
            {
                _status.text = result?.Value<string>("error") ?? "Run cancelled. No completed measurement.";
                return;
            }
            var text = new StringBuilder();
            foreach (JObject subject in (JArray)result["subjects"])
            {
                text.AppendLine($"{subject["name"]} | {subject["partName"]} | {subject["id"]}");
                text.AppendLine(subject.Value<string>("lastState"));
                foreach (JObject metric in (JArray)subject["metrics"])
                {
                    text.AppendLine($"{metric["name"]}: stated {metric["stated"]}, mean {metric.Value<double>("mean"):G6} {metric["unit"]} | difference {metric["differencePercent"]} % | within 5% {metric["withinThreshold"]}");
                    if (metric["vesselNetRate"] != null)
                        text.AppendLine($"  {metric["resource"]} vessel net {metric.Value<double>("vesselNetRate"):G6} resource units/s | difference {metric["netDifferencePercent"]} %");
                }
            }
            _results.text = text.ToString();
            _status.text = $"Completed {result["duration"]} s. Needs review: compare conditions and net resource flow. Capture writes the report.";
        }

        private void Capture()
        {
            try
            {
                _status.text = $"Captured: {Command("capture")["result"]?["reportPath"]}";
            }
            catch (Exception exception)
            {
                _status.text = exception.Message;
            }
        }

        private void StopRun()
        {
            if (!_running)
                return;
            _running = false;
            try
            {
                Command("stop");
            }
            finally
            {
                _module.SetEnabled(true);
                _preset.SetEnabled(true);
            }
        }

        private void OnDisable() => StopRun();

        private void OnDestroy() => StopRun();
    }
}

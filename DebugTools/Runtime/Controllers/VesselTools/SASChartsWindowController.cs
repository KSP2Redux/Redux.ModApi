using System.Collections.Generic;
using KSP.Sim;
using KSP.Sim.impl;
using NB.Charts;
using UniLinq;
using UnityEngine;
using UnityEngine.UIElements;


// ReSharper disable once CheckNamespace
namespace DebugTools.Runtime.Controllers.VesselTools
{
    public class SASChartsWindowController : BaseWindowController
    {
        private static readonly List<float> Ticks = new() { -180f, -90f, 0f, 90f, 180f };

        private LineChart _pitchChart;
        private readonly List<float> _pitchValue = Enumerable.Repeat(0f, 100).ToList();
        private readonly List<float> _pitchError = Enumerable.Repeat(0f, 100).ToList();
        private readonly List<float> _pitchCommand = Enumerable.Repeat(0f, 100).ToList();

        private LineChart _rollChart;
        private readonly List<float> _rollValue = Enumerable.Repeat(0f, 100).ToList();
        private readonly List<float> _rollError = Enumerable.Repeat(0f, 100).ToList();
        private readonly List<float> _rollCommand = Enumerable.Repeat(0f, 100).ToList();

        private LineChart _yawChart;
        private readonly List<float> _yawValue = Enumerable.Repeat(0f, 100).ToList();
        private readonly List<float> _yawError = Enumerable.Repeat(0f, 100).ToList();
        private readonly List<float> _yawCommand = Enumerable.Repeat(0f, 100).ToList();

        private int _currentIndex;
        
        private void OnEnable()
        {
            Enable();

            _pitchChart = SetupLineChart("pitch-chart");
            _rollChart = SetupLineChart("roll-chart");
            _yawChart = SetupLineChart("yaw-chart");
        }

        public void SyncTo(VesselComponent vessel, VesselBehavior behavior)
        {
            var sas = behavior.Autopilot.SAS;
            
            _pitchValue[_currentIndex] = (float)vessel.Pitch_HorizonRelative;
            _pitchChart.SetData(ListToIndexTimeSeries(_pitchValue), "value");
            _pitchError[_currentIndex] = (float)sas.angularDelta.x;
            _pitchChart.SetData(ListToIndexTimeSeries(_pitchError), "error");
            _pitchCommand[_currentIndex] = (float)sas.sasResponse.x * -180f;
            _pitchChart.SetData(ListToIndexTimeSeries(_pitchCommand), "command");

            _rollValue[_currentIndex] = (float)vessel.Roll_HorizonRelative;
            _rollChart.SetData(ListToIndexTimeSeries(_rollValue), "value");
            _rollError[_currentIndex] = (float)sas.angularDelta.y;
            _rollChart.SetData(ListToIndexTimeSeries(_rollError), "error");
            _rollCommand[_currentIndex] = (float)sas.sasResponse.y * -180f;
            _rollChart.SetData(ListToIndexTimeSeries(_rollCommand), "command");

            _yawValue[_currentIndex] = (float)vessel.Yaw_HorizonRelative;
            _yawChart.SetData(ListToIndexTimeSeries(_yawValue), "value");
            _yawError[_currentIndex] = (float)sas.angularDelta.z;
            _yawChart.SetData(ListToIndexTimeSeries(_yawError), "error");
            _yawCommand[_currentIndex] = (float)sas.sasResponse.z * -180f;
            _yawChart.SetData(ListToIndexTimeSeries(_yawCommand), "command");

            _currentIndex = (_currentIndex + 1) % 100;
        }

        private LineChart SetupLineChart(string chartName)
        {
            var chart = RootElement.Q<LineChart>(chartName);
            chart.SetDataRangeX(0f, 99f);
            chart.SetDataRangeY(-180f, 180f);

            foreach (var tick in Ticks)
            {
                chart.AddGridLine(GridDirection.Horizontal, tick);
            }

            return chart;
        }

        private List<Vector2> ListToIndexTimeSeries(List<float> series)
        {
            List<Vector2> res = new();
            var idx = 0;

            for (var i = (_currentIndex + 1) % series.Count; i < series.Count; i++)
            {
                res.Add(new Vector2(idx++, series[i]));
            }

            for (var i = 0; i <= _currentIndex; i++)
            {
                res.Add(new Vector2(idx++, series[i]));
            }

            return res;
        }
    }
}
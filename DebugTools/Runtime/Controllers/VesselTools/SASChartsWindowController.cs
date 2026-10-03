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
        private LineChart _pitchChart;
        private readonly List<float> _pitchError = Enumerable.Repeat(0f, 100).ToList();
        
        private LineChart _rollChart;
        private readonly List<float> _rollError = Enumerable.Repeat(0f, 100).ToList();
        
        private LineChart _yawChart;
        private readonly List<float> _yawError = Enumerable.Repeat(0f, 100).ToList();
        
        private int _currentIndex;
        
        private VesselSAS _sas;

        private void OnEnable()
        {
            Enable();
            
            _pitchChart = RootElement.Q<LineChart>("pitch-chart");
            _pitchChart.SetDataRangeX(0f, 99f);
            _pitchChart.SetDataRangeY(-180f, 180f);
            
            _rollChart = RootElement.Q<LineChart>("roll-chart");
            _rollChart.SetDataRangeX(0f, 99f);
            _rollChart.SetDataRangeY(-180f, 180f);
            
            _yawChart = RootElement.Q<LineChart>("yaw-chart");
            _yawChart.SetDataRangeX(0f, 99f);
            _yawChart.SetDataRangeY(-180f, 180f);
        }

        public void SyncTo(VesselComponent vessel, VesselBehavior behavior)
        {
            _sas = behavior.Autopilot.SAS;

            _pitchError[_currentIndex] = (float)_sas.rotationDelta.x;
            _pitchChart.SetData(ListToIndexTimeSeries(_pitchError), "error");

            _rollError[_currentIndex] = (float)_sas.rotationDelta.y;
            _rollChart.SetData(ListToIndexTimeSeries(_rollError), "error");

            _yawError[_currentIndex] = (float)_sas.rotationDelta.z;
            _yawChart.SetData(ListToIndexTimeSeries(_yawError), "error");
            
            _currentIndex = (_currentIndex + 1) % 100;
        }

        private List<Vector2> ListToIndexTimeSeries(List<float> series)
        {
            List<Vector2> res = new();
            var idx = 0;

            for (var i = _currentIndex; i < series.Count; i++)
            {
                res.Add(new Vector2(idx++, series[i]));
            }

            for (var i = 0; i < _currentIndex; i++)
            {
                res.Add(new Vector2(idx++, series[i]));
            }

            return res;
        }
    }
}
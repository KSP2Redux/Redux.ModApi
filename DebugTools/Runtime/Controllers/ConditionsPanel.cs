using System;
using System.Collections.Generic;
using KSP.Game;
using KSP.Sim.impl;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace DebugTools.Runtime.Controllers
{
    /// <summary>
    /// Shared bench environment controls with state independent of the live vessel.
    /// </summary>
    public class ConditionsPanel
    {
        private readonly VisualElement _root;
        private readonly GameInstance _game;
        private readonly List<CelestialBodyComponent> _bodies;
        private readonly DropdownField _body;
        private readonly DropdownField _preset;
        private bool _setting;

        /// <summary>
        /// Gets the selected atmosphere body.
        /// </summary>
        public CelestialBodyComponent Body => _game.UniverseModel.GetAllCelestialBodies().Find(body => body.Name == _body.value)
            ?? throw new InvalidOperationException("The selected atmosphere body is unavailable in this campaign.");

        /// <summary>
        /// Bind one ConditionsPanel template instance.
        /// </summary>
        /// <param name="root">The cloned template root.</param>
        /// <param name="game">The game providing atmosphere curves.</param>
        public ConditionsPanel(VisualElement root, GameInstance game)
        {
            _root = root;
            _game = game;
            _bodies = game.UniverseModel.GetAllCelestialBodies();
            _body = root.Q<DropdownField>("condition-body");
            _body.choices = _bodies.ConvertAll(body => body.Name);
            _body.index = Math.Max(0, _bodies.FindIndex(body => body.Name == "Kerbin"));
            _preset = root.Q<DropdownField>("condition-preset");
            _preset.choices = new List<string> { "Vacuum", "Kerbin SL (1 atm)", "Eve SL (4 atm)", "Kerbin 10 km at Mach 1", "Custom" };
            _preset.index = 1;
            _preset.RegisterValueChangedCallback(_ => ApplyPreset());
            _body.RegisterValueChangedCallback(_ => DeriveAtmosphere());
            Field("altitude").RegisterValueChangedCallback(_ => DeriveAtmosphere());
            foreach (string name in new[] { "temperature", "mach", "aoa", "sideslip" })
                Field(name).RegisterValueChangedCallback(_ => MarkCustom());
            root.Q<Slider>("condition-pressure").RegisterValueChangedCallback(_ => MarkCustom());
            ApplyPreset();
        }

        private FloatField Field(string name) => _root.Q<FloatField>($"condition-{name}");

        private void MarkCustom()
        {
            if (!_setting)
                _preset.SetValueWithoutNotify("Custom");
            float log = _root.Q<Slider>("condition-pressure").value;
            double pressure = log <= -3.99f ? 0 : Math.Pow(10, log) * 101.325;
            double density = Body.GetDensity(pressure, Field("temperature").value);
            double sound = AeroUtilities.GetSoundSpeed(Body.atmosphereAdiabaticIndex, pressure, density);
            _root.Q<Label>("condition-readout").text = FormattableString.Invariant($"{pressure:F4} kPa | {density:F6} kg/m3 | Mach {Field("mach").value:F3} = {sound * Field("mach").value:F2} m/s");
        }

        private void DeriveAtmosphere()
        {
            double altitude = Field("altitude").value;
            double pressure = Body.GetPressure(altitude);
            _root.Q<Slider>("condition-pressure").SetValueWithoutNotify((float)Math.Log10(Math.Max(0.0001, pressure / 101.325)));
            Field("temperature").SetValueWithoutNotify((float)Body.GetTemperature(altitude));
            MarkCustom();
        }

        private void ApplyPreset()
        {
            if (_preset.index == 4)
                return;
            _setting = true;
            try
            {
                string name = _preset.index == 2 ? "Eve" : "Kerbin";
                int index = _bodies.FindIndex(body => body.Name == name);
                if (index < 0)
                    throw new InvalidOperationException($"Body {name} is unavailable.");
                _body.SetValueWithoutNotify(_body.choices[index]);
                Field("altitude").SetValueWithoutNotify(_preset.index == 3 ? 10000 : 0);
                Field("mach").SetValueWithoutNotify(_preset.index == 3 ? 1 : 0.3f);
                Field("aoa").SetValueWithoutNotify(0);
                Field("sideslip").SetValueWithoutNotify(0);
                DeriveAtmosphere();
                if (_preset.index == 0)
                    _root.Q<Slider>("condition-pressure").SetValueWithoutNotify(-4);
                if (_preset.index == 1 || _preset.index == 2)
                    _root.Q<Slider>("condition-pressure").SetValueWithoutNotify(_preset.index == 1 ? 0 : Mathf.Log10(4));
                MarkCustom();
            }
            finally
            {
                _setting = false;
            }
        }

        /// <summary>
        /// Read validated conditions without changing the simulation.
        /// </summary>
        /// <returns>The body, altitude, pressure, temperature, density, Mach and angles with SI units.</returns>
        public JObject Read()
        {
            float log = _root.Q<Slider>("condition-pressure").value;
            double pressure = log <= -3.99f ? 0 : Math.Pow(10, log) * 101.325;
            var result = new JObject { ["body"] = Body.Name, ["pressureKPa"] = pressure };
            foreach (string name in new[] { "altitude", "temperature", "mach", "aoa", "sideslip" })
            {
                float value = Field(name).value;
                if (!float.IsFinite(value))
                    throw new ArgumentException($"{name} must be finite.");
                result[name] = value;
            }
            if (result.Value<double>("temperature") <= 0 || result.Value<double>("mach") < 0 ||
                Math.Abs(result.Value<double>("aoa")) > 85 || Math.Abs(result.Value<double>("sideslip")) > 85)
                throw new ArgumentException("Needs positive temperature, nonnegative Mach and angles within 85 degrees.");
            result["density"] = Body.GetDensity(pressure, result.Value<double>("temperature"));
            return result;
        }
    }
}

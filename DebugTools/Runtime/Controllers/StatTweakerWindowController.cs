using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using KSP.Game;
using KSP.IO;
using KSP.Logging;
using KSP.Modules;
using KSP.OAB;
using KSP.Sim;
using KSP.Sim.Definitions;
using KSP.Sim.impl;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Redux.QA;
using UnityEngine;
using UnityEngine.UIElements;

namespace DebugTools.Runtime.Controllers
{
    /// <summary>
    /// Edit placed VAB part definitions and module values for a reversible design session.
    /// </summary>
    public class StatTweakerWindowController : BaseWindowController
    {
        private readonly List<ObjectAssemblyPart> _parts = new();
        private readonly List<string> _deletedIds = new();
        private readonly Dictionary<ObjectAssemblyPart, Action> _definitions = new();
        private readonly Dictionary<string, (ObjectAssemblyPart part, string field, JToken oldValue, JToken newValue,
            Action<JToken> write, string lua, string module, string data)> _deltas = new();
        private readonly JsonSerializer _serializer = IOProvider.GetDefaultSerializer();
        private DropdownField _subject;
        private ObjectAssemblyBuilder _builder;
        private Label _status;

        private void OnEnable()
        {
            _subject = RootElement.Q<DropdownField>("subject");
            _status = RootElement.Q<Label>("status");
            RootElement.Q<Button>("refresh").clicked += Refresh;
            RootElement.Q<Button>("selected").clicked += UseSelected;
            RootElement.Q<Button>("stop").clicked += () => { Stop(); ShowFields(); };
            RootElement.Q<Button>("lua").clicked += () => Export(true);
            RootElement.Q<Button>("json").clicked += () => Export(false);
            _subject.RegisterValueChangedCallback(_ => ShowFields());
        }

        /// <inheritdoc />
        protected override void OnRequestedOpenChanged(bool isOpen)
        {
            if (isOpen)
            {
                _builder = Game.OAB.Current;
                Refresh();
            }
            else
            {
                Stop();
                _builder = null;
            }
        }

        private void OnDisable() => Stop();
        private void OnDestroy() => Stop();

        private void Update()
        {
            if (!IsRequestedOpen)
                return;
            if (Game.GlobalGameState.GetState() != GameState.VehicleAssemblyBuilder || _builder == null || _builder != Game.OAB.Current)
            {
                Stop();
                _builder = null;
                return;
            }
            // Remove closures pointing at a deleted part while the window remains open.
            if (_deltas.Count == 0)
                return;
            _deletedIds.Clear();
            foreach (var entry in _deltas)
            {
                if (entry.Value.part == null)
                    _deletedIds.Add(entry.Key);
            }
            foreach (string id in _deletedIds)
            {
                _deltas.Remove(id);
                DebugOverrides.Clear(id);
            }
        }

        private ObjectAssemblyPart Subject => _subject.index >= 0 && _subject.index < _parts.Count ? _parts[_subject.index] : null;

        private void Refresh()
        {
            if (Game.GlobalGameState.GetState() == GameState.VehicleAssemblyBuilder)
                _builder = Game.OAB.Current;
            _parts.Clear();
            var choices = new List<string>();
            if (_builder != null)
            {
                foreach (IObjectAssemblyPart candidate in _builder.ActivePartTracker.PartsMasterList)
                {
                    if (candidate is not ObjectAssemblyPart part || part == null || !part.IsFinalized)
                        continue;
                    _parts.Add(part);
                    choices.Add($"{part.GetDisplayName()} | {part.GlobalId}");
                }
            }
            _subject.choices = choices;
            _subject.index = choices.Count > 0 ? 0 : -1;
            ShowFields();
        }

        private void UseSelected()
        {
            IObjectAssemblyPart selected = _builder?.ActivePartTracker.PartBeingManipulated ?? _builder?.ActivePartTracker.partGrabbed;
            Refresh();
            _subject.index = selected is ObjectAssemblyPart part ? _parts.IndexOf(part) : -1;
            ShowFields();
        }

        private Dictionary<string, (Func<JToken> read, Action<JToken> write, string lua, string module, string data)> Fields(ObjectAssemblyPart part)
        {
            var fields = new Dictionary<string, (Func<JToken>, Action<JToken>, string, string, string)>();
            if (part == null)
                return fields;
            foreach (FieldInfo field in typeof(PartData).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!IsNumber(field.FieldType))
                    continue;
                fields.Add("Part / " + field.Name, (() => JToken.FromObject(field.GetValue(part.AvailablePart.PartData)),
                    value => field.SetValue(part.AvailablePart.PartData, value.ToObject(field.FieldType)),
                    "part[" + Quote(field.Name) + "]", "", ""));
            }
            foreach (IPartModule module in part.Modules.ValuesList)
            {
                if (module is not PartBehaviourModule behaviour)
                    continue;
                foreach (ModuleData data in behaviour.DataModules.ValuesList)
                {
                    string moduleName = behaviour.PartComponentModuleType.Name.Replace("PartComponent", "");
                    Walk(() => data, _ => { }, data.GetType(), data.GetType().Name, "data", moduleName, data.GetType().Name, fields, 0);
                }
            }
            if (part.Container == null)
                return fields;
            foreach (var resourceId in part.Container)
            {
                string resourceName = Game.ResourceDefinitionDatabase.GetResourceNameFromID(resourceId);
                double stored = part.Container.GetResourceStoredUnits(resourceId);
                fields.Add("Resources / " + resourceName + " capacity", (() => new JValue(part.Container.GetResourceCapacityUnits(resourceId)),
                    value =>
                    {
                        double capacity = value.Value<double>();
                        part.Container.SetResourceCapacity(resourceId, capacity);
                        part.Container.SetResourceStoredUnits(resourceId, Math.Min(stored, capacity));
                    }, "resource:" + resourceName, "", ""));
            }
            return fields;
        }

        private void Walk(Func<object> read, Action<object> write, Type type, string path, string lua, string module, string data,
            Dictionary<string, (Func<JToken> read, Action<JToken> write, string lua, string module, string data)> fields, int depth)
        {
            if (depth > 8 || read() == null)
                return;
            if (IsNumber(type) || type == typeof(FloatCurve))
            {
                fields[path] = (() => JToken.FromObject(read(), _serializer), value =>
                {
                    if (IsNumber(type) && value.Type is not (JTokenType.Float or JTokenType.Integer))
                        throw new ArgumentException("Enter a number using dot decimals.");
                    object converted = value.ToObject(type, _serializer);
                    if ((converted is float single && (float.IsNaN(single) || float.IsInfinity(single))) ||
                        (converted is double number && (double.IsNaN(number) || double.IsInfinity(number))))
                        throw new ArgumentException("The value is outside the field's finite range.");
                    write(converted);
                }, lua, module, data);
                return;
            }
            if (type.IsEnum || type == typeof(string) || typeof(UnityEngine.Object).IsAssignableFrom(type))
                return;
            if (read() is IList list)
            {
                for (int index = 0; index < list.Count; index++)
                {
                    int slot = index;
                    if (list[slot] == null)
                        continue;
                    Walk(() => ((IList)read())[slot], value => ((IList)read())[slot] = value, list[slot].GetType(),
                        path + " / " + slot.ToString(CultureInfo.InvariantCulture), lua + "[" + (slot + 1).ToString(CultureInfo.InvariantCulture) + "]", module, data, fields, depth + 1);
                }
                return;
            }
            if (_serializer.ContractResolver.ResolveContract(type) is not JsonObjectContract contract)
                return;
            foreach (JsonProperty property in contract.Properties)
            {
                if (property.Ignored || !property.Readable || !property.Writable)
                    continue;
                MemberInfo member = type.GetMember(property.UnderlyingName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Length > 0
                    ? type.GetMember(property.UnderlyingName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)[0] : null;
                if (depth == 0 && (member == null || !Attribute.IsDefined(member, typeof(KSPDefinitionAttribute))))
                    continue;
                Walk(() => property.ValueProvider.GetValue(read()), value =>
                {
                    object owner = read();
                    property.ValueProvider.SetValue(owner, value);
                    if (type.IsValueType)
                        write(owner);
                }, property.PropertyType, path + " / " + property.PropertyName, lua + "[" + Quote(property.PropertyName) + "]", module, data, fields, depth + 1);
            }
        }

        private static bool IsNumber(Type type) => type == typeof(float) || type == typeof(double) || type == typeof(int) || type == typeof(long);
        private static string Quote(string value) => JsonConvert.SerializeObject(value);
        private static string Display(JToken value) => value.Type is JTokenType.Float or JTokenType.Integer
            ? value.Value<double>().ToString("0.################", CultureInfo.InvariantCulture) : value.ToString(Formatting.None);

        private static void ValidateEdit(JToken before, JToken after)
        {
            if (before is JObject original && after is JObject replacement)
            {
                if (original.Count != replacement.Count)
                    throw new ArgumentException("Keep the curve's existing keys and structure. Edit numeric values only.");
                foreach (JProperty property in original.Properties())
                {
                    if (!replacement.TryGetValue(property.Name, out JToken value))
                        throw new ArgumentException("A curve field is missing: " + property.Name);
                    ValidateEdit(property.Value, value);
                }
                return;
            }
            if (before is JArray originalArray && after is JArray replacementArray)
            {
                if (originalArray.Count != replacementArray.Count)
                    throw new ArgumentException("Keep the existing curve key count.");
                for (int index = 0; index < originalArray.Count; index++)
                {
                    ValidateEdit(originalArray[index], replacementArray[index]);
                }
                return;
            }
            if (before.Type is JTokenType.Float or JTokenType.Integer)
            {
                if (after.Type is not (JTokenType.Float or JTokenType.Integer) ||
                    double.IsNaN(after.Value<double>()) || double.IsInfinity(after.Value<double>()))
                    throw new ArgumentException("Enter finite numbers using dot decimals.");
                return;
            }
            if (!JToken.DeepEquals(before, after))
                throw new ArgumentException("Only numeric values can be edited.");
        }

        private static string Friendly(string key)
        {
            string label = Regex.Replace(key.Replace("_", " "), "([a-z])([A-Z])", "$1 $2");
            label = Regex.Replace(label, @"\b(Data|Module) ", "");
            string lower = key.ToLowerInvariant();
            string unit = lower.Contains("atmospherecurve") ? "Isp s, pressure atm" : lower.EndsWith("torque") ? "kN m" : lower.EndsWith("thrust") ? "kN"
                : lower.EndsWith("mass") ? "t" : lower.Contains("temp") ? "K" : lower.Contains("crashtolerance") ? "m/s"
                : lower.Contains("capacity") ? "resource units" : lower.EndsWith("cost") ? "funds" : "config units";
            return label + " (" + unit + ")";
        }

        private void ShowFields()
        {
            VisualElement content = RootElement.Q<VisualElement>("fields");
            content.Clear();
            foreach (var entry in Fields(Subject))
            {
                int slash = entry.Key.IndexOf(" / ", StringComparison.Ordinal);
                string groupName = slash < 0 ? "Part" : entry.Key.Substring(0, slash);
                Foldout group = content.Q<Foldout>(groupName);
                if (group == null)
                {
                    group = new Foldout { name = groupName, text = Friendly(groupName), value = false };
                    content.Add(group);
                }
                var row = new VisualElement();
                row.AddToClassList("stat-row");
                var input = new TextField(Friendly(entry.Key.Substring(Math.Max(0, slash + 3)))) { value = Display(entry.Value.read()) };
                input.AddToClassList("kui-field");
                input.tooltip = entry.Key + ". Curves use the serialized numeric key object. Isp values are seconds, pressure keys are atm.";
                var apply = new Button(() => Apply(entry.Key, input.value)) { text = "Apply" };
                apply.AddToClassList("kui-button");
                row.Add(input);
                row.Add(apply);
                group.Add(row);
            }
            ShowDeltas();
        }

        private void Apply(string field, string text)
        {
            try
            {
                if (_builder == null || Game.GlobalGameState.GetState() != GameState.VehicleAssemblyBuilder || Subject == null)
                    throw new InvalidOperationException("Pick a current placed VAB part.");
                JToken value = JToken.Parse(text);
                foreach (JToken token in value is JContainer container ? container.DescendantsAndSelf() : new[] { value })
                {
                    if (token.Type is not (JTokenType.Float or JTokenType.Integer))
                        continue;
                    if (double.IsNaN(token.Value<double>()) || double.IsInfinity(token.Value<double>()))
                        throw new ArgumentException("Values must be finite.");
                }
                var targets = new List<ObjectAssemblyPart> { Subject };
                if (RootElement.Q<Toggle>("symmetry").value && Subject.SymmetrySet != null)
                {
                    foreach (IObjectAssemblyPart candidate in Subject.SymmetrySet.Parts)
                    {
                        if (candidate is ObjectAssemblyPart part && part != null && !targets.Contains(part))
                            targets.Add(part);
                    }
                }
                // Validate shape and shared constraints before changing any target.
                foreach (ObjectAssemblyPart part in targets)
                {
                    var fields = Fields(part);
                    if (!fields.ContainsKey(field))
                        throw new InvalidOperationException("A symmetry counterpart does not have the same field.");
                    ValidateEdit(fields[field].read(), value);
                    if (fields[field].read().Type == JTokenType.Integer && value.Value<double>() != Math.Truncate(value.Value<double>()))
                        throw new ArgumentException("This field needs a whole number.");
                    if ((field == "Part / mass" || field == "Part / maxTemp") && value.Value<double>() <= 0)
                        throw new ArgumentException("Mass and maximum temperature must be greater than zero.");
                    if (field.EndsWith(" / maxThrust", StringComparison.Ordinal) && value.Value<double>() <= 0)
                        throw new ArgumentException("Maximum thrust must be greater than zero.");
                    if (field.EndsWith(" / atmosphereCurve", StringComparison.Ordinal) && value.ToObject<FloatCurve>(_serializer).Evaluate(0f) <= 0)
                        throw new ArgumentException("Vacuum Isp must be greater than zero.");
                    if (fields[field].lua.StartsWith("resource:", StringComparison.Ordinal) && value.Value<double>() < 0)
                        throw new ArgumentException("Resource capacity cannot be negative.");
                }
                var applied = new List<(Action<JToken> write, JToken value)>();
                var previousDeltas = new Dictionary<string, (ObjectAssemblyPart part, string field, JToken oldValue, JToken newValue,
                    Action<JToken> write, string lua, string module, string data)>(_deltas);
                var changed = new List<string>();
                try
                {
                    foreach (ObjectAssemblyPart part in targets)
                    {
                        if (!_definitions.ContainsKey(part))
                            _definitions.Add(part, part.UseQaSessionDefinition());
                        var binding = Fields(part)[field];
                        JToken old = binding.read().DeepClone();
                        applied.Add((binding.write, old));
                        binding.write(value);
                        JToken current = binding.read().DeepClone();
                        string id = "vab:stats:" + part.GlobalId + ":" + field;
                        JToken original = _deltas.TryGetValue(id, out var previous) ? previous.oldValue : old;
                        _deltas[id] = (part, field, original, current, previousDeltas.ContainsKey(id) ? previous.write : binding.write, binding.lua, binding.module, binding.data);
                        changed.Add(id);
                    }
                }
                catch
                {
                    for (int index = applied.Count - 1; index >= 0; index--)
                        applied[index].write(applied[index].value);
                    _deltas.Clear();
                    foreach (var previous in previousDeltas)
                    {
                        _deltas.Add(previous.Key, previous.Value);
                    }
                    foreach (ObjectAssemblyPart part in targets)
                    {
                        bool hasEdits = false;
                        foreach (var delta in _deltas.Values)
                        {
                            if (delta.part == part)
                                hasEdits = true;
                        }
                        if (!hasEdits && _definitions.TryGetValue(part, out Action restore))
                        {
                            restore();
                            _definitions.Remove(part);
                        }
                        Notify(part);
                    }
                    throw;
                }
                foreach (string id in changed)
                {
                    var delta = _deltas[id];
                    if (JToken.DeepEquals(delta.oldValue, delta.newValue))
                    {
                        Revert(id);
                        continue;
                    }
                    DebugOverrides.Report(id, $"VAB stats: {delta.part.PartName} / {Friendly(field)} (restored before save or launch)", () => Revert(id));
                    Notify(delta.part);
                }
                ShowFields();
                _status.text = "Applied to placed instances. Stop or close restores values. Save and launch restore values before proceeding.";
            }
            catch (Exception exception)
            {
                _status.text = exception.Message;
            }
        }

        private void Notify(ObjectAssemblyPart part)
        {
            if (part == null)
                return;
            foreach (IPartModule module in part.Modules.ValuesList)
            {
                if (module is not PartBehaviourModule behaviour)
                    continue;
                foreach (ModuleData data in behaviour.DataModules.ValuesList)
                {
                    if (data is Data_Engine engine && engine.currentEngineModeData != null)
                        engine.SetupMixtures(part.GlobalId, part.ResourceFlowRequestBroker, part.Container);
                }
            }
            part.UpdateMassValues();
            if (part.Assembly is ObjectAssembly assembly)
            {
                assembly.ResourceFlowRequestManager?.MarkDirty();
                assembly.VesselDeltaV?.UpdateModuleEngines();
                assembly.VesselDeltaV?.OnPartMassChanged();
                assembly.VesselDeltaV?.OnFuelTankSettingsChanged();
                assembly.VesselDeltaV?.SetCalcsDirty();
            }
            _builder?.eventsBuilder.RequestInternalRefresh();
        }

        private void Revert(string id)
        {
            if (!_deltas.ContainsKey(id))
                return;
            try
            {
                RevertCore(id);
            }
            catch (Exception exception)
            {
                _deltas.Remove(id);
                GlobalLog.Warn(LogFilter.Gameplay, $"Could not revert VAB stat {id}: {exception.Message}");
            }
            finally
            {
                DebugOverrides.Clear(id);
            }
        }

        private void RevertCore(string id)
        {
            if (!_deltas.TryGetValue(id, out var delta))
                return;
            _deltas.Remove(id);
            try
            {
                if (delta.part != null)
                    delta.write(delta.oldValue);
            }
            finally
            {
                bool hasOtherEdits = false;
                foreach (var other in _deltas.Values)
                {
                    if (other.part == delta.part)
                        hasOtherEdits = true;
                }
                if (!hasOtherEdits && _definitions.TryGetValue(delta.part, out Action restore))
                {
                    _definitions.Remove(delta.part);
                    restore();
                }
            }
            Notify(delta.part);
            ShowDeltas();
        }

        private void Stop()
        {
            foreach (string id in new List<string>(_deltas.Keys))
            {
                Revert(id);
            }
            foreach (var definition in _definitions)
            {
                definition.Value();
                Notify(definition.Key);
            }
            _definitions.Clear();
            if (RootElement != null)
                ShowDeltas();
        }

        private void ShowDeltas()
        {
            var text = new StringBuilder();
            foreach (var delta in _deltas.Values)
            {
                text.AppendLine($"{delta.part.PartName} | {delta.part.GlobalId} | {Friendly(delta.field)}: {Display(delta.oldValue)} -> {Display(delta.newValue)}");
            }
            RootElement.Q<Label>("deltas").text = text.Length == 0 ? "No active stat tweaks." : text.ToString();
        }

        private static string Lua(JToken token)
        {
            if (token is JObject obj)
            {
                var members = new List<string>();
                foreach (JProperty property in obj.Properties())
                {
                    members.Add("[" + Quote(property.Name) + "]=" + Lua(property.Value));
                }
                return "{" + string.Join(",", members) + "}";
            }
            if (token is JArray array)
            {
                var items = new List<string>();
                foreach (JToken item in array)
                {
                    items.Add(Lua(item));
                }
                return items.Count == 0 ? "J.Empty()" : "{" + string.Join(",", items) + "}";
            }
            return token.Type == JTokenType.Null ? "nil" : token.ToString(Formatting.None);
        }

        private void Export(bool isLua)
        {
            try
            {
                if (_deltas.Count == 0)
                    throw new InvalidOperationException("Apply a stat edit before exporting.");
                var json = new JArray();
                var lua = new StringBuilder("PM:DefineFeature(\"QaStatTweaks\", { requires = { \"Ksp2Redux:Parts\" } })\n");
                var definitionValues = new Dictionary<string, JToken>();
                foreach (var delta in _deltas.Values)
                {
                    string key = delta.part.PartName + "/" + delta.field;
                    if (definitionValues.TryGetValue(key, out JToken previous) && !JToken.DeepEquals(previous, delta.newValue))
                        throw new InvalidOperationException("Different instances have conflicting values for " + Friendly(key) + ". Make them agree or revert one before exporting a definition patch.");
                    definitionValues[key] = delta.newValue;
                }
                int index = 0;
                foreach (var delta in _deltas.Values)
                {
                    json.Add(new JObject { ["partName"] = delta.part.PartName, ["partGuid"] = delta.part.GlobalId.ToString(),
                        ["module"] = delta.module, ["data"] = delta.data, ["field"] = delta.field, ["expression"] = delta.lua,
                        ["old"] = delta.oldValue.DeepClone(), ["new"] = delta.newValue.DeepClone() });
                    // Identical edits from symmetry parts are harmless repeated assignments.
                    lua.AppendLine($"PM.Parts:Patch(\"QaStatTweaks{(++index).ToString(CultureInfo.InvariantCulture)}\"):Named({Quote(delta.part.PartName)}):Early():First():Feature(\"QaStatTweaks\"):Do(function(part)");
                    if (delta.lua.StartsWith("resource:", StringComparison.Ordinal))
                    {
                        string name = delta.lua.Substring(9);
                        lua.AppendLine($"  if part.resourceContainers:Has({Quote(name)}) then");
                        lua.AppendLine($"    part.resourceContainers[{Quote(name)}].capacityUnits = {Lua(delta.newValue)}");
                        lua.AppendLine("  else");
                        lua.AppendLine($"    part.resourceContainers:Add({Quote(name)}, {Lua(delta.newValue)})");
                        lua.AppendLine("  end");
                    }
                    else if (delta.module.Length > 0)
                    {
                        lua.AppendLine($"  part:PatchModule({Quote(delta.module)}, function(m) m:PatchData({Quote(delta.data)}, function(data)");
                        lua.AppendLine($"    {delta.lua} = {Lua(delta.newValue)}");
                        lua.AppendLine("  end) end)");
                    }
                    else
                    {
                        lua.AppendLine($"  {delta.lua} = {Lua(delta.newValue)}");
                    }
                    lua.AppendLine("end)");
                }
                string snippet = lua.ToString();
                string fieldList = json.ToString(Formatting.Indented);
                var report = new QaReport("VAB stat tweaks", "bench-run");
                var result = new QaReportResult("vab-stat-tweaks", "VAB stat tweaks", "Session delta export", "snapshot", QaVerdict.NeedsReview,
                    "Export affects definitions, hence all future instances. Resolve differing edits to the same part field before use. Runtime acceptance pending.");
                result.Evidence.Add(new JObject { ["type"] = "log", ["caption"] = "Patch Manager Lua", ["text"] = snippet });
                result.Evidence.Add(new JObject { ["type"] = "log", ["caption"] = "Prefab field list", ["text"] = fieldList });
                report.Add(result);
                GUIUtility.systemCopyBuffer = isLua ? snippet : fieldList;
                _status.text = "Copied " + (isLua ? "Lua" : "JSON") + ". Both formats captured: " + report.Write();
            }
            catch (Exception exception)
            {
                _status.text = exception.Message;
            }
        }
    }
}

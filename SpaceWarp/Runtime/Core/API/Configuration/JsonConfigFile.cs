#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.UnityConverters.Configuration;
using UnityEngine;

namespace SpaceWarp2.API.Configuration
{
    /// <summary>
    /// A config file that uses JSON to store its data.
    /// </summary>
    [PublicAPI]
    public class JsonConfigFile : IConfigFile
    {
        [CanBeNull] private JObject _previousConfigObject;

        /// <inheritdoc />
        public ConfigSectionList Sections { get; } = new();

        private readonly string? _file;
        private bool _loading;

        /// <summary>
        /// Creates a new JSON config file object.
        /// </summary>
        /// <param name="file">The file path to use.</param>
        public JsonConfigFile(string file)
        {
            _file = file;
            if (File.Exists(file))
            {
                try
                {
                    _previousConfigObject = JObject.Parse(File.ReadAllText(file));
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error in attempting to load previous config file at '{file}': {e}");
                    QuarantineCorruptedFile(file);
                    _previousConfigObject = null;
                }
            }
        }

        /// <summary>
        /// Creates a config kept in memory rather than in a file, such as one stored in a save. Its owner stores it
        /// through <see cref="ToJObject" /> and restores it through <see cref="Load" />.
        /// </summary>
        /// <returns>The config.</returns>
        public static JsonConfigFile InMemory() => new();

        private JsonConfigFile()
        {
        }

        /// <summary>
        /// Replaces every value with the given ones, keeping the bound entries so whatever shows or reads them follows.
        /// Entries the values leave out return to their defaults.
        /// </summary>
        /// <param name="values">The values by section and key, or null to return every entry to its default.</param>
        public void Load(JObject? values)
        {
            _previousConfigObject = (JObject?)values?.DeepClone();
            _loading = true;
            try
            {
                foreach (JsonConfigSection section in Sections.OfType<JsonConfigSection>())
                {
                    section.Load(_previousConfigObject?[section.Name] as JObject);
                }
            }
            finally
            {
                _loading = false;
            }

            Save();
        }

        /// <summary>
        /// Gets every value by section and key, including those of entries nothing has bound.
        /// </summary>
        /// <returns>The values.</returns>
        public JObject ToJObject()
        {
            string? json = BuildJson();
            if (json != null)
                return JObject.Parse(json);

            return (JObject?)_previousConfigObject?.DeepClone() ?? new JObject();
        }

        private static void QuarantineCorruptedFile(string file)
        {
            try
            {
                string backupFile = file + ".bak";
                if (File.Exists(backupFile))
                {
                    File.Delete(backupFile);
                }
                File.Move(file, backupFile);
                Debug.LogWarning($"Corrupted config file at '{file}' was backed up to '{backupFile}' and will be regenerated.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to backup corrupted config file at '{file}': {ex}");
            }
        }

        /// <inheritdoc />
        public IConfigSection GetOrCreateSection(string name, string? localizationKey)
        {
            if (Sections.TryGet(name, out var existing))
            {
                return existing!;
            }

            JObject? slice = null;
            if (_previousConfigObject != null
                && _previousConfigObject.TryGetValue(name, out var token)
                && token is JObject obj)
            {
                slice = obj;
            }

            var section = new JsonConfigSection(this, name, slice, localizationKey);
            Sections.Add(section);
            return section;
        }

        /// <inheritdoc />
        public void Save()
        {
            if (_loading || _file == null)
                return;

            string? finalJson = BuildJson();
            if (finalJson == null)
                return;

            try
            {
                JObject.Parse(finalJson);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Generated config JSON for '{_file}' is invalid. Aborting save to avoid corrupting file: {ex}");
                return;
            }

            try
            {
                var directory = Path.GetDirectoryName(_file);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string tempFile = _file + ".tmp";
                File.WriteAllText(tempFile, finalJson);
                if (File.Exists(_file))
                {
                    File.Delete(_file);
                }
                File.Move(tempFile, _file);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error writing config file at '{_file}': {ex}");
            }
        }

        // Writes the bound entries with their comments, and the sections and keys nothing has bound as they were.
        // Returns null when nothing is bound.
        private string? BuildJson()
        {
            var nonEmptySections = Sections
                .OfType<JsonConfigSection>()
                .Where(s => s.Entries.Count > 0)
                .ToList();
            if (nonEmptySections.Count == 0) return null;

            var boundSections = nonEmptySections.ToDictionary(section => section.Name);
            var writtenSections = new HashSet<string>();
            var result = new StringBuilder();
            result.AppendLine("{");
            var hadPreviousSection = false;
            if (_previousConfigObject != null)
            {
                foreach (JProperty property in _previousConfigObject.Properties())
                {
                    if (property.Value is not JObject previousSection)
                        continue;

                    if (hadPreviousSection)
                    {
                        result.AppendLine(",");
                    }

                    if (boundSections.TryGetValue(property.Name, out JsonConfigSection section))
                    {
                        section.WriteTo(result);
                    }
                    else
                    {
                        DumpPreviousSection(result, property.Name, previousSection);
                    }

                    writtenSections.Add(property.Name);
                    hadPreviousSection = true;
                }
            }

            foreach (JsonConfigSection section in nonEmptySections)
            {
                if (!writtenSections.Add(section.Name))
                    continue;

                if (hadPreviousSection)
                {
                    result.AppendLine(",");
                }

                section.WriteTo(result);
                hadPreviousSection = true;
            }
            result.AppendLine("\n}");

            return result.ToString();
        }

        private static List<JsonConverter>? _defaultConverters;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _defaultConverters = null;
        }

        private static List<JsonConverter>? CreateDefaultConverters()
        {
            var method = Assembly.Load("Newtonsoft.Json.UnityConverters")
                .GetType("Newtonsoft.Json.UnityConverters.UnityConverterInitializer")
                .GetMethod("CreateConverters", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
            var parameters = new object[] { ScriptableObject.CreateInstance<UnityConvertersConfig>() };
            return (List<JsonConverter>)method.Invoke(null, parameters);
        }

        /// <summary>
        /// The default converters to use when serializing/deserializing JSON.
        /// </summary>
        public static List<JsonConverter>? DefaultConverters
        {
            get
            {
                if (_defaultConverters != null) return _defaultConverters;
                _defaultConverters = (CreateDefaultConverters() ?? new List<JsonConverter>())
                    .Where(x => !JsonConverterFilter.ShouldFilterConverter(x))
                    .ToList();
                _defaultConverters.Add(new StringEnumConverter());

                return _defaultConverters;
            }
        }

        internal static bool DumpEntry(
            StringBuilder result,
            bool hadPreviousKey,
            KeyValuePair<string, JsonConfigEntry> entry
        )
        {
            if (hadPreviousKey)
            {
                result.AppendLine(",");
            }

            // result.AppendLine($"        // {entry.Value.Description}");
            if (entry.Value.Description != "")
            {
                var descriptionLines = entry.Value.Description.Split('\n').Select(x => x.TrimEnd());
                foreach (var line in descriptionLines)
                {
                    result.AppendLine($"        // {line}");
                }
            }

            if (entry.Value.Constraint is IValueConstraint constraint)
            {
                var constraintLines = constraint.ConstraintDescription.Split('\n').Select(x => x.TrimEnd());
                foreach (var line in constraintLines)
                {
                    result.AppendLine($"        // {line}");
                }
            }

            result.AppendLine($"        // Default: {entry.Value.Default}");

            string serialized;
            try
            {
                var converters = DefaultConverters?.ToArray() ?? Array.Empty<JsonConverter>();
                serialized = JsonConvert.SerializeObject(entry.Value.Value, Formatting.Indented, converters);
                if (string.IsNullOrWhiteSpace(serialized))
                {
                    serialized = JsonConvert.SerializeObject(entry.Value.Value, Formatting.Indented, new StringEnumConverter());
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to serialize config entry '{entry.Key}': {ex}");
                try
                {
                    serialized = JsonConvert.SerializeObject(entry.Value.Value, Formatting.Indented, new StringEnumConverter());
                }
                catch
                {
                    serialized = JsonConvert.ToString(entry.Value.Value?.ToString() ?? "null");
                }
            }

            var serializedLines = serialized.Split('\n').Select(x => x.TrimEnd()).ToArray();
            if (serializedLines.Length > 1)
            {
                result.AppendLine($"        \"{EscapeKey(entry.Key)}\": ");
                for (var i = 0; i < serializedLines.Length; i++)
                {
                    if (i != serializedLines.Length - 1)
                    {
                        result.AppendLine($"        {serializedLines[i]}");
                    }
                    else
                    {
                        result.Append($"        {serializedLines[i]}");
                    }
                }
            }
            else
            {
                result.Append($"        \"{EscapeKey(entry.Key)}\": {serializedLines[0]}");
            }

            return true;
        }

        internal static bool DumpPreviousEntry(
            StringBuilder result,
            bool hadPreviousKey,
            JProperty property
        )
        {
            if (hadPreviousKey)
            {
                result.AppendLine(",");
            }

            DumpProperty(result, property.Name, property.Value);
            return true;
        }

        private static void DumpPreviousSection(StringBuilder result, string name, JObject section)
        {
            result.AppendLine($"    \"{EscapeKey(name)}\": {{");
            bool hadPreviousKey = false;
            foreach (JProperty property in section.Properties())
            {
                hadPreviousKey = DumpPreviousEntry(result, hadPreviousKey, property);
            }

            result.Append("\n    }");
        }

        private static void DumpProperty(StringBuilder result, string name, JToken value)
        {
            string serialized = value.ToString(Formatting.Indented);
            string[] serializedLines = serialized.Split('\n').Select(x => x.TrimEnd()).ToArray();
            if (serializedLines.Length > 1)
            {
                result.AppendLine($"        \"{EscapeKey(name)}\": ");
                for (int i = 0; i < serializedLines.Length; i++)
                {
                    if (i != serializedLines.Length - 1)
                    {
                        result.AppendLine($"        {serializedLines[i]}");
                    }
                    else
                    {
                        result.Append($"        {serializedLines[i]}");
                    }
                }
            }
            else
            {
                result.Append($"        \"{EscapeKey(name)}\": {serializedLines[0]}");
            }
        }

        private static string EscapeKey(string key) => key.Replace("\"", "\\\"").Replace("\n", "\\n");
    }
}

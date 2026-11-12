#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace SpaceWarp2.API.Configuration;

/// <summary>
/// A section within a <see cref="JsonConfigFile" />.
/// </summary>
[PublicAPI]
public class JsonConfigSection : IConfigSection
{
    private readonly JsonConfigFile _file;
    private JObject? _previousSlice;

    internal readonly Dictionary<string, JsonConfigEntry> Entries = new();

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string? LocalizationKey { get; }

    internal JsonConfigSection(JsonConfigFile file, string name, JObject? previousSlice, string? localizationKey)
    {
        _file = file;
        Name = name;
        _previousSlice = previousSlice;
        LocalizationKey = localizationKey;
    }

    /// <inheritdoc />
    public IConfigEntry this[string key] => Entries[key];

    /// <inheritdoc />
    public IReadOnlyList<string> Keys => Entries.Keys.ToList();

    /// <inheritdoc />
    public IConfigEntry BindEntry(
        Type valueType,
        string key,
        object? defaultValue = null,
        string description = "",
        IValueConstraint? constraint = null,
        string? nameLocalizationKey = null,
        string? descriptionLocalizationKey = null,
        IEnumerable<string>? tags = null
    )
    {
        if (Entries.TryGetValue(key, out var existing))
        {
            existing.MergeTags(tags);
            return existing;
        }

        object? value = StoredValueOr(key, valueType, defaultValue);
        Entries[key] = new JsonConfigEntry(_file, valueType, description, value, constraint, nameLocalizationKey, descriptionLocalizationKey, tags)
        {
            Default = defaultValue
        };

        _file.Save();
        return Entries[key];
    }

    // Sets every entry to the slice's value for it, or its default when the slice has none.
    internal void Load(JObject? slice)
    {
        _previousSlice = slice;
        foreach (KeyValuePair<string, JsonConfigEntry> entry in Entries)
        {
            entry.Value.Value = StoredValueOr(entry.Key, entry.Value.ValueType, entry.Value.Default);
        }
    }

    private object? StoredValueOr(string key, Type valueType, object? fallback)
    {
        if (_previousSlice == null || !_previousSlice.TryGetValue(key, out JToken token))
            return fallback;

        try
        {
            return token.ToObject(valueType);
        }
        catch
        {
            return fallback;
        }
    }

    internal void WriteTo(StringBuilder result)
    {
        result.AppendLine($"    \"{Name.Replace("\"", "\\\"").Replace("\n", "\\n")}\": {{");
        var hadPreviousKey = false;
        var writtenKeys = new HashSet<string>();
        if (_previousSlice != null)
        {
            foreach (JProperty property in _previousSlice.Properties())
            {
                if (Entries.TryGetValue(property.Name, out JsonConfigEntry entry))
                {
                    hadPreviousKey = JsonConfigFile.DumpEntry(
                        result,
                        hadPreviousKey,
                        new KeyValuePair<string, JsonConfigEntry>(property.Name, entry)
                    );
                }
                else
                {
                    hadPreviousKey = JsonConfigFile.DumpPreviousEntry(result, hadPreviousKey, property);
                }

                writtenKeys.Add(property.Name);
            }
        }

        foreach (var entry in Entries)
        {
            if (!writtenKeys.Add(entry.Key))
                continue;

            hadPreviousKey = JsonConfigFile.DumpEntry(result, hadPreviousKey, entry);
        }
        result.Append("\n    }");
    }
}

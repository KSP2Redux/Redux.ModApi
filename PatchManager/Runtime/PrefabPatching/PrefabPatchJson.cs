using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Redux.IO;

namespace PatchManager.PrefabPatching
{
    /// <summary>
    /// Canonical JSON shared by the editor compiler, resolver, cache, and fluent frontend.
    /// </summary>
    public static class PrefabPatchJson
    {
        /// <summary>
        /// Canonical serializer settings used for manifests, plans, and hashes.
        /// </summary>
        public static readonly JsonSerializerSettings Settings = new()
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            },
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Include,
            Converters = { new StringEnumConverter(), new SignedZeroConverter() }
        };

        /// <summary>
        /// Serializes a value using the canonical prefab-patch JSON format.
        /// </summary>
        /// <param name="value">Value to serialize.</param>
        /// <returns>Formatted canonical JSON.</returns>
        public static string Serialize(object value) =>
            JsonConvert.SerializeObject(value, Settings);

        /// <summary>
        /// Deserializes canonical prefab-patch JSON.
        /// </summary>
        /// <param name="json">JSON to deserialize.</param>
        /// <typeparam name="T">Expected result type.</typeparam>
        /// <returns>The deserialized value.</returns>
        public static T Deserialize<T>(string json) =>
            JsonConvert.DeserializeObject<T>(json, Settings);

        /// <summary>
        /// Calculates a manifest content hash without including its current hash field.
        /// </summary>
        /// <param name="manifest">Manifest to hash.</param>
        /// <returns>The canonical manifest hash.</returns>
        public static string CalculateManifestHash(PrefabPatchManifest manifest)
        {
            var previous = manifest.ManifestHash;
            manifest.ManifestHash = null;
            try
            {
                return Sha256.OfString(Serialize(manifest));
            }
            finally
            {
                manifest.ManifestHash = previous;
            }
        }

        // Mono loses numeric negative zero while formatting and parsing JSON.
        // A string token preserves it across the shared typed manifest codec.
        private sealed class SignedZeroConverter : JsonConverter<double>
        {
            /// <inheritdoc />
            public override void WriteJson(JsonWriter writer, double value, JsonSerializer serializer)
            {
                if (BitConverter.DoubleToInt64Bits(value) == long.MinValue)
                    writer.WriteValue("-0.0");
                else
                    writer.WriteValue(value);
            }

            /// <inheritdoc />
            public override double ReadJson(
                JsonReader reader,
                Type objectType,
                double existingValue,
                bool hasExistingValue,
                JsonSerializer serializer
            )
            {
                if (reader.TokenType == JsonToken.Integer || reader.TokenType == JsonToken.Float)
                    return Convert.ToDouble(reader.Value, CultureInfo.InvariantCulture);
                if (reader.TokenType == JsonToken.String)
                {
                    var text = (string)reader.Value;
                    if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    {
                        if (value == 0d && text.TrimStart().StartsWith("-", StringComparison.Ordinal))
                            return BitConverter.Int64BitsToDouble(long.MinValue);
                        return value;
                    }
                }
                throw new JsonSerializationException("Expected a floating-point prefab patch value.");
            }
        }
    }
}

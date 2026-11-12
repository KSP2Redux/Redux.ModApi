#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;

namespace SpaceWarp2.API.Configuration;

/// <summary>
/// Filters out unsafe or rogue JSON converters that cause infinite recursion loops or hijack base serialization.
/// </summary>
[PublicAPI]
public static class JsonConverterFilter
{
    private static readonly string[] _blacklistedConverterPrefixes =
    {
        "OpenQA.",
        "UnityEditor.",
        "Unity.AI.",
        "Microsoft.VisualStudio.",
        "Mono.Cecil.",
        "NUnit.",
        "nunit."
    };

    private static readonly Type[] _hijackableBaseTypes =
    {
        typeof(UnityEngine.Object),
        typeof(object),
        typeof(IDictionary),
        typeof(IEnumerable)
    };

    /// <summary>
    /// Known non-game test or editor tooling namespace and assembly prefixes to exclude from JSON converters.
    /// </summary>
    public static IReadOnlyList<string> BlacklistedConverterPrefixes => _blacklistedConverterPrefixes;

    /// <summary>
    /// Base types that no discovered converter is allowed to claim.
    /// </summary>
    public static IReadOnlyList<Type> HijackableBaseTypes => _hijackableBaseTypes;

    /// <summary>
    /// Checks whether a converter belongs to a blacklisted tooling namespace or assembly.
    /// </summary>
    /// <param name="converter">The converter to inspect.</param>
    /// <returns>True if the converter is from a blacklisted namespace or assembly, otherwise false.</returns>
    public static bool IsBlacklistedConverter(JsonConverter converter)
    {
        if (converter == null)
        {
            return false;
        }

        Type type = converter.GetType();
        string fullName = type.FullName ?? type.Name;
        string assemblyName = type.Assembly.GetName().Name ?? string.Empty;

        foreach (string prefix in _blacklistedConverterPrefixes)
        {
            if (fullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                assemblyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"Removing blacklisted converter of type: {type.FullName} from assembly {assemblyName}");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reports whether a converter claims a base type that would hijack object or collection serialization.
    /// </summary>
    /// <param name="converter">The converter to inspect.</param>
    /// <returns>True if the converter claims a hijackable base type, otherwise false.</returns>
    public static bool ClaimsHijackableBaseType(JsonConverter converter)
    {
        if (converter == null)
        {
            return false;
        }

        foreach (Type baseType in _hijackableBaseTypes)
        {
            try
            {
                if (converter.CanConvert(baseType))
                {
                    Debug.Log($"Removing converter of type: {converter.GetType()}, it claims {baseType.FullName}");
                    return true;
                }
            }
            catch (Exception)
            {
                // A converter that throws when asked about an unexpected type has not claimed it
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether a converter should be filtered out to prevent serialization hijacking or recursion loops.
    /// </summary>
    /// <param name="converter">The converter to inspect.</param>
    /// <returns>True if the converter should be filtered out, otherwise false.</returns>
    public static bool ShouldFilterConverter(JsonConverter? converter)
    {
        if (converter == null)
        {
            return true;
        }

        return IsBlacklistedConverter(converter) || ClaimsHijackableBaseType(converter);
    }
}

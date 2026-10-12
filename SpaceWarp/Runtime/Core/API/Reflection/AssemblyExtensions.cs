#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;

namespace SpaceWarp2.API.Reflection;

/// <summary>
/// Reflection helpers shared across the mod stack.
/// </summary>
[PublicAPI]
public static class AssemblyExtensions
{
    /// <summary>
    /// Returns the types in an assembly, tolerating one that is only partially loadable.
    /// </summary>
    /// <remarks>
    /// A mod assembly can reference a type that is not present at runtime, which makes
    /// <see cref="Assembly.GetTypes" /> throw <see cref="ReflectionTypeLoadException" />. The types that did load
    /// are still reachable on the exception, so this yields those and drops the ones that failed to load.
    /// </remarks>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>Every type that loaded successfully.</returns>
    public static IEnumerable<Type> GetLoadableTypes(this Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
    }

    /// <summary>
    /// Returns the types in every assembly loaded into a domain, for registries that discover their entries by
    /// scanning.
    /// </summary>
    /// <remarks>
    /// Dynamic assemblies are skipped. A runtime evaluator such as the CLI's C# REPL leaves them behind with
    /// unfinished types, which make <see cref="Assembly.GetTypes" /> throw, and nothing in them belongs in a
    /// registry. Partially loadable assemblies are handled as in <see cref="GetLoadableTypes(Assembly)" />.
    /// </remarks>
    /// <param name="domain">The domain to scan, usually <see cref="AppDomain.CurrentDomain" />.</param>
    /// <returns>Every type that loaded successfully from a static assembly.</returns>
    public static IEnumerable<Type> GetLoadableTypes(this AppDomain domain)
    {
        return domain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic)
            .SelectMany(assembly => assembly.GetLoadableTypes());
    }
}

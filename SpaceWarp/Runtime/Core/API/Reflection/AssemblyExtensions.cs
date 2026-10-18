#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using UnityEngine;

namespace SpaceWarp2.API.Reflection
{
    /// <summary>
    /// Reflection helpers shared across the mod stack.
    /// </summary>
    [PublicAPI]
    public static class AssemblyExtensions
    {
        // The last domain scan, kept until the domain loads another assembly. Assemblies never unload, so a changed
        // count always means new types.
        private static int _scannedAssemblyCount = -1;
        private static Type[] _scannedTypes = Array.Empty<Type>();

        // Assemblies of mods rejected at load. Mono cannot unload an assembly, so they stay in the domain and the
        // scans have to skip them instead.
        private static readonly HashSet<Assembly> _rejectedAssemblies = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStaticState()
        {
            _rejectedAssemblies.Clear();
            _scannedAssemblyCount = -1;
        }

        /// <summary>
        /// Hides an assembly from the domain scans, so registries that discover their entries by scanning never see
        /// its types.
        /// </summary>
        /// <param name="assembly">The assembly of a mod that was rejected at load.</param>
        internal static void Reject(this Assembly assembly)
        {
            _rejectedAssemblies.Add(assembly);
            _scannedAssemblyCount = -1;
        }

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
        /// The scan is cached until the domain loads another assembly, so the registries that each scan at boot share
        /// one. Dynamic assemblies are skipped. A runtime evaluator such as the CLI's C# REPL leaves them behind with
        /// unfinished types, which make <see cref="Assembly.GetTypes" /> throw, and nothing in them belongs in a
        /// registry. Partially loadable assemblies are handled as in <see cref="GetLoadableTypes(Assembly)" />, and
        /// the assemblies of mods rejected at load are skipped.
        /// </remarks>
        /// <param name="domain">The domain to scan, usually <see cref="AppDomain.CurrentDomain" />.</param>
        /// <returns>Every type that loaded successfully from a static assembly.</returns>
        public static IReadOnlyList<Type> GetLoadableTypes(this AppDomain domain)
        {
            Assembly[] assemblies = domain.GetAssemblies();
            if (assemblies.Length == _scannedAssemblyCount)
                return _scannedTypes;

            _scannedTypes = assemblies
                .Where(assembly => !assembly.IsDynamic && !_rejectedAssemblies.Contains(assembly))
                .SelectMany(assembly => assembly.GetLoadableTypes())
                .ToArray();
            _scannedAssemblyCount = assemblies.Length;
            return _scannedTypes;
        }

        /// <summary>
        /// Finds a type by its assembly-qualified or full name in any assembly loaded into a domain.
        /// </summary>
        /// <param name="domain">The domain to search, usually <see cref="AppDomain.CurrentDomain" />.</param>
        /// <param name="name">The assembly-qualified or full type name.</param>
        /// <param name="matchShortName">Whether a bare type name without its namespace also matches, for names people
        /// type in, such as at the console.</param>
        /// <returns>The type, or null when no loaded assembly has it.</returns>
        public static Type? FindType(this AppDomain domain, string name, bool matchShortName = false)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            Type? type = Type.GetType(name, false);
            if (type != null && !_rejectedAssemblies.Contains(type.Assembly))
                return type;

            foreach (Assembly assembly in domain.GetAssemblies())
            {
                if (_rejectedAssemblies.Contains(assembly))
                    continue;

                type = assembly.GetType(name, false);
                if (type != null)
                    return type;
            }

            return matchShortName ? domain.GetLoadableTypes().FirstOrDefault(candidate => candidate.Name == name) : null;
        }
    }
}

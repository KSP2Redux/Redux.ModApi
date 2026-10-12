using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SpaceWarp2.API.Reflection;
using VSwift.Attributes;

namespace VSwift.Utilities
{
    /// <summary>
    /// Registry mapping transformer types to their <see cref="Attributes.TransformerAdapter" />-decorated Lua wrappers, built by reflection on the first lookup.
    /// </summary>
    internal static class Adapters
    {
        private static Dictionary<Type, Type> _transformerAdapters;

        // Dropping the registry makes the next lookup rebuild it, so a play session started without a domain reload
        // also sees mod assemblies loaded since the last scan. Filling it here instead would scan before mods load.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStatics()
        {
            _transformerAdapters = null;
        }

        /// <summary>
        /// Looks up the adapter wrapper registered for the given transformer type.
        /// </summary>
        /// <param name="transformerType">The transformer type to look up.</param>
        /// <param name="adapterType">The adapter wrapper type, or <c>null</c> when no adapter is registered.</param>
        /// <returns>True if an adapter is registered, false otherwise.</returns>
        internal static bool TryGetAdapterFor(Type transformerType, out Type adapterType)
        {
            _transformerAdapters ??= FindAdapters(AppDomain.CurrentDomain.GetAssemblies());
            return _transformerAdapters.TryGetValue(transformerType, out adapterType);
        }

        /// <summary>
        /// Maps each transformer type to the class whose <see cref="TransformerAdapter" /> attribute names it.
        /// </summary>
        /// <remarks>
        /// Dynamic assemblies are skipped. Runtime evaluators such as the CLI's C# REPL leave unfinished dynamic types
        /// behind when Domain Reload is disabled, and enumerating them throws. A mod assembly that only partially
        /// loads contributes the types that did load.
        /// </remarks>
        /// <param name="assemblies">The assemblies to search.</param>
        /// <returns>The adapter type for every transformer type that has one.</returns>
        internal static Dictionary<Type, Type> FindAdapters(IEnumerable<Assembly> assemblies)
        {
            var adapters = new Dictionary<Type, Type>();
            IEnumerable<Type> types = assemblies
                .Where(assembly => !assembly.IsDynamic)
                .SelectMany(assembly => assembly.GetLoadableTypes());
            foreach (Type type in types)
            {
                foreach (TransformerAdapter attribute in type.GetCustomAttributes<TransformerAdapter>(false))
                {
                    adapters[attribute.TransformerType] = type;
                }
            }

            return adapters;
        }
    }
}

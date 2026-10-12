using System;
using System.Collections.Generic;
using KSP.Sim.Definitions;
using PatchManager.Parts.Attributes;
using SpaceWarp2.API.Reflection;
using UniLinq;

namespace PatchManager.Parts
{
    /// <summary>
    /// The Lua-facing adapters for part module data. The module types themselves are SpaceWarp's
    /// <c>PartModuleTypes</c>.
    /// </summary>
    public static class PartsUtilities
    {
        /// <summary>
        /// Map of <see cref="ModuleData" /> type to the registered Lua-facing adapter type. Populated by
        /// <see cref="GrabModuleDataAdapters" /> and <see cref="RegisterModuleDataAdapter{T}" />.
        /// </summary>
        internal static readonly Dictionary<Type, Type> ModuleDataAdapters = new();

        /// <summary>
        /// Discovers every type marked with <see cref="Attributes.ModuleDataAdapterAttribute" /> across all loaded
        /// assemblies and registers them in <see cref="ModuleDataAdapters" />.
        /// </summary>
        internal static void GrabModuleDataAdapters()
        {
            var adapters = AppDomain.CurrentDomain.GetLoadableTypes()
                .Select(type => (type, attr: (ModuleDataAdapterAttribute)type.GetCustomAttributes(typeof(ModuleDataAdapterAttribute), false).FirstOrDefault()))
                .Where(adapter => adapter.attr != null);
            foreach (var adapter in adapters)
            {
                foreach (var dataType in adapter.attr.ValidTypes)
                {
                    ModuleDataAdapters[dataType] = adapter.type;
                }
            }
        }

        /// <summary>
        /// Registers a module data adapter for the given types.
        /// </summary>
        /// <param name="validTargets">The types that this adapter is valid for.</param>
        /// <typeparam name="T">The type of the adapter.</typeparam>
        public static void RegisterModuleDataAdapter<T>(params Type[] validTargets)
        {
            foreach (var type in validTargets)
            {
                ModuleDataAdapters[type] = typeof(T);
            }
        }
    }
}

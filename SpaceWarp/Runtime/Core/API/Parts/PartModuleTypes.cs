using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using KSP.Sim.Definitions;
using KSP.Sim.impl;
using SpaceWarp2.API.Reflection;

namespace SpaceWarp2.API.Parts
{
    /// <summary>
    /// Every part module type the loaded assemblies define, by the short names data uses for them, such as a patch
    /// or a part switch naming <c>Module_Engine</c> or <c>Engine</c>.
    /// </summary>
    /// <remarks>
    /// Each map is built by scanning every loaded assembly the first time it is read.
    /// </remarks>
    [PublicAPI]
    public static class PartModuleTypes
    {
        private static Dictionary<string, (Type componentModule, Type behaviour)> _componentModules;
        private static Dictionary<string, Type> _dataModules;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStaticState()
        {
            _componentModules = null;
            _dataModules = null;
        }

        /// <summary>
        /// Gets each part component module's component type and behaviour type, by its name with and without the
        /// <c>PartComponent</c> prefix.
        /// </summary>
        public static IReadOnlyDictionary<string, (Type componentModule, Type behaviour)> ComponentModules =>
            _componentModules ??= BuildComponentModules();

        /// <summary>
        /// Gets each <see cref="ModuleData" /> type, by its name with and without the <c>Data_</c> prefix.
        /// </summary>
        public static IReadOnlyDictionary<string, Type> DataModules => _dataModules ??= BuildDataModules();

        private static Dictionary<string, (Type componentModule, Type behaviour)> BuildComponentModules()
        {
            var modules = new Dictionary<string, (Type componentModule, Type behaviour)>();
            IEnumerable<Type> types = AppDomain.CurrentDomain.GetLoadableTypes()
                .Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(PartComponentModule)));
            foreach (Type type in types)
            {
                // A module that cannot be built without its part has no behaviour type to report, so it is left out
                try
                {
                    var module = (PartComponentModule)Activator.CreateInstance(type);
                    (Type, Type) entry = (type, module.PartBehaviourModuleType);
                    modules[type.Name] = entry;
                    modules[type.Name.Replace("PartComponent", "")] = entry;
                }
                catch
                {
                }
            }

            return modules;
        }

        private static Dictionary<string, Type> BuildDataModules()
        {
            var modules = new Dictionary<string, Type>();
            IEnumerable<Type> types = AppDomain.CurrentDomain.GetLoadableTypes()
                .Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(ModuleData)));
            foreach (Type type in types)
            {
                modules[type.Name] = type;
                modules[type.Name.Replace("Data_", "")] = type;
            }

            return modules;
        }
    }
}

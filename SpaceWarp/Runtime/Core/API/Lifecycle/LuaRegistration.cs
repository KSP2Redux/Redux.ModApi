using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MoonSharp.Interpreter;
using PatchManager.LuaPatching.Utility;
using SpaceWarp2.API.Config;
using SpaceWarp2.API.Reflection;

namespace SpaceWarp2.API.Lifecycle
{
    /// <summary>
    /// The one place MoonSharp's process-wide Lua setup happens: the registration policy, every loaded assembly's
    /// <c>[MoonSharpUserData]</c> types and their delegate conversions, and the colour conversion.
    /// </summary>
    /// <remarks>
    /// MoonSharp's registries are static and outlive play sessions, so each assembly registers once per domain.
    /// Mods load their assemblies after the game's, so a later call registers whatever has loaded since.
    /// </remarks>
    internal static class LuaRegistration
    {
        private static readonly HashSet<Assembly> Registered = new();

        /// <summary>
        /// Registers every loaded assembly not registered yet, setting up the policy and conversions first.
        /// </summary>
        internal static void RegisterLoadedAssemblies()
        {
            if (Registered.Count == 0)
            {
                UserData.RegistrationPolicy = new FallbackRegistrationPolicy();
                LuaColor.RegisterConversions();
            }

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // Evaluator assemblies come and go, and none of them carries Lua types
                if (assembly.IsDynamic || !Registered.Add(assembly))
                    continue;

                UserData.RegisterAssembly(assembly, false);
                IEnumerable<Type> luaTypes = assembly.GetLoadableTypes()
                    .Where(type => type.GetCustomAttributes(typeof(MoonSharpUserDataAttribute), true).Length > 0);
                foreach (Type type in luaTypes)
                {
                    DelegateRegistry.RegisterDelegatesFromMethodsOf(type);
                }
            }
        }
    }
}

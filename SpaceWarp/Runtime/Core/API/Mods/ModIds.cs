using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace SpaceWarp2.API.Mods
{
    /// <summary>
    /// The ids of the mods built into the game assembly.
    /// </summary>
    [PublicAPI]
    public static class ModIds
    {
        /// <summary>
        /// Redux itself, which owns everything the game assembly registers without naming a mod.
        /// </summary>
        public const string KSP2_REDUX = "Ksp2Redux";

        /// <summary>
        /// The SpaceWarp mod loader.
        /// </summary>
        public const string SPACE_WARP = "SpaceWarp2";

        /// <summary>
        /// Patch Manager.
        /// </summary>
        public const string PATCH_MANAGER = "PatchManager";

        /// <summary>
        /// V-SwiFT, the part switching mod.
        /// </summary>
        public const string V_SWIFT = "VSwift";

        /// <summary>
        /// The modding framework, whose default features every campaign has and no player switches.
        /// </summary>
        public static readonly IReadOnlyCollection<string> AlwaysOn = new HashSet<string>(
            new[] { SPACE_WARP, PATCH_MANAGER, V_SWIFT },
            StringComparer.OrdinalIgnoreCase
        );
    }
}

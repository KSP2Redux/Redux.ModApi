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
    }
}

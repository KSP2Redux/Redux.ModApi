using System.Reflection;

namespace PatchManager.Core.Utility
{
    /// <summary>
    /// Utility class containing versions of KSP 2 and PatchManager.
    /// </summary>
    public static class Versions
    {
        /// <summary>
        /// Currently running KSP 2 version.
        /// </summary>
        public static string Ksp2Version
        {
            get
            {
                var type = typeof(VersionID);
                var field = type.GetField("VERSION_TEXT", BindingFlags.Static | BindingFlags.Public);
                var value = field?.GetValue(null) as string;
                return value;
            }
        }

        /// <summary>
        /// The version of the game assembly PatchManager compiles into. It changes with every build of that assembly,
        /// so a patch cache never outlives the code that made it.
        /// </summary>
        public static string PatchManagerVersion => Assembly.GetExecutingAssembly().GetName().Version.ToString();
    }
}
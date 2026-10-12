using UnityEngine;
using UnityEngine.UIElements;

namespace VSwift.UI
{
    /// <summary>
    /// Builds the stat blocks the part-switch popout shows for each variant.
    /// </summary>
    public static class VSwiftUI
    {
        /// <summary>
        /// The stat block template, loaded by <see cref="VSwiftPlugin.OnInitialized" />.
        /// </summary>
        internal static VisualTreeAsset StatBlockContainer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            StatBlockContainer = null;
        }

        /// <summary>
        /// Creates a single-statistic visual element for variant-info display.
        /// </summary>
        /// <param name="statBlockTitle">The stat name (e.g. <c>Engine ISP - Reverse</c>).</param>
        /// <param name="statBlockText">The stat value (e.g. <c>Sea Level 300s / Vacuum 1000000s</c>).</param>
        /// <returns>The stat block visual element.</returns>
        public static VisualElement CreateStatBlock(string statBlockTitle, string statBlockText)
        {
            var clone = StatBlockContainer.CloneTree();
            var container = clone.Q<VisualElement>("variant-stat");
            var name = container.Q<Label>("variant-stat-title");
            name.text = statBlockTitle.ToUpper();
            var info = container.Q<Label>("variant-stat-info");
            info.text = statBlockText;
            return container;
        }
    }
}

using KSP.Game;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace DebugTools.Runtime.Controllers
{
    /// <summary>
    /// Base of every Dev Tools tool controller. The hub creates the tool from its chrome-less UXML and hands the tool
    /// root to <see cref="Bind" /> before the controller component is enabled.
    /// </summary>
    public class BaseWindowController : KerbalMonoBehaviour
    {
        // Root VisualElement of the tool
        protected VisualElement RootElement;

        // The backing field for the IsWindowOpen property
        private bool _isWindowOpen;

        internal void Bind(VisualElement toolRoot)
        {
            RootElement = toolRoot;
        }

        /// <summary>
        /// Gets or sets whether the tool is shown in its current host.
        /// </summary>
        public bool IsWindowOpen
        {
            // HideManipulator uses visibility, independently of the requested display state.
            get => _isWindowOpen && RootElement != null &&
                RootElement.resolvedStyle.visibility == Visibility.Visible;
            set
            {
                _isWindowOpen = value;
                // The host shows or hides the tool by its display style. Every update of a tool returns at once while
                // the tool is not shown.
                RootElement.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>
        /// Gets whether the host wants the tool shown, which stays true while the game UI is hidden.
        /// </summary>
        /// <remarks>
        /// <see cref="IsWindowOpen" /> is also false while the UI is hidden with F2. A tool that drops state when it is
        /// closed uses this to tell a close from a hide.
        /// </remarks>
        protected bool IsRequestedOpen => _isWindowOpen;

        /// <summary>
        /// Brings a tool created while a campaign is loaded up to date with that campaign.
        /// </summary>
        /// <remarks>
        /// Tools are created the first time they are shown from Dev Tools, after the campaign's
        /// <c>GameLoadFinishedMessage</c> has gone out. A tool that sets itself up from that message overrides this to
        /// run the same setup.
        /// </remarks>
        public virtual void CatchUpWithLoadedGame()
        {
        }
    }
}

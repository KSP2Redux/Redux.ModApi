using JetBrains.Annotations;
using PatchManager.Shared.Modules;

namespace PatchManager.Parts
{
    /// <summary>
    /// Part patching module.
    /// </summary>
    [UsedImplicitly]
    public class PartsModule : BaseModule
    {
        /// <summary>
        /// Snapshots the available module-data adapters.
        /// </summary>
        public override void Init()
        {
            PartsUtilities.GrabModuleDataAdapters();
        }
    }
}

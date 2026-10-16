#nullable enable

using SpaceWarp2.API.Configuration;
using SpaceWarp2.API.Logging;
using SpaceWarp2.API.Mods;

namespace SpaceWarp2.API.Backend.Modding;

internal class AssetOnlyMod : ISpaceWarpMod
{
    public AssetOnlyMod(string name)
    {
        SWLogger = LogManager.GetLogger(name);
    }

    public void OnPreInitialized()
    {
    }

    public void OnInitialized()
    {
    }

    public void OnPostInitialized()
    {
    }

    public void OnCampaignLoaded()
    {
    }

    public void OnCampaignUnloading()
    {
    }

    public ILogger SWLogger { get; set; }
    public IConfigFile SWConfiguration { get; set; } = new EmptyConfigFile();

    public SpaceWarpPluginDescriptor SWMetadata { get; set; }
}
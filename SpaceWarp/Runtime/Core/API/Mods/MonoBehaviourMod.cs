#nullable enable

using HarmonyLib;
using SpaceWarp2.API.Configuration;
using UnityEngine;
using ILogger = SpaceWarp2.API.Logging.ILogger;

namespace SpaceWarp2.API.Mods;

public abstract class MonoBehaviourMod : MonoBehaviour, ISpaceWarpMod
{
    public virtual void OnPreInitialized()
    {
        
    }

    public virtual void OnInitialized()
    {
        
    }

    public virtual void OnPostInitialized()
    {
        
    }


    public ILogger SWLogger { get; set;  }
    public IConfigFile SWConfiguration { get; set; }
    public SpaceWarpPluginDescriptor SWMetadata { get; set; }
    
    public Harmony CreateHarmonyAndPatchAll()
    {
        var harmony = new Harmony(SWMetadata.Guid);
        harmony.PatchAll(GetType().Assembly);
        return harmony;
    }
}
using System;
using System.Security;
using BepInEx;
using BepInEx.Logging;

[module: UnverifiableCode]

namespace UQLTerminus;

[BepInPlugin("uql.terminus", "Local Terminus", "0.1.24")]
public partial class UQLTerminus : BaseUnityPlugin
{

    public void OnEnable()
    {
        On.RainWorld.OnModsInit += RainWorldOnOnModsInit;
        On.RainWorld.PostModsInit += RainWorldOnPostModsInit;
        logger = Logger;
        info = Info;
    }

    internal static void Log(string message)
    {
        UnityEngine.Debug.Log($"[{info.Metadata.Name}] " + message);
    }

    internal static void LogWarning(string message)
    {
        UnityEngine.Debug.LogWarning($"[{info.Metadata.Name}] " + message);
    }    

    private bool IsInit;

    internal static PluginInfo info;
    internal static ManualLogSource logger;

    private void RainWorldOnOnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);
        if (IsInit) return;

        try
        {
            IsInit = true;
            Hooks.Apply();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }
    }

    private void RainWorldOnPostModsInit(On.RainWorld.orig_PostModsInit orig, RainWorld self)
    {
        orig(self);
        Logger.LogInfo("Loading pearls after all mods have initialized");
        Hooks.LoadPearlSounds();
    }
}
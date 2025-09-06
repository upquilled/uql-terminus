using System;
using System.Security;
using System.Security.Permissions;
using BepInEx;
using BepInEx.Logging;

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace UQLTerminus;

[BepInPlugin("uql.terminus", "Local Terminus", "0.1.22")]
public partial class UQLTerminus : BaseUnityPlugin
{

    public void OnEnable()
    {
        On.RainWorld.OnModsInit += RainWorldOnOnModsInit;
        On.RainWorld.PostModsInit += RainWorldOnPostModsInit;
        logger = Logger;
        info = Info;

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
        Hooks.LoadPearlSounds(); // or whatever logic you need
    }
}
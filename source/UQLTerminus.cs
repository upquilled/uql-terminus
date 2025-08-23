using System;
using System.CodeDom;
using System.Security;
using System.Security.Permissions;
using BepInEx;
using BepInEx.Logging;

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace UQLTerminus;

[BepInPlugin("uql.terminus", "Local Terminus", "0.1.10")]
public partial class UQLTerminus : BaseUnityPlugin
{

    public void OnEnable()
    {
        On.RainWorld.OnModsInit += RainWorldOnOnModsInit;
        On.RainWorld.PostModsInit += RainWorldOnPostModsInit;
    }

    private bool IsInit;

    private void RainWorldOnOnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);
        if (IsInit) return;

        try
        {
            IsInit = true;
            new Hooks(Logger).Apply();
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
        new Hooks(Logger).LoadPearlSounds(); // or whatever logic you need
    }
}
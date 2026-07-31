using System.Collections.Generic;
using System.Linq;
using System.Security;
using BepInEx;
using BepInEx.Logging;
using UQLScribe.Registries;
using static UQLScribe.UQLTag;

[module: UnverifiableCode]

namespace UQLTerminus;

using static RegionJukeboxRegistry;

[BepInPlugin("uql.terminus", "Local Terminus", "0.1.33")]
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
        => UnityEngine.Debug.Log($"[Info   :{info.Metadata.Name}] " + message);

    internal static void LogWarning(string message)
        => UnityEngine.Debug.LogWarning($"[Warning:{info.Metadata.Name}] " + message);

    internal static void LogError(string message)
        => UnityEngine.Debug.LogError($"[Error  :{info.Metadata.Name}] " + message);

    private bool _initialized;

    internal static PluginInfo info = null!;
    internal static ManualLogSource logger = null!;

    private void RainWorldOnOnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);

        if (_initialized) return;

        _initialized = true;
        Hooks.Apply();
        Registrar.Register(new SaveRegistry(this));
    }

    private class SaveRegistry(BaseUnityPlugin plugin) : IRegistry
    {
        public BaseUnityPlugin plugin {get; private set;} = plugin;

        public void Load(Wrapper? wrapper, RainWorldGame? game)
        {
            if (wrapper == null) return;
            if (game == null)
            {
               LogError("Game instance wasn't provided on save Load()! Was mod loaded late?");
               return;
            }

            var regions = ((NamedGroup) wrapper.compounds
                .FirstOrDefault(x => x is NamedGroup group && group.label.val == "J")).compounds;

            if (regions == null) return;

            RegionToJukeboxes = new();
            
            foreach (var val in regions)
            {
                if (val is not NamedGroup regionEntry) continue;
                string name = regionEntry.label.val;
                foreach (var compound in regionEntry.compounds)
                {
                    if (compound is not Record jukeboxRecord) continue;
                    bool firstInit = jukeboxRecord.entries.Length == 4;
                    if (jukeboxRecord.entries.Length != 3 && !firstInit) continue;
                    string id = ((Label) jukeboxRecord.entries[0]).val;
                    string room = ((Label) jukeboxRecord.entries[1]).val;
                    string pearl = ((Label) jukeboxRecord.entries[2]).val;
                    Log($"Adding jukebox {id} from save data");
                    addJukebox(name, id,
                        () => JukeboxInfo.FromSave(id, room, pearl, game, firstInit));
                }
            }
        }

        public IEnumerable<Compound> Save()
        {
            List<NamedGroup> regions = new();
            foreach (var pair in RegionToJukeboxes)
            {
                var jukeboxes = pair.Value.Values;
                List<Record> records = new();
                foreach (var jukebox in jukeboxes)
                    records.Add(
                        new Record(
                            [
                                jukebox.JukeboxID,
                                jukebox.room,
                                jukebox.CurrentPearl?.value ?? "",
                                ..jukebox.firstInit ? [""] : (Label[])[]
                            ]
                        )
                    );
                regions.Add(new NamedGroup(pair.Key, records));
            }
            RegionToJukeboxes = new();
            return [new NamedGroup("J", regions)];
        }
    }

    private void RainWorldOnPostModsInit(On.RainWorld.orig_PostModsInit orig, RainWorld self)
    {
        orig(self);
        Logger.LogInfo("Loading pearls after all mods have initialized");
        Hooks.LoadPearlSounds();
    }
}
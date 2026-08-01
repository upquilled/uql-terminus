using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Mono.CompilerServices.SymbolWriter;

namespace UQLTerminus;

using static RegionJukeboxRegistry;

public class JukeboxResonance : UpdatableAndDeletable
{
    public class ReferencedOmni : OmniDirectionalSound
    {
        public ResonanceSound hook;

        public float configurationVolume;
        public float configurationPitch;

        public ReferencedOmni(ResonanceSound hook) : base(hook.GetPath(), false)
        {
            this.hook = hook;
            configurationVolume = volume;
            configurationPitch = pitch;
        }
    }
    public string ID { get; init; }

    public bool duplicate { get; private set; }

    private static readonly Dictionary<string, HashSet<JukeboxResonance>> GlobalResonances = [];

    private (AbstractRoom room, JukeboxObjectData data)? deferredInfoSearch;

    private (AbstractRoom room, JukeboxObjectData data)? SearchForJukebox()
    {
        (AbstractRoom room, JukeboxObjectData data)? found = null;

        foreach (AbstractRoom abstractRoom in room.world.abstractRooms)
        {
            RoomSettings settings = new(
                abstractRoom.name,
                room.world.region,
                false,
                false,
                room.game.TimelinePoint,
                room.game
            );

            foreach (PlacedObject obj in settings.placedObjects)
            {
                if (obj.data is JukeboxObjectData jdata && jdata.ID == ID)
                {
                    found = (abstractRoom, jdata);
                    break;
                }
            }

            if (found is not null) {
                UQLTerminus.Log($"Found Jukebox {ID} in room {found.Value.room.name}!");
                roomSearchComplete = true;
                return found;
            }
        }

        roomSearchComplete = true;
        return null;
    }

    private Task RoomSearchTask => new(() => {
        UQLTerminus.Log($"Starting room search for Jukebox {ID}");
        roomSearchComplete = true;
        deferredInfoSearch = SearchForJukebox();
    });

    private JukeboxInfo? GetInfoIfRoomsSearched()
    {
        if (deferredInfoSearch is null)
        {   if (roomSearchComplete is true) {
                var mic = room.game.cameras[0].virtualMicrophone;
                mic.ambientSoundPlayers.RemoveAll(test => test.GetType() == typeof(ReferencedOmni));
            } return null;
        }

        var foundRoom = deferredInfoSearch.Value.room;
        var foundData = deferredInfoSearch.Value.data;
        JukeboxInfo? jukeboxInfo = null;
        addJukebox(room.world.region.name, foundData.ID, () => {
            deferredInfoSearch = null;
            roomSearchComplete = false;
            return jukeboxInfo = JukeboxInfo.FromSave(
                foundData.ID,
                foundRoom.name,
                foundData.initiateWithPearl ? foundData.defaultPearl.value : "",
                room.game,
                true);
            });
        return jukeboxInfo;
    }

    public static IEnumerable<JukeboxResonance> GetResonances(string room)
    {
        if (!GlobalResonances.TryGetValue(room, out var list)) yield break;

        foreach (JukeboxResonance reso in list)
            if (!reso.duplicate) yield return reso;
    }
    public static IEnumerable<JukeboxResonance> GetResonancesOfID(string ID)
    {
        foreach (var list in GlobalResonances.Values)
            foreach (JukeboxResonance reso in list)
                if (reso.ID == ID) yield return reso;
    }

    private bool first = true;

    private bool? roomSearchComplete;

    public JukeboxResonanceData data;
    public JukeboxResonance(PlacedObject placedObj)
    {
        data = placedObj.data as JukeboxResonanceData ?? new JukeboxResonanceData(placedObj);
        ID = data.ID;
    }
    public override void Destroy()
    {
        GlobalResonances[room.abstractRoom.name].Remove(this);
        base.Destroy();
    }

    public override void Update(bool eu)
    {
        base.Update(eu);

        if (room is null) return;

        if (room.PlayersInRoom.Count == 0) return;

        if (!data.owner.active)
        {
            Destroy();
            return;
        }

        duplicate = room.roomSettings.placedObjects.Any(obj =>
            obj.data is JukeboxResonanceData resoData
            && resoData.ID == ID
            && resoData.owner != data.owner);

        if (duplicate) return;

        JukeboxInfo? jukeboxInfo = GetInfoIfRoomsSearched();

        if (jukeboxInfo is null) {
            RegionToJukeboxes.TryGetValue(room.world.region.name,
                out Dictionary<string,JukeboxInfo>? jukeboxList);
            jukeboxList?.TryGetValue(ID, out jukeboxInfo);
        }

        var mic = room.game.cameras[0].virtualMicrophone;


        if (first) {
            (GlobalResonances[room.abstractRoom.name] =
                GlobalResonances.TryGetValue(room.abstractRoom.name, out var resonances)
                ? resonances : []).Add(this);
            if (jukeboxInfo is not null) ReloadSounds(jukeboxInfo);
            first = false;
        }

        if (jukeboxInfo is null && roomSearchComplete is null)
            RoomSearchTask.Start();

        foreach (AmbientSoundPlayer soundPlayer in mic.ambientSoundPlayers)
        {
            if (!(soundPlayer.aSound is ReferencedOmni sound)) continue;

            if (!MultiFadeManager.isFading(sound, "configurationVolume"))
                sound.configurationVolume = data.volume;

            sound.volume = sound.hook.resonanceVolume * sound.configurationVolume;

            if (!MultiFadeManager.isFading(sound, "configurationPitch"))
                sound.configurationPitch = data.pitch;

            sound.pitch = sound.hook.soundData.BeatScale * sound.configurationPitch;
        }
    }

    public void ReloadSounds(JukeboxInfo? jukeboxInfo)
    {
        if (duplicate) return;
        UQLTerminus.Log($"Reloading sounds for Jukebox Resonance of Jukebox {ID}");
        var mic = room.game.cameras[0].virtualMicrophone;

        if (jukeboxInfo is null) {
            RegionToJukeboxes.TryGetValue(room.world.region.name, out var jukeboxList);
            jukeboxList?.TryGetValue(ID, out jukeboxInfo);
        }

        if (jukeboxInfo is null) return;

        UQLTerminus.Log("Found jukeboxInfo!");

        HashSet<ResonanceSound> existingSounds = [];

        foreach (AmbientSoundPlayer soundPlayer in mic.ambientSoundPlayers)
        {
            if (!(soundPlayer.aSound is ReferencedOmni existOmni)) continue;
            UQLTerminus.Log($"Modifying volume for existing sound {existOmni.hook.pearlType}");
            existingSounds.Add(existOmni.hook);
            MultiFadeManager.StopFade(existOmni, "volume");
            existOmni.configurationVolume = existOmni.hook.resonanceVolume == 0f
                                            ? 0f : existOmni.volume / existOmni.hook.resonanceVolume;
            MultiFadeManager.FadeField(room.game, existOmni, "configurationVolume",
                data.volume,
                ResonanceSound.shiftFadeDuration);
            MultiFadeManager.FadeField(room.game, existOmni, "configurationPitch",
                data.pitch,
                ResonanceSound.shiftFadeDuration);
        }

        foreach (ResonanceSound sound in jukeboxInfo.resonances)
        {
            if (existingSounds.Contains(sound)) continue;
            UQLTerminus.Log($"Reloading sound {sound.pearlType} at {sound.resonanceVolume}");

            if (mic.ambientSoundPlayers.Any(test => test.aSound is ReferencedOmni omni
                && omni.hook == sound)) continue;

            var realizedSound = new ReferencedOmni(sound)
            {
                volume = 0f,
                configurationVolume = 0f,
                pitch = sound.soundData.BeatScale * data.pitch
            };

            MultiFadeManager.FadeField(room.game, realizedSound, "configurationVolume",
                data.volume,
                ResonanceSound.shiftFadeDuration);

            mic.ambientSoundPlayers.Add(new AmbientSoundPlayer(mic, realizedSound));
            UQLTerminus.Log($"Added realized sound for {sound.pearlType}");
        }
    }
}

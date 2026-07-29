using System.Collections.Generic;
using System.Linq;

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

    public string ID {get; private set;}
    private bool _duplicate;

    public bool duplicate { get { return _duplicate; } private set { _duplicate = value; } }

    private static readonly Dictionary<string, HashSet<JukeboxResonance>> GlobalResonances = new();

    public static IEnumerable<JukeboxResonance> GetResonances(string room)
    {
        if (!GlobalResonances.TryGetValue(room, out var list)) yield break;

        foreach (JukeboxResonance reso in list)
            if (!reso.duplicate) yield return reso;
    }
    public static IEnumerable<JukeboxResonance> GetResonancesOfID(string ID)
    {
        foreach(var list in GlobalResonances.Values)
            foreach(JukeboxResonance reso in list)
                if (reso.ID == ID) yield return reso;
    }

    private bool first = true;

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

        if (room == null) return;

        if (room.PlayersInRoom.Count == 0) return;

        if (!data.owner.active) {
            Destroy();
            return;
        }

        duplicate = room.roomSettings.placedObjects.Any(obj =>
            obj.data is JukeboxResonanceData resoData
            && resoData.ID == ID
            && resoData.owner != data.owner);

        if (duplicate) return;
        
        JukeboxInfo? jukeboxInfo = null;
        RegionToJukeboxes.TryGetValue(room.world.region.name, out var jukeboxList);
        if(jukeboxList != null)
            jukeboxList.TryGetValue(ID, out jukeboxInfo);
        
        var mic = room.game.cameras[0].virtualMicrophone;


        if (first)
        {
            (GlobalResonances[room.abstractRoom.name] =
                GlobalResonances.TryGetValue(room.abstractRoom.name, out var resonances)
                ? resonances : new()).Add(this);
        }

        if (jukeboxInfo == null)
        {
            AbstractRoom? foundRoom = null;
            JukeboxObjectData? foundData = null;
            if (first)
            {
                foreach (AbstractRoom abstractRoom in room.world.abstractRooms)
                {
                    RoomSettings settings = new RoomSettings(
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
                            foundRoom = abstractRoom;
                            foundData = jdata;
                            break;
                        }
                    }

                    if (foundData != null)
                        break;
                }
            }
            if (foundRoom == null)
            {
                mic.ambientSoundPlayers.RemoveAll(test => test.GetType() == typeof(ReferencedOmni));
                first = false;
                return;
            }
            else
            {
                jukeboxInfo = JukeboxInfo.FromSave(
                    foundData.ID,
                    foundRoom.name,
                    foundData.initiateWithPearl ? foundData.defaultPearl.value : "",
                    room.game,
                    true);
                addJukebox(room.world.region.name, foundData.ID, () => jukeboxInfo);
            }
        } else if (first) ReloadSounds();

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

        first = false;
    }

    public void ReloadSounds()
    {
        if (duplicate) return;
        UQLTerminus.Log($"Reloading sounds for Jukebox Resonance of Jukebox {ID}");
        var mic = room.game.cameras[0].virtualMicrophone;
        
        JukeboxInfo? jukeboxInfo = null;

        RegionToJukeboxes.TryGetValue(room.world.region.name, out var jukeboxList);
        if(jukeboxList != null)
            jukeboxList.TryGetValue(ID, out jukeboxInfo);
        
        if (jukeboxInfo == null) return;

        UQLTerminus.Log("Found jukeboxInfo!");

        HashSet<ResonanceSound> existingSounds = new();

        foreach (AmbientSoundPlayer soundPlayer in mic.ambientSoundPlayers)
        {
            if (!(soundPlayer.aSound is ReferencedOmni existOmni)) continue;
            UQLTerminus.Log($"Modifying volume for existing sound {existOmni.hook.pearlType}");
            existingSounds.Add(existOmni.hook);
            MultiFadeManager.StopFade(existOmni, "volume");
            existOmni.configurationVolume = existOmni.hook.resonanceVolume == 0f
                                            ? 0f : existOmni.volume / existOmni.hook.resonanceVolume;
            MultiFadeManager.FadeField(existOmni, "configurationVolume",
                data.volume,
                ResonanceSound.shiftFadeDuration);
            MultiFadeManager.FadeField(existOmni, "configurationPitch",
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

            MultiFadeManager.FadeField(realizedSound, "configurationVolume",
                data.volume,
                ResonanceSound.shiftFadeDuration);

            mic.ambientSoundPlayers.Add(new AmbientSoundPlayer(mic, realizedSound));
            UQLTerminus.Log($"Added realized sound for {sound.pearlType}");
        }
    }
}

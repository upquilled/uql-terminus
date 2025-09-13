using System.Collections.Generic;
using System.Linq;
using static UQLTerminus.RegionJukeboxRegistry;

namespace UQLTerminus;

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

    private bool _duplicate;

    public bool duplicate { get { return _duplicate; } private set { _duplicate = value; } }

    private static readonly Dictionary<Room, HashSet<JukeboxResonance>> GlobalResonances = new();

    public static IEnumerable<JukeboxResonance> GetResonances(Room room)
    {
        var list = GlobalResonances.GetValueOrDefault(room);
        if (list == null) yield break;

        foreach (JukeboxResonance reso in list)
            if (!reso.duplicate) yield return reso;
    }

    private bool first = true;

    public JukeboxResonanceData data;
    public JukeboxResonance(PlacedObject placedObj)
    {
        data = placedObj.data as JukeboxResonanceData ?? new JukeboxResonanceData(placedObj);
    }
    public override void Destroy()
    {
        base.Destroy();
        GlobalResonances[room].Remove(this);
    }

    public override void Update(bool eu)
    {
        base.Update(eu);

        if (room == null) return;

        if (first)
        {
            (GlobalResonances[room] =
                GlobalResonances.GetValueOrDefault(room)
                ?? new()).Add(this);
            first = false;
        }

        if (room.PlayersInRoom.Count == 0) return;

        if (!data.owner.active) Destroy();

        duplicate = room.roomSettings.placedObjects.Any(obj =>
            obj.data is JukeboxResonanceData resoData
            && resoData.ID == data.ID
            && resoData.owner != data.owner);

        if (duplicate) return;

        RegionToJukeboxes.TryGetValue(room.world.region, out var jukeboxList);
        var jukeboxInfo = jukeboxList?.FirstOrDefault(j => j.JukeboxID == data.ID);
        var mic = room.game.cameras[0].virtualMicrophone;

        if (jukeboxInfo == null)
        {
            mic.ambientSoundPlayers.RemoveAll(test => test.GetType() == typeof(ReferencedOmni));
            return;
        }

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

    public void ReloadSounds()
    {
        if (duplicate) return;
        UQLTerminus.Log($"Reloading sounds for Jukebox Resonance of Jukebox {data.ID}");
        var mic = room.game.cameras[0].virtualMicrophone;
        RegionToJukeboxes.TryGetValue(room.world.region, out var jukeboxList);
        var jukeboxInfo = jukeboxList?.FirstOrDefault(j => j.JukeboxID == data.ID);
        if (jukeboxInfo == null) return;

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

using static DataPearl.AbstractDataPearl;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

namespace UQLTerminus;

public static class RegionJukeboxRegistry
{
    public class JukeboxInfo
    {

        public string JukeboxID = null!;
        public DataPearlType? CurrentPearl;

        public bool firstInit {get; internal set;}

        public RainWorldGame game = null!;

        public string room = null!;

        private bool _isPlaying = false;

        public bool isPlaying
        {
            get => _isPlaying;
            internal set => updateResonance(value);
        }

        public readonly List<ResonanceSound> resonances = [];

        public JukeboxInfo(JukeboxObject jukebox)
        {
            JukeboxID = jukebox.data.ID;
            room = jukebox.room.abstractRoom.name;
            CurrentPearl = jukebox.Pearl?.AbstractPearl.dataPearlType;
            isPlayingToAssign = jukebox.isPlaying;
            game = jukebox.room.game;
        }

        public void updateResonance(bool playing)
        {
            if (_isPlaying == playing) return;
            _isPlaying = playing;
            if (playing && CurrentPearl != null)
            {
                resonances.Add(new ResonanceSound(game, CurrentPearl, this));
                UQLTerminus.Log($"Found pearl resonance: {CurrentPearl.value}");
                foreach (JukeboxResonance reso
                            in JukeboxResonance.GetResonancesOfID(JukeboxID))
                    reso.ReloadSounds();
            }
            else if (resonances.Count > 0)
            {
                resonances.Last().Stop();
            }
        }

        public static JukeboxInfo FromSave(string id, string room, string pearl, RainWorldGame game, bool firstInit)
        {
            DataPearlType? CurrentPearl;

            if (pearl == "")
            {
                CurrentPearl = null;
            } else 
            {
                DataPearlType tryPearl =  new DataPearlType(pearl);
                if (tryPearl.Index == -1) CurrentPearl = null;
                else CurrentPearl = tryPearl;
            }

            bool isPlaying = pearl != "";

            return new JukeboxInfo
            {
                JukeboxID = id,
                room = room,
                CurrentPearl = CurrentPearl,
                game = game,
                firstInit = firstInit,
                isPlayingToAssign = isPlaying
            };
        }

        public bool updateIsPlaying()
        {
            if (isPlayingToAssign == null) return false;
            isPlaying = (bool) isPlayingToAssign;
            return true;
        }

        private bool? isPlayingToAssign = null;

        private JukeboxInfo() {}
    }

    public class ResonanceSound
    {
        public const float fadeDuration = 10f;
        public const float shiftFadeDuration = 2.5f;
        public float resonanceVolume = 0f;

        private bool _active = true;
        public DataPearlType pearlType;

        public RainWorldGame game;

        public JukeboxInfo parent;

        public SoundData soundData;

        public string GetPath()
        {
            return Path.Combine("..", "..", "music", "songs", Hooks.PearlSoundsDict[pearlType].Approach.Path + ".ogg");
        }

        public ResonanceSound(RainWorldGame game, DataPearlType pearlType, JukeboxInfo info)
        {
            parent = info;
            this.game = game;
            this.pearlType = pearlType;
            soundData = Hooks.PearlSoundsDict[pearlType].Approach;
            MultiFadeManager.FadeField(game, this, "resonanceVolume", soundData.Volume, fadeDuration);
        }

        public void Stop()
        {
            MultiFadeManager.FadeField(game, this, "resonanceVolume", 0f, fadeDuration,
                        onFinish: () => ImmediateStop());
        }

        public void ImmediateStop()
        {
            parent.resonances.Remove(this);
            _active = false;
            game.cameras[0].virtualMicrophone.ambientSoundPlayers.RemoveAll(x =>
                x.aSound is JukeboxResonance.ReferencedOmni omni
                && omni.hook == this);
        }

        public bool isActive()
        {
            return _active;
        }
    }
    public static Dictionary<string, Dictionary<string,JukeboxInfo>> RegionToJukeboxes = new();

    public static bool addJukebox(string region, string id, Func<JukeboxInfo> infoFactory)
    {
        if (RegionToJukeboxes.TryGetValue(region, out var registered))
        {
            if (registered.ContainsKey(id))
            {
                 UQLTerminus.Log($"{id} already exists in Jukebox registry!");
                 return false;
            }
        } else {
            registered = RegionToJukeboxes[region] = new();
        }
        var info = infoFactory();
        registered[id] = info;
        info.updateIsPlaying();
        UQLTerminus.Log($"new values for {region}: {string.Join(",",registered.Values.Select(x => x.JukeboxID))}");
        return true;
    }
    
}
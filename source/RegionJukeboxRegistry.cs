
using static DataPearl.AbstractDataPearl;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UQLTerminus;

public static class RegionJukeboxRegistry
{
    public class JukeboxInfo
    {

        public string JukeboxID;
        public DataPearlType? CurrentPearl;

        public Room room;

        private bool _isPlaying;

        public bool isPlaying
        {
            get => _isPlaying;
            internal set => updateResonance(value);
        }

        public readonly List<ResonanceSound> resonances = [];

        public JukeboxInfo(JukeboxObject jukebox)
        {
            JukeboxID = jukebox.data.ID;
            room = jukebox.room;
            CurrentPearl = jukebox.Pearl?.AbstractPearl.dataPearlType;
            _isPlaying = jukebox.isPlaying;
        }

        public void updateResonance(bool playing)
        {
            if (_isPlaying == playing) return;
            _isPlaying = playing;
            if (playing && CurrentPearl != null)
            {
                resonances.Add(new ResonanceSound(CurrentPearl, this));
                UQLTerminus.Log($"Found pearl resonance: {CurrentPearl.value}");
                foreach (JukeboxResonance reso
                            in JukeboxResonance.GetResonances(room))
                    reso.ReloadSounds();
            }
            else if (resonances.Count > 0)
            {
                resonances.Last().Stop();
            }
            resonances.RemoveAll(sound => sound.isFinished());
        }
    }

    public class ResonanceSound
    {
        public const float fadeDuration = 10f;
        public const float shiftFadeDuration = 2.5f;
        public float resonanceVolume = 0f;

        private bool _active = true;

        public bool active { get => _active; private set => _active = value; }
        public DataPearlType pearlType;

        public JukeboxInfo parent;

        public SoundData soundData;

        public string GetPath()
        {
            return Path.Combine("..", "..", "music", "songs", Hooks.PearlSoundsDict[pearlType].Approach.Path + ".ogg");
        }

        public ResonanceSound(DataPearlType pearlType, JukeboxInfo info)
        {
            parent = info;
            this.pearlType = pearlType;
            soundData = Hooks.PearlSoundsDict[pearlType].Approach;
            MultiFadeManager.FadeField(this, "resonanceVolume", soundData.Volume, fadeDuration);
        }

        public void Stop()
        {
            MultiFadeManager.FadeField(this, "resonanceVolume", 0f, fadeDuration);
            active = false;
        }

        public bool isFinished()
        {
            return resonanceVolume == 0 && !active;
        }
    }
    public static readonly Dictionary<Region, List<JukeboxInfo>> RegionToJukeboxes = new();
}
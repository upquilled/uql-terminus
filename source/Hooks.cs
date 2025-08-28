using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using System.Linq;

namespace UQLTerminus
{
    internal class Hooks
    {
        public static readonly Dictionary<DataPearl.AbstractDataPearl.DataPearlType, PearlSoundRefs> PearlSoundsDict
    = new Dictionary<DataPearl.AbstractDataPearl.DataPearlType, PearlSoundRefs>();

        public static void LoadPearlSounds()
        {
            foreach (var filePath in AssetManager.ListDirectory("music/songs")
            .Where(f => string.Equals(Path.GetExtension(f), ".txt", StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        LoadPearlSound(filePath); // You can define this method to handle parsing + dict insertion
                        UQLTerminus.logger.LogInfo($"Loaded pearl sound from {filePath.Replace(Path.DirectorySeparatorChar, '/')}");
                    }
                    catch (Exception ex)
                    {
                        UQLTerminus.logger.LogError($"Failed to load pearl sound file '{filePath.Replace(Path.DirectorySeparatorChar, '/')}': {ex}");
                    }
                }
        }

        private static SoundData ParseSoundLine(string line)
        {
            var soundData = new SoundData();

            // Split line at the first space
            int firstSpace = line.IndexOf(' ');
            if (firstSpace < 0) return soundData; // no data

            string type = line.Substring(0, firstSpace).Trim();
            string rest = line.Substring(firstSpace).Trim();

            // Check if line contains ':'
            int colonIndex = rest.IndexOf(':');
            string pathPart = colonIndex >= 0 ? rest.Substring(0, colonIndex).Trim() : rest.Trim();
            string paramPart = colonIndex >= 0 ? rest.Substring(colonIndex + 1).Trim() : "";

            soundData.Path = pathPart;

            if (!string.IsNullOrEmpty(paramPart))
            {
                var tokens = paramPart.Split([' '], StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length > 0) float.TryParse(tokens[0], out soundData.Volume);
                if (tokens.Length > 1) float.TryParse(tokens[1], out soundData.BeatScale);
            }

            return soundData;
        }


        private static void LoadPearlSound(string file)
        {
            var lines = File.ReadAllLines(file);
            var soundRefs = new PearlSoundRefs();

            foreach (var line in lines)
            {
                if (line.StartsWith("APPROACH "))
                    soundRefs.Approach = ParseSoundLine(line);
                else if (line.StartsWith("PLAY "))
                    soundRefs.Play = ParseSoundLine(line);
                else if (line.StartsWith("STOP "))
                    soundRefs.Stop = ParseSoundLine(line);
            }

            string pearlName = Path.GetFileNameWithoutExtension(file);

            if (ExtEnumBase.TryParse(typeof(DataPearl.AbstractDataPearl.DataPearlType), pearlName, true, out var pearlType))
            {
                PearlSoundsDict[(DataPearl.AbstractDataPearl.DataPearlType)pearlType] = soundRefs;
            }
            else
            {
                UQLTerminus.logger.LogWarning($"Could not parse pearl type from {pearlName}");
                throw new Exception("Pearl type not recognized!");
            }
        }
        public static void Apply()
        {
            Pom.Pom.RegisterManagedObject<JukeboxObject, JukeboxObjectData, Pom.Pom.ManagedRepresentation>("PearlJukebox", UQLTerminus.info.Metadata.Name);
            Pom.Pom.RegisterManagedObject<JukeboxResonance, JukeboxResonanceData, Pom.Pom.ManagedRepresentation>("JukeboxResonance", UQLTerminus.info.Metadata.Name);
            Pom.Pom.RegisterManagedObject<ScreenFilterObject, ScreenFilterObjectData, Pom.Pom.ManagedRepresentation>("ScreenFilter", UQLTerminus.info.Metadata.Name);
        }
    }
    public class SoundData
    {
        public string Path = "";
        public float Volume = 1f;
        public float BeatScale = 1f;
    }

    public class PearlSoundRefs
    {
        public SoundData Approach = new SoundData();
        public SoundData Play = new SoundData();
        public SoundData Stop = new SoundData();
    }

}
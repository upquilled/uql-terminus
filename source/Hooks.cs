using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace UQLTerminus;

public static class Hooks
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
                LoadPearlSound(filePath);
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

        int firstSpace = line.IndexOf(' ');
        if (firstSpace < 0) return soundData;

        string rest = line.Substring(firstSpace).Trim();

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

    private static bool existsReso;

    private static bool ReferencedOmniBypass(On.VirtualMicrophone.orig_ambientSoundCloseEnoughCheck orig,
                                            VirtualMicrophone self,
                                            AmbientSound A,
                                            AmbientSound B)
    {

        if (A is JukeboxResonance.ReferencedOmni omni)
        {
            if (omni.hook.isActive())
            {
                if (!existsReso) MultiFadeManager.FadeField(A, "volume", 0f,
                    RegionJukeboxRegistry.ResonanceSound.shiftFadeDuration);
                return true;
            }
            return false;
        }
        return orig(self, A, B);
    }

    private static void NewRoomBypass(On.VirtualMicrophone.orig_NewRoom orig, VirtualMicrophone self, Room room)
    {
        var list = JukeboxResonance.GetResonances(room);
        existsReso = false;
        foreach (JukeboxResonance reso in list)
        {
            existsReso = true;
            reso.ReloadSounds();
        }
        orig(self, room);
    }
    public static void Apply()
    {
        Pom.Pom.RegisterManagedObject<JukeboxObject, JukeboxObjectData, Pom.Pom.ManagedRepresentation>("PearlJukebox", UQLTerminus.info.Metadata.Name);
        Pom.Pom.RegisterManagedObject<JukeboxResonance, JukeboxResonanceData, Pom.Pom.ManagedRepresentation>("JukeboxResonance", UQLTerminus.info.Metadata.Name);
        Pom.Pom.RegisterManagedObject<ScreenFilterObject, ScreenFilterObjectData, Pom.Pom.ManagedRepresentation>("ScreenFilter", UQLTerminus.info.Metadata.Name);
        On.VirtualMicrophone.ambientSoundCloseEnoughCheck += ReferencedOmniBypass;
        On.VirtualMicrophone.NewRoom += NewRoomBypass;
        On.LightSource.DrawSprites += LightSourcePatch.DrawSprites;
        On.LightSource.InitiateSprites += LightSourcePatch.InitiateSprites;
    }
    private static class LightSourcePatch
    {

        private static readonly HashSet<LightSource> dirty = new();
        private static System.Reflection.FieldInfo shaderDirtyField =
        typeof(LightSource).GetField("shaderDirty",
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);
        public static void DrawSprites(On.LightSource.orig_DrawSprites orig, LightSource self,
            RoomCamera.SpriteLeaser sLeaser,
            RoomCamera rCam, float timeStacker,
            Vector2 camPos)
        {
            if (dirty.Contains(self) && sLeaser.sprites[0].shader.name == "NoLitWater") {
                UQLTerminus.Log($"Corrected flat shading for light source");
                shaderDirtyField.SetValue(self, true);
                dirty.Remove(self);
            }
            orig(self, sLeaser, rCam, timeStacker, camPos);
        }

        public static void InitiateSprites(On.LightSource.orig_InitiateSprites orig, LightSource self,
            RoomCamera.SpriteLeaser sLeaser,
            RoomCamera rCam)
        {
            orig(self, sLeaser, rCam);
            if (self.flat) dirty.Add(self);
        }
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
using System;
using System.Collections.Generic;
using System.IO;
using RWCustom;
using UnityEngine;

namespace UQLTerminus;

public class JukeboxObject : UpdatableAndDeletable, IDrawable
{
    private Queue<(Action action, int delay)> InvokeQueue = new();
    public JukeboxObjectData data;
    private PlacedObject placedObject;

    private PearlSoundRefs? pearlSoundRefs;

    private DataPearl? _pearl = null;

    private bool grabbedBefore = false;

    public bool isPlaying
    {
        get
        {
            return Pearl == null ? false : Pearl.grabbedBy.Count == 0;
        }
    }

    public DataPearl? Pearl
    {
        get => _pearl;
        private set {
            _pearl = value;
            updatePearlStatus();
        }
    }

    private void updatePearlStatus()
    {
        if (Pearl?.AbstractPearl == null) return;

        if (!Hooks.PearlSoundsDict.TryGetValue(Pearl.AbstractPearl.dataPearlType, out pearlSoundRefs))
        return;

        // registry lookup
        
        var region = room?.world?.region;
        if (region == null) return;

        if (RegionJukeboxRegistry.RegionToJukeboxes.TryGetValue(region, out var jukeboxList))
        {
            var info = jukeboxList.Find(j => j.JukeboxID == data.ID);
            if (info != null)
            {
                info.CurrentPearl = _pearl?.AbstractPearl.dataPearlType;
                info.isPlaying = isPlaying;
            }
        }
    }

    private float beatScale = 0f;

    public JukeboxObject(Room room, PlacedObject placedObj) : base()
    {
        data = placedObj.data as JukeboxObjectData ?? new JukeboxObjectData(placedObj);
        placedObject = placedObj;
        // will fix this later


        /* if (data.initiateWithPearl)
        {
            WorldCoordinate coord = room.ToWorldCoordinate(placedObject.pos);
            EntityID id = room.game.GetNewID();
            var abstractPearl = new DataPearl.AbstractDataPearl(
                room.world,                       // World
                    AbstractPhysicalObject.AbstractObjectType.DataPearl,  // Type
                    null,                             // Realized object (null for now)
                    coord,                            // Position
                    id,                               // ID
                    room.abstractRoom.index,          // Origin Room Index
                    -1,                               // PlacedObjectIndex (-1 if not applicable)
                    null,                             // PlacedObject.ConsumableObjectData (optional)
                    data.defaultPearl      // Pearl type (change as needed)
            );
            Pearl = new DataPearl(abstractPearl, room.world);
            Pearl.firstChunk.pos = placedObj.pos;
            room.AddObject(Pearl);
        } */

        this.room = room;

        Region region = room.world.region;

        (RegionJukeboxRegistry.RegionToJukeboxes[region] =
            RegionJukeboxRegistry.RegionToJukeboxes.TryGetValue(region, out var jukebox) ? jukebox : null
        ?? new List<RegionJukeboxRegistry.JukeboxInfo>()).Add(new RegionJukeboxRegistry.JukeboxInfo(this));
    }

    private void PearlUpdate()
    {
        bool sameRoom = room.PlayersInRoom.Count > 0;
        updatePearlStatus();
        if (Pearl.grabbedBy.Count == 0)
        {
            grabbedBefore = true;
            Pearl.firstChunk.vel *= Custom.LerpMap(Pearl.firstChunk.vel.magnitude, 1f, 6f, 0.999f, 0.999f-data.damping);
            Pearl.firstChunk.vel += Vector2.ClampMagnitude(placedObject.pos - Pearl.firstChunk.pos, 100f) / 100f * 0.4f * data.pullStrength;
            Pearl.gravity = 0f;
            if (sameRoom) { MusicControl(); return; }
        }
        MusicStop(sameRoom);
    }

    private void MusicControl()
    {
        if (room.game.manager.musicPlayer == null) return;

        if (room.game.manager.musicPlayer.song == null || room.game.manager.musicPlayer.song is not JukeboxSong)
        {
            JukeboxSong.Request(room.game.manager.musicPlayer, pearlSoundRefs.Play.Path, pearlSoundRefs.Play.Volume * data.volume);
            return;
        }

        float[] array = new float[1024];
        float num = 0f;
        if (room.game.manager.musicPlayer.song is JukeboxSong) room.game.manager.musicPlayer.song.subTracks[0].source.GetSpectrumData(array, 0, (FFTWindow)2);
        for (int i = 0; i < 1024; i++) num += array[i];
        beatScale = Mathf.Clamp(num * pearlSoundRefs.Play.BeatScale / pearlSoundRefs.Play.Volume / data.volume, 0f, 1f);
    }

    private void MusicStop(bool sameRoom)
    {
        if (room.game.manager.musicPlayer != null && room.game.manager.musicPlayer.song != null && room.game.manager.musicPlayer.song is JukeboxSong)
        {
            room.game.manager.musicPlayer.song.FadeOut(5f);
            if (sameRoom)
            {
                if (grabbedBefore) InvokeQueue.Enqueue((
                    () =>
                    {
                        MusicChunkSound(Pearl.firstChunk,
                                              pearlSoundRefs.Stop.Path,
                                              room,
                                              vol: pearlSoundRefs.Stop.Volume * data.volume,
                                              pitch: pearlSoundRefs.Stop.BeatScale);
                        Pearl = null;

                    }, 1));

                Pearl.gravity = 0.9f;
                beatScale = 0f;
            }
        }
    }

    public override void Update(bool eu)
    {
        base.Update(eu);

        if (!data.owner.active) Destroy();

        int count = InvokeQueue.Count;
        for (int i = 0; i < count; i++)
        {
            var item = InvokeQueue.Dequeue();

            if (item.delay <= 1)
            {
                item.action.Invoke();
                continue;
            }

            item.delay--;
            InvokeQueue.Enqueue(item);
        }

        if (data == null || room == null) return;

        if (Pearl == null)
        {

            DataPearl.AbstractDataPearl? closestPearl = null;
            float closestDistance = data.PickUpRadius;

            foreach (var entity in room.abstractRoom.entities)
            {
                if (entity is DataPearl.AbstractDataPearl pearl &&
                    Hooks.PearlSoundsDict.ContainsKey(pearl.dataPearlType) &&
                    pearl.realizedObject != null &&
                    pearl.realizedObject.room == room && pearl.realizedObject.grabbedBy.Count == 0)
                {
                    float distance = Vector2.Distance(pearl.realizedObject.firstChunk.pos, placedObject.pos);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestPearl = pearl;
                    }
                }
            }
            if (closestPearl != null && closestPearl.realizedObject is DataPearl realizedPearl)
            {
                Pearl = realizedPearl;
                grabbedBefore = false;
            }
        }
        if (Pearl != null)
        {
            PearlUpdate();
        }
    }

    public override void Destroy()
    {
        base.Destroy();
        var region = room.world.region;
        if (region == null) return;

        if (RegionJukeboxRegistry.RegionToJukeboxes.TryGetValue(region, out var jukeboxList))
        {
            var infoToRemove = jukeboxList.Find(j => j.JukeboxID == data.ID);
            if (infoToRemove != null)
            {
                jukeboxList.Remove(infoToRemove);
            }

            if (jukeboxList.Count == 0)
            {
                RegionJukeboxRegistry.RegionToJukeboxes.Remove(region);
            }
        }

    }
    public virtual void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
    {
        sLeaser.sprites = new FSprite[1];
        sLeaser.sprites[0] = new FSprite("LizardBubble6");
        AddToContainer(sLeaser, rCam, rCam.ReturnFContainer("Items"));
    }

    public void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
    {

        if (slatedForDeletetion || room != rCam.room)
        {
            sLeaser.CleanSpritesAndRemove();
            return;
        }
        bool showCondition = Pearl != null && Pearl.grabbedBy.Count == 0;

        Vector2 vector = Vector2.Lerp(showCondition ? Pearl.firstChunk.lastPos : placedObject.pos, Pearl != null ? Pearl.firstChunk.lastPos : placedObject.pos, timeStacker) - camPos;
        sLeaser.sprites[0].x = vector.x;
        sLeaser.sprites[0].y = vector.y;
        sLeaser.sprites[0].scale = beatScale * 0.75f;
        sLeaser.sprites[0].color = Color.red;
        sLeaser.sprites[0].alpha = showCondition ? 0.25f + beatScale * 0.65f : 0f;
        sLeaser.sprites[0].isVisible = true;
    }

    public void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
    {
        if (newContatiner == null)
        {
            newContatiner = rCam.ReturnFContainer("Items");
        }

        newContatiner.AddChild(sLeaser.sprites[0]);
    }

    public void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
    { }

    private static ChunkSoundEmitter? MusicChunkSound(BodyChunk chunk, string path, Room room, bool loop = false, float vol = 1f, float pitch = 1f)
    {
        string text3 = Path.Combine("Music", "Songs", path + ".ogg");
        string text4 = AssetManager.ResolveFilePath(text3);
        if (!Application.isConsolePlatform && text4 != Path.Combine(Custom.RootFolderDirectory(), text3.ToLowerInvariant()) && File.Exists(text4))
        {
            ChunkSoundEmitter chunkSoundEmitter = new ChunkSoundEmitter(chunk, vol, pitch);
            foreach (RoomCamera camera in room.game.cameras)
            {
                SoundLoader.SoundData soundData = camera.virtualMicrophone.GetSoundData(SoundID.Slugcat_Stash_Spear_On_Back, -1);
                soundData.dontAutoPlay = true;
                soundData.soundName = path;
                VirtualMicrophone.PositionedSound positionedSound = new VirtualMicrophone.ObjectSound(camera.virtualMicrophone, soundData, loop, chunkSoundEmitter, vol, pitch, false);
                positionedSound.singleUseSound = true;
                _ = Application.dataPath;
                positionedSound.audioSource.clip = AssetManager.SafeWWWAudioClip("file://" + text4, threeD: false, stream: true, AudioType.OGGVORBIS);
                camera.virtualMicrophone.soundObjects.Add(positionedSound);
            }
            return chunkSoundEmitter;
        }
        UQLTerminus.LogWarning($"Loading sound {text4.Replace(Path.DirectorySeparatorChar, '/')} failed!");
        return null;
    }
}
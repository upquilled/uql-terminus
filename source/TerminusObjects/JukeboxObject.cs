using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RWCustom;
using UnityEngine;

namespace UQLTerminus;

using static RegionJukeboxRegistry;
using Random = UnityEngine.Random;

public class JukeboxObject : UpdatableAndDeletable, IDrawable
{
    private Queue<(Action action, int delay)> InvokeQueue = new();
    public JukeboxObjectData data;
    private PlacedObject placedObject;

    private PearlSoundRefs? pearlSoundRefs;

    public string ID {get; private set;}

    private DataPearl.AbstractDataPearl? queuedPearl = null;

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
        if (Pearl != null && !Hooks.PearlSoundsDict.TryGetValue(
            Pearl.AbstractPearl.dataPearlType, out pearlSoundRefs))
            return;
        
        var region = room?.world?.region;
        if (region == null) return;

        if (RegionToJukeboxes.TryGetValue(region.name, out var jukeboxList))
        {
            jukeboxList.TryGetValue(ID, out JukeboxInfo? info);
            if (info != null)
            {
                info.CurrentPearl = _pearl?.AbstractPearl.dataPearlType;
                info.isPlaying = isPlaying;
            }
        }
    }

    private float beatScale = 0f;

    private void FixatePearl()
    {
        if (Pearl == null) return;
        Pearl.firstChunk.pos = placedObject.pos + Random.insideUnitCircle*data.PickUpRadius*0.5f;
        Pearl.firstChunk.vel = Random.insideUnitCircle*data.PickUpRadius*0.25f;
    }

    public JukeboxObject(Room room, PlacedObject placedObj) : base()
    {
        data = placedObj.data as JukeboxObjectData ?? new JukeboxObjectData(placedObj);
        placedObject = placedObj;
        ID = data.ID;
        
        this.room = room;

        Region region = room.world.region;

        JukeboxInfo? info = null;

        if (RegionToJukeboxes.TryGetValue(room.world.region.name, out var regionEntry))
            regionEntry.TryGetValue(ID, out info);
        
        if ((info?.isPlaying ?? false) && !info.firstInit)
        {
            UQLTerminus.Log("Trying to adapt pearl position");
            var abstractPearl = (DataPearl.AbstractDataPearl?) room.abstractRoom.entities.FirstOrDefault(
                x => x is DataPearl.AbstractDataPearl pearl 
                && pearl.dataPearlType == info.CurrentPearl);
            if (abstractPearl != null) queuedPearl = abstractPearl;
            else Pearl = null;
        } else if (data.initiateWithPearl && (info?.firstInit ?? true))
        {
            WorldCoordinate coord = room.ToWorldCoordinate(placedObject.pos);
            EntityID id = room.game.GetNewID();
            var abstractPearl = new DataPearl.AbstractDataPearl(
                room.world,
                    AbstractPhysicalObject.AbstractObjectType.DataPearl,
                    null,
                    coord,
                    id,
                    room.abstractRoom.index,
                    -1,
                    null,
                    data.defaultPearl
            ){placedObjectIndex=room.roomSettings.placedObjects.IndexOf(placedObject)};
            UQLTerminus.Log("Trying to spawn pearl");
            room.abstractRoom.AddEntity(abstractPearl);
            abstractPearl.RealizeInRoom();
            room.AddObject(abstractPearl.realizedObject);
            Pearl = (DataPearl) abstractPearl.realizedObject;
            FixatePearl();
            info?.firstInit = false;
        }
        if (info == null)
            addJukebox(region.name, ID, () => new JukeboxInfo(this));
    }

    private void PearlUpdate()
    {
        if (Pearl == null) return;
        bool sameRoom = room.PlayersInRoom.Count > 0;
        updatePearlStatus();
        if (Pearl.grabbedBy.Count == 0)
        {
            grabbedBefore = true;
            Pearl.firstChunk.vel *= Custom.LerpMap(Pearl.firstChunk.vel.magnitude, 1f, 6f, 0.999f, 0.999f-data.damping);
            Pearl.firstChunk.vel += Vector2.ClampMagnitude(placedObject.pos - Pearl.firstChunk.pos, 100f) / 100f * 0.4f * data.pullStrength;
            Pearl.SetLocalGravity(0f);
            if (sameRoom) { MusicControl(); return; }
        }
        MusicStop(sameRoom);
    }

    private void MusicControl()
    {
        if (pearlSoundRefs == null) return;
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
        if (Pearl == null) return;
        if (pearlSoundRefs == null) return;
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

                Pearl.SetLocalGravity(0.9f);
                beatScale = 0f;
            }
        }
    }

    public override void Update(bool eu)
    {
        base.Update(eu);

        if (!data.owner.active)
        {
            Destroy();
            return;
        }

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

        if (queuedPearl != null)
        {
            if (queuedPearl.realizedObject != null) {
                Pearl = (DataPearl) queuedPearl.realizedObject;
                FixatePearl();
                queuedPearl = null;
            }
            return;
        }

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
        if (Pearl != null) PearlUpdate();
    }

    public override void Destroy()
    {
        var region = room.world.region;
        if (region == null) return;

        if (RegionToJukeboxes.TryGetValue(region.name, out var jukeboxes))
        {
            jukeboxes.TryGetValue(ID, out JukeboxInfo? infoToRemove);
            if (infoToRemove != null)
                jukeboxes.Remove(ID);

            if (jukeboxes.Count == 0)
                RegionToJukeboxes.Remove(region.name);
        }
        base.Destroy();
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

        Vector2 vector = Vector2.Lerp(showCondition ? Pearl!.firstChunk.lastPos : placedObject.pos, Pearl != null ? Pearl.firstChunk.lastPos : placedObject.pos, timeStacker) - camPos;
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
            newContatiner = rCam.ReturnFContainer("Items");

        newContatiner.AddChild(sLeaser.sprites[0]);
    }

    public void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette) {}

    private static ChunkSoundEmitter? MusicChunkSound(BodyChunk chunk, string path, Room room, bool loop = false, float vol = 1f, float pitch = 1f)
    {
        string localPath = Path.Combine("Music", "Songs", path + ".ogg");
        string truePath = AssetManager.ResolveFilePath(localPath);
        if (!Application.isConsolePlatform && truePath != Path.Combine(Custom.RootFolderDirectory(), localPath.ToLowerInvariant()) && File.Exists(truePath))
        {
            ChunkSoundEmitter chunkSoundEmitter = new ChunkSoundEmitter(chunk, vol, pitch);
            foreach (RoomCamera camera in room.game.cameras)
            {
                SoundLoader.SoundData soundData = camera.virtualMicrophone.GetSoundData(SoundID.Slugcat_Stash_Spear_On_Back, -1);
                soundData.dontAutoPlay = true;
                soundData.soundName = path;
                VirtualMicrophone.PositionedSound positionedSound = 
                    new VirtualMicrophone.ObjectSound(camera.virtualMicrophone, soundData, loop, chunkSoundEmitter, vol, pitch, false)
                    {singleUseSound = true};
                positionedSound.audioSource.clip = AssetManager.SafeWWWAudioClip("file://" + truePath, threeD: false, stream: true, AudioType.OGGVORBIS);
                camera.virtualMicrophone.soundObjects.Add(positionedSound);
            }
            return chunkSoundEmitter;
        }
        UQLTerminus.LogWarning($"Loading sound {truePath.Replace(Path.DirectorySeparatorChar, '/')} failed!");
        return null;
    }
}
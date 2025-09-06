using System.Linq;

namespace UQLTerminus;

public class JukeboxResonance(PlacedObject placedObj) : UpdatableAndDeletable()
{
    public JukeboxResonanceData data = placedObj.data as JukeboxResonanceData;

    private DisembodiedLoopEmitter? emitter;
    private DataPearl.AbstractDataPearl.DataPearlType? currentPearl;

    private int logTicker = 0;

    public override void Update(bool eu)
    {
        base.Update(eu);
        RegionJukeboxRegistry.RegionToJukeboxes.TryGetValue(room.world.region, out var jukeboxList);
        var jukeboxInfo = jukeboxList?.FirstOrDefault(j => j.JukeboxID == data.ID);
        if (logTicker > 30)
        {
            string isIt = jukeboxInfo?.isPlaying ?? false ? "" : " not";
            UnityEngine.Debug.Log($"[{UQLTerminus.info.Metadata.Name}] Pearl {jukeboxInfo?.CurrentPearl} is{isIt} playing!");
            logTicker = 0;
        }
        if (jukeboxInfo != null && jukeboxInfo.isPlaying)
        {
            var pearlType = jukeboxInfo.CurrentPearl;
            var soundRefs = Hooks.PearlSoundsDict[pearlType];

            var approach = soundRefs.Approach;

            float finalVolume = approach.Volume * data.volume;
            float finalPitch = approach.BeatScale * data.pitch;

            // Play or update looping sound in this resonance room
            if (pearlType != currentPearl || emitter == null)
            {
                emitter?.Destroy();
                emitter = JukeboxObject.MusicDisembodiedSound(approach.Path, room, vol: finalVolume, pitch: finalPitch);
                UnityEngine.Debug.Log($"{emitter}");
                currentPearl = pearlType;
            }
        }
        /*else
        {
            // No jukebox or no pearl: stop any looping approach sound playing here
            emitter?.Destroy();
            emitter = null;
            currentPearl = null;
        }*/

        logTicker++;
    }

    public override void Destroy()
    {
        base.Destroy();
        emitter?.Destroy();
    }
}

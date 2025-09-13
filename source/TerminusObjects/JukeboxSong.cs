using Music;

namespace UQLTerminus;

public class JukeboxSong : Song
{

    public JukeboxSong(MusicPlayer musicPlayer, string song, float volume)
        : base(musicPlayer, song, MusicPlayer.MusicContext.StoryMode)
    {
        priority = 1.1f;
        stopAtGate = true;
        stopAtDeath = true;
        fadeInTime = 20f;
        baseVolume = volume;
        Loop = true;
    }

    public static void Request(MusicPlayer musicPlayer, string songName, float volume)
    {
        if ((musicPlayer.song == null || !(musicPlayer.song is JukeboxSong)) && (musicPlayer.nextSong == null || !(musicPlayer.nextSong is JukeboxSong)) && musicPlayer.manager.rainWorld.setup.playMusic)
        {
            Song song = new JukeboxSong(musicPlayer, songName, volume);
            if (musicPlayer.song == null)
            {
                musicPlayer.song = song;
                musicPlayer.song.playWhenReady = true;
            }
            else
            {
                musicPlayer.song.FadeOut(1.5f);
                musicPlayer.nextSong = song;
                musicPlayer.nextSong.playWhenReady = false;
            }
        }
    }
}
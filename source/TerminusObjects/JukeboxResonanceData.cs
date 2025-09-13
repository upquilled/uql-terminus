namespace UQLTerminus;

public class JukeboxResonanceData : Pom.Pom.ManagedData
{

    [Pom.Pom.StringField("ID", "0", displayName: "Jukebox ID")]
    public string ID = "0";

    [Pom.Pom.FloatField("volume", 0f, 1f, 1f, 0.01f, displayName: "Volume")]
    public float volume = 1f;

    [Pom.Pom.FloatField("pitch", 0.1f, 1.89f, 1f, 0.01f, displayName: "Pitch")]
    public float pitch = 1f;
    public JukeboxResonanceData(PlacedObject pObj) : base(pObj, null) { }
}
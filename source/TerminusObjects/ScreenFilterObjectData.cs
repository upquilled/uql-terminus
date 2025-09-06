namespace UQLTerminus;

public class ScreenFilterObjectData : Pom.Pom.ManagedData
{
    [Pom.Pom.StringField("ID", "-1")]
    internal string ID = "-1";

    [Pom.Pom.StringField("Screen", "-1")]
    internal string Screen = "-1";

    [Pom.Pom.BooleanField("Log Distance", false)]
    internal bool logDistance = false;

    [Pom.Pom.FloatField("Threshold", 0f, 100f, 50f)]
    internal float threshold = 50f;


    public int GetID()
    {
        return int.TryParse(ID, out var val) ? val : -1;
    }

    public int GetScreen()
    {
        return int.TryParse(Screen, out var val) ? val : -1;
    }

    public ScreenFilterObjectData(PlacedObject pObj) : base(pObj, null)
    { }
}
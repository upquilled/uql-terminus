using UnityEngine;
namespace UQLTerminus;

public class JukeboxObjectData : Pom.Pom.ManagedData
{
    [Pom.Pom.StringField("ID", "0")]
    internal string ID = null!;

    [Pom.Pom.BooleanField("initateWithPearl", false, displayName: "Start with Pearl")]
    internal bool initiateWithPearl = false;

    [Pom.Pom.ExtEnumField<DataPearl.AbstractDataPearl.DataPearlType>("defaultPearl", "JX_HALCYON", displayName: "Default Pearl")]
    internal DataPearl.AbstractDataPearl.DataPearlType defaultPearl = null!;
    
    [Pom.Pom.FloatField("volume", 0f, 1f, 0.5f, 0.01f, displayName: "Volume")]
    internal float volume = 0.5f;

    [Pom.Pom.FloatField("pullStrength", 0f, 2f, 1f, 0.01f, displayName: "Pull Strength")]
    internal float pullStrength = 1f;

    [Pom.Pom.FloatField("damping", 0f, 1f, 0.1f, 0.01f, displayName: "Damping")]
    internal float damping = 0.1f;

    [Pom.Pom.Vector2Field("pickupVector", 0f, 1f, reprType: Pom.Pom.Vector2Field.VectorReprType.circle)]
    internal Vector2 pickupVector;

    internal float PickUpRadius
    {
        get
        {
            return pickupVector.magnitude;
        }
    }

    public JukeboxObjectData(PlacedObject pObj) : base(pObj, null)
    {}
}
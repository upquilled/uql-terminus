using UnityEngine;

namespace UQLTerminus;
public class ScreenFilterObject : UpdatableAndDeletable
{
	public ScreenFilterObjectData data;
	private PlacedObject placedObject;

	private Room occupiedRoom;

	private PlacedObject trackedObject;

	private Vector2? objectPos;

	public ScreenFilterObject(Room room, PlacedObject placedObj) : base()
	{
		data = placedObj.data as ScreenFilterObjectData ?? new ScreenFilterObjectData(placedObj);
		placedObject = placedObj;
		occupiedRoom = room;
	}

	public override void Update(bool eu)
	{
		base.Update(eu);
		
		if (occupiedRoom.PlayersInRoom.Count == 0)
			return;

		if (!data.owner.active) Destroy();

		int id = data.GetID();
		int screen = data.GetScreen();

		if (screen < 0 || screen >= occupiedRoom.cameraPositions.Length)
			return;

		float dist = Vector2.Distance(occupiedRoom.game.cameras[0].pos, occupiedRoom.cameraPositions[screen]);

		if (data.logDistance) UQLTerminus.Log($"[ScreenFilter] Distance from target screen {screen}: {dist}");

		if (id < 0 || id >= occupiedRoom.roomSettings.placedObjects.Count)
			return;

		trackedObject = occupiedRoom.roomSettings.placedObjects[id];

		if (Vector2.Distance(occupiedRoom.game.cameras[0].pos, occupiedRoom.cameraPositions[screen]) < data.threshold)
		{
			if (!objectPos.HasValue)
				objectPos = trackedObject.pos;

			trackedObject.pos = placedObject.pos;
		}
		else if (objectPos is Vector2 pos)
		{
			trackedObject.pos = pos;
			objectPos = null;
		}
	}

	public override void Destroy()
	{
		base.Destroy();

		if (trackedObject != null && objectPos.HasValue)
		{
			trackedObject.pos = objectPos.Value;
			objectPos = null;
		}

		trackedObject = null;
		placedObject = null;
		occupiedRoom = null;
	}
}
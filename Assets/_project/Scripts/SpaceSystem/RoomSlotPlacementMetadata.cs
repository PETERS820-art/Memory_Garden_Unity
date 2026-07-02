using System.Collections.Generic;
using UnityEngine;

public enum RoomSlotSurfaceType
{
    Floor,
    Wall
}

[DisallowMultipleComponent]
public class RoomSlotPlacementMetadata : MonoBehaviour
{
    public string slotPlacementId;
    public string blockTypeId;
    public string blockInstanceId;
    public string slotPrefabKey;
    public RoomSlotSurfaceType surfaceType = RoomSlotSurfaceType.Floor;
    public int gridX;
    public int gridZ;
    public int widthUnits = 1;
    public int depthUnits = 1;
    public float rotationY;
    public WallSide wallSide = WallSide.North;
    public int wallGridPosition;
    public float heightOffset;
    public string furnitureId;
    public List<string> slotIds = new List<string>();
    public Vector3 localPosition;
    public Vector3 localEulerAngles;
    public Vector3 localScale = Vector3.one;

    [ContextMenu("Capture Transform Data")]
    public void CaptureTransformData()
    {
        localPosition = transform.localPosition;
        localEulerAngles = transform.localEulerAngles;
        localScale = transform.localScale;
    }

    private void Reset()
    {
        CaptureTransformData();
    }

    private void OnValidate()
    {
        widthUnits = Mathf.Max(1, widthUnits);
        depthUnits = Mathf.Max(1, depthUnits);
        CaptureTransformData();
    }
}

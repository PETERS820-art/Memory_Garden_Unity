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
    public int floorGridXHalf;
    public int floorGridZHalf;
    public int floorWidthHalf = 2;
    public int floorDepthHalf = 2;
    public float rotationY;
    public WallSide wallSide = WallSide.North;
    public int wallGridPosition;
    public float heightOffset;
    public int wallLayerIndex;
    public int wallLayerCount = 1;
    public float wallSurfaceHeight;
    public string furnitureId;
    public List<string> slotIds = new List<string>();
    public bool hasDisplaySlots = true;
    public int lightFeatureCount;
    public int frameSurfaceCount;
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
        floorWidthHalf = Mathf.Max(1, floorWidthHalf > 0 ? floorWidthHalf : widthUnits * 2);
        floorDepthHalf = Mathf.Max(1, floorDepthHalf > 0 ? floorDepthHalf : depthUnits * 2);
        if (surfaceType == RoomSlotSurfaceType.Floor)
        {
            if (floorGridXHalf == 0 && gridX != 0)
            {
                floorGridXHalf = gridX * 2;
            }

            if (floorGridZHalf == 0 && gridZ != 0)
            {
                floorGridZHalf = gridZ * 2;
            }
        }

        wallLayerCount = Mathf.Max(1, wallLayerCount);
        wallLayerIndex = Mathf.Clamp(wallLayerIndex, 0, wallLayerCount - 1);
        lightFeatureCount = Mathf.Max(0, lightFeatureCount);
        frameSurfaceCount = Mathf.Max(0, frameSurfaceCount);
        CaptureTransformData();
    }
}

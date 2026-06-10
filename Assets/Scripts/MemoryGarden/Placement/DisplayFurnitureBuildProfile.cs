using UnityEngine;

[CreateAssetMenu(
    fileName = "DisplayFurnitureBuildProfile",
    menuName = "Memory Garden/Display Furniture Build Profile")]
public class DisplayFurnitureBuildProfile : ScriptableObject
{
    public string furnitureId;
    public FurnitureType furnitureType = FurnitureType.Custom;
    public SlotPreset slotPreset = SlotPreset.Custom;
    public float slotHeightOffset = 0.02f;
    public Vector3 boundsPadding = Vector3.zero;
    public bool createPlacementBounds = true;
    public bool createBlockingCollider = true;
    public bool generateSlotsFromPlacementBounds = true;

    private void OnValidate()
    {
        boundsPadding = new Vector3(
            Mathf.Max(0f, boundsPadding.x),
            Mathf.Max(0f, boundsPadding.y),
            Mathf.Max(0f, boundsPadding.z));
    }
}

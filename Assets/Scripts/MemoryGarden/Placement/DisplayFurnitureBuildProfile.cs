using System.Collections.Generic;
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
    public PrefabTransformOverrideData prefabRootTransform = new PrefabTransformOverrideData();
    public PrefabTransformOverrideData modelContainerTransform = new PrefabTransformOverrideData();
    public PrefabTransformOverrideData modelAssetTransform = new PrefabTransformOverrideData();
    public BoxColliderOverrideData placementBoundsOverride = new BoxColliderOverrideData();
    public List<NamedTransformOverrideData> slotOverrides = new List<NamedTransformOverrideData>();

    private void OnValidate()
    {
        boundsPadding = new Vector3(
            Mathf.Max(0f, boundsPadding.x),
            Mathf.Max(0f, boundsPadding.y),
            Mathf.Max(0f, boundsPadding.z));

        prefabRootTransform ??= new PrefabTransformOverrideData();
        modelContainerTransform ??= new PrefabTransformOverrideData();
        modelAssetTransform ??= new PrefabTransformOverrideData();
        placementBoundsOverride ??= new BoxColliderOverrideData();
        slotOverrides ??= new List<NamedTransformOverrideData>();

        prefabRootTransform.localScale = EnsureNonZeroScale(prefabRootTransform.localScale);
        modelContainerTransform.localScale = EnsureNonZeroScale(modelContainerTransform.localScale);
        modelAssetTransform.localScale = EnsureNonZeroScale(modelAssetTransform.localScale);
        placementBoundsOverride.localScale = EnsureNonZeroScale(placementBoundsOverride.localScale);
        placementBoundsOverride.size = new Vector3(
            Mathf.Max(0.01f, placementBoundsOverride.size.x),
            Mathf.Max(0.01f, placementBoundsOverride.size.y),
            Mathf.Max(0.01f, placementBoundsOverride.size.z));

        for (int i = 0; i < slotOverrides.Count; i++)
        {
            NamedTransformOverrideData slotOverride = slotOverrides[i];
            if (slotOverride == null)
            {
                slotOverrides[i] = new NamedTransformOverrideData();
                slotOverride = slotOverrides[i];
            }

            slotOverride.localScale = EnsureNonZeroScale(slotOverride.localScale);
        }
    }

    private static Vector3 EnsureNonZeroScale(Vector3 value)
    {
        return new Vector3(
            Mathf.Approximately(value.x, 0f) ? 1f : value.x,
            Mathf.Approximately(value.y, 0f) ? 1f : value.y,
            Mathf.Approximately(value.z, 0f) ? 1f : value.z);
    }
}

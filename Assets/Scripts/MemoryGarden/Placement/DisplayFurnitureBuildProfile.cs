using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "DisplayFurnitureBuildProfile",
    menuName = "Memory Garden/Display Furniture Build Profile")]
public class DisplayFurnitureBuildProfile : ScriptableObject
{
    public string furnitureId;
    public DisplayFurnitureAuthoringMode authoringMode = DisplayFurnitureAuthoringMode.Display;
    public bool enableDisplayFeature = true;
    public bool enableLightFeature;
    public bool enableFrameFeature;
    public FurnitureType furnitureType = FurnitureType.Custom;
    public SlotPreset slotPreset = SlotPreset.Custom;
    public LightFeaturePreset lightFeaturePreset = LightFeaturePreset.None;
    public FrameSurfacePreset frameSurfacePreset = FrameSurfacePreset.None;
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
    public List<NamedLightOverrideData> lightOverrides = new List<NamedLightOverrideData>();
    public List<NamedFrameSurfaceOverrideData> frameSurfaceOverrides = new List<NamedFrameSurfaceOverrideData>();

    private void OnValidate()
    {
        switch (authoringMode)
        {
            case DisplayFurnitureAuthoringMode.Display:
                enableDisplayFeature = true;
                enableLightFeature = false;
                enableFrameFeature = false;
                break;

            case DisplayFurnitureAuthoringMode.Light:
                enableDisplayFeature = false;
                enableLightFeature = true;
                enableFrameFeature = false;
                break;

            case DisplayFurnitureAuthoringMode.Frame:
                enableDisplayFeature = false;
                enableLightFeature = false;
                enableFrameFeature = true;
                break;
        }

        boundsPadding = new Vector3(
            Mathf.Max(0f, boundsPadding.x),
            Mathf.Max(0f, boundsPadding.y),
            Mathf.Max(0f, boundsPadding.z));

        prefabRootTransform ??= new PrefabTransformOverrideData();
        modelContainerTransform ??= new PrefabTransformOverrideData();
        modelAssetTransform ??= new PrefabTransformOverrideData();
        placementBoundsOverride ??= new BoxColliderOverrideData();
        slotOverrides ??= new List<NamedTransformOverrideData>();
        lightOverrides ??= new List<NamedLightOverrideData>();
        frameSurfaceOverrides ??= new List<NamedFrameSurfaceOverrideData>();

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

        for (int i = 0; i < lightOverrides.Count; i++)
        {
            NamedLightOverrideData lightOverride = lightOverrides[i];
            if (lightOverride == null)
            {
                lightOverrides[i] = new NamedLightOverrideData();
                lightOverride = lightOverrides[i];
            }

            lightOverride.localScale = EnsureNonZeroScale(lightOverride.localScale);
            lightOverride.intensity = Mathf.Max(0f, lightOverride.intensity);
            lightOverride.range = Mathf.Max(0.01f, lightOverride.range);
            lightOverride.spotAngle = Mathf.Clamp(lightOverride.spotAngle, 1f, 179f);
            lightOverride.innerSpotAngle = Mathf.Clamp(lightOverride.innerSpotAngle, 0f, lightOverride.spotAngle);
        }

        for (int i = 0; i < frameSurfaceOverrides.Count; i++)
        {
            NamedFrameSurfaceOverrideData frameOverride = frameSurfaceOverrides[i];
            if (frameOverride == null)
            {
                frameSurfaceOverrides[i] = new NamedFrameSurfaceOverrideData();
                frameOverride = frameSurfaceOverrides[i];
            }

            frameOverride.localScale = EnsureNonZeroScale(frameOverride.localScale);
            frameOverride.texturePropertyName = string.IsNullOrWhiteSpace(frameOverride.texturePropertyName)
                ? "_BaseMap"
                : frameOverride.texturePropertyName;
            frameOverride.emissionColorPropertyName = string.IsNullOrWhiteSpace(frameOverride.emissionColorPropertyName)
                ? "_EmissionColor"
                : frameOverride.emissionColorPropertyName;
            frameOverride.emissionTexturePropertyName = string.IsNullOrWhiteSpace(frameOverride.emissionTexturePropertyName)
                ? "_EmissionMap"
                : frameOverride.emissionTexturePropertyName;
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

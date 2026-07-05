using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MemoryDisplayFurniture : MonoBehaviour
{
    private const float FurnitureGizmoSphereRadius = 0.035f;
    private const string PlacementBoundsObjectName = "_PlacementBounds";

    [Header("Furniture Identity")]
    [SerializeField] private string furnitureId;
    [SerializeField] private FurnitureType furnitureType = FurnitureType.Custom;

    [Header("Furniture Rules")]
    [SerializeField] private bool editableInArrangementMode = true;
    [SerializeField] private bool lockedInExploreMode = true;

    [Header("Placement Bounds")]
    [SerializeField] private BoxCollider placementBoundsCollider;
    [SerializeField] private bool usePlacementBoundsForSlots = true;
    [SerializeField] private bool createBlockingCollider = true;

    [Header("Slot References")]
    [SerializeField] private List<MemoryDisplaySlot> slots = new List<MemoryDisplaySlot>();

    [Header("Feature References")]
    [SerializeField] private List<MemoryDisplayLight> lights = new List<MemoryDisplayLight>();
    [SerializeField] private List<MemoryDisplayFrameSurface> frameSurfaces = new List<MemoryDisplayFrameSurface>();

    public string FurnitureId => furnitureId;
    public FurnitureType Type => furnitureType;
    public bool EditableInArrangementMode => editableInArrangementMode;
    public bool LockedInExploreMode => lockedInExploreMode;
    public BoxCollider PlacementBoundsCollider => placementBoundsCollider;
    public bool UsePlacementBoundsForSlots => usePlacementBoundsForSlots;
    public bool CreateBlockingCollider => createBlockingCollider;
    public IReadOnlyList<MemoryDisplaySlot> Slots => slots;
    public IReadOnlyList<MemoryDisplayLight> Lights => lights;
    public IReadOnlyList<MemoryDisplayFrameSurface> FrameSurfaces => frameSurfaces;

    public void AutoCollectSlots()
    {
        MemoryDisplaySlot[] collectedSlots = GetComponentsInChildren<MemoryDisplaySlot>(true);
        ReplaceComponentList(ref slots, collectedSlots);
    }

    public void AutoCollectLights()
    {
        MemoryDisplayLight[] collectedLights = GetComponentsInChildren<MemoryDisplayLight>(true);
        ReplaceComponentList(ref lights, collectedLights);
    }

    public void AutoCollectFrameSurfaces()
    {
        MemoryDisplayFrameSurface[] collectedFrameSurfaces = GetComponentsInChildren<MemoryDisplayFrameSurface>(true);
        ReplaceComponentList(ref frameSurfaces, collectedFrameSurfaces);
    }

    [ContextMenu("Refresh Feature References")]
    public void AutoCollectFeatures()
    {
        AutoCollectSlots();
        AutoCollectLights();
        AutoCollectFrameSurfaces();
    }

    public List<MemoryDisplaySlot> GetAvailableSlots()
    {
        if (slots == null || slots.Count == 0)
        {
            AutoCollectSlots();
        }

        List<MemoryDisplaySlot> availableSlots = new List<MemoryDisplaySlot>();
        for (int i = 0; i < slots.Count; i++)
        {
            MemoryDisplaySlot slot = slots[i];
            if (slot == null)
            {
                continue;
            }

            if (!slot.IsOccupied)
            {
                availableSlots.Add(slot);
            }
        }

        return availableSlots;
    }

    public bool HasDisplaySlots()
    {
        if (slots == null || slots.Count == 0)
        {
            AutoCollectSlots();
        }

        return slots != null && slots.Count > 0;
    }

    public bool HasLightFeatures()
    {
        if (lights == null || lights.Count == 0)
        {
            AutoCollectLights();
        }

        return lights != null && lights.Count > 0;
    }

    public bool HasFrameSurfaces()
    {
        if (frameSurfaces == null || frameSurfaces.Count == 0)
        {
            AutoCollectFrameSurfaces();
        }

        return frameSurfaces != null && frameSurfaces.Count > 0;
    }

    public MemoryDisplaySlot GetSlotById(string slotId)
    {
        if (string.IsNullOrWhiteSpace(slotId))
        {
            return null;
        }

        if (slots == null || slots.Count == 0)
        {
            AutoCollectSlots();
        }

        for (int i = 0; i < slots.Count; i++)
        {
            MemoryDisplaySlot slot = slots[i];
            if (slot == null)
            {
                continue;
            }

            if (string.Equals(slot.SlotId, slotId, StringComparison.OrdinalIgnoreCase))
            {
                return slot;
            }
        }

        return null;
    }

    public MemoryDisplayLight GetLightById(string lightId)
    {
        if (string.IsNullOrWhiteSpace(lightId))
        {
            return null;
        }

        if (lights == null || lights.Count == 0)
        {
            AutoCollectLights();
        }

        for (int i = 0; i < lights.Count; i++)
        {
            MemoryDisplayLight lightFeature = lights[i];
            if (lightFeature == null)
            {
                continue;
            }

            if (string.Equals(lightFeature.LightId, lightId, StringComparison.OrdinalIgnoreCase))
            {
                return lightFeature;
            }
        }

        return null;
    }

    public MemoryDisplayFrameSurface GetFrameSurfaceById(string surfaceId)
    {
        if (string.IsNullOrWhiteSpace(surfaceId))
        {
            return null;
        }

        if (frameSurfaces == null || frameSurfaces.Count == 0)
        {
            AutoCollectFrameSurfaces();
        }

        for (int i = 0; i < frameSurfaces.Count; i++)
        {
            MemoryDisplayFrameSurface frameSurface = frameSurfaces[i];
            if (frameSurface == null)
            {
                continue;
            }

            if (string.Equals(frameSurface.SurfaceId, surfaceId, StringComparison.OrdinalIgnoreCase))
            {
                return frameSurface;
            }
        }

        return null;
    }

    public Bounds GetPlacementBounds()
    {
        if (usePlacementBoundsForSlots)
        {
            AutoAssignPlacementBounds();
            if (placementBoundsCollider != null)
            {
                return placementBoundsCollider.bounds;
            }
        }

        if (TryGetCombinedRendererBounds(out Bounds rendererBounds))
        {
            return rendererBounds;
        }

        return new Bounds(transform.position, Vector3.zero);
    }

    public void AutoAssignPlacementBounds()
    {
        if (placementBoundsCollider != null)
        {
            return;
        }

        Transform placementBoundsTransform = transform.Find(PlacementBoundsObjectName);
        if (placementBoundsTransform != null)
        {
            placementBoundsCollider = placementBoundsTransform.GetComponent<BoxCollider>();
            if (placementBoundsCollider != null)
            {
                return;
            }
        }

        BoxCollider[] colliders = GetComponentsInChildren<BoxCollider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            BoxCollider collider = colliders[i];
            if (collider != null && string.Equals(collider.gameObject.name, PlacementBoundsObjectName, StringComparison.Ordinal))
            {
                placementBoundsCollider = collider;
                return;
            }
        }
    }

    public void AutoFitPlacementBoundsFromRenderers()
    {
        AutoAssignPlacementBounds();
        if (placementBoundsCollider == null)
        {
            return;
        }

        if (!TryGetCombinedLocalRendererBounds(out Bounds localBounds))
        {
            return;
        }

        if (!TrySetColliderToFurnitureLocalBounds(placementBoundsCollider, localBounds))
        {
            return;
        }
    }

    private void Reset()
    {
        if (string.IsNullOrWhiteSpace(furnitureId))
        {
            furnitureId = gameObject.name;
        }

        AutoAssignPlacementBounds();
        AutoCollectFeatures();
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(furnitureId))
        {
            furnitureId = gameObject.name;
        }

        AutoAssignPlacementBounds();
        EnsureFeatureListsInitialized();
        CleanupNullReferences(slots);
        CleanupNullReferences(lights);
        CleanupNullReferences(frameSurfaces);

        // Auto-rebuild newly introduced feature lists so older prefabs migrate without manual fixes.
        AutoCollectFeatures();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 1f);
        Gizmos.DrawSphere(transform.position, FurnitureGizmoSphereRadius);

        if (placementBoundsCollider != null)
        {
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = placementBoundsCollider.transform.localToWorldMatrix;
            Gizmos.color = new Color(0.2f, 1f, 0.45f, 1f);
            Gizmos.DrawWireCube(placementBoundsCollider.center, placementBoundsCollider.size);
            Gizmos.matrix = previousMatrix;
        }
        else if (TryGetCombinedRendererBounds(out Bounds bounds))
        {
            Gizmos.color = new Color(0.2f, 1f, 0.45f, 1f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }

        if (slots == null || slots.Count == 0)
        {
            AutoCollectSlots();
        }

        for (int i = 0; i < slots.Count; i++)
        {
            MemoryDisplaySlot slot = slots[i];
            if (slot == null)
            {
                continue;
            }

            Gizmos.DrawLine(transform.position, slot.transform.position);
        }

        if (lights == null || lights.Count == 0)
        {
            AutoCollectLights();
        }

        Gizmos.color = new Color(1f, 0.55f, 0.1f, 1f);
        for (int i = 0; i < lights.Count; i++)
        {
            MemoryDisplayLight lightFeature = lights[i];
            if (lightFeature == null)
            {
                continue;
            }

            Gizmos.DrawLine(transform.position, lightFeature.transform.position);
            Gizmos.DrawWireSphere(lightFeature.transform.position, FurnitureGizmoSphereRadius * 0.8f);
        }

        if (frameSurfaces == null || frameSurfaces.Count == 0)
        {
            AutoCollectFrameSurfaces();
        }

        Gizmos.color = new Color(0.25f, 0.65f, 1f, 1f);
        for (int i = 0; i < frameSurfaces.Count; i++)
        {
            MemoryDisplayFrameSurface frameSurface = frameSurfaces[i];
            if (frameSurface == null)
            {
                continue;
            }

            Gizmos.DrawLine(transform.position, frameSurface.transform.position);
            Gizmos.DrawWireCube(frameSurface.transform.position, frameSurface.transform.lossyScale * 0.1f);
        }
    }

    private void EnsureFeatureListsInitialized()
    {
        slots ??= new List<MemoryDisplaySlot>();
        lights ??= new List<MemoryDisplayLight>();
        frameSurfaces ??= new List<MemoryDisplayFrameSurface>();
    }

    private static void CleanupNullReferences<T>(List<T> references) where T : UnityEngine.Object
    {
        if (references == null)
        {
            return;
        }

        for (int i = references.Count - 1; i >= 0; i--)
        {
            if (references[i] == null)
            {
                references.RemoveAt(i);
            }
        }
    }

    private static void ReplaceComponentList<T>(ref List<T> references, T[] collected) where T : Component
    {
        if (references == null)
        {
            references = new List<T>(collected != null ? collected.Length : 0);
        }
        else
        {
            references.Clear();
        }

        if (collected == null)
        {
            return;
        }

        for (int i = 0; i < collected.Length; i++)
        {
            T component = collected[i];
            if (component != null)
            {
                references.Add(component);
            }
        }
    }

    private bool TryGetCombinedRendererBounds(out Bounds combinedBounds)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        combinedBounds = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = renderer.bounds;
                hasBounds = true;
                continue;
            }

            combinedBounds.Encapsulate(renderer.bounds);
        }

        return hasBounds;
    }

    private bool TryGetCombinedLocalRendererBounds(out Bounds localBounds)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Bounds rendererBounds = renderer.bounds;
            Vector3 center = rendererBounds.center;
            Vector3 extents = rendererBounds.extents;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 worldCorner = center + new Vector3(
                            extents.x * x,
                            extents.y * y,
                            extents.z * z);
                        Vector3 localCorner = transform.InverseTransformPoint(worldCorner);

                        if (!hasBounds)
                        {
                            min = localCorner;
                            max = localCorner;
                            hasBounds = true;
                            continue;
                        }

                        min = Vector3.Min(min, localCorner);
                        max = Vector3.Max(max, localCorner);
                    }
                }
            }
        }

        if (!hasBounds)
        {
            localBounds = default;
            return false;
        }

        localBounds = new Bounds((min + max) * 0.5f, max - min);
        return true;
    }

    private bool TrySetColliderToFurnitureLocalBounds(BoxCollider collider, Bounds furnitureLocalBounds)
    {
        if (collider == null)
        {
            return false;
        }

        Vector3[] worldCorners = new Vector3[8];
        Vector3 center = furnitureLocalBounds.center;
        Vector3 extents = furnitureLocalBounds.extents;
        int index = 0;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 localCorner = center + new Vector3(
                        extents.x * x,
                        extents.y * y,
                        extents.z * z);
                    worldCorners[index] = transform.TransformPoint(localCorner);
                    index++;
                }
            }
        }

        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;
        bool hasCorner = false;

        for (int i = 0; i < worldCorners.Length; i++)
        {
            Vector3 colliderLocalCorner = collider.transform.InverseTransformPoint(worldCorners[i]);
            if (!hasCorner)
            {
                min = colliderLocalCorner;
                max = colliderLocalCorner;
                hasCorner = true;
                continue;
            }

            min = Vector3.Min(min, colliderLocalCorner);
            max = Vector3.Max(max, colliderLocalCorner);
        }

        if (!hasCorner)
        {
            return false;
        }

        collider.center = (min + max) * 0.5f;
        collider.size = Vector3.Max(max - min, Vector3.one * 0.01f);
        return true;
    }
}

[DisallowMultipleComponent]
public class MemoryDisplayLight : MonoBehaviour
{
    [SerializeField] private string lightId;
    [SerializeField] private FurnitureLightRole lightRole = FurnitureLightRole.Decorative;
    [SerializeField] private Light targetLight;
    [SerializeField] private bool allowRuntimeAdjustment = true;

    public string LightId => string.IsNullOrWhiteSpace(lightId) ? gameObject.name : lightId;
    public FurnitureLightRole LightRole => lightRole;
    public Light TargetLight => targetLight;
    public bool AllowRuntimeAdjustment => allowRuntimeAdjustment;

    public void ApplyAuthoringData(string id, FurnitureLightRole role, Light lightComponent, bool allowAdjustment)
    {
        lightId = string.IsNullOrWhiteSpace(id) ? gameObject.name : id;
        lightRole = role;
        targetLight = lightComponent;
        allowRuntimeAdjustment = allowAdjustment;
    }

    public void AutoAssignLight()
    {
        if (targetLight != null)
        {
            return;
        }

        targetLight = GetComponent<Light>();
        if (targetLight == null)
        {
            targetLight = GetComponentInChildren<Light>(true);
        }
    }

    private void Reset()
    {
        if (string.IsNullOrWhiteSpace(lightId))
        {
            lightId = gameObject.name;
        }

        AutoAssignLight();
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(lightId))
        {
            lightId = gameObject.name;
        }

        AutoAssignLight();
    }
}

[DisallowMultipleComponent]
public class MemoryDisplayFrameSurface : MonoBehaviour
{
    [SerializeField] private string surfaceId;
    [SerializeField] private FrameSurfaceContentType contentType = FrameSurfaceContentType.MemoryItemImage;
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private bool emissiveDisplay;
    [SerializeField] private Color emissionColor = Color.white;
    [SerializeField] private string texturePropertyName = "_BaseMap";
    [SerializeField] private string emissionColorPropertyName = "_EmissionColor";
    [SerializeField] private string emissionTexturePropertyName = "_EmissionMap";

    public string SurfaceId => string.IsNullOrWhiteSpace(surfaceId) ? gameObject.name : surfaceId;
    public FrameSurfaceContentType ContentType => contentType;
    public Renderer TargetRenderer => targetRenderer;
    public bool EmissiveDisplay => emissiveDisplay;
    public Color EmissionColor => emissionColor;
    public string TexturePropertyName => texturePropertyName;
    public string EmissionColorPropertyName => emissionColorPropertyName;
    public string EmissionTexturePropertyName => emissionTexturePropertyName;

    public void ApplyAuthoringData(
        string id,
        FrameSurfaceContentType targetContentType,
        Renderer renderer,
        bool isEmissive,
        Color targetEmissionColor,
        string targetTexturePropertyName,
        string targetEmissionColorPropertyName,
        string targetEmissionTexturePropertyName)
    {
        surfaceId = string.IsNullOrWhiteSpace(id) ? gameObject.name : id;
        contentType = targetContentType;
        targetRenderer = renderer;
        emissiveDisplay = isEmissive;
        emissionColor = targetEmissionColor;
        texturePropertyName = string.IsNullOrWhiteSpace(targetTexturePropertyName) ? "_BaseMap" : targetTexturePropertyName;
        emissionColorPropertyName = string.IsNullOrWhiteSpace(targetEmissionColorPropertyName) ? "_EmissionColor" : targetEmissionColorPropertyName;
        emissionTexturePropertyName = string.IsNullOrWhiteSpace(targetEmissionTexturePropertyName) ? "_EmissionMap" : targetEmissionTexturePropertyName;
    }

    public void AutoAssignRenderer()
    {
        if (targetRenderer != null)
        {
            return;
        }

        targetRenderer = GetComponent<Renderer>();
        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>(true);
        }
    }

    private void Reset()
    {
        if (string.IsNullOrWhiteSpace(surfaceId))
        {
            surfaceId = gameObject.name;
        }

        texturePropertyName = string.IsNullOrWhiteSpace(texturePropertyName) ? "_BaseMap" : texturePropertyName;
        emissionColorPropertyName = string.IsNullOrWhiteSpace(emissionColorPropertyName) ? "_EmissionColor" : emissionColorPropertyName;
        emissionTexturePropertyName = string.IsNullOrWhiteSpace(emissionTexturePropertyName) ? "_EmissionMap" : emissionTexturePropertyName;
        AutoAssignRenderer();
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(surfaceId))
        {
            surfaceId = gameObject.name;
        }

        texturePropertyName = string.IsNullOrWhiteSpace(texturePropertyName) ? "_BaseMap" : texturePropertyName;
        emissionColorPropertyName = string.IsNullOrWhiteSpace(emissionColorPropertyName) ? "_EmissionColor" : emissionColorPropertyName;
        emissionTexturePropertyName = string.IsNullOrWhiteSpace(emissionTexturePropertyName) ? "_EmissionMap" : emissionTexturePropertyName;
        AutoAssignRenderer();
    }
}

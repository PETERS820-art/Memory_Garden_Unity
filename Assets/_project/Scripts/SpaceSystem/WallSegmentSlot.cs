using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;

public class WallSegmentSlot : MonoBehaviour
{
    private const float WallThickness = 0.4f;
    private static readonly Quaternion CornerBaseRotation = Quaternion.Euler(-90f, 180f, 90f);

    public WallSide side;
    public int segmentIndex;
    public string segmentId;
    public string overlayId;
    public Transform segmentRoot;
    public Transform overlayRoot;
    public bool allowConnection;
    public CornerPlacement cornerPlacement;
    public bool preserveOverlayTransform;
    public Vector3 preservedOverlayLocalPosition;
    public Vector3 preservedOverlayLocalEulerAngles;
    public Vector3 preservedOverlayLocalScale = Vector3.one;

#if UNITY_EDITOR
    [System.NonSerialized] private bool editorRefreshQueued;
#endif

    public void SetSegment(SpaceSegmentDefinition definition)
    {
        EnsureRoots();
        ClearSegmentChildren();
        segmentId = string.Empty;

        if (definition == null)
        {
            allowConnection = false;
            return;
        }

        segmentId = definition.segmentId;
        allowConnection = IsConnectionVariant(definition.variant);

        if (definition.prefab == null)
        {
            return;
        }

        GameObject instance = Instantiate(definition.prefab, segmentRoot, false);
        instance.name = definition.prefab.name;
        ApplySegmentTransform(instance.transform, definition);
    }

    public void ClearSegment()
    {
        EnsureRoots();
        ClearSegmentChildren();
        segmentId = string.Empty;
        allowConnection = false;
    }

    public void SetOverlay(SpaceSegmentDefinition overlayDefinition)
    {
        EnsureRoots();
        ClearChildren(overlayRoot);
        overlayId = string.Empty;

        if (overlayDefinition == null)
        {
            return;
        }

        overlayId = overlayDefinition.segmentId;
        if (overlayDefinition.prefab == null)
        {
            return;
        }

        GameObject instance = Instantiate(overlayDefinition.prefab, overlayRoot, false);
        instance.name = overlayDefinition.prefab.name;
        ApplyOverlayTransform(instance.transform, overlayDefinition);
    }

    public void ClearOverlay()
    {
        EnsureRoots();
        ClearChildren(overlayRoot);
        overlayId = string.Empty;
        preserveOverlayTransform = false;
        preservedOverlayLocalPosition = Vector3.zero;
        preservedOverlayLocalEulerAngles = Vector3.zero;
        preservedOverlayLocalScale = Vector3.one;
    }

    public void SetOverlayTransformOverride(Transform overlayTransform)
    {
        if (overlayTransform == null)
        {
            preserveOverlayTransform = false;
            preservedOverlayLocalPosition = Vector3.zero;
            preservedOverlayLocalEulerAngles = Vector3.zero;
            preservedOverlayLocalScale = Vector3.one;
            return;
        }

        preserveOverlayTransform = true;
        preservedOverlayLocalPosition = overlayTransform.localPosition;
        preservedOverlayLocalEulerAngles = overlayTransform.localEulerAngles;
        preservedOverlayLocalScale = overlayTransform.localScale;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = GetGizmoColor();
        Gizmos.DrawWireCube(transform.position, new Vector3(0.85f, 2.2f, 0.12f));
        Gizmos.DrawSphere(transform.position, 0.06f);
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

#if UNITY_EDITOR
        QueueEditorRefresh();
#endif
    }

    private void EnsureRoots()
    {
        if (segmentRoot == null)
        {
            segmentRoot = transform;
        }

        if (overlayRoot == null)
        {
            overlayRoot = GetOrCreateChild("OverlayRoot").transform;
        }
    }

    private GameObject GetOrCreateChild(string childName)
    {
        Transform child = transform.Find(childName);
        if (child != null)
        {
            return child.gameObject;
        }

        GameObject childObject = new GameObject(childName);
        childObject.transform.SetParent(transform, false);
        return childObject;
    }

    private void AssignExistingRoots()
    {
        if (segmentRoot == null || segmentRoot != transform)
        {
            segmentRoot = transform;
        }

        if (overlayRoot == null)
        {
            Transform existingOverlayRoot = transform.Find("OverlayRoot");
            if (existingOverlayRoot != null)
            {
                overlayRoot = existingOverlayRoot;
            }
        }
    }

#if UNITY_EDITOR
    private void QueueEditorRefresh()
    {
        if (editorRefreshQueued)
        {
            return;
        }

        editorRefreshQueued = true;
        EditorApplication.delayCall += ApplyEditorRefresh;
    }

    private void ApplyEditorRefresh()
    {
        editorRefreshQueued = false;

        if (this == null || Application.isPlaying)
        {
            return;
        }

        AssignExistingRoots();
        NormalizeRootTransforms();
        RefreshPlacedContent();
    }
#endif

    private void NormalizeRootTransforms()
    {
        if (segmentRoot == null || segmentRoot != transform)
        {
            segmentRoot = transform;
        }

        transform.localRotation = Quaternion.identity;

        if (overlayRoot != null)
        {
            overlayRoot.localPosition = Vector3.zero;
            overlayRoot.localRotation = Quaternion.identity;
            overlayRoot.localScale = Vector3.one;
        }
    }

    public void NormalizeSlotTransformState()
    {
        AssignExistingRoots();
        NormalizeRootTransforms();
        RefreshPlacedContent();
    }

    private void RefreshPlacedContent()
    {
        MemorySpaceBlock block = GetComponentInParent<MemorySpaceBlock>();
        SpaceSegmentKit kit = block != null ? block.segmentKit : null;

        Transform currentSegment = GetCurrentSegmentInstance();
        if (currentSegment != null && kit != null && !string.IsNullOrWhiteSpace(segmentId))
        {
            SpaceSegmentDefinition segmentDefinition = kit.GetSegment(segmentId);
            if (segmentDefinition != null)
            {
                ApplySegmentTransform(currentSegment, segmentDefinition);
            }
        }

        Transform currentOverlay = GetCurrentOverlayInstance();
        if (currentOverlay != null && kit != null && !string.IsNullOrWhiteSpace(overlayId))
        {
            SpaceSegmentDefinition overlayDefinition = kit.GetSegment(overlayId);
            if (overlayDefinition != null)
            {
                ApplyOverlayTransform(currentOverlay, overlayDefinition);
            }
        }
    }

    private Transform GetCurrentSegmentInstance()
    {
        if (segmentRoot == null)
        {
            return null;
        }

        for (int i = 0; i < segmentRoot.childCount; i++)
        {
            Transform child = segmentRoot.GetChild(i);
            if (overlayRoot != null && child == overlayRoot)
            {
                continue;
            }

            return child;
        }

        return null;
    }

    private Transform GetCurrentOverlayInstance()
    {
        if (overlayRoot == null || overlayRoot.childCount == 0)
        {
            return null;
        }

        return overlayRoot.GetChild(0);
    }

    private void ClearSegmentChildren()
    {
        if (segmentRoot == null)
        {
            return;
        }

        for (int i = segmentRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = segmentRoot.GetChild(i);
            if (overlayRoot != null && child == overlayRoot)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private static void ClearChildren(Transform root)
    {
        if (root == null)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private static bool IsConnectionVariant(SegmentVariant variant)
    {
        switch (variant)
        {
            case SegmentVariant.DoorCenter:
            case SegmentVariant.DoorLeft:
            case SegmentVariant.DoorRight:
            case SegmentVariant.Empty:
                return true;
            default:
                return false;
        }
    }

    private static bool IsDoorVariant(SegmentVariant variant)
    {
        switch (variant)
        {
            case SegmentVariant.DoorCenter:
            case SegmentVariant.DoorLeft:
            case SegmentVariant.DoorRight:
                return true;
            default:
                return false;
        }
    }

    private static Quaternion GetCornerRotation(CornerPlacement placement)
    {
        float yawDegrees;
        switch (placement)
        {
            case CornerPlacement.NorthEast:
                yawDegrees = 0f;
                break;
            case CornerPlacement.SouthEast:
                yawDegrees = 90f;
                break;
            case CornerPlacement.SouthWest:
                yawDegrees = 180f;
                break;
            case CornerPlacement.NorthWest:
                yawDegrees = -90f;
                break;
            default:
                yawDegrees = 0f;
                break;
        }

        return Quaternion.Euler(0f, yawDegrees, 0f) * CornerBaseRotation;
    }

    private static Quaternion GetSideRotation(WallSide wallSide)
    {
        switch (wallSide)
        {
            case WallSide.East:
                return Quaternion.Euler(0f, 90f, 0f);
            case WallSide.South:
                return Quaternion.Euler(0f, 180f, 0f);
            case WallSide.West:
                return Quaternion.Euler(0f, -90f, 0f);
            default:
                return Quaternion.identity;
        }
    }

    private static Quaternion GetSegmentRotation(SegmentVariant variant, CornerPlacement placement, WallSide wallSide)
    {
        if (variant == SegmentVariant.Corner)
        {
            return GetCornerRotation(placement);
        }

        Quaternion sideRotation = GetSideRotation(wallSide);

        if (IsDoorVariant(variant))
        {
            if (variant == SegmentVariant.DoorRight)
            {
                return sideRotation * Quaternion.Euler(-90f, 180f, 90f);
            }

            return sideRotation * Quaternion.Euler(-90f, 0f, 90f);
        }

        return sideRotation;
    }

    private static Quaternion GetPlacementRotation(SpaceSegmentDefinition definition, CornerPlacement placement, WallSide wallSide)
    {
        Quaternion authoringRotation = GetAuthoringPlacementRotation(definition);

        if (definition != null
            && definition.category == SegmentCategory.OpeningOverlay
            && IsDoorwayDefinition(definition))
        {
            return GetSideRotation(wallSide) * Quaternion.Euler(-90f, 0f, 90f) * authoringRotation;
        }

        return GetSegmentRotation(definition != null ? definition.variant : SegmentVariant.Default, placement, wallSide)
            * authoringRotation;
    }

    private Vector2 GetWallFaceOffset()
    {
        switch (side)
        {
            case WallSide.East:
                return new Vector2(WallThickness * 0.5f, 0f);
            case WallSide.South:
                return new Vector2(0f, -WallThickness * 0.5f);
            case WallSide.West:
                return new Vector2(-WallThickness * 0.5f, 0f);
            default:
                return new Vector2(0f, WallThickness * 0.5f);
        }
    }

    private void ApplySegmentTransform(Transform instanceTransform, SpaceSegmentDefinition definition)
    {
        if (instanceTransform == null || definition == null)
        {
            return;
        }

        instanceTransform.localScale = GetAuthoringPlacementScale(definition);

        switch (definition.category)
        {
            case SegmentCategory.Wall:
                instanceTransform.localRotation = GetPlacementRotation(definition, cornerPlacement, side);
                if (definition.variant == SegmentVariant.Corner && cornerPlacement != CornerPlacement.None)
                {
                    AlignCornerToLocalBounds(instanceTransform, cornerPlacement);
                }
                else if (definition.variant == SegmentVariant.DoorCenter)
                {
                    Vector2 wallFaceOffset = GetWallFaceOffset();
                    AlignTopToLocalBounds(instanceTransform, wallFaceOffset.x, definition.height, wallFaceOffset.y);
                }
                else
                {
                    Vector2 wallFaceOffset = GetWallFaceOffset();
                    AlignToLocalBounds(instanceTransform, wallFaceOffset.x, 0f, wallFaceOffset.y);
                }
                break;
            case SegmentCategory.OpeningOverlay:
                instanceTransform.localRotation = GetPlacementRotation(definition, cornerPlacement, side);
                Vector2 overlayFaceOffset = GetWallFaceOffset();
                Vector2 overlaySpanOffset = GetOverlaySpanOffset(definition);
                AlignToLocalBounds(
                    instanceTransform,
                    overlayFaceOffset.x + overlaySpanOffset.x,
                    0f,
                    overlayFaceOffset.y + overlaySpanOffset.y);
                break;
            default:
                instanceTransform.localPosition = Vector3.zero;
                instanceTransform.localRotation = Quaternion.identity;
                break;
        }
    }

    private void ApplyOverlayTransform(Transform instanceTransform, SpaceSegmentDefinition definition)
    {
        if (instanceTransform == null || definition == null)
        {
            return;
        }

        instanceTransform.localScale = GetAuthoringPlacementScale(definition);

        if (definition.category == SegmentCategory.OpeningOverlay || definition.category == SegmentCategory.Wall)
        {
            instanceTransform.localRotation = GetPlacementRotation(definition, cornerPlacement, side);
            Vector2 overlayFaceOffset = GetWallFaceOffset();
            if (definition.category == SegmentCategory.OpeningOverlay)
            {
                Vector2 overlaySpanOffset = GetOverlaySpanOffset(definition);
                AlignToLocalBounds(
                    instanceTransform,
                    overlayFaceOffset.x + overlaySpanOffset.x,
                    0f,
                    overlayFaceOffset.y + overlaySpanOffset.y);
                ApplyPreservedOverlayTransform(instanceTransform, definition);
            }
            else
            {
                AlignToLocalBounds(instanceTransform, overlayFaceOffset.x, 0f, overlayFaceOffset.y);
            }
            return;
        }

        instanceTransform.localPosition = Vector3.zero;
        instanceTransform.localRotation = Quaternion.identity;
    }

    private static Quaternion GetAuthoringPlacementRotation(SpaceSegmentDefinition definition)
    {
        return definition != null && definition.hasPlacementAuthoringOverride
            ? Quaternion.Euler(definition.placementAuthoringEulerAngles)
            : Quaternion.identity;
    }

    private static Vector3 GetAuthoringPlacementScale(SpaceSegmentDefinition definition)
    {
        return definition != null && definition.hasPlacementAuthoringOverride
            ? SanitizeScale(definition.placementAuthoringScale)
            : Vector3.one;
    }

    private static Vector3 SanitizeScale(Vector3 scale)
    {
        return new Vector3(
            Mathf.Approximately(scale.x, 0f) ? 1f : scale.x,
            Mathf.Approximately(scale.y, 0f) ? 1f : scale.y,
            Mathf.Approximately(scale.z, 0f) ? 1f : scale.z);
    }

    private static bool IsIdentityRotation(Quaternion rotation)
    {
        return Quaternion.Angle(rotation, Quaternion.identity) <= 0.001f;
    }

    private static bool IsUnitScale(Vector3 scale)
    {
        return Mathf.Approximately(scale.x, 1f)
            && Mathf.Approximately(scale.y, 1f)
            && Mathf.Approximately(scale.z, 1f);
    }

    private static void ApplyLegacyScaleCorrection(Transform instanceTransform, SpaceSegmentDefinition definition)
    {
        if (instanceTransform == null || definition == null)
        {
            return;
        }

        if (!TryGetLocalBounds(instanceTransform, out _, out Vector3 localSize))
        {
            return;
        }

        float correction = GetConsistentUniformScale(
            Mathf.Max(definition.sizeXZ.x, definition.sizeXZ.y),
            Mathf.Max(Mathf.Abs(localSize.x), Mathf.Abs(localSize.z)),
            definition.height,
            Mathf.Abs(localSize.y));
        if (Mathf.Approximately(correction, 1f))
        {
            return;
        }

        instanceTransform.localScale *= correction;
    }

    private static float GetConsistentUniformScale(
        float expectedPrimary,
        float measuredPrimary,
        float expectedSecondary,
        float measuredSecondary)
    {
        const float MinimumMeasuredSize = 0.0001f;
        const float RatioTolerance = 0.15f;
        const float IdentityTolerance = 0.02f;

        bool hasPrimary = expectedPrimary > MinimumMeasuredSize && measuredPrimary > MinimumMeasuredSize;
        bool hasSecondary = expectedSecondary > MinimumMeasuredSize && measuredSecondary > MinimumMeasuredSize;
        if (!hasPrimary && !hasSecondary)
        {
            return 1f;
        }

        float primaryRatio = hasPrimary ? expectedPrimary / measuredPrimary : 1f;
        float secondaryRatio = hasSecondary ? expectedSecondary / measuredSecondary : 1f;

        if (hasPrimary && hasSecondary)
        {
            float average = (primaryRatio + secondaryRatio) * 0.5f;
            if (average <= MinimumMeasuredSize)
            {
                return 1f;
            }

            if (Mathf.Abs(primaryRatio - secondaryRatio) / average > RatioTolerance)
            {
                return 1f;
            }

            return Mathf.Abs(average - 1f) <= IdentityTolerance ? 1f : average;
        }

        float singleRatio = hasPrimary ? primaryRatio : secondaryRatio;
        return Mathf.Abs(singleRatio - 1f) <= IdentityTolerance ? 1f : singleRatio;
    }

    private static void RefitBoxColliderToRenderBounds(Transform instanceTransform)
    {
        if (instanceTransform == null)
        {
            return;
        }

        BoxCollider collider = instanceTransform.GetComponent<BoxCollider>();
        if (collider == null)
        {
            return;
        }

        if (!TryGetSelfSpaceBounds(instanceTransform, out Vector3 localCenter, out Vector3 localSize))
        {
            return;
        }

        collider.center = localCenter;
        collider.size = new Vector3(
            Mathf.Max(0.01f, Mathf.Abs(localSize.x)),
            Mathf.Max(0.01f, Mathf.Abs(localSize.y)),
            Mathf.Max(0.01f, Mathf.Abs(localSize.z)));
    }

    private static bool TryGetSelfSpaceBounds(Transform rootTransform, out Vector3 localCenter, out Vector3 localSize)
    {
        localCenter = Vector3.zero;
        localSize = Vector3.zero;
        if (rootTransform == null)
        {
            return false;
        }

        Renderer[] renderers = rootTransform.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return false;
        }

        Matrix4x4 rootWorldToLocal = rootTransform.worldToLocalMatrix;
        Bounds combinedBounds = default;
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (!TryGetRendererBoundsInSelfSpace(renderers[i], rootWorldToLocal, out Bounds rendererBounds))
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = rendererBounds;
                hasBounds = true;
                continue;
            }

            combinedBounds.Encapsulate(rendererBounds.min);
            combinedBounds.Encapsulate(rendererBounds.max);
        }

        if (!hasBounds)
        {
            return false;
        }

        localCenter = combinedBounds.center;
        localSize = combinedBounds.size;
        return true;
    }

    private static bool TryGetRendererBoundsInSelfSpace(Renderer renderer, Matrix4x4 rootWorldToLocal, out Bounds bounds)
    {
        bounds = default;
        if (renderer == null)
        {
            return false;
        }

        Bounds sourceBounds;
        Matrix4x4 sourceLocalToWorld;

        if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
        {
            sourceBounds = skinnedMeshRenderer.localBounds;
            sourceLocalToWorld = skinnedMeshRenderer.transform.localToWorldMatrix;
        }
        else
        {
            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                return false;
            }

            sourceBounds = meshFilter.sharedMesh.bounds;
            sourceLocalToWorld = meshFilter.transform.localToWorldMatrix;
        }

        Vector3 min = sourceBounds.min;
        Vector3 max = sourceBounds.max;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, max.y, max.z)
        };

        Vector3 firstPoint = rootWorldToLocal.MultiplyPoint3x4(sourceLocalToWorld.MultiplyPoint3x4(corners[0]));
        bounds = new Bounds(firstPoint, Vector3.zero);
        for (int i = 1; i < corners.Length; i++)
        {
            Vector3 point = rootWorldToLocal.MultiplyPoint3x4(sourceLocalToWorld.MultiplyPoint3x4(corners[i]));
            bounds.Encapsulate(point);
        }

        return true;
    }

    private void ApplyPreservedOverlayTransform(Transform instanceTransform, SpaceSegmentDefinition definition)
    {
        if (!preserveOverlayTransform || instanceTransform == null || !IsDoorwayDefinition(definition))
        {
            return;
        }

        instanceTransform.localPosition = preservedOverlayLocalPosition;
        instanceTransform.localRotation = Quaternion.Euler(preservedOverlayLocalEulerAngles);
        instanceTransform.localScale = preservedOverlayLocalScale;
    }

    private static void AlignCornerToLocalBounds(Transform instanceTransform, CornerPlacement placement)
    {
        if (instanceTransform == null || instanceTransform.parent == null)
        {
            return;
        }

        instanceTransform.localPosition = Vector3.zero;
        if (!TryGetLocalBounds(instanceTransform, out Vector3 localCenter, out Vector3 localSize))
        {
            return;
        }

        float minX = localCenter.x - (localSize.x * 0.5f);
        float maxX = localCenter.x + (localSize.x * 0.5f);
        float minZ = localCenter.z - (localSize.z * 0.5f);
        float maxZ = localCenter.z + (localSize.z * 0.5f);
        float localMinY = localCenter.y - (localSize.y * 0.5f);

        float innerCornerX;
        float innerCornerZ;

        switch (placement)
        {
            case CornerPlacement.NorthWest:
                innerCornerX = minX + WallThickness;
                innerCornerZ = maxZ - WallThickness;
                break;
            case CornerPlacement.NorthEast:
                innerCornerX = maxX - WallThickness;
                innerCornerZ = maxZ - WallThickness;
                break;
            case CornerPlacement.SouthEast:
                innerCornerX = maxX - WallThickness;
                innerCornerZ = minZ + WallThickness;
                break;
            case CornerPlacement.SouthWest:
                innerCornerX = minX + WallThickness;
                innerCornerZ = minZ + WallThickness;
                break;
            default:
                innerCornerX = localCenter.x;
                innerCornerZ = localCenter.z;
                break;
        }

        instanceTransform.localPosition = new Vector3(
            -innerCornerX,
            -localMinY,
            -innerCornerZ);
    }

    private static void AlignTopToLocalBounds(Transform instanceTransform, float targetCenterX, float targetMaxY, float targetCenterZ)
    {
        if (instanceTransform == null || instanceTransform.parent == null)
        {
            return;
        }

        instanceTransform.localPosition = Vector3.zero;

        if (!TryGetLocalBounds(instanceTransform, out Vector3 localCenter, out Vector3 localSize))
        {
            return;
        }

        float localMaxY = localCenter.y + (localSize.y * 0.5f);
        instanceTransform.localPosition = new Vector3(
            targetCenterX - localCenter.x,
            targetMaxY - localMaxY,
            targetCenterZ - localCenter.z);
    }

    private static void AlignCenterToLocalBounds(Transform instanceTransform, float targetCenterX, float targetCenterY, float targetCenterZ)
    {
        if (instanceTransform == null || instanceTransform.parent == null)
        {
            return;
        }

        instanceTransform.localPosition = Vector3.zero;
        if (!TryGetLocalBounds(instanceTransform, out Vector3 localCenter, out Vector3 localSize))
        {
            return;
        }

        instanceTransform.localPosition = new Vector3(
            targetCenterX - localCenter.x,
            targetCenterY - localCenter.y,
            targetCenterZ - localCenter.z);
    }

    private static float GetDefinitionPlacementHeight(SpaceSegmentDefinition definition)
    {
        if (definition == null)
        {
            return 1f;
        }

        if (definition.height > 0f)
        {
            return definition.height;
        }

        if ((definition.category == SegmentCategory.OpeningOverlay || definition.category == SegmentCategory.Wall)
            && TryExtractDefinitionSize(definition, out _, out float parsedHeight))
        {
            return Mathf.Max(1f, parsedHeight);
        }

        if (definition.category == SegmentCategory.OpeningOverlay && definition.sizeXZ.y > 1f)
        {
            return definition.sizeXZ.y;
        }

        return definition.category == SegmentCategory.Wall ? 1f : Mathf.Max(1f, definition.sizeXZ.y);
    }

    private float GetDefinitionPlacementWidth(SpaceSegmentDefinition definition)
    {
        if (definition == null)
        {
            return 1f;
        }

        if (definition.sizeXZ.x > 1f)
        {
            return Mathf.Max(1f, definition.sizeXZ.x);
        }

        if (TryExtractDefinitionSize(definition, out float parsedWidth, out _))
        {
            return Mathf.Max(1f, parsedWidth);
        }

        return Mathf.Max(1f, definition.sizeXZ.x);
    }

    private Vector2 GetOverlaySpanOffset(SpaceSegmentDefinition definition)
    {
        SpaceSegmentPlacementMetadata metadata = GetComponent<SpaceSegmentPlacementMetadata>();
        if (metadata == null || metadata.record == null || definition == null)
        {
            return Vector2.zero;
        }

        float wallWidth = Mathf.Max(1f, metadata.record.footprint.x);
        float overlayWidth = GetDefinitionPlacementWidth(definition);
        float lateralOffset = (overlayWidth - wallWidth) * 0.5f;

        switch (side)
        {
            case WallSide.North:
            case WallSide.South:
                return new Vector2(lateralOffset, 0f);
            case WallSide.East:
            case WallSide.West:
                return new Vector2(0f, lateralOffset);
            default:
                return Vector2.zero;
        }
    }

    private static bool IsDoorwayDefinition(SpaceSegmentDefinition definition)
    {
        return definition != null
            && !string.IsNullOrWhiteSpace(definition.segmentId)
            && definition.segmentId.IndexOf("doorway", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool TryExtractDefinitionSize(SpaceSegmentDefinition definition, out float sizeX, out float sizeY)
    {
        sizeX = 1f;
        sizeY = 1f;
        if (definition == null || string.IsNullOrWhiteSpace(definition.segmentId))
        {
            return false;
        }

        string[] tokens = definition.segmentId.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = tokens.Length - 1; i >= 0; i--)
        {
            if (TryParseSizeToken(tokens[i], out sizeX, out sizeY))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryParseSizeToken(string token, out float sizeX, out float sizeY)
    {
        sizeX = 1f;
        sizeY = 1f;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        string[] parts = token.Split('x', 'X');
        if (parts.Length != 2)
        {
            return false;
        }

        return float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out sizeX)
            && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out sizeY);
    }

    private static void AlignToLocalBounds(Transform instanceTransform, float targetCenterX, float targetMinY, float targetCenterZ)
    {
        if (instanceTransform == null || instanceTransform.parent == null)
        {
            return;
        }

        instanceTransform.localPosition = Vector3.zero;

        if (!TryGetLocalBounds(instanceTransform, out Vector3 localCenter, out Vector3 localSize))
        {
            return;
        }

        float localMinY = localCenter.y - (localSize.y * 0.5f);
        instanceTransform.localPosition = new Vector3(
            targetCenterX - localCenter.x,
            targetMinY - localMinY,
            targetCenterZ - localCenter.z);
    }

    private static bool TryGetLocalBounds(Transform instanceTransform, out Vector3 localCenter, out Vector3 localSize)
    {
        localCenter = Vector3.zero;
        localSize = Vector3.zero;

        Renderer[] renderers = instanceTransform.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return false;
        }

        Transform parent = instanceTransform.parent;
        if (parent == null)
        {
            return false;
        }

        Matrix4x4 parentWorldToLocal = parent.worldToLocalMatrix;
        Bounds combinedBounds = default;
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (!TryGetRendererBoundsInParentSpace(renderers[i], parentWorldToLocal, out Bounds rendererBounds))
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = rendererBounds;
                hasBounds = true;
                continue;
            }

            combinedBounds.Encapsulate(rendererBounds.min);
            combinedBounds.Encapsulate(rendererBounds.max);
        }

        if (!hasBounds)
        {
            return false;
        }

        localCenter = combinedBounds.center;
        localSize = combinedBounds.size;
        return true;
    }

    private static bool TryGetRendererBoundsInParentSpace(Renderer renderer, Matrix4x4 parentWorldToLocal, out Bounds bounds)
    {
        bounds = default;
        if (renderer == null)
        {
            return false;
        }

        Bounds sourceBounds;
        Matrix4x4 sourceLocalToWorld;

        if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
        {
            sourceBounds = skinnedMeshRenderer.localBounds;
            sourceLocalToWorld = skinnedMeshRenderer.transform.localToWorldMatrix;
        }
        else
        {
            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                return TryGetFallbackRendererBoundsInParentSpace(renderer, parentWorldToLocal, out bounds);
            }

            sourceBounds = meshFilter.sharedMesh.bounds;
            sourceLocalToWorld = meshFilter.transform.localToWorldMatrix;
        }

        Vector3 min = sourceBounds.min;
        Vector3 max = sourceBounds.max;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, max.y, max.z)
        };

        Vector3 firstPoint = parentWorldToLocal.MultiplyPoint3x4(sourceLocalToWorld.MultiplyPoint3x4(corners[0]));
        bounds = new Bounds(firstPoint, Vector3.zero);
        for (int i = 1; i < corners.Length; i++)
        {
            Vector3 point = parentWorldToLocal.MultiplyPoint3x4(sourceLocalToWorld.MultiplyPoint3x4(corners[i]));
            bounds.Encapsulate(point);
        }

        return true;
    }

    private static bool TryGetFallbackRendererBoundsInParentSpace(Renderer renderer, Matrix4x4 parentWorldToLocal, out Bounds bounds)
    {
        bounds = default;
        if (renderer == null)
        {
            return false;
        }

        Bounds worldBounds = renderer.bounds;
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, max.y, max.z)
        };

        Vector3 firstPoint = parentWorldToLocal.MultiplyPoint3x4(corners[0]);
        bounds = new Bounds(firstPoint, Vector3.zero);
        for (int i = 1; i < corners.Length; i++)
        {
            bounds.Encapsulate(parentWorldToLocal.MultiplyPoint3x4(corners[i]));
        }

        return true;
    }

    private Color GetGizmoColor()
    {
        switch (side)
        {
            case WallSide.North:
                return new Color(0.35f, 0.8f, 1f, 1f);
            case WallSide.South:
                return new Color(1f, 0.65f, 0.3f, 1f);
            case WallSide.East:
                return new Color(0.45f, 1f, 0.45f, 1f);
            case WallSide.West:
                return new Color(1f, 0.45f, 0.75f, 1f);
            default:
                return Color.white;
        }
    }
}

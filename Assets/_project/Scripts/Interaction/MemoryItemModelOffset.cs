using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class MemoryItemModelOffset : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform modelRoot;
    [SerializeField] private BoxCollider targetBoxCollider;

    [Header("Transform")]
    [InspectorName("Position")]
    [SerializeField] private Vector3 modelLocalPosition = Vector3.zero;
    [InspectorName("Rotation")]
    [SerializeField] private Vector3 modelLocalEulerAngles = Vector3.zero;
    [InspectorName("Scale")]
    [SerializeField] private Vector3 modelLocalScale = Vector3.one;

    [Header("Collider Fit")]
    [SerializeField] private bool autoFitBoxCollider = true;
    [SerializeField] private Vector3 colliderCenterOffset = Vector3.zero;
    [SerializeField] private Vector3 colliderSizePadding = Vector3.zero;

    public Transform ModelRoot => modelRoot;

    private void Awake()
    {
        EnsureReferences();
        ApplyOffsetsAndCollider();
    }

    private void OnEnable()
    {
        EnsureReferences();
        ApplyOffsetsAndCollider();
    }

    private void OnValidate()
    {
        EnsureReferences();
        ApplyOffsetsAndCollider();
    }

    [ContextMenu("Apply Offsets And Refit Collider")]
    public void ApplyOffsetsAndCollider()
    {
        ApplyModelOffset();

        if (autoFitBoxCollider)
        {
            RefitBoxColliderToModel();
        }
    }

    [ContextMenu("Refit Collider To Model")]
    public void RefitBoxColliderToModel()
    {
        EnsureReferences();
        if (modelRoot == null || targetBoxCollider == null)
        {
            return;
        }

        Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            targetBoxCollider.center = colliderCenterOffset;
            targetBoxCollider.size = Vector3.one * 0.25f;
            return;
        }

        Bounds combinedBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            Renderer currentRenderer = renderers[i];
            if (currentRenderer == null)
            {
                continue;
            }

            combinedBounds.Encapsulate(currentRenderer.bounds);
        }

        Vector3 localCenter = transform.InverseTransformPoint(combinedBounds.center);
        Vector3 localSize = transform.InverseTransformVector(combinedBounds.size);
        Vector3 paddedSize = new Vector3(
            Mathf.Max(Mathf.Abs(localSize.x) + Mathf.Max(0f, colliderSizePadding.x), 0.01f),
            Mathf.Max(Mathf.Abs(localSize.y) + Mathf.Max(0f, colliderSizePadding.y), 0.01f),
            Mathf.Max(Mathf.Abs(localSize.z) + Mathf.Max(0f, colliderSizePadding.z), 0.01f));

        targetBoxCollider.center = localCenter + colliderCenterOffset;
        targetBoxCollider.size = paddedSize;
    }

    public void SetReferences(Transform newModelRoot, BoxCollider newTargetBoxCollider)
    {
        modelRoot = newModelRoot;
        targetBoxCollider = newTargetBoxCollider;
        ApplyOffsetsAndCollider();
    }

    private void ApplyModelOffset()
    {
        if (modelRoot == null)
        {
            return;
        }

        modelRoot.localPosition = modelLocalPosition;
        modelRoot.localRotation = Quaternion.Euler(modelLocalEulerAngles);
        modelRoot.localScale = new Vector3(
            Mathf.Max(modelLocalScale.x, 0.0001f),
            Mathf.Max(modelLocalScale.y, 0.0001f),
            Mathf.Max(modelLocalScale.z, 0.0001f));
    }

    private void EnsureReferences()
    {
        if (modelRoot == null)
        {
            Transform foundModel = transform.Find("Model");
            if (foundModel != null)
            {
                modelRoot = foundModel;
            }
        }

        if (targetBoxCollider == null)
        {
            targetBoxCollider = GetComponent<BoxCollider>();
        }
    }
}

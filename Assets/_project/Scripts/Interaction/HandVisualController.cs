using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class HandVisualController : MonoBehaviour
{
    [SerializeField] private Transform handModelRoot;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private Transform directionReference;
    [SerializeField] private Vector3 positionOffset = Vector3.zero;
    [SerializeField] private Vector3 rotationOffsetEuler = Vector3.zero;
    [SerializeField] private Vector3 scale = Vector3.one;
    [SerializeField] private Vector3 contentPositionOffset = Vector3.zero;
    [SerializeField] private Vector3 contentRotationOffsetEuler = Vector3.zero;
    [SerializeField] private Material handMaterial;
    [SerializeField] private bool isLeftHand = true;
    [SerializeField] private bool showHand = true;

    private Renderer[] cachedRenderers;
    private Transform cachedContentTransform;
    private Vector3 baseContentLocalPosition;
    private Quaternion baseContentLocalRotation;

    public Transform HandModelRoot => handModelRoot;
    public Vector3 PositionOffset
    {
        get => positionOffset;
        set => positionOffset = value;
    }

    public Vector3 RotationOffsetEuler
    {
        get => rotationOffsetEuler;
        set => rotationOffsetEuler = value;
    }

    public Transform DirectionReference
    {
        get => directionReference;
        set => directionReference = value;
    }

    public Vector3 ContentPositionOffset
    {
        get => contentPositionOffset;
        set => contentPositionOffset = value;
    }

    public Vector3 ContentRotationOffsetEuler
    {
        get => contentRotationOffsetEuler;
        set => contentRotationOffsetEuler = value;
    }

    public Vector3 Scale
    {
        get => scale;
        set => scale = value;
    }

    public Material HandMaterial
    {
        get => handMaterial;
        set
        {
            handMaterial = value;
            ApplyMaterialOverride();
        }
    }

    public bool IsLeftHand
    {
        get => isLeftHand;
        set => isLeftHand = value;
    }

    public bool ShowHand
    {
        get => showHand;
        set
        {
            showHand = value;
            ApplyVisualState();
        }
    }

    private void Reset()
    {
        if (transform.childCount > 0)
        {
            handModelRoot = transform.GetChild(0);
        }

        if (handModelRoot != null && handModelRoot.childCount > 0)
        {
            contentRoot = handModelRoot.GetChild(0);
        }

        CacheRenderers();
    }

    private void OnEnable()
    {
        CacheContentBaseTransform();
        CacheRenderers();
        ApplyMaterialOverride();
        ApplyVisualState();
    }

    private void LateUpdate()
    {
        ApplyVisualState();
    }

    private void OnValidate()
    {
        CacheContentBaseTransform();
        CacheRenderers();
        ApplyMaterialOverride();
        ApplyVisualState();
    }

    public void ApplyVisualState()
    {
        if (handModelRoot == null)
        {
            return;
        }

        CacheContentBaseTransform();

        handModelRoot.localPosition = positionOffset;
        handModelRoot.localRotation = GetReferenceAlignmentRotation() * Quaternion.Euler(rotationOffsetEuler);
        handModelRoot.localScale = scale;

        if (contentRoot != null)
        {
            Quaternion contentRotation = baseContentLocalRotation * Quaternion.Euler(contentRotationOffsetEuler);
            contentRoot.localRotation = contentRotation;
            contentRoot.localPosition = baseContentLocalPosition + (contentRotation * contentPositionOffset);
        }

        if (handModelRoot.gameObject.activeSelf != showHand)
        {
            handModelRoot.gameObject.SetActive(showHand);
        }
    }

    private Quaternion GetReferenceAlignmentRotation()
    {
        if (directionReference == null)
        {
            return Quaternion.identity;
        }

        Vector3 localDirection = transform.InverseTransformPoint(directionReference.position);
        if (localDirection.sqrMagnitude < 0.000001f)
        {
            return Quaternion.identity;
        }

        return Quaternion.FromToRotation(Vector3.up, localDirection.normalized);
    }

    private void CacheRenderers()
    {
        if (handModelRoot == null)
        {
            cachedRenderers = null;
            return;
        }

        Transform rendererRoot = contentRoot != null ? contentRoot : handModelRoot;
        cachedRenderers = rendererRoot.GetComponentsInChildren<Renderer>(true);
    }

    private void CacheContentBaseTransform()
    {
        if (contentRoot == null)
        {
            cachedContentTransform = null;
            baseContentLocalPosition = Vector3.zero;
            baseContentLocalRotation = Quaternion.identity;
            return;
        }

        if (cachedContentTransform == contentRoot)
        {
            return;
        }

        cachedContentTransform = contentRoot;
        Quaternion configuredContentRotation = Quaternion.Euler(contentRotationOffsetEuler);
        baseContentLocalRotation = contentRoot.localRotation * Quaternion.Inverse(configuredContentRotation);
        baseContentLocalPosition = contentRoot.localPosition - (contentRoot.localRotation * contentPositionOffset);
    }

    private void ApplyMaterialOverride()
    {
        if (handMaterial == null)
        {
            return;
        }

        if (cachedRenderers == null || cachedRenderers.Length == 0)
        {
            CacheRenderers();
        }

        if (cachedRenderers == null)
        {
            return;
        }

        foreach (var renderer in cachedRenderers)
        {
            if (renderer == null)
            {
                continue;
            }

            var sharedMaterials = renderer.sharedMaterials;
            var changed = false;

            for (var i = 0; i < sharedMaterials.Length; i++)
            {
                if (sharedMaterials[i] == handMaterial)
                {
                    continue;
                }

                sharedMaterials[i] = handMaterial;
                changed = true;
            }

            if (changed)
            {
                renderer.sharedMaterials = sharedMaterials;
            }
        }
    }
}

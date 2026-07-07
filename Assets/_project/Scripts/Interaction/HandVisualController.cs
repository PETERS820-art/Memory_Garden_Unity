using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class HandVisualController : MonoBehaviour
{
    [SerializeField] private Transform handModelRoot;
    [SerializeField] private Vector3 positionOffset = Vector3.zero;
    [SerializeField] private Vector3 rotationOffsetEuler = Vector3.zero;
    [SerializeField] private Vector3 scale = Vector3.one;
    [SerializeField] private Material handMaterial;
    [SerializeField] private bool isLeftHand = true;
    [SerializeField] private bool showHand = true;

    private Renderer[] cachedRenderers;

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

        CacheRenderers();
    }

    private void OnEnable()
    {
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

        handModelRoot.localPosition = positionOffset;
        handModelRoot.localRotation = Quaternion.Euler(rotationOffsetEuler);
        handModelRoot.localScale = scale;

        if (handModelRoot.gameObject.activeSelf != showHand)
        {
            handModelRoot.gameObject.SetActive(showHand);
        }
    }

    private void CacheRenderers()
    {
        if (handModelRoot == null)
        {
            cachedRenderers = null;
            return;
        }

        cachedRenderers = handModelRoot.GetComponentsInChildren<Renderer>(true);
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

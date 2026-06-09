using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRGrabInteractable))]
public class MemoryObject : MonoBehaviour
{
    [Header("Memory Data")]
    public string itemId;
    public string itemName;
    [TextArea]
    public string shortDescription;
    public string emotionType;

    [Header("Observe Settings")]
    public float observeRequiredTime = 2f;
    public float maxObserveAngle = 25f;

    [Header("Visual Feedback")]
    public Color highlightColor = Color.cyan;

    public bool IsHeld { get; private set; }
    public bool IsBeingObserved { get; private set; }
    public float ObserveProgress { get; private set; }

    private XRGrabInteractable grabInteractable;
    private Renderer cachedRenderer;
    private Material runtimeMaterial;
    private bool hasTriggeredWhileHeld;
    private bool hasCachedOriginalVisuals;
    private bool originalEmissionKeywordEnabled;
    private Color originalBaseColor = Color.white;
    private Color originalEmissionColor = Color.black;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        cachedRenderer = GetComponentInChildren<Renderer>();

        if (grabInteractable == null)
        {
            Debug.LogError($"[{nameof(MemoryObject)}] Missing {nameof(XRGrabInteractable)} on {name}.", this);
        }

        if (cachedRenderer == null)
        {
            Debug.LogWarning($"[{nameof(MemoryObject)}] No Renderer found on {name}. Highlight feedback will be skipped.", this);
            return;
        }

        runtimeMaterial = cachedRenderer.material;
        CacheOriginalVisuals();
    }

    private void OnEnable()
    {
        if (grabInteractable == null)
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
        }

        if (grabInteractable == null)
        {
            return;
        }

        grabInteractable.selectEntered.AddListener(OnSelectEntered);
        grabInteractable.selectExited.AddListener(OnSelectExited);
    }

    private void OnDisable()
    {
        if (grabInteractable == null)
        {
            return;
        }

        grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
        grabInteractable.selectExited.RemoveListener(OnSelectExited);
    }

    private void Update()
    {
        if (!IsHeld)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            IsBeingObserved = false;
            return;
        }

        Vector3 directionToObject = transform.position - mainCamera.transform.position;
        if (directionToObject.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        float angle = Vector3.Angle(mainCamera.transform.forward, directionToObject.normalized);

        if (angle <= maxObserveAngle)
        {
            IsBeingObserved = true;
            ObserveProgress += Time.deltaTime;

            Debug.Log(
                $"[MemoryObject] Observing {itemName} ({ObserveProgress:F2}/{observeRequiredTime:F2}s).",
                this);

            if (!hasTriggeredWhileHeld && ObserveProgress >= observeRequiredTime)
            {
                hasTriggeredWhileHeld = true;
                ObserveProgress = observeRequiredTime;

                Debug.Log($"[MemoryObject] Memory triggered for {itemName}.", this);

                if (MemoryModeManager.Instance != null)
                {
                    MemoryModeManager.Instance.EnterMemoryMode(this);
                }
                else
                {
                    Debug.LogWarning("[MemoryObject] MemoryModeManager.Instance is null.", this);
                }
            }
        }
        else
        {
            if (ObserveProgress > 0f)
            {
                Debug.Log($"[MemoryObject] Lost observation on {itemName}. Progress reset.", this);
            }

            IsBeingObserved = false;
            ObserveProgress = 0f;
        }
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        IsHeld = true;
        IsBeingObserved = false;
        ObserveProgress = 0f;
        hasTriggeredWhileHeld = false;

        Debug.Log($"[MemoryObject] Grabbed {itemName}.", this);
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        IsHeld = false;
        IsBeingObserved = false;
        ObserveProgress = 0f;
        hasTriggeredWhileHeld = false;

        Debug.Log($"[MemoryObject] Released {itemName}. Observation reset.", this);
    }

    public void SetHighlight(bool enabled)
    {
        if (cachedRenderer == null)
        {
            Debug.LogWarning($"[{nameof(MemoryObject)}] Cannot toggle highlight on {name} because no Renderer is assigned.", this);
            return;
        }

        if (runtimeMaterial == null)
        {
            runtimeMaterial = cachedRenderer.material;
        }

        if (runtimeMaterial == null)
        {
            Debug.LogWarning($"[{nameof(MemoryObject)}] Cannot toggle highlight on {name} because no runtime Material is available.", this);
            return;
        }

        if (!hasCachedOriginalVisuals)
        {
            CacheOriginalVisuals();
        }

        if (enabled)
        {
            SetMaterialColor(highlightColor);
            SetMaterialEmission(highlightColor * 1.5f, true);
            Debug.Log($"[MemoryObject] Highlight enabled for {itemName}.", this);
            return;
        }

        SetMaterialColor(originalBaseColor);
        SetMaterialEmission(originalEmissionColor, originalEmissionKeywordEnabled);
        Debug.Log($"[MemoryObject] Highlight disabled for {itemName}.", this);
    }

    private void CacheOriginalVisuals()
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        if (runtimeMaterial.HasProperty("_BaseColor"))
        {
            originalBaseColor = runtimeMaterial.GetColor("_BaseColor");
        }
        else if (runtimeMaterial.HasProperty("_Color"))
        {
            originalBaseColor = runtimeMaterial.GetColor("_Color");
        }

        if (runtimeMaterial.HasProperty("_EmissionColor"))
        {
            originalEmissionColor = runtimeMaterial.GetColor("_EmissionColor");
        }

        originalEmissionKeywordEnabled = runtimeMaterial.IsKeywordEnabled("_EMISSION");
        hasCachedOriginalVisuals = true;
    }

    private void SetMaterialColor(Color color)
    {
        if (runtimeMaterial.HasProperty("_BaseColor"))
        {
            runtimeMaterial.SetColor("_BaseColor", color);
        }

        if (runtimeMaterial.HasProperty("_Color"))
        {
            runtimeMaterial.SetColor("_Color", color);
        }
    }

    private void SetMaterialEmission(Color emissionColor, bool enableKeyword)
    {
        if (!runtimeMaterial.HasProperty("_EmissionColor"))
        {
            return;
        }

        runtimeMaterial.SetColor("_EmissionColor", emissionColor);

        if (enableKeyword)
        {
            runtimeMaterial.EnableKeyword("_EMISSION");
            return;
        }

        runtimeMaterial.DisableKeyword("_EMISSION");
    }
}

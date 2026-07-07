using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[ExecuteAlways]
[DisallowMultipleComponent]
public class XRRayVisualToggle : MonoBehaviour
{
    [SerializeField] private bool showDebugRays = false;
    [SerializeField] private List<XRInteractorLineVisual> lineVisuals = new List<XRInteractorLineVisual>();
    [SerializeField] private List<LineRenderer> lineRenderers = new List<LineRenderer>();
    [SerializeField] private List<GameObject> reticleObjects = new List<GameObject>();

    public bool ShowDebugRays
    {
        get => showDebugRays;
        set
        {
            showDebugRays = value;
            ApplyVisibility();
        }
    }

    private void Reset()
    {
        AutoAssignVisuals();
        ApplyVisibility();
    }

    private void OnEnable()
    {
        if (lineVisuals.Count == 0 && lineRenderers.Count == 0)
        {
            AutoAssignVisuals();
        }

        ApplyVisibility();
    }

    private void OnValidate()
    {
        AutoAssignVisuals();
        ApplyVisibility();
    }

    public void SetDebugRays(bool isVisible)
    {
        showDebugRays = isVisible;
        ApplyVisibility();
    }

    private void AutoAssignVisuals()
    {
        lineVisuals.Clear();
        lineRenderers.Clear();

        GetComponents(lineVisuals);
        GetComponents(lineRenderers);
    }

    private void ApplyVisibility()
    {
        for (int i = 0; i < lineVisuals.Count; i++)
        {
            XRInteractorLineVisual lineVisual = lineVisuals[i];
            if (lineVisual != null)
            {
                lineVisual.enabled = showDebugRays;
            }
        }

        for (int i = 0; i < lineRenderers.Count; i++)
        {
            LineRenderer lineRenderer = lineRenderers[i];
            if (lineRenderer != null)
            {
                lineRenderer.enabled = showDebugRays;
            }
        }

        for (int i = 0; i < reticleObjects.Count; i++)
        {
            GameObject reticleObject = reticleObjects[i];
            if (reticleObject != null && reticleObject.activeSelf != showDebugRays)
            {
                reticleObject.SetActive(showDebugRays);
            }
        }
    }
}

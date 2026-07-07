using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class MemoryItemHighlight : MonoBehaviour
{
    private const string OutlineShellName = "__MemoryItemOutlineShell";
    private const string OutlineShaderName = "MemoryGarden/Memory Item Outline";
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

    [Header("State")]
    [SerializeField] private bool isHighlighted;
    [SerializeField] private bool isArmedHighlight;

    [Header("Outline Visuals")]
    [SerializeField] private Color hoverColor = new Color(0.86f, 0.94f, 1f, 0.95f);
    [SerializeField] private Color armedColor = new Color(1f, 0.94f, 0.78f, 1f);
    [SerializeField] private float hoverIntensity = 1f;
    [SerializeField] private float armedIntensity = 1.35f;
    [SerializeField] private float hoverOutlineWidth = 0.0035f;
    [SerializeField] private float armedOutlineWidth = 0.005f;
    [SerializeField] private Renderer[] targetRenderers = new Renderer[0];
    [SerializeField] private bool useMaterialPropertyBlock = true;

    private MaterialPropertyBlock propertyBlock;
    private readonly List<OutlineShell> outlineShells = new List<OutlineShell>();
    private Material runtimeOutlineMaterial;
    private bool shellsBuilt;
    private bool hasVisibleOutline;
    private bool loggedUnsupportedHighlight;

    public bool IsHighlighted => isHighlighted;
    public bool IsArmedHighlight => isArmedHighlight;
    public Color HoverColor => hoverColor;
    public Color ArmedColor => armedColor;
    public float HoverIntensity => hoverIntensity;
    public float ArmedIntensity => armedIntensity;
    public Renderer[] TargetRenderers => targetRenderers;
    public bool UseMaterialPropertyBlock => useMaterialPropertyBlock;

    private void Awake()
    {
        EnsureRuntimeData();
        AutoAssignRenderersIfNeeded();
    }

    private void OnValidate()
    {
        EnsureRuntimeData();
        AutoAssignRenderersIfNeeded();

        if (!Application.isPlaying)
        {
            return;
        }

        ApplyCurrentHighlight();
    }

    private void OnDestroy()
    {
        if (runtimeOutlineMaterial != null)
        {
            Destroy(runtimeOutlineMaterial);
            runtimeOutlineMaterial = null;
        }
    }

    public void SetHoverHighlight(bool enabled)
    {
        isHighlighted = enabled;
        ApplyCurrentHighlight();
    }

    public void SetArmedHighlight(bool enabled)
    {
        isArmedHighlight = enabled;
        ApplyCurrentHighlight();
    }

    public void ClearHighlight()
    {
        isHighlighted = false;
        isArmedHighlight = false;
        ApplyCurrentHighlight();
    }

    private void ApplyCurrentHighlight()
    {
        EnsureRuntimeData();
        AutoAssignRenderersIfNeeded();

        bool hasHighlight = isHighlighted || isArmedHighlight;
        if (!hasHighlight)
        {
            HideOutlineShells();
            return;
        }

        if (!EnsureOutlineShells())
        {
            return;
        }

        Color activeColor = isArmedHighlight ? armedColor : hoverColor;
        float activeIntensity = isArmedHighlight ? armedIntensity : hoverIntensity;
        float activeWidth = isArmedHighlight ? armedOutlineWidth : hoverOutlineWidth;
        bool anyRendererUpdated = false;

        for (int i = 0; i < outlineShells.Count; i++)
        {
            OutlineShell shell = outlineShells[i];
            if (shell.Renderer == null)
            {
                continue;
            }

            anyRendererUpdated = true;
            shell.Renderer.enabled = true;

            if (!useMaterialPropertyBlock)
            {
                Material material = shell.Renderer.material;
                if (material == null)
                {
                    continue;
                }

                material.SetColor(OutlineColorId, GetActiveOutlineColor(activeColor, activeIntensity));
                material.SetFloat(OutlineWidthId, activeWidth);
                continue;
            }

            propertyBlock.Clear();
            propertyBlock.SetColor(OutlineColorId, GetActiveOutlineColor(activeColor, activeIntensity));
            propertyBlock.SetFloat(OutlineWidthId, activeWidth);
            shell.Renderer.SetPropertyBlock(propertyBlock);
        }

        if (!anyRendererUpdated && !loggedUnsupportedHighlight)
        {
            Debug.LogWarning(
                $"[{nameof(MemoryItemHighlight)}] {name} has no supported mesh renderers for outline highlighting.",
                this);
            loggedUnsupportedHighlight = true;
        }

        hasVisibleOutline = anyRendererUpdated;
    }

    private void HideOutlineShells()
    {
        if (!hasVisibleOutline)
        {
            return;
        }

        for (int i = 0; i < outlineShells.Count; i++)
        {
            OutlineShell shell = outlineShells[i];
            if (shell.Renderer == null)
            {
                continue;
            }

            if (useMaterialPropertyBlock)
            {
                propertyBlock.Clear();
                shell.Renderer.SetPropertyBlock(propertyBlock);
            }

            shell.Renderer.enabled = false;
        }

        hasVisibleOutline = false;
    }

    private void AutoAssignRenderersIfNeeded()
    {
        if (targetRenderers != null && targetRenderers.Length > 0)
        {
            return;
        }

        List<Renderer> renderers = new List<Renderer>();
        Renderer[] candidates = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            Renderer candidate = candidates[i];
            if (candidate == null
                || candidate is LineRenderer
                || candidate is TrailRenderer
                || candidate is ParticleSystemRenderer
                || candidate.gameObject.name.StartsWith(OutlineShellName))
            {
                continue;
            }

            renderers.Add(candidate);
        }

        targetRenderers = renderers.ToArray();
    }

    private bool EnsureOutlineShells()
    {
        if (shellsBuilt)
        {
            return outlineShells.Count > 0;
        }

        shellsBuilt = true;

        Material outlineMaterial = GetOutlineMaterial();
        if (outlineMaterial == null)
        {
            return false;
        }

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer targetRenderer = targetRenderers[i];
            if (targetRenderer == null)
            {
                continue;
            }

            if (targetRenderer is MeshRenderer meshRenderer)
            {
                MeshFilter sourceMeshFilter = meshRenderer.GetComponent<MeshFilter>();
                if (sourceMeshFilter == null || sourceMeshFilter.sharedMesh == null)
                {
                    continue;
                }

                GameObject shellObject = CreateShellObject(meshRenderer.transform);
                MeshFilter shellMeshFilter = shellObject.AddComponent<MeshFilter>();
                MeshRenderer shellRenderer = shellObject.AddComponent<MeshRenderer>();

                shellMeshFilter.sharedMesh = sourceMeshFilter.sharedMesh;
                shellRenderer.sharedMaterials = CreateMaterialArray(outlineMaterial, GetSubMeshMaterialCount(meshRenderer, sourceMeshFilter.sharedMesh));
                ConfigureShellRenderer(shellRenderer, meshRenderer.sortingLayerID, meshRenderer.sortingOrder + 1);

                outlineShells.Add(new OutlineShell(shellRenderer));
                continue;
            }

            if (targetRenderer is SkinnedMeshRenderer skinnedMeshRenderer && skinnedMeshRenderer.sharedMesh != null)
            {
                GameObject shellObject = CreateShellObject(skinnedMeshRenderer.transform);
                SkinnedMeshRenderer shellRenderer = shellObject.AddComponent<SkinnedMeshRenderer>();

                shellRenderer.sharedMesh = skinnedMeshRenderer.sharedMesh;
                shellRenderer.sharedMaterials = CreateMaterialArray(outlineMaterial, GetSubMeshMaterialCount(skinnedMeshRenderer, skinnedMeshRenderer.sharedMesh));
                shellRenderer.rootBone = skinnedMeshRenderer.rootBone;
                shellRenderer.bones = skinnedMeshRenderer.bones;
                shellRenderer.localBounds = skinnedMeshRenderer.localBounds;
                shellRenderer.updateWhenOffscreen = true;
                shellRenderer.quality = skinnedMeshRenderer.quality;
                ConfigureShellRenderer(shellRenderer, skinnedMeshRenderer.sortingLayerID, skinnedMeshRenderer.sortingOrder + 1);

                outlineShells.Add(new OutlineShell(shellRenderer));
            }
        }

        return outlineShells.Count > 0;
    }

    private Material GetOutlineMaterial()
    {
        if (runtimeOutlineMaterial != null)
        {
            return runtimeOutlineMaterial;
        }

        Shader outlineShader = Shader.Find(OutlineShaderName);
        if (outlineShader == null)
        {
            if (!loggedUnsupportedHighlight)
            {
                Debug.LogWarning(
                    $"[{nameof(MemoryItemHighlight)}] Could not find shader '{OutlineShaderName}' for {name}.",
                    this);
                loggedUnsupportedHighlight = true;
            }

            return null;
        }

        runtimeOutlineMaterial = new Material(outlineShader)
        {
            name = $"Runtime_{OutlineShellName}_{name}",
            hideFlags = HideFlags.HideAndDontSave
        };

        return runtimeOutlineMaterial;
    }

    private void EnsureRuntimeData()
    {
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
    }

    private static GameObject CreateShellObject(Transform parent)
    {
        Transform existingShell = parent.Find(OutlineShellName);
        if (existingShell != null)
        {
            existingShell.gameObject.SetActive(true);
            return existingShell.gameObject;
        }

        GameObject shellObject = new GameObject(OutlineShellName);
        shellObject.layer = parent.gameObject.layer;
        shellObject.transform.SetParent(parent, false);
        shellObject.transform.localPosition = Vector3.zero;
        shellObject.transform.localRotation = Quaternion.identity;
        shellObject.transform.localScale = Vector3.one;
        return shellObject;
    }

    private static void ConfigureShellRenderer(Renderer renderer, int sortingLayerId, int sortingOrder)
    {
        renderer.enabled = false;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.allowOcclusionWhenDynamic = false;
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = sortingOrder;
    }

    private static Material[] CreateMaterialArray(Material outlineMaterial, int count)
    {
        int safeCount = Mathf.Max(1, count);
        Material[] materials = new Material[safeCount];
        for (int i = 0; i < safeCount; i++)
        {
            materials[i] = outlineMaterial;
        }

        return materials;
    }

    private static int GetSubMeshMaterialCount(Renderer renderer, Mesh mesh)
    {
        int sourceMaterialCount = renderer.sharedMaterials != null ? renderer.sharedMaterials.Length : 0;
        int subMeshCount = mesh != null ? mesh.subMeshCount : 0;
        return Mathf.Max(1, Mathf.Max(sourceMaterialCount, subMeshCount));
    }

    private static Color GetActiveOutlineColor(Color baseColor, float intensity)
    {
        Color hdrColor = baseColor * Mathf.Max(0.01f, intensity);
        hdrColor.a = Mathf.Clamp01(baseColor.a);
        return hdrColor;
    }

    private readonly struct OutlineShell
    {
        public OutlineShell(Renderer renderer)
        {
            Renderer = renderer;
        }

        public Renderer Renderer { get; }
    }
}

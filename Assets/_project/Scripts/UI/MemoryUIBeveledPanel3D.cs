using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MemoryUIBeveledPanel3D : MonoBehaviour
{
    private const string FrostedShaderName = "MemoryGarden/UI/Frosted Glass";
    private const string FrostedShaderAssetPath = "Assets/_project/Art/Memory Materials/MemoryUIFrostedGlass.shader";
    private const string FrostedMaterialAssetPath = "Assets/_project/Art/Memory Materials/MemoryUIFrostedGlass.mat";

    [SerializeField] private RectTransform targetRect;
    [SerializeField] private Mesh overrideMesh;
    [SerializeField] private Material sharedMaterial;
    [SerializeField] private bool syncWithTarget = true;
    [SerializeField] private Vector2 sizePadding = Vector2.zero;
    [SerializeField] private float zOffset = -8f;
    [SerializeField] private float thickness = 14f;
    [SerializeField] private float cornerRadius = 36f;
    [SerializeField] private float bevelSize = 5f;
    [SerializeField] [Range(2, 12)] private int cornerSegments = 6;
    [SerializeField] private Color tintColor = new Color(0.22f, 0.22f, 0.25f, 0.24f);
    [SerializeField] private Color emissionColor = new Color(0.82f, 0.76f, 0.92f, 1f);
    [SerializeField] [Range(0f, 4f)] private float emissionIntensity = 1.01f;
    [SerializeField] [Range(0f, 24f)] private float blurPixels = 10.5f;
    [SerializeField] [Range(0f, 1f)] private float tintStrength = 0.242f;
    [SerializeField] [Range(0f, 2f)] private float backgroundInfluence = 1.711f;
    [SerializeField] [Range(0f, 2f)] private float backgroundLumaThreshold = 0.50f;
    [SerializeField] [Range(0.01f, 2f)] private float backgroundLumaKnee = 0.25f;
    [SerializeField] [Range(0f, 1.5f)] private float brightSceneAbsorption = 0.78f;
    [SerializeField] [Range(0.5f, 8f)] private float fresnelPower = 1.76f;
    [SerializeField] [Range(0f, 2f)] private float edgeStrength = 0.46f;
    [SerializeField] [Range(0f, 8f)] private float alphaSoftness = 2.78f;
    [SerializeField] [Range(0f, 0.05f)] private float refractionStrength = 0.012f;
    [SerializeField] [Range(0f, 0.05f)] private float distortionStrength = 0.0022f;
    [SerializeField] [Range(4f, 48f)] private float distortionScale = 18.5f;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh generatedMesh;
    private Material runtimeMaterial;
    private MaterialPropertyBlock propertyBlock;
    private bool warnedFallbackMaterial;
#if UNITY_EDITOR
    private bool pendingEditorSync;
    private int pendingEditorSyncFrames;
#endif

    private Vector2 lastSize;
    private Vector2 lastPivot;
    private Vector2 lastPadding;
    private float lastZOffset;
    private float lastThickness;
    private float lastCornerRadius;
    private float lastBevelSize;
    private int lastCornerSegments;
    private Mesh lastOverrideMesh;

    public void Configure(
        RectTransform rectTarget,
        float panelZOffset,
        float panelThickness,
        Vector2 panelPadding,
        float panelCornerRadius,
        float panelBevelSize,
        Material panelMaterial,
        Color panelTint,
        Color panelEmission,
        float panelEmissionIntensity)
    {
        targetRect = rectTarget;
        zOffset = panelZOffset;
        thickness = panelThickness;
        sizePadding = panelPadding;
        cornerRadius = panelCornerRadius;
        bevelSize = panelBevelSize;
        sharedMaterial = panelMaterial;
        tintColor = panelTint;
        emissionColor = panelEmission;
        emissionIntensity = panelEmissionIntensity;
        RequestSync();
    }

    public void ApplyRecommendedGlassTuning()
    {
        zOffset = -8f;
        thickness = 14f;
        cornerRadius = 36f;
        bevelSize = 5f;
        cornerSegments = 6;
        emissionIntensity = 1.01f;
        blurPixels = 10.5f;
        tintStrength = 0.242f;
        backgroundInfluence = 1.711f;
        backgroundLumaThreshold = 0.50f;
        backgroundLumaKnee = 0.25f;
        brightSceneAbsorption = 0.78f;
        fresnelPower = 1.76f;
        edgeStrength = 0.46f;
        alphaSoftness = 2.78f;
        refractionStrength = 0.012f;
        distortionStrength = 0.0022f;
        distortionScale = 18.5f;

        if (tintColor.a > 0.08f)
        {
            tintColor = new Color(tintColor.r, tintColor.g, tintColor.b, 0.08f);
        }

        RequestSync();
    }

    private void Reset()
    {
        targetRect = transform.parent as RectTransform;
        RequestSync();
    }

    private void OnEnable()
    {
        RequestSync();
    }

    private void OnValidate()
    {
        RequestSync();
    }

    private void LateUpdate()
    {
        if (!syncWithTarget)
        {
            return;
        }

        SyncNow();
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR
        EditorApplication.update -= DelayedEditorSync;
#endif

        if (generatedMesh != null)
        {
            if (Application.isPlaying)
            {
                Destroy(generatedMesh);
            }
            else
            {
                DestroyImmediate(generatedMesh);
            }
        }

        if (runtimeMaterial != null)
        {
            if (Application.isPlaying)
            {
                Destroy(runtimeMaterial);
            }
            else
            {
                DestroyImmediate(runtimeMaterial);
            }
        }
    }

    private void RequestSync()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            QueueEditorSync();
            return;
        }
#endif

        SyncNow();
    }

#if UNITY_EDITOR
    public void QueueSync()
    {
        if (Application.isPlaying)
        {
            SyncNow();
            return;
        }

        QueueEditorSync();
    }

    private void QueueEditorSync()
    {
        pendingEditorSync = true;
        pendingEditorSyncFrames = Mathf.Max(pendingEditorSyncFrames, 2);
        EditorApplication.update -= DelayedEditorSync;
        EditorApplication.update += DelayedEditorSync;
    }
#endif

#if UNITY_EDITOR
    private void DelayedEditorSync()
    {
        if (this == null)
        {
            EditorApplication.update -= DelayedEditorSync;
            return;
        }

        if (pendingEditorSyncFrames > 0)
        {
            pendingEditorSyncFrames--;
            return;
        }

        pendingEditorSync = false;
        EditorApplication.update -= DelayedEditorSync;

        if (this == null || !isActiveAndEnabled)
        {
            return;
        }

        SyncNow();
    }
#endif

    public void SyncNow()
    {
        EnsureComponents();

        if (targetRect == null)
        {
            targetRect = transform.parent as RectTransform;
        }

        if (targetRect == null)
        {
            return;
        }

        Vector2 size = targetRect.rect.size + sizePadding;
        size.x = Mathf.Max(4f, size.x);
        size.y = Mathf.Max(4f, size.y);

        if (NeedsRebuild(size))
        {
            RebuildMesh(size);
        }

        ApplyTransform(size);
        ApplyMaterial();
    }

    private bool NeedsRebuild(Vector2 size)
    {
        return meshFilter == null
            || meshFilter.sharedMesh == null
            || size != lastSize
            || targetRect.pivot != lastPivot
            || sizePadding != lastPadding
            || !Mathf.Approximately(zOffset, lastZOffset)
            || !Mathf.Approximately(thickness, lastThickness)
            || !Mathf.Approximately(cornerRadius, lastCornerRadius)
            || !Mathf.Approximately(bevelSize, lastBevelSize)
            || cornerSegments != lastCornerSegments
            || overrideMesh != lastOverrideMesh;
    }

    private void EnsureComponents()
    {
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
    }

    private void RebuildMesh(Vector2 size)
    {
        if (overrideMesh != null)
        {
            if (generatedMesh != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(generatedMesh);
                }
                else
                {
                    DestroyImmediate(generatedMesh);
                }

                generatedMesh = null;
            }

            meshFilter.sharedMesh = overrideMesh;
        }
        else
        {
            if (generatedMesh != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(generatedMesh);
                }
                else
                {
                    DestroyImmediate(generatedMesh);
                }
            }

            generatedMesh = BuildBeveledRoundedPanelMesh(
                size.x,
                size.y,
                Mathf.Max(1f, thickness),
                cornerRadius,
                bevelSize,
                Mathf.Max(2, cornerSegments));
            generatedMesh.name = "MemoryUIBeveledPanel3D_Generated";
            generatedMesh.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            meshFilter.sharedMesh = generatedMesh;
        }

        lastSize = size;
        lastPivot = targetRect.pivot;
        lastPadding = sizePadding;
        lastZOffset = zOffset;
        lastThickness = thickness;
        lastCornerRadius = cornerRadius;
        lastBevelSize = bevelSize;
        lastCornerSegments = cornerSegments;
        lastOverrideMesh = overrideMesh;
    }

    private void ApplyTransform(Vector2 size)
    {
        float centerX = (0.5f - targetRect.pivot.x) * size.x;
        float centerY = (0.5f - targetRect.pivot.y) * size.y;
        transform.localPosition = new Vector3(centerX, centerY, zOffset);
        transform.localRotation = Quaternion.identity;

        if (overrideMesh != null && overrideMesh.bounds.size.sqrMagnitude > 0.0001f)
        {
            Vector3 boundsSize = overrideMesh.bounds.size;
            transform.localScale = new Vector3(
                boundsSize.x > 0.0001f ? size.x / boundsSize.x : 1f,
                boundsSize.y > 0.0001f ? size.y / boundsSize.y : 1f,
                boundsSize.z > 0.0001f ? Mathf.Max(1f, thickness) / boundsSize.z : 1f);
        }
        else
        {
            transform.localScale = Vector3.one;
        }
    }

    private void ApplyMaterial()
    {
        Material materialToUse = sharedMaterial;
#if UNITY_EDITOR
        if (materialToUse == null && !Application.isPlaying)
        {
            materialToUse = EnsureEditorSharedMaterial();
            if (materialToUse != null)
            {
                sharedMaterial = materialToUse;
            }
        }
#endif

        if (materialToUse == null)
        {
            if (runtimeMaterial == null)
            {
                Shader shader = Shader.Find(FrostedShaderName);
                if (shader == null)
                {
                    shader = Shader.Find("Universal Render Pipeline/Lit");
                }

                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                runtimeMaterial = new Material(shader);
                runtimeMaterial.name = "MemoryUIBeveledPanel3D_Runtime";
                runtimeMaterial.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                ConfigureFallbackMaterial(runtimeMaterial);

                if (shader.name != FrostedShaderName && !warnedFallbackMaterial)
                {
                    warnedFallbackMaterial = true;
                    Debug.LogWarning($"[MemoryUIBeveledPanel3D] Could not resolve '{FrostedShaderName}' for '{name}'. Falling back to '{shader.name}'.");
                }
            }

            materialToUse = runtimeMaterial;
        }

        if (meshRenderer.sharedMaterial != materialToUse)
        {
            meshRenderer.sharedMaterial = materialToUse;
        }

        propertyBlock.Clear();
        propertyBlock.SetColor("_BaseColor", tintColor);
        propertyBlock.SetColor("_Color", tintColor);
        propertyBlock.SetColor("_EdgeColor", emissionColor * Mathf.Max(0.15f, emissionIntensity * 1.5f));
        propertyBlock.SetColor("_EmissionColor", emissionColor * Mathf.Max(0f, emissionIntensity));
        propertyBlock.SetFloat("_BlurPixels", blurPixels);
        propertyBlock.SetFloat("_TintStrength", tintStrength);
        propertyBlock.SetFloat("_BackgroundInfluence", backgroundInfluence);
        propertyBlock.SetFloat("_BackgroundLumaThreshold", backgroundLumaThreshold);
        propertyBlock.SetFloat("_BackgroundLumaKnee", backgroundLumaKnee);
        propertyBlock.SetFloat("_BrightSceneAbsorption", brightSceneAbsorption);
        propertyBlock.SetFloat("_FresnelPower", fresnelPower);
        propertyBlock.SetFloat("_EdgeStrength", edgeStrength);
        propertyBlock.SetFloat("_AlphaSoftness", alphaSoftness);
        propertyBlock.SetFloat("_RefractionStrength", refractionStrength);
        propertyBlock.SetFloat("_NoiseStrength", distortionStrength);
        propertyBlock.SetFloat("_NoiseScale", distortionScale);
        meshRenderer.SetPropertyBlock(propertyBlock);
    }

    private static void ConfigureFallbackMaterial(Material material)
    {
        material.renderQueue = (int)RenderQueue.Transparent;

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", (float)CullMode.Off);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.92f);
        }

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0.03f);
        }

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_EMISSION");
    }

#if UNITY_EDITOR
    private static Material EnsureEditorSharedMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(FrostedMaterialAssetPath);
        if (material != null)
        {
            return material;
        }

        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(FrostedShaderAssetPath);
        if (shader == null)
        {
            shader = Shader.Find(FrostedShaderName);
        }

        if (shader == null)
        {
            return null;
        }

        material = new Material(shader)
        {
            name = "MemoryUIFrostedGlass"
        };

        ConfigureFrostedMaterialDefaults(material);
        AssetDatabase.CreateAsset(material, FrostedMaterialAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return AssetDatabase.LoadAssetAtPath<Material>(FrostedMaterialAssetPath);
    }
#endif

    private static void ConfigureFrostedMaterialDefaults(Material material)
    {
        if (material == null)
        {
            return;
        }

        material.renderQueue = (int)RenderQueue.Transparent;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", new Color(0.22f, 0.22f, 0.26f, 0.08f));
        }

        if (material.HasProperty("_EdgeColor"))
        {
            material.SetColor("_EdgeColor", new Color(0.82f, 0.76f, 0.92f, 1f));
        }

        if (material.HasProperty("_SpecularColor"))
        {
            material.SetColor("_SpecularColor", new Color(1f, 0.97f, 0.94f, 1f));
        }

        if (material.HasProperty("_ReflectionColor"))
        {
            material.SetColor("_ReflectionColor", new Color(0.92f, 0.94f, 1f, 1f));
        }

        SetFloatIfPresent(material, "_BlurPixels", 10.5f);
        SetFloatIfPresent(material, "_TintStrength", 0.242f);
        SetFloatIfPresent(material, "_BackgroundInfluence", 1.711f);
        SetFloatIfPresent(material, "_BackgroundLumaThreshold", 0.50f);
        SetFloatIfPresent(material, "_BackgroundLumaKnee", 0.25f);
        SetFloatIfPresent(material, "_BrightSceneAbsorption", 0.78f);
        SetFloatIfPresent(material, "_FresnelPower", 1.76f);
        SetFloatIfPresent(material, "_EdgeStrength", 0.46f);
        SetFloatIfPresent(material, "_AlphaSoftness", 2.78f);
        SetFloatIfPresent(material, "_SpecularStrength", 0.45f);
        SetFloatIfPresent(material, "_SpecularPower", 48f);
        SetFloatIfPresent(material, "_ReflectionStrength", 0.28f);
        SetFloatIfPresent(material, "_RefractionStrength", 0.012f);
        SetFloatIfPresent(material, "_NoiseStrength", 0.0022f);
        SetFloatIfPresent(material, "_NoiseScale", 18.5f);
        SetFloatIfPresent(material, "_DebugView", 0f);
    }

    private static void SetFloatIfPresent(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static Mesh BuildBeveledRoundedPanelMesh(
        float width,
        float height,
        float depth,
        float cornerRadiusValue,
        float bevel,
        int segments)
    {
        float clampedDepth = Mathf.Max(1f, depth);
        float maxRadius = Mathf.Max(0f, Mathf.Min(width, height) * 0.5f - 0.5f);
        float clampedRadius = Mathf.Clamp(cornerRadiusValue, 0f, maxRadius);
        float maxInset = Mathf.Max(0.5f, Mathf.Min(clampedRadius - 0.25f, Mathf.Min(width, height) * 0.25f));
        float clampedBevel = Mathf.Clamp(bevel, 0.5f, maxInset);

        float innerWidth = Mathf.Max(2f, width - (clampedBevel * 2f));
        float innerHeight = Mathf.Max(2f, height - (clampedBevel * 2f));
        float innerRadius = Mathf.Max(0f, clampedRadius - clampedBevel);

        List<Vector2> outerLoop = BuildRoundedRectLoop(width, height, clampedRadius, segments);
        List<Vector2> innerLoop = BuildRoundedRectLoop(innerWidth, innerHeight, innerRadius, segments);

        int loopCount = outerLoop.Count;
        List<Vector3> vertices = new List<Vector3>(loopCount * 4 + 2);
        List<Vector2> uvs = new List<Vector2>(loopCount * 4 + 2);
        List<int> triangles = new List<int>(loopCount * 24);

        float frontInnerZ = clampedDepth * 0.5f;
        float frontOuterZ = frontInnerZ - clampedBevel;
        float backOuterZ = -frontInnerZ + clampedBevel;
        float backInnerZ = -frontInnerZ;

        int frontCenter = AddVertex(vertices, uvs, Vector3.forward * frontInnerZ, width, height);
        int frontInnerStart = AddLoop(vertices, uvs, innerLoop, frontInnerZ, width, height);
        int frontOuterStart = AddLoop(vertices, uvs, outerLoop, frontOuterZ, width, height);
        int backOuterStart = AddLoop(vertices, uvs, outerLoop, backOuterZ, width, height);
        int backInnerStart = AddLoop(vertices, uvs, innerLoop, backInnerZ, width, height);
        int backCenter = AddVertex(vertices, uvs, Vector3.back * frontInnerZ, width, height);

        AddFan(triangles, frontCenter, frontInnerStart, loopCount, false);
        AddStrip(triangles, frontInnerStart, frontOuterStart, loopCount, false);
        AddStrip(triangles, frontOuterStart, backOuterStart, loopCount, false);
        AddStrip(triangles, backOuterStart, backInnerStart, loopCount, false);
        AddFan(triangles, backCenter, backInnerStart, loopCount, true);

        Mesh mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static List<Vector2> BuildRoundedRectLoop(float width, float height, float radius, int segments)
    {
        List<Vector2> points = new List<Vector2>();

        if (radius <= 0.01f)
        {
            points.Add(new Vector2(width * 0.5f, height * 0.5f));
            points.Add(new Vector2(-width * 0.5f, height * 0.5f));
            points.Add(new Vector2(-width * 0.5f, -height * 0.5f));
            points.Add(new Vector2(width * 0.5f, -height * 0.5f));
            return points;
        }

        Vector2 topRight = new Vector2((width * 0.5f) - radius, (height * 0.5f) - radius);
        Vector2 topLeft = new Vector2((-width * 0.5f) + radius, (height * 0.5f) - radius);
        Vector2 bottomLeft = new Vector2((-width * 0.5f) + radius, (-height * 0.5f) + radius);
        Vector2 bottomRight = new Vector2((width * 0.5f) - radius, (-height * 0.5f) + radius);

        AddArc(points, topRight, radius, 0f, 90f, segments, false);
        AddArc(points, topLeft, radius, 90f, 180f, segments, true);
        AddArc(points, bottomLeft, radius, 180f, 270f, segments, true);
        AddArc(points, bottomRight, radius, 270f, 360f, segments, true);
        return points;
    }

    private static void AddArc(List<Vector2> points, Vector2 center, float radius, float startAngle, float endAngle, int segments, bool skipFirst)
    {
        int startIndex = skipFirst ? 1 : 0;
        for (int i = startIndex; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(startAngle, endAngle, t) * Mathf.Deg2Rad;
            points.Add(new Vector2(
                center.x + (Mathf.Cos(angle) * radius),
                center.y + (Mathf.Sin(angle) * radius)));
        }
    }

    private static int AddLoop(List<Vector3> vertices, List<Vector2> uvs, List<Vector2> loop, float z, float fullWidth, float fullHeight)
    {
        int start = vertices.Count;
        for (int i = 0; i < loop.Count; i++)
        {
            AddVertex(vertices, uvs, new Vector3(loop[i].x, loop[i].y, z), fullWidth, fullHeight);
        }

        return start;
    }

    private static int AddVertex(List<Vector3> vertices, List<Vector2> uvs, Vector3 position, float fullWidth, float fullHeight)
    {
        vertices.Add(position);
        uvs.Add(new Vector2(
            Mathf.InverseLerp(-fullWidth * 0.5f, fullWidth * 0.5f, position.x),
            Mathf.InverseLerp(-fullHeight * 0.5f, fullHeight * 0.5f, position.y)));
        return vertices.Count - 1;
    }

    private static void AddFan(List<int> triangles, int centerIndex, int ringStart, int count, bool reverse)
    {
        for (int i = 0; i < count; i++)
        {
            int current = ringStart + i;
            int next = ringStart + ((i + 1) % count);

            if (reverse)
            {
                triangles.Add(centerIndex);
                triangles.Add(next);
                triangles.Add(current);
            }
            else
            {
                triangles.Add(centerIndex);
                triangles.Add(current);
                triangles.Add(next);
            }
        }
    }

    private static void AddStrip(List<int> triangles, int loopAStart, int loopBStart, int count, bool reverse)
    {
        for (int i = 0; i < count; i++)
        {
            int a0 = loopAStart + i;
            int a1 = loopAStart + ((i + 1) % count);
            int b0 = loopBStart + i;
            int b1 = loopBStart + ((i + 1) % count);

            if (reverse)
            {
                triangles.Add(a0);
                triangles.Add(b1);
                triangles.Add(b0);
                triangles.Add(a0);
                triangles.Add(a1);
                triangles.Add(b1);
            }
            else
            {
                triangles.Add(a0);
                triangles.Add(b0);
                triangles.Add(b1);
                triangles.Add(a0);
                triangles.Add(b1);
                triangles.Add(a1);
            }
        }
    }
}

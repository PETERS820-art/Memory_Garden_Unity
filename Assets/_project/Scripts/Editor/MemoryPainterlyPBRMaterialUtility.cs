#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

internal static class MemoryPainterlyPBRMaterialUtility
{
    internal const string ShaderName = "MemoryGarden/Memory Painterly PBR";

    internal enum SurfaceType
    {
        Opaque = 0,
        Transparent = 1
    }

    internal enum BlendMode
    {
        Alpha = 0,
        Premultiply = 1,
        Multiply = 2
    }

    internal static bool IsPainterlyPbrShader(Shader shader)
    {
        return shader != null && string.Equals(shader.name, ShaderName, StringComparison.Ordinal);
    }

    internal static float InferSurfaceType(Material material)
    {
        if (material == null)
        {
            return (float)SurfaceType.Opaque;
        }

        if (material.HasProperty("_Surface"))
        {
            return material.GetFloat("_Surface");
        }

        string renderType = material.GetTag("RenderType", false, string.Empty);
        if (string.Equals(renderType, "Transparent", StringComparison.OrdinalIgnoreCase))
        {
            return (float)SurfaceType.Transparent;
        }

        if (material.renderQueue >= (int)RenderQueue.Transparent)
        {
            return (float)SurfaceType.Transparent;
        }

        float srcBlend = GetFloat(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        float dstBlend = GetFloat(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        if (!Mathf.Approximately(srcBlend, (float)UnityEngine.Rendering.BlendMode.One)
            || !Mathf.Approximately(dstBlend, (float)UnityEngine.Rendering.BlendMode.Zero))
        {
            return (float)SurfaceType.Transparent;
        }

        return (float)SurfaceType.Opaque;
    }

    internal static float InferBlendMode(Material material)
    {
        if (material == null)
        {
            return (float)BlendMode.Alpha;
        }

        if (material.HasProperty("_Blend"))
        {
            return material.GetFloat("_Blend");
        }

        float srcBlend = GetFloat(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        float dstBlend = GetFloat(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);

        if (Mathf.Approximately(srcBlend, (float)UnityEngine.Rendering.BlendMode.DstColor)
            && Mathf.Approximately(dstBlend, (float)UnityEngine.Rendering.BlendMode.Zero))
        {
            return (float)BlendMode.Multiply;
        }

        if (Mathf.Approximately(srcBlend, (float)UnityEngine.Rendering.BlendMode.One)
            && Mathf.Approximately(dstBlend, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha))
        {
            return (float)BlendMode.Premultiply;
        }

        return (float)BlendMode.Alpha;
    }

    internal static int InferQueueOffset(Material material, float surfaceValue, float alphaClipValue)
    {
        if (material == null || material.renderQueue < 0)
        {
            return 0;
        }

        return material.renderQueue - GetBaseQueue(surfaceValue, alphaClipValue);
    }

    internal static void SetupMaterial(Material material)
    {
        if (material == null || !IsPainterlyPbrShader(material.shader))
        {
            return;
        }

        float surfaceValue = GetFloat(material, "_Surface", (float)SurfaceType.Opaque);
        float blendValue = GetFloat(material, "_Blend", (float)BlendMode.Alpha);
        float alphaClipValue = GetFloat(material, "_AlphaClip", 0f);
        float queueOffsetValue = GetFloat(material, "_QueueOffset", 0f);

        SurfaceType surfaceType = surfaceValue >= 0.5f ? SurfaceType.Transparent : SurfaceType.Opaque;
        BlendMode blendMode = ResolveBlendMode(blendValue);
        bool alphaClip = alphaClipValue >= 0.5f;
        bool transparent = surfaceType == SurfaceType.Transparent;
        bool premultiply = transparent && blendMode == BlendMode.Premultiply;
        bool multiply = transparent && blendMode == BlendMode.Multiply;

        SetFloatIfPresent(material, "_SrcBlend", transparent
            ? (multiply
                ? (float)UnityEngine.Rendering.BlendMode.DstColor
                : (premultiply ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.SrcAlpha))
            : (float)UnityEngine.Rendering.BlendMode.One);
        SetFloatIfPresent(material, "_DstBlend", transparent
            ? (multiply
                ? (float)UnityEngine.Rendering.BlendMode.Zero
                : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha)
            : (float)UnityEngine.Rendering.BlendMode.Zero);
        SetFloatIfPresent(material, "_ZWrite", transparent ? 0f : 1f);

        int baseQueue = GetBaseQueue(surfaceValue, alphaClipValue);
        material.renderQueue = baseQueue + Mathf.RoundToInt(queueOffsetValue);
        material.SetOverrideTag("RenderType", transparent ? "Transparent" : (alphaClip ? "TransparentCutout" : "Opaque"));

        SetKeyword(material, "_ALPHATEST_ON", alphaClip);
        SetKeyword(material, "_ALPHAPREMULTIPLY_ON", premultiply);
        SetKeyword(material, "_ALPHAMODULATE_ON", multiply);
        SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", transparent);
        SetKeyword(material, "_NORMALMAP", material.GetTexture("_BumpMap") != null);
        SetKeyword(material, "_METALLICSPECGLOSSMAP", material.GetTexture("_MetallicGlossMap") != null);
        SetKeyword(material, "_OCCLUSIONMAP", material.GetTexture("_OcclusionMap") != null);

        Color emissionColor = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
        bool hasEmission = material.GetTexture("_EmissionMap") != null || emissionColor.maxColorComponent > 0.0001f;
        SetKeyword(material, "_EMISSION", hasEmission);

        if (hasEmission)
        {
            material.globalIlluminationFlags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }
        else
        {
            material.globalIlluminationFlags |= MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }

        bool enableDepthLikePasses = !transparent || alphaClip;
        material.SetShaderPassEnabled("ShadowCaster", enableDepthLikePasses);
        material.SetShaderPassEnabled("DepthOnly", enableDepthLikePasses);
    }

    private static int GetBaseQueue(float surfaceValue, float alphaClipValue)
    {
        bool transparent = surfaceValue >= 0.5f;
        bool alphaClip = alphaClipValue >= 0.5f;

        if (transparent)
        {
            return (int)RenderQueue.Transparent;
        }

        return alphaClip ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
    }

    private static float GetFloat(Material material, string propertyName, float fallback)
    {
        return material != null && material.HasProperty(propertyName) ? material.GetFloat(propertyName) : fallback;
    }

    private static BlendMode ResolveBlendMode(float blendValue)
    {
        if (blendValue >= 1.5f)
        {
            return BlendMode.Multiply;
        }

        if (blendValue >= 0.5f)
        {
            return BlendMode.Premultiply;
        }

        return BlendMode.Alpha;
    }

    private static void SetFloatIfPresent(Material material, string propertyName, float value)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static void SetKeyword(Material material, string keyword, bool enabled)
    {
        if (material == null || string.IsNullOrWhiteSpace(keyword))
        {
            return;
        }

        if (enabled)
        {
            material.EnableKeyword(keyword);
        }
        else
        {
            material.DisableKeyword(keyword);
        }
    }
}
#endif

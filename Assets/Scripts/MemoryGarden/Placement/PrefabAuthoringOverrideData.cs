using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PrefabTransformOverrideData
{
    public bool enabled;
    public Vector3 localPosition = Vector3.zero;
    public Vector3 localEulerAngles = Vector3.zero;
    public Vector3 localScale = Vector3.one;
}

[Serializable]
public class NamedTransformOverrideData
{
    public string id;
    public Vector3 localPosition = Vector3.zero;
    public Vector3 localEulerAngles = Vector3.zero;
    public Vector3 localScale = Vector3.one;
}

[Serializable]
public class BoxColliderOverrideData
{
    public bool enabled;
    public Vector3 localPosition = Vector3.zero;
    public Vector3 localEulerAngles = Vector3.zero;
    public Vector3 localScale = Vector3.one;
    public Vector3 center = Vector3.zero;
    public Vector3 size = Vector3.one;
}

[Serializable]
public class NamedLightOverrideData
{
    public string id;
    public FurnitureLightRole lightRole = FurnitureLightRole.Decorative;
    public bool allowRuntimeAdjustment = true;
    public bool gameObjectActive = true;
    public bool lightEnabled = true;
    public Vector3 localPosition = Vector3.zero;
    public Vector3 localEulerAngles = Vector3.zero;
    public Vector3 localScale = Vector3.one;
    public LightType lightType = LightType.Point;
    public Color color = Color.white;
    public float intensity = 1f;
    public float range = 10f;
    public float spotAngle = 30f;
    public float innerSpotAngle = 21.8f;
    public LightShadows shadows = LightShadows.None;
}

[Serializable]
public class NamedFrameSurfaceOverrideData
{
    public string id;
    public FrameSurfaceContentType contentType = FrameSurfaceContentType.MemoryItemImage;
    public bool createQuadIfMissing = true;
    public bool gameObjectActive = true;
    public bool rendererEnabled = true;
    public Vector3 localPosition = Vector3.zero;
    public Vector3 localEulerAngles = Vector3.zero;
    public Vector3 localScale = Vector3.one;
    public bool emissiveDisplay;
    public Color emissionColor = Color.white;
    public string texturePropertyName = "_BaseMap";
    public string emissionColorPropertyName = "_EmissionColor";
    public string emissionTexturePropertyName = "_EmissionMap";
}

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

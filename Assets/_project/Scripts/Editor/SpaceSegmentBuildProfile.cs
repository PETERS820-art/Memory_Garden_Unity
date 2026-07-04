using UnityEngine;

[CreateAssetMenu(
    fileName = "SpaceSegmentBuildProfile",
    menuName = "Memory Garden/Space Segment Build Profile")]
public class SpaceSegmentBuildProfile : ScriptableObject
{
    public string segmentId;
    public PrefabTransformOverrideData prefabRootTransform = new PrefabTransformOverrideData();
    public PrefabTransformOverrideData modelContainerTransform = new PrefabTransformOverrideData();
    public PrefabTransformOverrideData modelAssetTransform = new PrefabTransformOverrideData();

    private void OnValidate()
    {
        prefabRootTransform ??= new PrefabTransformOverrideData();
        modelContainerTransform ??= new PrefabTransformOverrideData();
        modelAssetTransform ??= new PrefabTransformOverrideData();

        prefabRootTransform.localScale = EnsureNonZeroScale(prefabRootTransform.localScale);
        modelContainerTransform.localScale = EnsureNonZeroScale(modelContainerTransform.localScale);
        modelAssetTransform.localScale = EnsureNonZeroScale(modelAssetTransform.localScale);
    }

    private static Vector3 EnsureNonZeroScale(Vector3 value)
    {
        return new Vector3(
            Mathf.Approximately(value.x, 0f) ? 1f : value.x,
            Mathf.Approximately(value.y, 0f) ? 1f : value.y,
            Mathf.Approximately(value.z, 0f) ? 1f : value.z);
    }
}

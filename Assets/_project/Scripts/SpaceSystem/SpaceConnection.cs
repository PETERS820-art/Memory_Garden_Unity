using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SpaceConnection : MonoBehaviour
{
    public SpaceOpeningPort portA;
    public SpaceOpeningPort portB;
    public bool isPreview;
    public bool autoAlignedBlockB;
    public float connectorWidth;
    public float connectorHeight;
    public float connectorLength;
    public MemorySpaceBlock mergedBlockB;
    public Transform mergedBlockBOriginalParent;
    public int mergedBlockBOriginalSiblingIndex = -1;

    public void Bind(
        SpaceOpeningPort sourcePortA,
        SpaceOpeningPort sourcePortB,
        bool preview,
        bool autoAlign,
        float width,
        float height,
        float length)
    {
        portA = sourcePortA;
        portB = sourcePortB;
        isPreview = preview;
        autoAlignedBlockB = autoAlign;
        connectorWidth = width;
        connectorHeight = height;
        connectorLength = length;
    }

    public void CaptureMergedBlockState(MemorySpaceBlock block)
    {
        mergedBlockB = block;
        if (block == null)
        {
            mergedBlockBOriginalParent = null;
            mergedBlockBOriginalSiblingIndex = -1;
            return;
        }

        mergedBlockBOriginalParent = block.transform.parent;
        mergedBlockBOriginalSiblingIndex = block.transform.GetSiblingIndex();
    }

    public void RestoreMergedBlockParent()
    {
        if (mergedBlockB == null)
        {
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.SetTransformParent(mergedBlockB.transform, mergedBlockBOriginalParent, "Clear Space Connection");
        }
        else
#endif
        {
            mergedBlockB.transform.SetParent(mergedBlockBOriginalParent, true);
        }

        if (mergedBlockBOriginalSiblingIndex >= 0)
        {
            mergedBlockB.transform.SetSiblingIndex(mergedBlockBOriginalSiblingIndex);
        }
    }

    public bool Matches(SpaceOpeningPort sourcePortA, SpaceOpeningPort sourcePortB)
    {
        if (sourcePortA == null && sourcePortB == null)
        {
            return false;
        }

        if (sourcePortB == null)
        {
            return portA == sourcePortA || portB == sourcePortA;
        }

        return (portA == sourcePortA && portB == sourcePortB)
            || (portA == sourcePortB && portB == sourcePortA);
    }
}

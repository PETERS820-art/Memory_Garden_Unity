using UnityEngine;

public static class MemoryUIHierarchyUtility
{
    public static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindDeepChild(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    public static T FindComponentInDeepChild<T>(Transform parent, string childName) where T : Component
    {
        Transform child = FindDeepChild(parent, childName);
        if (child == null)
        {
            return null;
        }

        return child.GetComponent<T>();
    }
}

#if UNITY_EDITOR
using UnityEditor;

public static class EmotionMaterialLogEditorUtility
{
    public const string RecommendedFolderPath = "Assets/_project/ScriptableObjects/EmotionMaterialLog";

    public static EmotionMaterialLog FindEmotionMaterialLogAsset()
    {
        string[] guids = AssetDatabase.FindAssets("t:EmotionMaterialLog", new[] { RecommendedFolderPath });
        if (guids.Length == 0)
        {
            guids = AssetDatabase.FindAssets("t:EmotionMaterialLog");
        }

        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            EmotionMaterialLog log = AssetDatabase.LoadAssetAtPath<EmotionMaterialLog>(assetPath);
            if (log != null)
            {
                return log;
            }
        }

        return null;
    }

    public static string[] GetEmotionOptions()
    {
        EmotionMaterialLog log = FindEmotionMaterialLogAsset();
        return log != null ? log.GetEmotionTypes() : new string[0];
    }

    public static string GetDefaultEmotionType()
    {
        EmotionMaterialLog log = FindEmotionMaterialLogAsset();
        return log != null ? log.GetDefaultEmotionType() : "neutral";
    }
}
#endif

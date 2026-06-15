using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EmotionMaterialLog", menuName = "Memory Garden/Emotion Material Log")]
public class EmotionMaterialLog : ScriptableObject
{
    [Serializable]
    public class EmotionMaterialEntry
    {
        public string emotionType;
        public Material memoryMaterial;
    }

    [SerializeField] private Material fallbackMaterial;
    [SerializeField] private List<EmotionMaterialEntry> entries = new List<EmotionMaterialEntry>();

    public Material FallbackMaterial => fallbackMaterial;
    public IReadOnlyList<EmotionMaterialEntry> Entries => entries;

    public bool TryGetMaterial(string emotionType, out Material memoryMaterial)
    {
        memoryMaterial = null;

        if (string.IsNullOrWhiteSpace(emotionType))
        {
            return false;
        }

        string normalizedEmotionType = emotionType.Trim();
        for (int i = 0; i < entries.Count; i++)
        {
            EmotionMaterialEntry entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.emotionType) || entry.memoryMaterial == null)
            {
                continue;
            }

            if (string.Equals(entry.emotionType.Trim(), normalizedEmotionType, StringComparison.OrdinalIgnoreCase))
            {
                memoryMaterial = entry.memoryMaterial;
                return true;
            }
        }

        return false;
    }

    public Material ResolveMaterial(string emotionType, Material fallbackOverride = null)
    {
        if (TryGetMaterial(emotionType, out Material memoryMaterial))
        {
            return memoryMaterial;
        }

        if (fallbackOverride != null)
        {
            return fallbackOverride;
        }

        return fallbackMaterial;
    }

    public string[] GetEmotionTypes()
    {
        List<string> validEmotionTypes = new List<string>();
        for (int i = 0; i < entries.Count; i++)
        {
            EmotionMaterialEntry entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.emotionType))
            {
                continue;
            }

            string normalized = entry.emotionType.Trim();
            if (!validEmotionTypes.Contains(normalized))
            {
                validEmotionTypes.Add(normalized);
            }
        }

        return validEmotionTypes.ToArray();
    }

    public string GetDefaultEmotionType()
    {
        string[] emotionTypes = GetEmotionTypes();
        return emotionTypes.Length > 0 ? emotionTypes[0] : "neutral";
    }
}

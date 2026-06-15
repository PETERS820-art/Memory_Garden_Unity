#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MemoryItemData))]
public class MemoryItemDataEditor : Editor
{
    private SerializedProperty itemIdProperty;
    private SerializedProperty itemNameProperty;
    private SerializedProperty shortDescriptionProperty;
    private SerializedProperty emotionTypeProperty;
    private SerializedProperty storyTextProperty;
    private SerializedProperty photosProperty;
    private SerializedProperty timelineEventsProperty;

    private void OnEnable()
    {
        itemIdProperty = serializedObject.FindProperty("itemId");
        itemNameProperty = serializedObject.FindProperty("itemName");
        shortDescriptionProperty = serializedObject.FindProperty("shortDescription");
        emotionTypeProperty = serializedObject.FindProperty("emotionType");
        storyTextProperty = serializedObject.FindProperty("storyText");
        photosProperty = serializedObject.FindProperty("photos");
        timelineEventsProperty = serializedObject.FindProperty("timelineEvents");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(itemIdProperty);
        EditorGUILayout.PropertyField(itemNameProperty);
        EditorGUILayout.PropertyField(shortDescriptionProperty);

        DrawEmotionPopup();

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(storyTextProperty);
        EditorGUILayout.PropertyField(photosProperty, true);
        EditorGUILayout.PropertyField(timelineEventsProperty, true);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawEmotionPopup()
    {
        EmotionMaterialLog log = EmotionMaterialLogEditorUtility.FindEmotionMaterialLogAsset();
        string[] options = log != null ? log.GetEmotionTypes() : new string[0];

        if (log == null || options.Length == 0)
        {
            EditorGUILayout.HelpBox(
                $"No EmotionMaterialLog asset with valid entries was found. Create or assign one under {EmotionMaterialLogEditorUtility.RecommendedFolderPath}.",
                MessageType.Warning);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Emotion Type", emotionTypeProperty.stringValue);
            }

            return;
        }

        int selectedIndex = Mathf.Max(0, System.Array.IndexOf(options, emotionTypeProperty.stringValue));
        int newSelectedIndex = EditorGUILayout.Popup("Emotion Type", selectedIndex, options);
        emotionTypeProperty.stringValue = options[newSelectedIndex];

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Emotion Material Log", log, typeof(EmotionMaterialLog), false);
        }
    }
}
#endif

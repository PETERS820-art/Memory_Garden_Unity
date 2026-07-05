using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MemoryDisplayFurniture))]
public class MemoryDisplayFurnitureEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MemoryDisplayFurniture furniture = (MemoryDisplayFurniture)target;
        if (furniture == null)
        {
            return;
        }

        EditorGUILayout.Space();
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Feature Summary", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Slots", furniture.Slots != null ? furniture.Slots.Count.ToString() : "0");
            EditorGUILayout.LabelField("Lights", furniture.Lights != null ? furniture.Lights.Count.ToString() : "0");
            EditorGUILayout.LabelField("Frame Surfaces", furniture.FrameSurfaces != null ? furniture.FrameSurfaces.Count.ToString() : "0");

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "Use Refresh Feature References to migrate older prefabs so slots, lights, and frame surfaces are all collected into MemoryDisplayFurniture.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh Feature References"))
                {
                    Undo.RecordObject(furniture, "Refresh Display Furniture Features");
                    furniture.AutoCollectFeatures();
                    EditorUtility.SetDirty(furniture);
                }

                if (GUILayout.Button("Auto Assign Placement Bounds"))
                {
                    Undo.RecordObject(furniture, "Auto Assign Placement Bounds");
                    furniture.AutoAssignPlacementBounds();
                    EditorUtility.SetDirty(furniture);
                }
            }
        }
    }
}

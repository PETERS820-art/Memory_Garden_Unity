using System;
using UnityEditor;
using UnityEngine;

public class AppPreviewCatalogExporterWindow : EditorWindow
{
    private AppPreviewCatalogScanResult lastScanResult;
    private AppPreviewCatalogWriteResult lastWriteResult;
    private string exportDirectory = AppPreviewCatalogExporter.DefaultExportDirectory;
    private Vector2 scrollPosition;
    private string statusMessage = "Ready.";
    private MessageType statusType = MessageType.Info;

    [MenuItem("Memory Garden/App Preview/Export Preview Catalog")]
    public static void OpenWindow()
    {
        AppPreviewCatalogExporterWindow window = GetWindow<AppPreviewCatalogExporterWindow>("App Preview Catalog");
        window.minSize = new Vector2(520f, 420f);
        window.Show();
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.LabelField("App Preview Catalog Exporter", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Exports lightweight preview metadata only. No runtime logic, prefab mutation, scene save, or GLB export is performed in this tool.",
            MessageType.Info);

        DrawExportDirectory();
        EditorGUILayout.Space(8f);
        DrawActions();
        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(statusMessage, statusType);
        EditorGUILayout.Space(8f);
        DrawSummary();
        DrawExportPaths();
        DrawWarnings();

        EditorGUILayout.EndScrollView();
    }

    private void DrawExportDirectory()
    {
        EditorGUILayout.LabelField("Export Directory", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        exportDirectory = EditorGUILayout.TextField(exportDirectory);
        if (GUILayout.Button("Browse", GUILayout.Width(84f)))
        {
            string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
            string initialDirectory = ResolveAbsolutePath(exportDirectory, projectRoot);
            string selected = EditorUtility.OpenFolderPanel("Select Export Folder", initialDirectory, string.Empty);
            if (!string.IsNullOrWhiteSpace(selected))
            {
                string normalizedProjectRoot = projectRoot.Replace('\\', '/').TrimEnd('/');
                string normalizedSelected = selected.Replace('\\', '/');
                if (!normalizedSelected.StartsWith(normalizedProjectRoot, StringComparison.OrdinalIgnoreCase))
                {
                    statusMessage = "Export directory must stay inside this Unity project.";
                    statusType = MessageType.Error;
                }
                else
                {
                    exportDirectory = normalizedSelected.Substring(normalizedProjectRoot.Length + 1);
                    exportDirectory = exportDirectory.Replace('\\', '/');
                    statusMessage = "Export directory updated.";
                    statusType = MessageType.Info;
                }
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawActions()
    {
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Scan Project", GUILayout.Height(32f)))
        {
            try
            {
                lastScanResult = AppPreviewCatalogExporter.ScanProject();
                statusMessage = "Project scan completed.";
                statusType = MessageType.Info;
            }
            catch (Exception exception)
            {
                statusMessage = exception.Message;
                statusType = MessageType.Error;
            }
        }

        if (GUILayout.Button("Export Preview Catalog", GUILayout.Height(32f)))
        {
            try
            {
                lastWriteResult = AppPreviewCatalogExporter.ExportPreviewCatalog(exportDirectory);
                lastScanResult = AppPreviewCatalogExporter.ScanProject();
                statusMessage = "Preview catalog export completed.";
                statusType = MessageType.Info;
            }
            catch (Exception exception)
            {
                statusMessage = exception.Message;
                statusType = MessageType.Error;
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawSummary()
    {
        EditorGUILayout.LabelField("Scan Summary", EditorStyles.boldLabel);
        if (lastScanResult == null)
        {
            EditorGUILayout.HelpBox("Run Scan Project or Export Preview Catalog to see counts.", MessageType.None);
            return;
        }

        EditorGUILayout.LabelField("Block definitions", lastScanResult.summary.blockDefinitionCount.ToString());
        EditorGUILayout.LabelField("Space block prefabs", lastScanResult.summary.spaceBlockPrefabCount.ToString());
        EditorGUILayout.LabelField("Furniture prefabs", lastScanResult.summary.furniturePrefabCount.ToString());
        EditorGUILayout.LabelField("Furniture placements", lastScanResult.summary.furniturePlacementCount.ToString());
        EditorGUILayout.LabelField("Memory item prefabs", lastScanResult.summary.memoryItemPrefabCount.ToString());
        EditorGUILayout.LabelField("Blocks exported", lastScanResult.summary.blockCount.ToString());
        EditorGUILayout.LabelField("Furniture exported", lastScanResult.summary.furnitureCount.ToString());
        EditorGUILayout.LabelField("Slots exported", lastScanResult.summary.slotCount.ToString());
        EditorGUILayout.LabelField("Items exported", lastScanResult.summary.itemCount.ToString());
        EditorGUILayout.LabelField("Doorway ports exported", lastScanResult.summary.portCount.ToString());
        EditorGUILayout.LabelField("Warnings", lastScanResult.summary.warningCount.ToString());
    }

    private void DrawExportPaths()
    {
        if (lastWriteResult == null)
        {
            return;
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Last Export", EditorStyles.boldLabel);
        EditorGUILayout.SelectableLabel(lastWriteResult.blockCatalogPath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        EditorGUILayout.SelectableLabel(lastWriteResult.furnitureCatalogPath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        EditorGUILayout.SelectableLabel(lastWriteResult.itemCatalogPath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        EditorGUILayout.SelectableLabel(lastWriteResult.manifestPath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        EditorGUILayout.SelectableLabel(lastWriteResult.reportPath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
    }

    private void DrawWarnings()
    {
        if (lastScanResult == null || lastScanResult.warnings == null || lastScanResult.warnings.Count == 0)
        {
            return;
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Warnings", EditorStyles.boldLabel);
        for (int i = 0; i < lastScanResult.warnings.Count; i++)
        {
            AppPreviewCatalogWarning warning = lastScanResult.warnings[i];
            EditorGUILayout.HelpBox(
                warning.code + Environment.NewLine +
                warning.sourcePath + Environment.NewLine +
                warning.message,
                MessageType.Warning);
        }
    }

    private static string ResolveAbsolutePath(string assetPath, string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return projectRoot;
        }

        if (System.IO.Path.IsPathRooted(assetPath))
        {
            return assetPath;
        }

        string normalizedRelative = assetPath.Replace('/', System.IO.Path.DirectorySeparatorChar);
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(projectRoot, normalizedRelative));
    }
}

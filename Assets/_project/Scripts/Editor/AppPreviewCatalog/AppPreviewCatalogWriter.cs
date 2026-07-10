using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AppPreviewCatalogWriter
{
    public static AppPreviewCatalogWriteResult Write(AppPreviewCatalogScanResult result)
    {
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        string exportDirectory = string.IsNullOrWhiteSpace(result.exportDirectory)
            ? AppPreviewCatalogExporter.DefaultExportDirectory
            : result.exportDirectory;
        if (!exportDirectory.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Export directory must stay inside the Unity project Assets folder.");
        }

        string absoluteDirectory = GetAbsolutePath(exportDirectory);
        Directory.CreateDirectory(absoluteDirectory);

        AppPreviewCatalogWriteResult writeResult = new AppPreviewCatalogWriteResult();
        writeResult.exportDirectory = exportDirectory;
        writeResult.blockCatalogPath = WriteJson(
            absoluteDirectory,
            "block-preview-catalog.json",
            new BlockPreviewCatalogDocument
            {
                schemaVersion = result.schemaVersion,
                generatedAtUtc = result.generatedAtUtc,
                records = result.blocks
            });
        writeResult.furnitureCatalogPath = WriteJson(
            absoluteDirectory,
            "furniture-preview-catalog.json",
            new FurniturePreviewCatalogDocument
            {
                schemaVersion = result.schemaVersion,
                generatedAtUtc = result.generatedAtUtc,
                records = result.furniture
            });
        writeResult.itemCatalogPath = WriteJson(
            absoluteDirectory,
            "item-preview-catalog.json",
            new ItemPreviewCatalogDocument
            {
                schemaVersion = result.schemaVersion,
                generatedAtUtc = result.generatedAtUtc,
                records = result.items
            });
        writeResult.manifestPath = WriteJson(
            absoluteDirectory,
            "preview-asset-manifest.json",
            new PreviewAssetManifestDocument
            {
                schemaVersion = result.schemaVersion,
                generatedAtUtc = result.generatedAtUtc,
                records = result.manifest
            });
        writeResult.reportPath = WriteText(
            absoluteDirectory,
            "export-report.md",
            BuildReport(result, exportDirectory));

        AssetDatabase.Refresh();
        return writeResult;
    }

    private static string WriteJson<TDocument>(string absoluteDirectory, string fileName, TDocument document)
    {
        string json = JsonUtility.ToJson(document, true);
        JsonUtility.FromJson<TDocument>(json);
        return WriteText(absoluteDirectory, fileName, json);
    }

    private static string WriteText(string absoluteDirectory, string fileName, string content)
    {
        string absolutePath = Path.Combine(absoluteDirectory, fileName);
        File.WriteAllText(absolutePath, content ?? string.Empty);
        return ToAssetPath(absolutePath);
    }

    private static string BuildReport(AppPreviewCatalogScanResult result, string exportDirectory)
    {
        List<string> lines = new List<string>();
        lines.Add("# App Preview Catalog Export Report");
        lines.Add(string.Empty);
        lines.Add("- Schema version: `" + result.schemaVersion + "`");
        lines.Add("- Generated at (UTC): `" + result.generatedAtUtc + "`");
        lines.Add("- Export directory: `" + exportDirectory + "`");
        lines.Add("- Scan mode: asset and prefab scan only; unopened scenes were not auto-opened in v0.");
        lines.Add(string.Empty);

        lines.Add("## Scan Scope");
        for (int i = 0; i < result.scanRoots.Count; i++)
        {
            lines.Add("- `" + result.scanRoots[i] + "`");
        }
        lines.Add(string.Empty);

        lines.Add("## Export Counts");
        lines.Add("- block definitions scanned: " + result.summary.blockDefinitionCount);
        lines.Add("- space block prefabs scanned: " + result.summary.spaceBlockPrefabCount);
        lines.Add("- furniture prefabs scanned: " + result.summary.furniturePrefabCount);
        lines.Add("- furniture placements scanned: " + result.summary.furniturePlacementCount);
        lines.Add("- memory item prefabs scanned: " + result.summary.memoryItemPrefabCount);
        lines.Add("- memory item data assets scanned: " + result.summary.memoryItemDataCount);
        lines.Add("- blocks exported: " + result.summary.blockCount);
        lines.Add("- furniture records exported: " + result.summary.furnitureCount);
        lines.Add("- slots exported: " + result.summary.slotCount);
        lines.Add("- items exported: " + result.summary.itemCount);
        lines.Add("- doorway ports exported: " + result.summary.portCount);
        lines.Add("- warnings: " + result.summary.warningCount);
        lines.Add(string.Empty);

        lines.Add("## Warning Summary");
        if (result.warnings.Count == 0)
        {
            lines.Add("- none");
        }
        else
        {
            Dictionary<string, int> countsByCode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < result.warnings.Count; i++)
            {
                string code = result.warnings[i].code;
                countsByCode[code] = countsByCode.TryGetValue(code, out int currentCount) ? currentCount + 1 : 1;
            }

            foreach (KeyValuePair<string, int> entry in countsByCode)
            {
                lines.Add("- `" + entry.Key + "`: " + entry.Value);
            }

            lines.Add(string.Empty);
            lines.Add("### Warning Details");
            for (int i = 0; i < result.warnings.Count; i++)
            {
                AppPreviewCatalogWarning warning = result.warnings[i];
                string source = string.IsNullOrWhiteSpace(warning.sourcePath) ? "(no source path)" : warning.sourcePath;
                lines.Add("- `" + warning.code + "` | `" + source + "` | " + warning.message);
            }
        }
        lines.Add(string.Empty);

        lines.Add("## Procedural-Only Content");
        if (result.blocks.Count == 0)
        {
            lines.Add("- none");
        }
        else
        {
            for (int i = 0; i < result.blocks.Count; i++)
            {
                lines.Add("- `" + result.blocks[i].previewAssetKey + "` rebuilt from grid and wall-edge data");
            }
        }
        lines.Add(string.Empty);

        lines.Add("## Good GLB Candidates Later");
        bool hasGlbCandidates = false;
        for (int i = 0; i < result.furniture.Count; i++)
        {
            lines.Add("- furniture: `" + result.furniture[i].previewAssetKey + "` (" + result.furniture[i].silhouetteType + ")");
            hasGlbCandidates = true;
        }

        for (int i = 0; i < result.items.Count; i++)
        {
            if (string.Equals(result.items[i].previewMode, "primitive", StringComparison.OrdinalIgnoreCase))
            {
                lines.Add("- item: `" + result.items[i].previewAssetKey + "` (" + result.items[i].silhouetteType + ")");
                hasGlbCandidates = true;
            }
        }

        if (!hasGlbCandidates)
        {
            lines.Add("- none");
        }
        lines.Add(string.Empty);

        lines.Add("## Prefabs Not Recommended For Direct App Use");
        lines.Add("- `Assets/_project/Prefabs/Environment/SpaceBlocks/*` should stay metadata-driven and procedural in the App.");
        lines.Add("- `Assets/_project/Prefabs/DisplayFurniture/*` should be simplified to primitive or later GLB previews.");
        lines.Add("- `Assets/_project/Prefabs/MemoryItems/*` should stay lightweight and should not carry XR or Memory Mode logic.");
        lines.Add(string.Empty);

        lines.Add("## React App Next Step");
        lines.Add("- Yes: wire the React Three.js App to read these preview catalogs next.");
        lines.Add("- Start with `block-preview-catalog.json` for procedural block generation, then join `furniture-preview-catalog.json` on block identity and anchors.");
        lines.Add("- Keep `preview-asset-manifest.json` as the lookup layer for renderer choice, silhouette fallback, and later GLB upgrades.");

        return string.Join(Environment.NewLine, lines);
    }

    private static string GetAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string normalizedRelative = assetPath.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(projectRoot, normalizedRelative));
    }

    private static string ToAssetPath(string absolutePath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string normalizedRoot = projectRoot.Replace('\\', '/').TrimEnd('/');
        string normalizedPath = Path.GetFullPath(absolutePath).Replace('\\', '/');
        if (!normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return absolutePath;
        }

        return normalizedPath.Substring(normalizedRoot.Length + 1);
    }
}

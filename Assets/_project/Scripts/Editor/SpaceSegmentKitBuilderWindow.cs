#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public class SpaceSegmentKitBuilderWindow : EditorWindow
{
    private const string MenuItemPath = "Tools/Memory Garden/Segment Kit Builder";
    private const string WindowTitle = "Segment Kit Builder";
    private const string DefaultSourceFolder = "Assets/_project/Art/Models/SegmentKit";
    private const string DefaultPrefabOutputFolder = "Assets/_project/Prefabs/SegmentKit";
    private const string DefaultDefinitionOutputFolder = "Assets/_project/ScriptableObjects/SegmentKit";
    private const string DefaultKitAssetName = "SK_DefaultSegmentKit.asset";
    private const string DefaultPainterlyTemplatePath = "Assets/_project/Art/Memory Materials/MMS_BW_001.mat";
    private const string PainterlyPbrShaderName = "MemoryGarden/Memory Painterly PBR";
    private const string PainterlyPbrShaderPath = "Assets/_project/Art/Memory Materials/MemoryPainterlyPBR.shader";
    private const string PrefabPrefix = "PF_";
    private const string DefinitionPrefix = "SD_";
    private const string ModelContainerObjectName = "Model";
    private const float BoundsMinSize = 0.01f;

    private static readonly Regex SizePattern = new Regex(
        @"^(?<x>\d+(?:\.\d+)?)x(?<y>\d+(?:\.\d+)?)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [SerializeField] private string sourceFolder = DefaultSourceFolder;
    [SerializeField] private string prefabOutputFolder = DefaultPrefabOutputFolder;
    [SerializeField] private string definitionOutputFolder = DefaultDefinitionOutputFolder;
    [SerializeField] private bool autoMigrateExtractedMaterialsToPainterly = true;
    [SerializeField] private Material painterlyTemplateMaterial;

    [MenuItem(MenuItemPath)]
    public static void OpenWindow()
    {
        SpaceSegmentKitBuilderWindow window = GetWindow<SpaceSegmentKitBuilderWindow>();
        window.titleContent = new GUIContent(WindowTitle);
        window.minSize = new Vector2(520f, 260f);
        window.Show();
    }

    private void OnEnable()
    {
        if (string.IsNullOrWhiteSpace(sourceFolder))
        {
            sourceFolder = DefaultSourceFolder;
        }

        if (string.IsNullOrWhiteSpace(prefabOutputFolder))
        {
            prefabOutputFolder = DefaultPrefabOutputFolder;
        }

        if (string.IsNullOrWhiteSpace(definitionOutputFolder))
        {
            definitionOutputFolder = DefaultDefinitionOutputFolder;
        }

        if (painterlyTemplateMaterial == null)
        {
            painterlyTemplateMaterial = ResolveDefaultPainterlyTemplateMaterial();
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Scans FBX files under the SegmentKit source folder and generates prefabs, segment definitions, and a default segment kit asset.",
            MessageType.Info);

        sourceFolder = EditorGUILayout.TextField("Source Folder", sourceFolder);
        prefabOutputFolder = EditorGUILayout.TextField("Prefab Output Folder", prefabOutputFolder);
        definitionOutputFolder = EditorGUILayout.TextField("Definition Output Folder", definitionOutputFolder);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Painterly Migration", EditorStyles.boldLabel);
        autoMigrateExtractedMaterialsToPainterly = EditorGUILayout.ToggleLeft(
            "Auto migrate extracted external materials to Painterly PBR during prefab build",
            autoMigrateExtractedMaterialsToPainterly);

        using (new EditorGUI.DisabledScope(!autoMigrateExtractedMaterialsToPainterly))
        {
            painterlyTemplateMaterial = (Material)EditorGUILayout.ObjectField(
                "Painterly Template",
                painterlyTemplateMaterial,
                typeof(Material),
                false);
        }

        EditorGUILayout.HelpBox(
            "If a SegmentKit FBX already references extracted external .mat assets, the builder can convert those materials to Memory Painterly PBR while preserving the local PBR textures. Embedded FBX materials are not force-converted; the builder will warn so you can Extract Materials first.",
            MessageType.None);

        EditorGUILayout.Space();

        if (GUILayout.Button("Create Missing Folders"))
        {
            CreateMissingFolders();
        }

        if (GUILayout.Button("Build / Update All Segment Prefabs"))
        {
            BuildOrUpdateAllSegmentPrefabs();
        }

        if (GUILayout.Button("Generate / Update Segment Definitions"))
        {
            GenerateOrUpdateSegmentDefinitions();
        }

        if (GUILayout.Button("Generate / Update Default SegmentKit Asset"))
        {
            GenerateOrUpdateDefaultSegmentKitAsset();
        }

        if (GUILayout.Button("Validate Segment Naming"))
        {
            ValidateSegmentNaming();
        }
    }

    private void CreateMissingFolders()
    {
        try
        {
            EnsureConfiguredOutputFolders();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ShowSummary("Created or verified SegmentKit output folders.");
        }
        catch (Exception exception)
        {
            ReportException("Create Missing Folders", exception);
        }
    }

    private void BuildOrUpdateAllSegmentPrefabs()
    {
        try
        {
            EnsureConfiguredOutputFolders();

            SegmentScanResult scan = ScanSegmentSources();
            if (scan.Sources.Count == 0)
            {
                ShowSummary("No SegmentKit FBX files were found to build.");
                return;
            }

            List<SegmentSourceInfo> targets = GetUniqueSources(scan);
            List<string> generatedPrefabs = new List<string>();
            int migratedMaterialCount = 0;
            int alreadyPainterlyMaterialCount = 0;
            int embeddedMaterialCount = 0;

            for (int i = 0; i < targets.Count; i++)
            {
                SegmentSourceInfo source = targets[i];
                EditorUtility.DisplayProgressBar(
                    WindowTitle,
                    $"Building prefab {source.segmentId} ({i + 1}/{targets.Count})",
                    (float)(i + 1) / targets.Count);

                BuildPrefabResult buildResult = BuildPrefabForSource(source, scan.Warnings);
                generatedPrefabs.Add(buildResult.prefabAssetPath);
                migratedMaterialCount += buildResult.materialMigrationResult.migratedMaterialCount;
                alreadyPainterlyMaterialCount += buildResult.materialMigrationResult.alreadyPainterlyMaterialCount;
                embeddedMaterialCount += buildResult.materialMigrationResult.embeddedMaterialCount;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            LogWarnings(scan.Warnings);

            for (int i = 0; i < generatedPrefabs.Count; i++)
            {
                Debug.Log($"[SpaceSegmentKitBuilder] Generated prefab: {generatedPrefabs[i]}");
            }

            string summary =
                $"Built or updated {generatedPrefabs.Count} SegmentKit prefab(s). " +
                $"Migrated {migratedMaterialCount} extracted material(s) to Painterly PBR.";

            if (alreadyPainterlyMaterialCount > 0)
            {
                summary += $" {alreadyPainterlyMaterialCount} material(s) were already using Painterly PBR.";
            }

            if (embeddedMaterialCount > 0)
            {
                summary += $" {embeddedMaterialCount} embedded FBX material reference(s) still need Extract Materials before migration.";
            }

            ShowSummary(summary);
        }
        catch (Exception exception)
        {
            ReportException("Build / Update All Segment Prefabs", exception);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private void GenerateOrUpdateSegmentDefinitions()
    {
        try
        {
            EnsureConfiguredOutputFolders();
            SegmentScanResult scan = ScanSegmentSources();
            if (scan.Sources.Count == 0)
            {
                ShowSummary("No SegmentKit FBX files were found to generate definitions.");
                return;
            }

            List<SpaceSegmentDefinition> definitions = GenerateOrUpdateDefinitions(scan);
            ShowSummary($"Generated or updated {definitions.Count} SegmentKit definition asset(s).");
        }
        catch (Exception exception)
        {
            ReportException("Generate / Update Segment Definitions", exception);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private void GenerateOrUpdateDefaultSegmentKitAsset()
    {
        try
        {
            EnsureConfiguredOutputFolders();
            SegmentScanResult scan = ScanSegmentSources();
            if (scan.Sources.Count == 0)
            {
                ShowSummary("No SegmentKit FBX files were found to include in the default kit.");
                return;
            }

            List<SpaceSegmentDefinition> definitions = GenerateOrUpdateDefinitions(scan);
            string normalizedDefinitionFolder = NormalizeAssetPath(definitionOutputFolder).TrimEnd('/');
            string kitAssetPath = $"{normalizedDefinitionFolder}/{DefaultKitAssetName}";
            SpaceSegmentKit kitAsset = AssetDatabase.LoadAssetAtPath<SpaceSegmentKit>(kitAssetPath);
            if (kitAsset == null)
            {
                kitAsset = ScriptableObject.CreateInstance<SpaceSegmentKit>();
                AssetDatabase.CreateAsset(kitAsset, kitAssetPath);
            }

            definitions.Sort(CompareDefinitions);
            kitAsset.kitId = "DefaultSegmentKit";
            kitAsset.segments = definitions;

            EditorUtility.SetDirty(kitAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SpaceSegmentKitBuilder] Updated default SegmentKit asset: {kitAssetPath}");
            ShowSummary($"Generated or updated default SegmentKit asset with {definitions.Count} segment(s).");
        }
        catch (Exception exception)
        {
            ReportException("Generate / Update Default SegmentKit Asset", exception);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private void ValidateSegmentNaming()
    {
        try
        {
            SegmentScanResult scan = ScanSegmentSources();
            if (scan.Sources.Count == 0)
            {
                ShowSummary("No SegmentKit FBX files were found to validate.");
                return;
            }

            LogWarnings(scan.Warnings);

            if (scan.Warnings.Count == 0)
            {
                ShowSummary($"Validated {scan.Sources.Count} SegmentKit source file(s). No warnings.");
                return;
            }

            ShowSummary($"Validated {scan.Sources.Count} SegmentKit source file(s). Warnings: {scan.Warnings.Count}.");
        }
        catch (Exception exception)
        {
            ReportException("Validate Segment Naming", exception);
        }
    }

    private List<SpaceSegmentDefinition> GenerateOrUpdateDefinitions(SegmentScanResult scan)
    {
        List<SegmentSourceInfo> targets = GetUniqueSources(scan);
        List<SpaceSegmentDefinition> definitions = new List<SpaceSegmentDefinition>();

        for (int i = 0; i < targets.Count; i++)
        {
            SegmentSourceInfo source = targets[i];
            EditorUtility.DisplayProgressBar(
                WindowTitle,
                $"Generating definition {source.segmentId} ({i + 1}/{targets.Count})",
                (float)(i + 1) / targets.Count);

            SpaceSegmentDefinition definition = GetOrCreateDefinition(source.definitionAssetPath);
            definition.segmentId = source.segmentId;
            definition.category = source.category;
            definition.styleId = source.styleId;
            definition.sizeXZ = source.sizeXZ;
            definition.height = source.height;
            definition.variant = source.variant;
            definition.hasCollider = source.hasCollider;
            definition.canBeWallSegment = source.canBeWallSegment;
            definition.canBeOpeningOverlay = source.canBeOpeningOverlay;
            definition.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(source.prefabAssetPath);

            if (definition.prefab == null)
            {
                scan.Warnings.Add(
                    $"Missing prefab reference for segment '{source.segmentId}'. Expected prefab: {source.prefabAssetPath}");
            }

            EditorUtility.SetDirty(definition);
            definitions.Add(definition);

            Debug.Log($"[SpaceSegmentKitBuilder] Generated definition: {source.definitionAssetPath}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        LogWarnings(scan.Warnings);
        return definitions;
    }

    private SegmentScanResult ScanSegmentSources()
    {
        SegmentScanResult result = new SegmentScanResult();
        string normalizedSourceFolder = NormalizeAssetPath(sourceFolder).TrimEnd('/');
        ValidateAssetFolderPath(normalizedSourceFolder, "Source folder");

        if (!AssetDatabase.IsValidFolder(normalizedSourceFolder))
        {
            result.Warnings.Add($"Source folder does not exist: {normalizedSourceFolder}");
            return result;
        }

        string sourceAbsolutePath = ToAbsolutePath(normalizedSourceFolder);
        if (!Directory.Exists(sourceAbsolutePath))
        {
            result.Warnings.Add($"Source folder does not exist on disk: {normalizedSourceFolder}");
            return result;
        }

        CheckTopLevelCategoryFolders(result, sourceAbsolutePath);

        string[] fbxAbsolutePaths = Directory.GetFiles(sourceAbsolutePath, "*.fbx", SearchOption.AllDirectories);
        Array.Sort(fbxAbsolutePaths, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < fbxAbsolutePaths.Length; i++)
        {
            SegmentSourceInfo source = ParseSegmentSource(fbxAbsolutePaths[i], result.Warnings);
            if (source != null)
            {
                result.Sources.Add(source);
            }
        }

        result.Sources.Sort(CompareSources);

        Dictionary<string, int> segmentIdCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < result.Sources.Count; i++)
        {
            SegmentSourceInfo source = result.Sources[i];
            if (!segmentIdCounts.ContainsKey(source.segmentId))
            {
                segmentIdCounts[source.segmentId] = 0;
            }

            segmentIdCounts[source.segmentId]++;
        }

        foreach (KeyValuePair<string, int> entry in segmentIdCounts)
        {
            if (entry.Value > 1)
            {
                result.Warnings.Add($"Duplicate segmentId detected: {entry.Key}");
            }
        }

        return result;
    }

    private SegmentSourceInfo ParseSegmentSource(string fbxAbsolutePath, List<string> warnings)
    {
        string normalizedSourceFolder = NormalizeAssetPath(sourceFolder).TrimEnd('/');
        string normalizedPrefabFolder = NormalizeAssetPath(prefabOutputFolder).TrimEnd('/');
        string normalizedDefinitionFolder = NormalizeAssetPath(definitionOutputFolder).TrimEnd('/');
        string assetPath = ToAssetPath(fbxAbsolutePath);
        string relativePath = NormalizeAssetPath(assetPath).Substring(normalizedSourceFolder.Length).TrimStart('/');
        string[] relativeParts = relativePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

        string relativeDirectoryPath = Path.GetDirectoryName(relativePath);
        relativeDirectoryPath = string.IsNullOrWhiteSpace(relativeDirectoryPath)
            ? string.Empty
            : NormalizeAssetPath(relativeDirectoryPath).Trim('/');

        if (relativeParts.Length < 2)
        {
            warnings.Add($"Unrecognized source layout: {assetPath}");
            return null;
        }

        string primaryFolderName = relativeParts[0];
        SegmentCategory category = ParseCategory(primaryFolderName, out bool supportedFolderCategory);
        if (!supportedFolderCategory)
        {
            warnings.Add($"Unsupported folder category '{primaryFolderName}' for asset {assetPath}");
        }

        string fileName = Path.GetFileNameWithoutExtension(assetPath);
        string[] rawTokens = fileName.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
        if (rawTokens.Length == 0)
        {
            warnings.Add($"Unrecognized file name: {assetPath}");
            return null;
        }

        bool recognizedName = true;
        int styleStartIndex = 0;
        if (string.Equals(rawTokens[0], "SM", StringComparison.OrdinalIgnoreCase))
        {
            styleStartIndex = 1;
        }
        else
        {
            recognizedName = false;
            warnings.Add($"Unrecognized naming prefix for asset {assetPath}. Expected 'SM_'.");
        }

        if (styleStartIndex < rawTokens.Length)
        {
            if (CategoryTokenMatches(rawTokens[styleStartIndex], primaryFolderName))
            {
                styleStartIndex++;
            }
            else if (TryResolveCategoryToken(rawTokens[styleStartIndex], out SegmentCategory fileNameCategory))
            {
                if (fileNameCategory != category)
                {
                    recognizedName = false;
                    warnings.Add($"Category token does not match folder category for asset {assetPath}");
                }
            }
        }

        SegmentVariant variant = GetDefaultVariant(category);
        int dataEndIndex = rawTokens.Length - 1;
        if (TryParseVariant(rawTokens[dataEndIndex], out SegmentVariant parsedVariant))
        {
            variant = parsedVariant;
            dataEndIndex--;
        }

        bool hasSize = false;
        float sizeX = 1f;
        float sizeY = 1f;
        int sizeTokenIndex = -1;
        for (int i = dataEndIndex; i >= styleStartIndex; i--)
        {
            if (!TryParseSizeToken(rawTokens[i], out sizeX, out sizeY))
            {
                continue;
            }

            hasSize = true;
            sizeTokenIndex = i;
            break;
        }

        List<string> styleTokens = new List<string>();
        for (int i = styleStartIndex; i <= dataEndIndex; i++)
        {
            if (i == sizeTokenIndex)
            {
                continue;
            }

            styleTokens.Add(rawTokens[i]);
        }

        if (styleTokens.Count == 0)
        {
            recognizedName = false;
            warnings.Add($"Could not resolve styleId from asset {assetPath}");
        }

        string styleId = styleTokens.Count > 0 ? string.Join("_", styleTokens.ToArray()) : "default";
        Vector2 sizeXZ = Vector2.one;
        float height = 0f;
        bool validWallSize = true;

        if (category == SegmentCategory.Wall)
        {
            if (hasSize)
            {
                sizeXZ = new Vector2(sizeX, 1f);
                height = sizeY;
            }
            else
            {
                validWallSize = false;
                warnings.Add($"Missing or invalid wall size for asset {assetPath}. Expected patterns like 1x2.5.");
            }
        }
        else if (category == SegmentCategory.OpeningOverlay)
        {
            if (hasSize)
            {
                sizeXZ = new Vector2(sizeX, 1f);
                height = sizeY;
            }
        }
        else
        {
            if (hasSize)
            {
                sizeXZ = new Vector2(sizeX, sizeY);
            }
        }

        if (!recognizedName)
        {
            warnings.Add($"Unrecognized name pattern: {assetPath}");
        }

        return new SegmentSourceInfo
        {
            assetPath = assetPath,
            definitionAssetPath = $"{normalizedDefinitionFolder}/{DefinitionPrefix}{fileName}.asset",
            hasCollider = ShouldHaveCollider(category),
            canBeOpeningOverlay = category == SegmentCategory.OpeningOverlay,
            canBeWallSegment = category == SegmentCategory.Wall,
            category = category,
            height = height,
            hasValidWallSize = validWallSize,
            prefabAssetPath = CombineAssetPath(normalizedPrefabFolder, relativeDirectoryPath, $"{PrefabPrefix}{fileName}.prefab"),
            primaryFolderName = primaryFolderName,
            segmentId = fileName,
            sizeXZ = sizeXZ,
            styleId = styleId,
            variant = variant
        };
    }

    private static SegmentVariant GetDefaultVariant(SegmentCategory category)
    {
        return category == SegmentCategory.Wall ? SegmentVariant.Solid : SegmentVariant.Default;
    }

    private static bool ShouldHaveCollider(SegmentCategory category)
    {
        switch (category)
        {
            case SegmentCategory.Wall:
            case SegmentCategory.Floor:
            case SegmentCategory.Ceiling:
            case SegmentCategory.Beam:
            case SegmentCategory.Threshold:
                return true;
            default:
                return false;
        }
    }

    private BuildPrefabResult BuildPrefabForSource(SegmentSourceInfo source, List<string> warnings)
    {
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(source.assetPath);
        if (modelAsset == null)
        {
            throw new InvalidOperationException($"Could not load FBX asset at {source.assetPath}.");
        }

        EnsureParentFolderForAssetPath(source.prefabAssetPath);

        GameObject root = new GameObject($"{PrefabPrefix}{source.segmentId}");
        MaterialMigrationResult materialMigrationResult = default;
        try
        {
            GameObject modelContainer = new GameObject(ModelContainerObjectName);
            modelContainer.transform.SetParent(root.transform, false);

            GameObject modelInstance = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
            if (modelInstance == null)
            {
                modelInstance = UnityEngine.Object.Instantiate(modelAsset);
            }

            modelInstance.name = modelAsset.name;
            modelInstance.transform.SetParent(modelContainer.transform, false);
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;

            if (autoMigrateExtractedMaterialsToPainterly)
            {
                materialMigrationResult = MigrateReferencedMaterialsToPainterly(modelInstance, source, warnings);
            }

            if (source.hasCollider)
            {
                ConfigureBoxCollider(root);
            }

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, source.prefabAssetPath);
            if (savedPrefab == null)
            {
                throw new InvalidOperationException($"Failed to save SegmentKit prefab at {source.prefabAssetPath}.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        return new BuildPrefabResult
        {
            prefabAssetPath = source.prefabAssetPath,
            materialMigrationResult = materialMigrationResult
        };
    }

    private MaterialMigrationResult MigrateReferencedMaterialsToPainterly(
        GameObject modelInstance,
        SegmentSourceInfo source,
        List<string> warnings)
    {
        MaterialMigrationResult result = default;
        if (modelInstance == null)
        {
            return result;
        }

        Shader painterlyShader = ResolvePainterlyPbrShader();
        if (painterlyShader == null)
        {
            warnings.Add($"Could not resolve Painterly PBR shader '{PainterlyPbrShaderName}'.");
            return result;
        }

        Material templateMaterial = painterlyTemplateMaterial != null
            ? painterlyTemplateMaterial
            : ResolveDefaultPainterlyTemplateMaterial();

        HashSet<Material> uniqueMaterials = new HashSet<Material>();
        Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Material[] sharedMaterials = renderer.sharedMaterials;
            for (int j = 0; j < sharedMaterials.Length; j++)
            {
                Material material = sharedMaterials[j];
                if (material == null)
                {
                    continue;
                }

                string materialAssetPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(material));
                if (string.IsNullOrWhiteSpace(materialAssetPath))
                {
                    warnings.Add($"Segment '{source.segmentId}' references a material with no asset path: {material.name}");
                    continue;
                }

                if (string.Equals(materialAssetPath, NormalizeAssetPath(source.assetPath), StringComparison.OrdinalIgnoreCase))
                {
                    result.embeddedMaterialCount++;
                    warnings.Add(
                        $"Segment '{source.segmentId}' still uses embedded FBX material '{material.name}'. " +
                        "Extract Materials in Unity first, then rebuild to migrate it to Painterly PBR.");
                    continue;
                }

                if (!materialAssetPath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add($"Segment '{source.segmentId}' references a non-.mat material asset: {materialAssetPath}");
                    continue;
                }

                uniqueMaterials.Add(material);
            }
        }

        foreach (Material material in uniqueMaterials)
        {
            switch (MigrateMaterialToPainterlyPbr(material, painterlyShader, templateMaterial, warnings))
            {
                case MaterialMigrationStatus.Migrated:
                    result.migratedMaterialCount++;
                    break;
                case MaterialMigrationStatus.AlreadyPainterly:
                    result.alreadyPainterlyMaterialCount++;
                    break;
            }
        }

        return result;
    }

    private static Shader ResolvePainterlyPbrShader()
    {
        Shader shader = Shader.Find(PainterlyPbrShaderName);
        if (shader != null)
        {
            return shader;
        }

        return AssetDatabase.LoadAssetAtPath<Shader>(PainterlyPbrShaderPath);
    }

    private static Material ResolveDefaultPainterlyTemplateMaterial()
    {
        EmotionMaterialLog log = EmotionMaterialLogEditorUtility.FindEmotionMaterialLogAsset();
        if (log != null && log.FallbackMaterial != null)
        {
            return log.FallbackMaterial;
        }

        return AssetDatabase.LoadAssetAtPath<Material>(DefaultPainterlyTemplatePath);
    }

    private static MaterialMigrationStatus MigrateMaterialToPainterlyPbr(
        Material material,
        Shader painterlyShader,
        Material templateMaterial,
        List<string> warnings)
    {
        if (material == null || painterlyShader == null)
        {
            return MaterialMigrationStatus.Skipped;
        }

        if (material.shader == painterlyShader
            || MemoryPainterlyPBRMaterialUtility.IsPainterlyPbrShader(material.shader))
        {
            return MaterialMigrationStatus.AlreadyPainterly;
        }

        MaterialSurfaceSnapshot snapshot = CaptureMaterialSurfaceSnapshot(material);

        material.shader = painterlyShader;

        if (templateMaterial != null)
        {
            material.CopyPropertiesFromMaterial(templateMaterial);
            material.shader = painterlyShader;
        }

        ApplyDefaultPainterlyTexturesIfNeeded(material);
        ApplyMaterialSurfaceSnapshot(material, snapshot);
        MemoryPainterlyPBRMaterialUtility.SetupMaterial(material);
        EnsureTextureMarkedAsNormalMap(snapshot.normalMap, warnings);
        EditorUtility.SetDirty(material);
        return MaterialMigrationStatus.Migrated;
    }

    private static MaterialSurfaceSnapshot CaptureMaterialSurfaceSnapshot(Material material)
    {
        Texture baseMap = GetTexture(material, "_BaseMap");
        if (baseMap == null)
        {
            baseMap = GetTexture(material, "_MainTex");
        }

        return new MaterialSurfaceSnapshot
        {
            baseMap = baseMap,
            baseMapScale = GetTextureScale(material, baseMap != null && material.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex", Vector2.one),
            baseMapOffset = GetTextureOffset(material, baseMap != null && material.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex", Vector2.zero),
            baseColor = GetColor(material, "_BaseColor", GetColor(material, "_Color", Color.white)),
            normalMap = GetTexture(material, "_BumpMap"),
            bumpScale = GetFloat(material, "_BumpScale", 1f),
            metallicGlossMap = GetTexture(material, "_MetallicGlossMap"),
            metallic = GetFloat(material, "_Metallic", 0f),
            smoothness = GetFloat(material, "_Smoothness", GetFloat(material, "_Glossiness", 0.5f)),
            occlusionMap = GetTexture(material, "_OcclusionMap"),
            occlusionStrength = GetFloat(material, "_OcclusionStrength", 1f),
            emissionMap = GetTexture(material, "_EmissionMap"),
            emissionColor = GetColor(material, "_EmissionColor", Color.black),
            parallaxMap = GetTexture(material, "_ParallaxMap"),
            parallax = GetFloat(material, "_Parallax", 0.005f),
            cull = Mathf.RoundToInt(GetFloat(material, "_Cull", 2f)),
            cutoff = GetFloat(material, "_Cutoff", 0.5f),
            alphaClip = GetFloat(material, "_AlphaClip", 0f),
            receiveShadows = GetFloat(material, "_ReceiveShadows", 1f),
            surface = MemoryPainterlyPBRMaterialUtility.InferSurfaceType(material),
            blend = MemoryPainterlyPBRMaterialUtility.InferBlendMode(material),
            queueOffset = MemoryPainterlyPBRMaterialUtility.InferQueueOffset(
                material,
                MemoryPainterlyPBRMaterialUtility.InferSurfaceType(material),
                GetFloat(material, "_AlphaClip", 0f)),
            enableInstancing = material.enableInstancing,
            doubleSidedGi = material.doubleSidedGI,
            globalIlluminationFlags = material.globalIlluminationFlags
        };
    }

    private static void ApplyMaterialSurfaceSnapshot(Material material, MaterialSurfaceSnapshot snapshot)
    {
        if (material == null)
        {
            return;
        }

        SetTexture(material, "_BaseMap", snapshot.baseMap);
        SetTextureScale(material, "_BaseMap", snapshot.baseMapScale);
        SetTextureOffset(material, "_BaseMap", snapshot.baseMapOffset);
        SetColor(material, "_BaseColor", snapshot.baseColor);
        SetColor(material, "_PainterlyBaseColor", new Color(1f, 1f, 1f, 0f));

        SetTexture(material, "_BumpMap", snapshot.normalMap);
        SetFloat(material, "_BumpScale", snapshot.bumpScale);
        SetTexture(material, "_MetallicGlossMap", snapshot.metallicGlossMap);
        SetFloat(material, "_Metallic", snapshot.metallic);
        SetFloat(material, "_Smoothness", snapshot.smoothness);
        SetTexture(material, "_OcclusionMap", snapshot.occlusionMap);
        SetFloat(material, "_OcclusionStrength", snapshot.occlusionStrength);
        SetTexture(material, "_EmissionMap", snapshot.emissionMap);
        SetColor(material, "_EmissionColor", snapshot.emissionColor);
        SetTexture(material, "_ParallaxMap", snapshot.parallaxMap);
        SetFloat(material, "_Parallax", snapshot.parallax);
        SetFloat(material, "_Cull", snapshot.cull);
        SetFloat(material, "_Cutoff", snapshot.cutoff);
        SetFloat(material, "_AlphaClip", snapshot.alphaClip);
        SetFloat(material, "_ReceiveShadows", snapshot.receiveShadows);
        SetFloat(material, "_Surface", snapshot.surface);
        SetFloat(material, "_Blend", snapshot.blend);
        SetFloat(material, "_QueueOffset", snapshot.queueOffset);
        SetFloat(material, "_MemoryBlend", 0f);

        material.enableInstancing = snapshot.enableInstancing;
        material.doubleSidedGI = snapshot.doubleSidedGi;
        material.globalIlluminationFlags = snapshot.globalIlluminationFlags;

        SyncMaterialKeyword(material, "_NORMALMAP", snapshot.normalMap != null);
        SyncMaterialKeyword(material, "_METALLICSPECGLOSSMAP", snapshot.metallicGlossMap != null);

        bool hasEmission = snapshot.emissionMap != null || snapshot.emissionColor.maxColorComponent > 0.0001f;
        SyncMaterialKeyword(material, "_EMISSION", hasEmission);
    }

    private static void ApplyDefaultPainterlyTexturesIfNeeded(Material material)
    {
        if (material == null)
        {
            return;
        }

        TryAssignDefaultTexture(material, "_BrushRampTex", "Assets/_project/Art/Memory Materials/Textures/Brush_Ramp_01.png");
        TryAssignDefaultTexture(material, "_BrushGrainTex", "Assets/_project/Art/Memory Materials/Textures/Brush_Grain_01.png");
        TryAssignDefaultTexture(material, "_DryBrushTex", "Assets/_project/Art/Memory Materials/Textures/DryBrush_Noise_01.png");
        TryAssignDefaultTexture(material, "_WatercolorTex", "Assets/_project/Art/Memory Materials/Textures/Watercolor_Cloud_01.png");
        TryAssignDefaultTexture(material, "_EdgeBreakTex", "Assets/_project/Art/Memory Materials/Textures/EdgeBreak_Noise_01.png");
    }

    private static void TryAssignDefaultTexture(Material material, string propertyName, string assetPath)
    {
        if (material == null || !material.HasProperty(propertyName) || material.GetTexture(propertyName) != null)
        {
            return;
        }

        Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(assetPath);
        if (texture != null)
        {
            material.SetTexture(propertyName, texture);
        }
    }

    private static void EnsureTextureMarkedAsNormalMap(Texture texture, List<string> warnings)
    {
        if (texture == null)
        {
            return;
        }

        string texturePath = AssetDatabase.GetAssetPath(texture);
        if (string.IsNullOrWhiteSpace(texturePath))
        {
            return;
        }

        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer == null || importer.textureType == TextureImporterType.NormalMap)
        {
            return;
        }

        importer.textureType = TextureImporterType.NormalMap;
        importer.sRGBTexture = false;
        importer.SaveAndReimport();

        if (warnings != null)
        {
            warnings.Add($"Marked normal map texture importer correctly for: {texturePath}");
        }
    }

    private static Texture GetTexture(Material material, string propertyName)
    {
        return material != null && material.HasProperty(propertyName) ? material.GetTexture(propertyName) : null;
    }

    private static Color GetColor(Material material, string propertyName, Color fallback)
    {
        return material != null && material.HasProperty(propertyName) ? material.GetColor(propertyName) : fallback;
    }

    private static float GetFloat(Material material, string propertyName, float fallback)
    {
        return material != null && material.HasProperty(propertyName) ? material.GetFloat(propertyName) : fallback;
    }

    private static Vector2 GetTextureScale(Material material, string propertyName, Vector2 fallback)
    {
        return material != null && material.HasProperty(propertyName) ? material.GetTextureScale(propertyName) : fallback;
    }

    private static Vector2 GetTextureOffset(Material material, string propertyName, Vector2 fallback)
    {
        return material != null && material.HasProperty(propertyName) ? material.GetTextureOffset(propertyName) : fallback;
    }

    private static void SetTexture(Material material, string propertyName, Texture texture)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetTexture(propertyName, texture);
        }
    }

    private static void SetColor(Material material, string propertyName, Color color)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, color);
        }
    }

    private static void SetFloat(Material material, string propertyName, float value)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static void SetTextureScale(Material material, string propertyName, Vector2 scale)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetTextureScale(propertyName, scale);
        }
    }

    private static void SetTextureOffset(Material material, string propertyName, Vector2 offset)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetTextureOffset(propertyName, offset);
        }
    }

    private static void SyncMaterialKeyword(Material material, string keyword, bool enabled)
    {
        if (material == null || string.IsNullOrWhiteSpace(keyword))
        {
            return;
        }

        if (enabled)
        {
            material.EnableKeyword(keyword);
        }
        else
        {
            material.DisableKeyword(keyword);
        }
    }

    private static void ConfigureBoxCollider(GameObject root)
    {
        BoxCollider collider = root.GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = root.AddComponent<BoxCollider>();
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            collider.center = Vector3.zero;
            collider.size = Vector3.one * 0.25f;
            return;
        }

        Bounds combinedBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            combinedBounds.Encapsulate(renderers[i].bounds);
        }

        collider.center = root.transform.InverseTransformPoint(combinedBounds.center);
        Vector3 localSize = root.transform.InverseTransformVector(combinedBounds.size);
        collider.size = new Vector3(
            Mathf.Max(Mathf.Abs(localSize.x), BoundsMinSize),
            Mathf.Max(Mathf.Abs(localSize.y), BoundsMinSize),
            Mathf.Max(Mathf.Abs(localSize.z), BoundsMinSize));
    }

    private SpaceSegmentDefinition GetOrCreateDefinition(string assetPath)
    {
        EnsureParentFolderForAssetPath(assetPath);

        SpaceSegmentDefinition definition = AssetDatabase.LoadAssetAtPath<SpaceSegmentDefinition>(assetPath);
        if (definition != null)
        {
            return definition;
        }

        definition = ScriptableObject.CreateInstance<SpaceSegmentDefinition>();
        AssetDatabase.CreateAsset(definition, assetPath);
        return definition;
    }

    private void CheckTopLevelCategoryFolders(SegmentScanResult result, string sourceAbsolutePath)
    {
        string[] topLevelFolders = Directory.GetDirectories(sourceAbsolutePath, "*", SearchOption.TopDirectoryOnly);
        Array.Sort(topLevelFolders, StringComparer.OrdinalIgnoreCase);

        bool hasCellingFolder = false;
        bool hasCeilingFolder = false;

        for (int i = 0; i < topLevelFolders.Length; i++)
        {
            string folderName = Path.GetFileName(topLevelFolders[i]);
            string normalizedFolderName = NormalizeCategoryToken(folderName);

            if (string.Equals(normalizedFolderName, "celling", StringComparison.OrdinalIgnoreCase))
            {
                hasCellingFolder = true;
            }

            if (string.Equals(normalizedFolderName, "ceiling", StringComparison.OrdinalIgnoreCase))
            {
                hasCeilingFolder = true;
            }

            ParseCategory(folderName, out bool supportedFolderCategory);
            if (!supportedFolderCategory)
            {
                result.Warnings.Add($"Unsupported folder category under SegmentKit source: {folderName}");
            }
        }

        if (hasCellingFolder && hasCeilingFolder)
        {
            result.Warnings.Add("Both Celling and Ceiling folders exist under SegmentKit source. Keep only one naming convention.");
        }
    }

    private static SegmentCategory ParseCategory(string categoryFolderName, out bool supportedFolderCategory)
    {
        string normalized = NormalizeCategoryToken(categoryFolderName);
        supportedFolderCategory = true;

        switch (normalized)
        {
            case "floor":
                return SegmentCategory.Floor;
            case "wall":
                return SegmentCategory.Wall;
            case "ceiling":
            case "celling":
                return SegmentCategory.Ceiling;
            case "beam":
                return SegmentCategory.Beam;
            case "openingoverlay":
                return SegmentCategory.OpeningOverlay;
            case "threshold":
                return SegmentCategory.Threshold;
            case "corner":
                return SegmentCategory.Corner;
            case "custom":
                return SegmentCategory.Custom;
            default:
                supportedFolderCategory = false;
                return SegmentCategory.Custom;
        }
    }

    private static bool CategoryTokenMatches(string token, string categoryFolderName)
    {
        string normalizedToken = NormalizeCategoryToken(token);
        string normalizedFolder = NormalizeCategoryToken(categoryFolderName);

        if (string.Equals(normalizedToken, normalizedFolder, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if ((string.Equals(normalizedToken, "ceiling", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedToken, "celling", StringComparison.OrdinalIgnoreCase))
            && (string.Equals(normalizedFolder, "ceiling", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedFolder, "celling", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static bool TryResolveCategoryToken(string token, out SegmentCategory category)
    {
        category = ParseCategory(token, out bool supportedFolderCategory);
        return supportedFolderCategory;
    }

    private static bool TryParseVariant(string token, out SegmentVariant variant)
    {
        switch (NormalizeCategoryToken(token))
        {
            case "solid":
                variant = SegmentVariant.Solid;
                return true;
            case "empty":
                variant = SegmentVariant.Empty;
                return true;
            case "corner":
                variant = SegmentVariant.Corner;
                return true;
            case "doorcenter":
                variant = SegmentVariant.DoorCenter;
                return true;
            case "doorleft":
                variant = SegmentVariant.DoorLeft;
                return true;
            case "doorright":
                variant = SegmentVariant.DoorRight;
                return true;
            case "lowwall":
                variant = SegmentVariant.LowWall;
                return true;
            case "lattice":
                variant = SegmentVariant.Lattice;
                return true;
            case "default":
                variant = SegmentVariant.Default;
                return true;
            case "custom":
                variant = SegmentVariant.Custom;
                return true;
            default:
                variant = SegmentVariant.Default;
                return false;
        }
    }

    private static bool TryParseSizeToken(string token, out float x, out float y)
    {
        x = 1f;
        y = 1f;

        Match match = SizePattern.Match(token);
        if (!match.Success)
        {
            return false;
        }

        return float.TryParse(match.Groups["x"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out x)
               && float.TryParse(match.Groups["y"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out y);
    }

    private void EnsureConfiguredOutputFolders()
    {
        ValidateAssetFolderPath(prefabOutputFolder, "Prefab output folder");
        ValidateAssetFolderPath(definitionOutputFolder, "Definition output folder");

        EnsureFolder(prefabOutputFolder);
        EnsureFolder(definitionOutputFolder);
    }

    private static void ValidateAssetFolderPath(string assetPath, string label)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            throw new InvalidOperationException($"{label} cannot be empty.");
        }

        if (!NormalizeAssetPath(assetPath).StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(NormalizeAssetPath(assetPath), "Assets", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{label} must be under Assets/: {assetPath}");
        }
    }

    private static void EnsureFolder(string assetPath)
    {
        assetPath = NormalizeAssetPath(assetPath);
        if (AssetDatabase.IsValidFolder(assetPath))
        {
            return;
        }

        string parentPath = Path.GetDirectoryName(assetPath);
        string folderName = Path.GetFileName(assetPath);

        if (string.IsNullOrWhiteSpace(parentPath) || string.IsNullOrWhiteSpace(folderName))
        {
            throw new InvalidOperationException($"Could not create folder: {assetPath}");
        }

        parentPath = NormalizeAssetPath(parentPath);
        EnsureFolder(parentPath);
        AssetDatabase.CreateFolder(parentPath, folderName);
    }

    private static string ToAbsolutePath(string assetPath)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static string ToAssetPath(string absolutePath)
    {
        string normalizedAbsolute = NormalizeAssetPath(absolutePath);
        string normalizedDataPath = NormalizeAssetPath(Application.dataPath);

        if (!normalizedAbsolute.StartsWith(normalizedDataPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Path is outside the Unity Assets folder: {absolutePath}");
        }

        return $"Assets{normalizedAbsolute.Substring(normalizedDataPath.Length)}";
    }

    private static string NormalizeAssetPath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace("\\", "/");
    }

    private static string CombineAssetPath(string basePath, string relativeDirectoryPath, string fileName)
    {
        string normalizedBasePath = NormalizeAssetPath(basePath).TrimEnd('/');
        string normalizedRelativePath = NormalizeAssetPath(relativeDirectoryPath).Trim('/');

        if (string.IsNullOrWhiteSpace(normalizedRelativePath))
        {
            return $"{normalizedBasePath}/{fileName}";
        }

        return $"{normalizedBasePath}/{normalizedRelativePath}/{fileName}";
    }

    private static void EnsureParentFolderForAssetPath(string assetPath)
    {
        string parentPath = Path.GetDirectoryName(NormalizeAssetPath(assetPath));
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            return;
        }

        EnsureFolder(parentPath);
    }

    private static string NormalizeCategoryToken(string token)
    {
        return NormalizeAssetPath(token).Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static List<SegmentSourceInfo> GetUniqueSources(SegmentScanResult scan)
    {
        List<SegmentSourceInfo> uniqueSources = new List<SegmentSourceInfo>();
        HashSet<string> seenSegmentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < scan.Sources.Count; i++)
        {
            SegmentSourceInfo source = scan.Sources[i];
            if (seenSegmentIds.Add(source.segmentId))
            {
                uniqueSources.Add(source);
            }
        }

        return uniqueSources;
    }

    private static int CompareSources(SegmentSourceInfo a, SegmentSourceInfo b)
    {
        int segmentIdCompare = string.Compare(a.segmentId, b.segmentId, StringComparison.OrdinalIgnoreCase);
        if (segmentIdCompare != 0)
        {
            return segmentIdCompare;
        }

        return string.Compare(a.assetPath, b.assetPath, StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareDefinitions(SpaceSegmentDefinition a, SpaceSegmentDefinition b)
    {
        string aId = a != null ? a.segmentId : string.Empty;
        string bId = b != null ? b.segmentId : string.Empty;
        return string.Compare(aId, bId, StringComparison.OrdinalIgnoreCase);
    }

    private static void LogWarnings(List<string> warnings)
    {
        if (warnings == null)
        {
            return;
        }

        HashSet<string> seenWarnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < warnings.Count; i++)
        {
            string warning = warnings[i];
            if (string.IsNullOrWhiteSpace(warning) || !seenWarnings.Add(warning))
            {
                continue;
            }

            Debug.LogWarning($"[SpaceSegmentKitBuilder] {warning}");
        }
    }

    private static void ShowSummary(string message)
    {
        Debug.Log($"[SpaceSegmentKitBuilder] {message}");
        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog(WindowTitle, message, "OK");
        }
    }

    private static void ReportException(string actionLabel, Exception exception)
    {
        Debug.LogError($"[SpaceSegmentKitBuilder] {actionLabel} failed: {exception}");
        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog(WindowTitle, exception.Message, "OK");
        }
    }

    private sealed class SegmentScanResult
    {
        public readonly List<SegmentSourceInfo> Sources = new List<SegmentSourceInfo>();
        public readonly List<string> Warnings = new List<string>();
    }

    private enum MaterialMigrationStatus
    {
        Skipped,
        AlreadyPainterly,
        Migrated
    }

    private struct MaterialSurfaceSnapshot
    {
        public Texture baseMap;
        public Vector2 baseMapScale;
        public Vector2 baseMapOffset;
        public Color baseColor;
        public Texture normalMap;
        public float bumpScale;
        public Texture metallicGlossMap;
        public float metallic;
        public float smoothness;
        public Texture occlusionMap;
        public float occlusionStrength;
        public Texture emissionMap;
        public Color emissionColor;
        public Texture parallaxMap;
        public float parallax;
        public int cull;
        public float cutoff;
        public float alphaClip;
        public float receiveShadows;
        public float surface;
        public float blend;
        public float queueOffset;
        public bool enableInstancing;
        public bool doubleSidedGi;
        public MaterialGlobalIlluminationFlags globalIlluminationFlags;
    }

    private struct MaterialMigrationResult
    {
        public int migratedMaterialCount;
        public int alreadyPainterlyMaterialCount;
        public int embeddedMaterialCount;
    }

    private struct BuildPrefabResult
    {
        public string prefabAssetPath;
        public MaterialMigrationResult materialMigrationResult;
    }

    private sealed class SegmentSourceInfo
    {
        public string assetPath;
        public string definitionAssetPath;
        public bool hasCollider;
        public bool canBeOpeningOverlay;
        public bool canBeWallSegment;
        public SegmentCategory category;
        public float height;
        public bool hasValidWallSize;
        public string prefabAssetPath;
        public string primaryFolderName;
        public string segmentId;
        public Vector2 sizeXZ;
        public string styleId;
        public SegmentVariant variant;
    }
}
#endif

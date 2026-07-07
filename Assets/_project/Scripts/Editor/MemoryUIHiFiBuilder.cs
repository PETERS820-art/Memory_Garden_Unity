using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MemoryUIHiFiBuilder
{
    private const string RootName = "MemoryUIRoot_HiFi";
    private const float WorldCanvasScale = 0.001f;
    private const string HazeTexturePath = "Assets/_project/Art/Memory Materials/Textures/Watercolor_Cloud_01.png";
    private const string GrainTexturePath = "Assets/_project/Art/Memory Materials/Textures/Brush_Grain_01.png";
    private const string FrostedMaterialAssetPath = "Assets/_project/Art/Memory Materials/MemoryUIFrostedGlass.mat";
    private const string GeneratedUiSpriteFolder = "Assets/_project/Art/Generated/MemoryUI";
    private const string RoundedSpriteAssetPath = GeneratedUiSpriteFolder + "/MemoryUIRoundedRect.png";
    private const string CircleSpriteAssetPath = GeneratedUiSpriteFolder + "/MemoryUICircle.png";
    private static readonly Vector2 StoryBoardModuleAnchoredPosition = new Vector2(-78f, -44f);
    private static readonly Vector3 StoryBoardModuleScale = new Vector3(0.82f, 0.82f, 1f);
    private static readonly Vector3 StoryBoardModuleRotation = new Vector3(0f, -16f, 0f);
    private const float StoryBoardModuleDepth = 280f;
    private static readonly Vector2 InfoGridModuleAnchoredPosition = new Vector2(1188f, -74f);
    private static readonly Vector3 InfoGridModuleScale = new Vector3(0.80f, 0.80f, 1f);
    private static readonly Vector3 InfoGridModuleRotation = new Vector3(0f, 18f, 0f);
    private const float InfoGridModuleDepth = 320f;
    private static readonly Vector3 TimelineModuleScale = new Vector3(0.94f, 0.94f, 1f);
    private static readonly Vector3 TimelineModuleRotation = new Vector3(8f, 0f, 0f);
    private const float TimelineModuleDepth = 220f;

    private sealed class CardScaffold
    {
        public RectTransform root;
        public RectTransform motionRoot;
        public RectTransform shell;
        public RectTransform accentPanel;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI bodyText;
        public RectTransform tagContainer;
    }

    private struct GlassStyle
    {
        public Color fillColor;
        public Color borderColor;
        public Color topHighlightColor;
        public Color bottomShadeColor;
        public Color hazeColor;
        public Color grainColor;
        public Color shadowColor;
        public Vector2 shadowDistance;
    }

    private enum CardIconType
    {
        Photo,
        Story,
        Memories,
        Tags
    }

    private static TMP_FontAsset defaultFontAsset;
    private static TMP_FontAsset headlineFontAsset;
    private static TMP_FontAsset bodyFontAsset;
    private static TMP_FontAsset monoFontAsset;
    private static Sprite roundedSprite;
    private static Sprite circleSprite;
    private static Texture2D hazeTexture;
    private static Texture2D grainTexture;
    private static Material frostedMaterial;

    [MenuItem("Memory Garden/UI/Build VR Memory UI")]
    public static void BuildVRMemoryUI()
    {
        LoadVisualResources();

        GameObject existingRoot = FindSceneObject(RootName);
        if (existingRoot != null)
        {
            bool shouldReplace = EditorUtility.DisplayDialog(
                "Replace Existing HiFi UI",
                $"{RootName} already exists in the active scene.\n\nReplace the existing generated root and rebuild it?",
                "Replace",
                "Cancel");

            if (!shouldReplace)
            {
                Selection.activeGameObject = existingRoot;
                return;
            }

            Undo.DestroyObjectImmediate(existingRoot);
        }

        GameObject uiContainer = FindSceneObject("--03-UI");
        GameObject oldRoot = FindSceneObject("MemoryModelRoot");
        Transform parent = uiContainer != null
            ? uiContainer.transform
            : oldRoot != null && oldRoot.transform.parent != null
                ? oldRoot.transform.parent
                : null;

        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build VR Memory UI");
        if (parent != null)
        {
            root.transform.SetParent(parent, false);
        }

        CopyLayer(root, oldRoot != null ? oldRoot : uiContainer);
        root.AddComponent<CanvasGroup>();
        root.AddComponent<MemoryUIBillboard>();
        root.AddComponent<MemoryModeUIFollower>();
        MemoryUIRootController controller = root.AddComponent<MemoryUIRootController>();

        if (oldRoot != null)
        {
            root.transform.SetPositionAndRotation(oldRoot.transform.position + (oldRoot.transform.right * 2.1f), oldRoot.transform.rotation);
        }

        RectTransform canvasRect = CreateCanvas(root.transform, oldRoot);
        BuildStoryBoardModule(canvasRect);
        BuildInfoGridModule(canvasRect);
        BuildTimelineModule(canvasRect);

        controller.SetStaticDemoContent();
        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("[MemoryUIHiFiBuilder] Rebuilt MemoryUIRoot_HiFi with motion-ready roots and layered glass styling.");
    }

    private static void LoadVisualResources()
    {
        EnsureGeneratedSpriteAssets();
        defaultFontAsset = ResolveDefaultFontAsset();
        headlineFontAsset = defaultFontAsset;
        bodyFontAsset = defaultFontAsset;
        monoFontAsset = defaultFontAsset;
        roundedSprite = ResolveRoundedSprite();
        circleSprite = ResolveCircleSprite();
        hazeTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(HazeTexturePath);
        grainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(GrainTexturePath);
        frostedMaterial = AssetDatabase.LoadAssetAtPath<Material>(FrostedMaterialAssetPath);

        if (defaultFontAsset == null)
        {
            Debug.LogWarning("[MemoryUIHiFiBuilder] TextMesh Pro essentials appear to be missing. Text objects will still be created, but font assignment may need manual repair.");
        }
    }

    private static RectTransform CreateCanvas(Transform parent, GameObject oldRoot)
    {
        GameObject canvasObject = new GameObject("Canvas_WorldSpace", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(parent, false);
        CopyLayer(canvasObject, oldRoot);

        RectTransform rect = canvasObject.GetComponent<RectTransform>();
        SetRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920f, 1080f));
        rect.localScale = Vector3.one * WorldCanvasScale;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.pixelPerfect = false;
        canvas.sortingOrder = 10;
        canvas.worldCamera = Camera.main;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.dynamicPixelsPerUnit = 24f;

        AddBestRaycaster(canvasObject);
        return rect;
    }

    private static void BuildStoryBoardModule(RectTransform canvas)
    {
        RectTransform module = CreateMotionContainer("StoryBoardModule", canvas, out RectTransform motionRoot);
        module.gameObject.AddComponent<StoryBoardModuleView>();
        SetRect(module, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), StoryBoardModuleAnchoredPosition, new Vector2(860f, 720f));
        ApplyModuleSpatialPose(motionRoot, StoryBoardModuleScale, StoryBoardModuleRotation, StoryBoardModuleDepth);

        CreateText(
            motionRoot,
            "ObjectPillLabel",
            "Object Pill",
            16f,
            new Color(0.74f, 0.71f, 0.78f, 0.85f),
            TextAlignmentOptions.TopLeft,
            FontStyles.Normal,
            bodyFontAsset,
            0f,
            0f,
            false);
        SetRect((RectTransform)motionRoot.Find("ObjectPillLabel"), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(220f, 24f));
        motionRoot.Find("ObjectPillLabel").gameObject.SetActive(false);

        RectTransform headerPill = CreateMotionContainer("HeaderPill", motionRoot, out RectTransform headerMotion);
        SetRect(headerPill, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -28f), new Vector2(246f, 60f));
        RectTransform pillShell = CreateGlassPanel(
            headerMotion,
            "PillShell",
            CreateGlassStyle(
                new Color(0.22f, 0.20f, 0.24f, 0.90f),
                new Color(0.78f, 0.74f, 0.82f, 0.34f),
                new Color(1f, 1f, 1f, 0.06f),
                new Color(0f, 0f, 0f, 0.16f),
                new Color(1f, 1f, 1f, 0.035f),
                new Color(1f, 1f, 1f, 0.03f),
                new Color(0.03f, 0.03f, 0.08f, 0.26f),
                new Vector2(0f, -6f)));
        StretchToParent(pillShell, 0f, 0f, 0f, 0f);
        AttachBeveledBacker(pillShell, "BeveledBacker3D", -5f, 8f, 28f, 4f, 0.14f, 0.12f);

        RectTransform icon = CreateGlassPanel(
            pillShell,
            "Icon",
            CreateGlassStyle(
                new Color(0.35f, 0.34f, 0.37f, 0.45f),
                new Color(1f, 1f, 1f, 0.08f),
                new Color(1f, 1f, 1f, 0.03f),
                new Color(0f, 0f, 0f, 0.08f),
                new Color(1f, 1f, 1f, 0.02f),
                new Color(1f, 1f, 1f, 0f),
                new Color(0f, 0f, 0f, 0.12f),
                new Vector2(0f, -2f)));
        SetRect(icon, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(34f, 34f));

        CreateText(
            pillShell,
            "ObjectNameText",
            "ITEM NAME",
            19f,
            new Color(0.98f, 0.97f, 1f, 1f),
            TextAlignmentOptions.Left,
            FontStyles.Bold,
            bodyFontAsset,
            6f,
            0f,
            false);
        SetRect((RectTransform)pillShell.Find("ObjectNameText"), new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(58f, 0f), new Vector2(-78f, 24f));

        CreateText(
            motionRoot,
            "StoryPanelLabel",
            "Story Panel",
            16f,
            new Color(0.74f, 0.71f, 0.78f, 0.85f),
            TextAlignmentOptions.TopLeft,
            FontStyles.Normal,
            bodyFontAsset,
            0f,
            0f,
            false);
        SetRect((RectTransform)motionRoot.Find("StoryPanelLabel"), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -90f), new Vector2(220f, 24f));
        motionRoot.Find("StoryPanelLabel").gameObject.SetActive(false);

        RectTransform panel = CreateGlassPanel(
            motionRoot,
            "GlassPanel",
            CreateGlassStyle(
                new Color(0.21f, 0.19f, 0.21f, 0.92f),
                new Color(0.75f, 0.71f, 0.80f, 0.30f),
                new Color(1f, 1f, 1f, 0.05f),
                new Color(0f, 0f, 0f, 0.16f),
                new Color(1f, 1f, 1f, 0.04f),
                new Color(1f, 1f, 1f, 0.03f),
                new Color(0.03f, 0.03f, 0.07f, 0.30f),
                new Vector2(0f, -16f)));
        SetRect(panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -118f), new Vector2(820f, 640f));
        AttachBeveledBacker(panel, "BeveledBacker3D", -8f, 14f, 36f, 5f, 0.04f, 1.01f);

        RectTransform content = CreateUIObject("Content", motionRoot);
        SetRect(content, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(54f, -186f), new Vector2(708f, 512f));

        CreateText(
            content,
            "ModuleLabelText",
            "FEATURED MEMORY",
            16f,
            new Color(1f, 0.80f, 0.66f, 1f),
            TextAlignmentOptions.TopLeft,
            FontStyles.Bold,
            bodyFontAsset,
            3f,
            0f,
            false);
        SetRect((RectTransform)content.Find("ModuleLabelText"), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(260f, 24f));
        content.Find("ModuleLabelText").gameObject.SetActive(false);

        CreateText(
            content,
            "TitleText",
            "STORY BOARD",
            66f,
            new Color(0.98f, 0.96f, 1f, 1f),
            TextAlignmentOptions.TopLeft,
            FontStyles.Normal,
            headlineFontAsset,
            -0.5f,
            0f,
            false);
        SetRect((RectTransform)content.Find("TitleText"), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, -56f), new Vector2(0f, 80f));

        CreateText(
            content,
            "BodyText",
            "This teddy bear was a birthday gift from Mom when I was 6. It has been with me through every move and every milestone.",
            28f,
            new Color(0.90f, 0.86f, 0.96f, 1f),
            TextAlignmentOptions.TopLeft,
            FontStyles.Normal,
            bodyFontAsset,
            0f,
            14f,
            true);
        SetRect((RectTransform)content.Find("BodyText"), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, -166f), new Vector2(-16f, 154f));

        RectTransform tagGroup = CreateUIObject("TagPillGroup", content);
        SetRect(tagGroup, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, -346f), new Vector2(0f, 60f));
        HorizontalLayoutGroup tagLayout = tagGroup.gameObject.AddComponent<HorizontalLayoutGroup>();
        tagLayout.spacing = 16f;
        tagLayout.childAlignment = TextAnchor.MiddleLeft;
        tagLayout.childControlWidth = false;
        tagLayout.childControlHeight = false;
        tagLayout.childForceExpandWidth = false;
        tagLayout.childForceExpandHeight = false;

        CreateTagTemplate(tagGroup, "TagPill_01");
    }

    private static void BuildInfoGridModule(RectTransform canvas)
    {
        RectTransform module = CreateMotionContainer("InfoGridModule", canvas, out RectTransform motionRoot);
        module.gameObject.AddComponent<InfoGridModuleView>();
        SetRect(module, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), InfoGridModuleAnchoredPosition, new Vector2(746f, 720f));
        ApplyModuleSpatialPose(motionRoot, InfoGridModuleScale, InfoGridModuleRotation, InfoGridModuleDepth);

        CreateText(
            motionRoot,
            "IntroTextBlock",
            "Info Grid",
            18f,
            new Color(0.74f, 0.71f, 0.78f, 0.90f),
            TextAlignmentOptions.TopLeft,
            FontStyles.Normal,
            bodyFontAsset,
            0f,
            0f,
            false);
        SetRect((RectTransform)motionRoot.Find("IntroTextBlock"), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(220f, 24f));
        motionRoot.Find("IntroTextBlock").gameObject.SetActive(false);

        RectTransform cardGrid = CreateUIObject("CardGrid", motionRoot);
        SetRect(cardGrid, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -38f), new Vector2(746f, 700f));

        BuildPhotoCardHiFi(cardGrid, new Vector2(0f, 0f));
        BuildStoryCardHiFi(cardGrid, new Vector2(382f, 0f));
        BuildMemoriesCardHiFi(cardGrid, new Vector2(0f, -360f));
        BuildTagsCardHiFi(cardGrid, new Vector2(382f, -360f));
    }

    private static void BuildPhotoCardHiFi(Transform parent, Vector2 position)
    {
        CardScaffold card = CreateInfoCardScaffoldHiFi(parent, "PhotoCard", position, "PHOTO", "12", CardIconType.Photo);

        RectTransform back = CreateDecorativePanel(card.shell, "PhotoBack", new Color(1f, 1f, 1f, 0.05f), new Color(1f, 1f, 1f, 0.08f));
        SetRect(back, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -92f), new Vector2(186f, 186f));
        back.localRotation = Quaternion.Euler(0f, 0f, -5f);
        CreatePhotoViewport(back, "PhotoViewport_03", "PhotoImage_03", 6f);

        RectTransform mid = CreateDecorativePanel(card.shell, "PhotoMid", new Color(1f, 1f, 1f, 0.08f), new Color(1f, 1f, 1f, 0.08f));
        SetRect(mid, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(84f, -108f), new Vector2(186f, 186f));
        mid.localRotation = Quaternion.Euler(0f, 0f, 2f);
        CreatePhotoViewport(mid, "PhotoViewport_02", "PhotoImage_02", 6f);

        SetRect(card.accentPanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(110f, -120f), new Vector2(196f, 196f));
        card.accentPanel.localRotation = Quaternion.Euler(0f, 0f, 1.5f);
        CreatePhotoViewport(card.accentPanel, "PhotoViewport_01", "PhotoImage_01", 6f);

        SetRect(card.titleText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(20f, 46f), new Vector2(-40f, 24f));
        SetRect(card.bodyText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(20f, 18f), new Vector2(-40f, 22f));
    }

    private static void BuildStoryCardHiFi(Transform parent, Vector2 position)
    {
        CardScaffold card = CreateInfoCardScaffoldHiFi(parent, "StoryCard", position, "STORY", "5", CardIconType.Story);

        card.accentPanel.gameObject.SetActive(false);

        SetRect(card.titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(18f, -72f), new Vector2(-36f, 24f));
        SetRect(card.bodyText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(18f, -82f), new Vector2(-36f, 124f));
        card.bodyText.fontSize = 18f;
        card.bodyText.lineSpacing = 10f;
    }

    private static void BuildMemoriesCardHiFi(Transform parent, Vector2 position)
    {
        CardScaffold card = CreateInfoCardScaffoldHiFi(parent, "MemoriesCard", position, "SOUNDS", "8", CardIconType.Memories);

        SetRect(card.accentPanel, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(28f, -2f), new Vector2(42f, 42f));
        RectTransform pulseBarsViewport = CreateUIObject("PulseBarsViewport", card.shell);
        SetRect(pulseBarsViewport, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(60f, 92f), new Vector2(-124f, 46f));
        RectMask2D pulseMask = pulseBarsViewport.gameObject.AddComponent<RectMask2D>();
        pulseMask.padding = Vector4.zero;

        RectTransform pulseBars = CreateUIObject("PulseBars", pulseBarsViewport);
        StretchToParent(pulseBars, 0f, 0f, 0f, 0f);

        int[] heights = { 10, 18, 14, 24, 16, 28, 20, 12, 22, 16 };
        float startX = 0f;
        float spacing = 16f;
        for (int i = 0; i < heights.Length; i++)
        {
            RectTransform bar = CreateUIObject($"Bar_{i + 1:00}", pulseBars);
            SetRect(
                bar,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(startX + (i * spacing), 0f),
                new Vector2(10f, heights[i]));
            Image image = bar.gameObject.AddComponent<Image>();
            ApplyImageSprite(image);
            image.color = i == 0
                ? new Color(0.83f, 0.71f, 1f, 0.62f)
                : new Color(1f, 1f, 1f, 0.12f);
            image.raycastTarget = false;
        }

        CreateText(
            card.shell,
            "DurationText",
            "00:45",
            16f,
            new Color(0.84f, 0.80f, 0.92f, 1f),
            TextAlignmentOptions.BottomRight,
            FontStyles.Normal,
            monoFontAsset,
            0f,
            0f,
            false);
        SetRect((RectTransform)card.shell.Find("DurationText"), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 16f), new Vector2(80f, 20f));

        SetRect(card.titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(18f, -74f), new Vector2(-36f, 24f));
        SetRect(card.bodyText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(18f, 18f), new Vector2(-36f, 80f));
        card.bodyText.fontSize = 16f;
    }

    private static void BuildTagsCardHiFi(Transform parent, Vector2 position)
    {
        CardScaffold card = CreateInfoCardScaffoldHiFi(parent, "TagsCard", position, "TAGS", "6", CardIconType.Tags);

        card.accentPanel.gameObject.SetActive(false);

        card.tagContainer = CreateUIObject("TagContainer", card.shell);
        SetRect(card.tagContainer, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(18f, -76f), new Vector2(-36f, 210f));

        RectTransform tagsWrap = CreateUIObject("TagsWrap", card.tagContainer);
        StretchToParent(tagsWrap, 0f, 0f, 0f, 0f);
        HorizontalLayoutGroup wrapLayout = tagsWrap.gameObject.AddComponent<HorizontalLayoutGroup>();
        wrapLayout.spacing = 12f;
        wrapLayout.childAlignment = TextAnchor.UpperLeft;
        wrapLayout.childControlWidth = true;
        wrapLayout.childControlHeight = false;
        wrapLayout.childForceExpandWidth = true;
        wrapLayout.childForceExpandHeight = false;

        RectTransform tagColumnA = CreateTagColumn(tagsWrap, "TagColumnA");
        CreateTagColumn(tagsWrap, "TagColumnB");
        CreateTagTemplate(tagColumnA, "TagPill_01");

        SetRect(card.titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(18f, -74f), new Vector2(-36f, 24f));
        card.titleText.gameObject.SetActive(false);

        SetRect(card.bodyText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(18f, 18f), new Vector2(-36f, 18f));
        card.bodyText.gameObject.SetActive(false);
    }

    private static CardScaffold CreateInfoCardScaffoldHiFi(Transform parent, string name, Vector2 anchoredPosition, string header, string badge, CardIconType iconType)
    {
        RectTransform root = CreateMotionContainer(name, parent, out RectTransform motionRoot);
        root.gameObject.AddComponent<InfoCardView>();
        SetRect(root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), anchoredPosition, new Vector2(364f, 340f));

        RectTransform shell = CreateGlassPanel(
            motionRoot,
            "CardShell",
            CreateGlassStyle(
                new Color(0.21f, 0.20f, 0.22f, 0.90f),
                new Color(0.74f, 0.70f, 0.78f, 0.25f),
                new Color(1f, 1f, 1f, 0.045f),
                new Color(0f, 0f, 0f, 0.14f),
                new Color(1f, 1f, 1f, 0.035f),
                new Color(1f, 1f, 1f, 0.028f),
                new Color(0.03f, 0.03f, 0.07f, 0.28f),
                new Vector2(0f, -12f)));
        StretchToParent(shell, 0f, 0f, 0f, 0f);
        AttachBeveledBacker(shell, "BeveledBacker3D", -8f, 14f, 36f, 5f, 0.05f, 1.01f);

        RectTransform iconShell = CreateGlassPanel(
            shell,
            "IconShell",
            CreateGlassStyle(
                new Color(1f, 1f, 1f, 0.08f),
                new Color(1f, 1f, 1f, 0.06f),
                new Color(1f, 1f, 1f, 0.02f),
                new Color(0f, 0f, 0f, 0.05f),
                new Color(1f, 1f, 1f, 0f),
                new Color(1f, 1f, 1f, 0f),
                new Color(0f, 0f, 0f, 0.08f),
                new Vector2(0f, -2f)));
        SetRect(iconShell, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(38f, 38f));

        RectTransform iconGlyph = CreateUIObject("IconGlyph", iconShell);
        StretchToParent(iconGlyph, 0f, 0f, 0f, 0f);
        CreateCardIcon(iconGlyph, iconType);

        CreateText(
            shell,
            "HeaderText",
            header,
            18f,
            new Color(0.98f, 0.97f, 1f, 1f),
            TextAlignmentOptions.Left,
            FontStyles.Bold,
            bodyFontAsset,
            4f,
            0f,
            false);
        SetRect((RectTransform)shell.Find("HeaderText"), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(68f, -20f), new Vector2(-134f, 22f));

        RectTransform badgeShell = CreateGlassPanel(
            shell,
            "BadgeShell",
            CreateGlassStyle(
                new Color(0.39f, 0.33f, 0.30f, 0.34f),
                new Color(0.98f, 0.81f, 0.69f, 0.32f),
                new Color(1f, 1f, 1f, 0.03f),
                new Color(0f, 0f, 0f, 0.07f),
                new Color(1f, 1f, 1f, 0f),
                new Color(1f, 1f, 1f, 0f),
                new Color(0f, 0f, 0f, 0.10f),
                new Vector2(0f, -2f)));
        SetRect(badgeShell, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -18f), new Vector2(42f, 30f));

        CreateText(
            badgeShell,
            "BadgeText",
            badge,
            14f,
            new Color(1f, 0.82f, 0.66f, 1f),
            TextAlignmentOptions.Center,
            FontStyles.Normal,
            monoFontAsset,
            0f,
            0f,
            false);
        StretchToParent((RectTransform)badgeShell.Find("BadgeText"), 0f, 0f, 0f, 0f);

        RectTransform accentPanel = CreateGlassPanel(
            shell,
            "AccentPanel",
            CreateGlassStyle(
                new Color(0.83f, 0.70f, 1f, 0.22f),
                new Color(1f, 1f, 1f, 0.08f),
                new Color(1f, 1f, 1f, 0.04f),
                new Color(0f, 0f, 0f, 0.06f),
                new Color(1f, 1f, 1f, 0.04f),
                new Color(1f, 1f, 1f, 0.02f),
                new Color(0.02f, 0.02f, 0.08f, 0.20f),
                new Vector2(0f, -8f)));
        SetRect(accentPanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(98f, -118f), new Vector2(188f, 112f));

        TextMeshProUGUI titleText = CreateText(
            shell,
            "TitleText",
            "Card Title",
            24f,
            new Color(0.97f, 0.96f, 1f, 1f),
            TextAlignmentOptions.TopLeft,
            FontStyles.Normal,
            bodyFontAsset,
            0f,
            0f,
            true);
        SetRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(18f, -74f), new Vector2(-36f, 32f));

        TextMeshProUGUI bodyText = CreateText(
            shell,
            "BodyText",
            "Card body",
            16f,
            new Color(0.90f, 0.86f, 0.96f, 1f),
            TextAlignmentOptions.TopLeft,
            FontStyles.Normal,
            bodyFontAsset,
            0f,
            8f,
            true);
        SetRect(bodyText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(18f, 18f), new Vector2(-36f, 88f));

        return new CardScaffold
        {
            root = root,
            motionRoot = motionRoot,
            shell = shell,
            accentPanel = accentPanel,
            titleText = titleText,
            bodyText = bodyText
        };
    }

    private static void BuildPhotoCard(Transform parent, Vector2 position)
    {
        CardScaffold card = CreateInfoCardScaffold(parent, "PhotoCard", position, "PHOTO", "12", "◫");

        RectTransform back = CreateDecorativePanel(card.shell, "PhotoBack", new Color(1f, 1f, 1f, 0.05f), new Color(1f, 1f, 1f, 0.08f));
        SetRect(back, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(54f, -92f), new Vector2(186f, 110f));
        back.localRotation = Quaternion.Euler(0f, 0f, -5f);

        RectTransform mid = CreateDecorativePanel(card.shell, "PhotoMid", new Color(1f, 1f, 1f, 0.08f), new Color(1f, 1f, 1f, 0.08f));
        SetRect(mid, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(86f, -112f), new Vector2(186f, 96f));
        mid.localRotation = Quaternion.Euler(0f, 0f, 2f);

        SetRect(card.accentPanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(116f, -122f), new Vector2(196f, 120f));
        card.accentPanel.localRotation = Quaternion.Euler(0f, 0f, 1.5f);

        SetRect(card.titleText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(20f, 46f), new Vector2(-40f, 24f));
        SetRect(card.bodyText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(20f, 18f), new Vector2(-40f, 22f));
    }

    private static void BuildStoryCard(Transform parent, Vector2 position)
    {
        CardScaffold card = CreateInfoCardScaffold(parent, "StoryCard", position, "STORY", "5", "▥");

        card.accentPanel.GetComponent<Image>().color = new Color(0.78f, 0.72f, 0.88f, 0.10f);
        SetRect(card.accentPanel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(-36f, 1f));

        SetRect(card.titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(18f, -72f), new Vector2(-36f, 24f));
        SetRect(card.bodyText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(18f, -82f), new Vector2(-36f, 124f));
        card.bodyText.fontSize = 18f;
        card.bodyText.lineSpacing = 10f;
    }

    private static void BuildMemoriesCard(Transform parent, Vector2 position)
    {
        CardScaffold card = CreateInfoCardScaffold(parent, "MemoriesCard", position, "MEMORIES", "8", "◍");

        SetRect(card.accentPanel, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(38f, -4f), new Vector2(52f, 52f));
        RectTransform pulseBars = CreateUIObject("PulseBars", card.shell);
        SetRect(pulseBars, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(64f, 82f), new Vector2(-84f, 52f));
        HorizontalLayoutGroup barsLayout = pulseBars.gameObject.AddComponent<HorizontalLayoutGroup>();
        barsLayout.spacing = 10f;
        barsLayout.childControlWidth = false;
        barsLayout.childControlHeight = false;
        barsLayout.childForceExpandWidth = false;
        barsLayout.childForceExpandHeight = false;
        barsLayout.childAlignment = TextAnchor.LowerCenter;

        int[] heights = { 16, 28, 22, 38, 26, 44, 32, 20, 36, 28 };
        for (int i = 0; i < heights.Length; i++)
        {
            RectTransform bar = CreateUIObject($"Bar_{i + 1:00}", pulseBars);
            LayoutElement layout = bar.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 12f;
            layout.preferredHeight = heights[i];
            Image image = bar.gameObject.AddComponent<Image>();
            ApplyImageSprite(image);
            image.color = i == 0
                ? new Color(0.83f, 0.71f, 1f, 0.62f)
                : new Color(1f, 1f, 1f, 0.12f);
            image.raycastTarget = false;
        }

        CreateText(
            card.shell,
            "DurationText",
            "00:45",
            16f,
            new Color(0.84f, 0.80f, 0.92f, 1f),
            TextAlignmentOptions.BottomRight,
            FontStyles.Normal,
            monoFontAsset,
            0f,
            0f,
            false);
        SetRect((RectTransform)card.shell.Find("DurationText"), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 16f), new Vector2(80f, 20f));

        SetRect(card.titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(18f, -74f), new Vector2(-36f, 24f));
        SetRect(card.bodyText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(18f, 18f), new Vector2(-36f, 80f));
        card.bodyText.fontSize = 16f;
    }

    private static void BuildTagsCard(Transform parent, Vector2 position)
    {
        CardScaffold card = CreateInfoCardScaffold(parent, "TagsCard", position, "TAGS", "6", "◆");

        card.accentPanel.GetComponent<Image>().color = new Color(0.94f, 0.91f, 1f, 0.08f);
        SetRect(card.accentPanel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(-36f, 1f));

        card.tagContainer = CreateUIObject("TagContainer", card.shell);
        SetRect(card.tagContainer, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(18f, -76f), new Vector2(-36f, 196f));
        GridLayoutGroup tagGrid = card.tagContainer.gameObject.AddComponent<GridLayoutGroup>();
        tagGrid.cellSize = new Vector2(150f, 42f);
        tagGrid.spacing = new Vector2(12f, 14f);
        tagGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        tagGrid.constraintCount = 2;
        tagGrid.childAlignment = TextAnchor.UpperLeft;

        CreateTagTemplate(card.tagContainer, "TagPill_01");

        SetRect(card.titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(18f, -74f), new Vector2(-36f, 24f));
        card.titleText.gameObject.SetActive(false);

        SetRect(card.bodyText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(18f, 18f), new Vector2(-36f, 18f));
        card.bodyText.gameObject.SetActive(false);
    }

    private static CardScaffold CreateInfoCardScaffold(Transform parent, string name, Vector2 anchoredPosition, string header, string badge, string iconGlyph)
    {
        RectTransform root = CreateMotionContainer(name, parent, out RectTransform motionRoot);
        root.gameObject.AddComponent<InfoCardView>();
        SetRect(root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), anchoredPosition, new Vector2(364f, 340f));

        RectTransform shell = CreateGlassPanel(
            motionRoot,
            "CardShell",
            CreateGlassStyle(
                new Color(0.21f, 0.20f, 0.22f, 0.90f),
                new Color(0.74f, 0.70f, 0.78f, 0.25f),
                new Color(1f, 1f, 1f, 0.045f),
                new Color(0f, 0f, 0f, 0.14f),
                new Color(1f, 1f, 1f, 0.035f),
                new Color(1f, 1f, 1f, 0.028f),
                new Color(0.03f, 0.03f, 0.07f, 0.28f),
                new Vector2(0f, -12f)));
        StretchToParent(shell, 0f, 0f, 0f, 0f);

        RectTransform iconShell = CreateGlassPanel(
            shell,
            "IconShell",
            CreateGlassStyle(
                new Color(1f, 1f, 1f, 0.08f),
                new Color(1f, 1f, 1f, 0.06f),
                new Color(1f, 1f, 1f, 0.02f),
                new Color(0f, 0f, 0f, 0.05f),
                new Color(1f, 1f, 1f, 0f),
                new Color(1f, 1f, 1f, 0f),
                new Color(0f, 0f, 0f, 0.08f),
                new Vector2(0f, -2f)));
        SetRect(iconShell, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(38f, 38f));

        CreateText(
            iconShell,
            "IconGlyph",
            iconGlyph,
            14f,
            new Color(0.97f, 0.95f, 1f, 1f),
            TextAlignmentOptions.Center,
            FontStyles.Bold,
            bodyFontAsset,
            0f,
            0f,
            false);
        StretchToParent((RectTransform)iconShell.Find("IconGlyph"), 0f, 0f, 0f, 0f);

        CreateText(
            shell,
            "HeaderText",
            header,
            18f,
            new Color(0.98f, 0.97f, 1f, 1f),
            TextAlignmentOptions.Left,
            FontStyles.Bold,
            bodyFontAsset,
            4f,
            0f,
            false);
        SetRect((RectTransform)shell.Find("HeaderText"), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(68f, -20f), new Vector2(-134f, 22f));

        RectTransform badgeShell = CreateGlassPanel(
            shell,
            "BadgeShell",
            CreateGlassStyle(
                new Color(0.39f, 0.33f, 0.30f, 0.34f),
                new Color(0.98f, 0.81f, 0.69f, 0.32f),
                new Color(1f, 1f, 1f, 0.03f),
                new Color(0f, 0f, 0f, 0.07f),
                new Color(1f, 1f, 1f, 0f),
                new Color(1f, 1f, 1f, 0f),
                new Color(0f, 0f, 0f, 0.10f),
                new Vector2(0f, -2f)));
        SetRect(badgeShell, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -18f), new Vector2(42f, 30f));

        CreateText(
            badgeShell,
            "BadgeText",
            badge,
            14f,
            new Color(1f, 0.82f, 0.66f, 1f),
            TextAlignmentOptions.Center,
            FontStyles.Normal,
            monoFontAsset,
            0f,
            0f,
            false);
        StretchToParent((RectTransform)badgeShell.Find("BadgeText"), 0f, 0f, 0f, 0f);

        RectTransform accentPanel = CreateGlassPanel(
            shell,
            "AccentPanel",
            CreateGlassStyle(
                new Color(0.83f, 0.70f, 1f, 0.22f),
                new Color(1f, 1f, 1f, 0.08f),
                new Color(1f, 1f, 1f, 0.04f),
                new Color(0f, 0f, 0f, 0.06f),
                new Color(1f, 1f, 1f, 0.04f),
                new Color(1f, 1f, 1f, 0.02f),
                new Color(0.02f, 0.02f, 0.08f, 0.20f),
                new Vector2(0f, -8f)));
        SetRect(accentPanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(98f, -118f), new Vector2(188f, 112f));

        TextMeshProUGUI titleText = CreateText(
            shell,
            "TitleText",
            "Card Title",
            24f,
            new Color(0.97f, 0.96f, 1f, 1f),
            TextAlignmentOptions.TopLeft,
            FontStyles.Normal,
            bodyFontAsset,
            0f,
            0f,
            true);
        SetRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(18f, -74f), new Vector2(-36f, 32f));

        TextMeshProUGUI bodyText = CreateText(
            shell,
            "BodyText",
            "Card body",
            16f,
            new Color(0.90f, 0.86f, 0.96f, 1f),
            TextAlignmentOptions.TopLeft,
            FontStyles.Normal,
            bodyFontAsset,
            0f,
            8f,
            true);
        SetRect(bodyText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(18f, 18f), new Vector2(-36f, 88f));

        return new CardScaffold
        {
            root = root,
            motionRoot = motionRoot,
            shell = shell,
            accentPanel = accentPanel,
            titleText = titleText,
            bodyText = bodyText
        };
    }

    private static void BuildTimelineModule(RectTransform canvas)
    {
        RectTransform module = CreateMotionContainer("TimelineModule", canvas, out RectTransform motionRoot);
        module.gameObject.AddComponent<TimelineModuleView>();
        SetRect(module, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(-84f, 172f));
        ApplyModuleSpatialPose(motionRoot, TimelineModuleScale, TimelineModuleRotation, TimelineModuleDepth);

        CreateText(
            motionRoot,
            "IntroTextBlock",
            "Timeline Shell",
            18f,
            new Color(0.74f, 0.71f, 0.78f, 0.90f),
            TextAlignmentOptions.TopLeft,
            FontStyles.Normal,
            bodyFontAsset,
            0f,
            0f,
            false);
        SetRect((RectTransform)motionRoot.Find("IntroTextBlock"), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(220f, 24f));
        motionRoot.Find("IntroTextBlock").gameObject.SetActive(false);

        RectTransform timelineBar = CreateMotionContainer("TimelineBar", motionRoot, out RectTransform timelineMotion);
        SetRect(timelineBar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, -8f), new Vector2(0f, 148f));

        RectTransform shell = CreateGlassPanel(
            timelineMotion,
            "Shell",
            CreateGlassStyle(
                new Color(0.22f, 0.20f, 0.22f, 0.92f),
                new Color(0.74f, 0.70f, 0.78f, 0.24f),
                new Color(1f, 1f, 1f, 0.04f),
                new Color(0f, 0f, 0f, 0.16f),
                new Color(1f, 1f, 1f, 0.03f),
                new Color(1f, 1f, 1f, 0.02f),
                new Color(0.03f, 0.03f, 0.07f, 0.28f),
                new Vector2(0f, -14f)));
        StretchToParent(shell, 0f, 0f, 0f, 0f);
        AttachBeveledBacker(shell, "BeveledBacker3D", -8f, 14f, 36f, 5f, 0.04f, 1.01f);

        CreateText(
            shell,
            "TimelineTitle",
            "TIME LINE",
            30f,
            new Color(0.98f, 0.96f, 1f, 1f),
            TextAlignmentOptions.TopLeft,
            FontStyles.Normal,
            headlineFontAsset,
            0f,
            0f,
            false);
        SetRect((RectTransform)shell.Find("TimelineTitle"), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -24f), new Vector2(320f, 48f));

        RectTransform line = CreateUIObject("TimelineLine", shell);
        Image lineImage = line.gameObject.AddComponent<Image>();
        ApplyImageSprite(lineImage);
        lineImage.color = new Color(0.60f, 0.55f, 0.65f, 0.32f);
        lineImage.raycastTarget = false;
        SetRect(line, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 54f), new Vector2(-120f, 2f));

        RectTransform nodeGroup = CreateUIObject("TimelineNodeGroup", shell);
        SetRect(nodeGroup, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(-96f, 82f));
        HorizontalLayoutGroup nodeLayout = nodeGroup.gameObject.AddComponent<HorizontalLayoutGroup>();
        nodeLayout.padding = new RectOffset(42, 42, 0, 0);
        nodeLayout.spacing = 0f;
        nodeLayout.childControlWidth = true;
        nodeLayout.childControlHeight = true;
        nodeLayout.childForceExpandWidth = true;
        nodeLayout.childForceExpandHeight = false;
        nodeLayout.childAlignment = TextAnchor.UpperCenter;

        CreateTimelineNode(nodeGroup, "TimelineNode_01");

        RectTransform leftButton = CreateCircleButton(shell, "LeftArrowButton", "<");
        SetRect(leftButton, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-108f, -16f), new Vector2(38f, 38f));

        RectTransform rightButton = CreateCircleButton(shell, "RightArrowButton", ">");
        SetRect(rightButton, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -16f), new Vector2(38f, 38f));
    }

    private static RectTransform CreateTimelineNode(Transform parent, string name)
    {
        RectTransform root = CreateMotionContainer(name, parent, out RectTransform motionRoot);
        root.gameObject.AddComponent<TimelineNodeView>();
        LayoutElement layout = root.gameObject.AddComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        layout.minWidth = 180f;

        RectTransform dotGlow = CreateDecorativePanel(
            motionRoot,
            "DotGlow",
            new Color(0.83f, 0.70f, 1f, 0.18f),
            new Color(0.83f, 0.70f, 1f, 0f));
        ApplyCircleSprite(dotGlow.GetComponent<Image>());
        SetRect(dotGlow, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(38f, 38f));

        RectTransform dot = CreateDecorativePanel(
            motionRoot,
            "Dot",
            new Color(0.84f, 0.69f, 1f, 1f),
            new Color(0.98f, 0.92f, 1f, 0.55f));
        ApplyCircleSprite(dot.GetComponent<Image>());
        SetRect(dot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(16f, 16f));

        TextMeshProUGUI yearText = CreateText(
            motionRoot,
            "YearText",
            "2010",
            22f,
            new Color(0.98f, 0.96f, 1f, 1f),
            TextAlignmentOptions.Center,
            FontStyles.Normal,
            monoFontAsset,
            0f,
            0f,
            false);
        SetRect(yearText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(-12f, 24f));

        TextMeshProUGUI labelText = CreateText(
            motionRoot,
            "LabelText",
            "Birthday Gift",
            16f,
            new Color(0.80f, 0.76f, 0.88f, 1f),
            TextAlignmentOptions.Top,
            FontStyles.Normal,
            bodyFontAsset,
            0f,
            4f,
            true);
        SetRect(labelText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(-12f, 42f));
        return root;
    }

    private static RectTransform CreateCircleButton(Transform parent, string name, string label)
    {
        RectTransform root = CreateMotionContainer(name, parent, out RectTransform motionRoot);
        RectTransform shell = CreateGlassPanel(
            motionRoot,
            "Shell",
            CreateGlassStyle(
                new Color(1f, 1f, 1f, 0.06f),
                new Color(1f, 1f, 1f, 0.05f),
                new Color(1f, 1f, 1f, 0.02f),
                new Color(0f, 0f, 0f, 0.06f),
                new Color(1f, 1f, 1f, 0f),
                new Color(1f, 1f, 1f, 0f),
                new Color(0f, 0f, 0f, 0.12f),
                new Vector2(0f, -3f)));
        StretchToParent(shell, 0f, 0f, 0f, 0f);
        Image shellImage = shell.GetComponent<Image>();
        shellImage.raycastTarget = true;
        Button button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = shellImage;

        CreateText(
            shell,
            "LabelText",
            label,
            20f,
            new Color(0.98f, 0.97f, 1f, 1f),
            TextAlignmentOptions.Center,
            FontStyles.Bold,
            bodyFontAsset,
            0f,
            0f,
            false);
        StretchToParent((RectTransform)shell.Find("LabelText"), 0f, 0f, 0f, 0f);
        return root;
    }

    private static RectTransform CreateTagTemplate(Transform parent, string name)
    {
        RectTransform root = CreateUIObject(name, parent);
        root.gameObject.AddComponent<TagPillView>();
        LayoutElement rootLayout = root.gameObject.AddComponent<LayoutElement>();
        rootLayout.flexibleWidth = 0f;
        rootLayout.flexibleHeight = 0f;

        RectTransform shell = CreateGlassPanel(
            root,
            "BaseFill",
            CreateGlassStyle(
                new Color(1f, 1f, 1f, 0.07f),
                new Color(1f, 1f, 1f, 0.10f),
                new Color(1f, 1f, 1f, 0.02f),
                new Color(0f, 0f, 0f, 0.05f),
                new Color(1f, 1f, 1f, 0.02f),
                new Color(1f, 1f, 1f, 0.015f),
                new Color(0f, 0f, 0f, 0.10f),
                new Vector2(0f, -2f)));
        StretchToParent(shell, 0f, 0f, 0f, 0f);
        HorizontalLayoutGroup layout = shell.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 12, 12);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        RectTransform dot = CreateDecorativePanel(
            shell,
            "Dot",
            new Color(0.84f, 0.69f, 1f, 1f),
            new Color(1f, 1f, 1f, 0f));
        ApplyCircleSprite(dot.GetComponent<Image>());
        SetIgnoreLayout(dot, false);
        SetRect(dot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3f, 3f));
        LayoutElement dotLayout = dot.gameObject.AddComponent<LayoutElement>();
        dotLayout.ignoreLayout = false;
        dotLayout.preferredWidth = 12f;
        dotLayout.preferredHeight = 12f;
        dotLayout.minWidth = 12f;
        dotLayout.minHeight = 12f;

        CreateText(
            shell,
            "LabelText",
            "Tag",
            17f,
            new Color(0.98f, 0.97f, 1f, 1f),
            TextAlignmentOptions.Left,
            FontStyles.Bold,
            bodyFontAsset,
            0f,
            0f,
            false);
        return root;
    }

    private static RectTransform CreateMotionContainer(string name, Transform parent, out RectTransform motionRoot)
    {
        RectTransform root = CreateUIObject(name, parent);
        motionRoot = CreateUIObject("MotionRoot", root);
        StretchToParent(motionRoot, 0f, 0f, 0f, 0f);
        CanvasGroup canvasGroup = motionRoot.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        MemoryUIMotionNode motionNode = root.gameObject.AddComponent<MemoryUIMotionNode>();
        motionNode.Configure(motionRoot, canvasGroup);
        return root;
    }

    private static void ApplyModuleSpatialPose(RectTransform module, Vector3 scale, Vector3 eulerRotation, float zOffset)
    {
        module.localScale = scale;
        module.localRotation = Quaternion.Euler(eulerRotation);

        Vector3 position = module.localPosition;
        position.z = zOffset;
        module.localPosition = position;
    }

    private static RectTransform CreateGlassPanel(Transform parent, string name, GlassStyle style)
    {
        RectTransform rect = CreateUIObject(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        ApplyImageSprite(image);
        image.color = style.fillColor;
        image.raycastTarget = false;

        Shadow shadow = rect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = style.shadowColor;
        shadow.effectDistance = style.shadowDistance;

        Image border = CreateLayerImage(rect, "Border", style.borderColor, false);
        StretchToParent((RectTransform)border.transform, 0f, 0f, 0f, 0f);

        Image innerBorder = CreateLayerImage(rect, "InnerBorder", new Color(1f, 1f, 1f, style.borderColor.a * 0.42f), false);
        StretchToParent((RectTransform)innerBorder.transform, 1.5f, 1.5f, 1.5f, 1.5f);

        Image bottomShade = CreateLayerImage(rect, "BottomShade", new Color(style.bottomShadeColor.r, style.bottomShadeColor.g, style.bottomShadeColor.b, 0f), false);
        RectTransform bottomShadeRect = (RectTransform)bottomShade.transform;
        bottomShadeRect.anchorMin = new Vector2(0f, 0f);
        bottomShadeRect.anchorMax = new Vector2(1f, 0.58f);
        bottomShadeRect.offsetMin = new Vector2(2f, 2f);
        bottomShadeRect.offsetMax = new Vector2(-2f, 0f);
        bottomShadeRect.localScale = Vector3.one;
        bottomShadeRect.localRotation = Quaternion.identity;

        if (hazeTexture != null)
        {
            RawImage haze = CreateTextureLayer(rect, "HazeLayer", hazeTexture, style.hazeColor, false);
            StretchToParent((RectTransform)haze.transform, 0f, 0f, 0f, 0f);
            haze.uvRect = new Rect(0f, 0f, 1.4f, 1.2f);
        }

        if (grainTexture != null)
        {
            RawImage grain = CreateTextureLayer(rect, "GrainLayer", grainTexture, style.grainColor, false);
            StretchToParent((RectTransform)grain.transform, 0f, 0f, 0f, 0f);
            grain.uvRect = new Rect(0f, 0f, 2.4f, 2.4f);
        }

        return rect;
    }

    private static void AttachBeveledBacker(
        RectTransform target,
        string name,
        float zOffset,
        float thickness,
        float cornerRadius,
        float bevelSize,
        float overlayAlphaMultiplier,
        float emissionIntensity)
    {
        if (target == null)
        {
            return;
        }

        Transform existing = target.Find(name);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        Image overlayImage = target.GetComponent<Image>();
        if (overlayImage != null)
        {
            Color faded = MultiplyAlpha(overlayImage.color, overlayAlphaMultiplier);
            overlayImage.color = new Color(faded.r, faded.g, faded.b, 0f);
        }

        Shadow shadow = target.GetComponent<Shadow>();
        if (shadow != null)
        {
            shadow.effectColor = MultiplyAlpha(shadow.effectColor, 0.58f);
        }

        RemoveLegacyGlassLayers(target);

        Transform borderTransform = target.Find("Border");
        Image borderImage = borderTransform != null ? borderTransform.GetComponent<Image>() : null;
        Color overlayColor = overlayImage != null ? overlayImage.color : new Color(0.22f, 0.22f, 0.25f, 0.22f);
        Color borderColor = borderImage != null ? borderImage.color : new Color(0.78f, 0.74f, 0.84f, 0.30f);

        GameObject backerObject = new GameObject(name, typeof(MemoryUIBeveledPanel3D));
        Undo.RegisterCreatedObjectUndo(backerObject, "Create Memory UI 3D Glass Backer");
        backerObject.transform.SetParent(target, false);
        CopyLayer(backerObject, target.gameObject);

        MemoryUIBeveledPanel3D backer = backerObject.GetComponent<MemoryUIBeveledPanel3D>();
        backer.Configure(
            target,
            zOffset,
            thickness,
            Vector2.zero,
            cornerRadius,
            bevelSize,
            frostedMaterial,
            new Color(
                overlayColor.r,
                overlayColor.g,
                overlayColor.b,
                Mathf.Clamp(overlayColor.a * 1.1f, 0.12f, 0.32f)),
            borderColor,
            emissionIntensity);
    }

    private static void ScaleRawLayerAlpha(Transform parent, string childName, float multiplier)
    {
        Transform child = parent.Find(childName);
        if (child == null)
        {
            return;
        }

        RawImage rawImage = child.GetComponent<RawImage>();
        if (rawImage == null)
        {
            return;
        }

        rawImage.color = MultiplyAlpha(rawImage.color, multiplier);
    }

    private static void ScaleImageLayerAlpha(Transform parent, string childName, float multiplier)
    {
        Transform child = parent.Find(childName);
        if (child == null)
        {
            return;
        }

        Image image = child.GetComponent<Image>();
        if (image == null)
        {
            return;
        }

        image.color = MultiplyAlpha(image.color, multiplier);
    }

    private static void RemoveLegacyGlassLayers(Transform target)
    {
        RemoveChildIfExists(target, "Border");
        RemoveChildIfExists(target, "InnerBorder");
        RemoveChildIfExists(target, "BottomShade");
        RemoveChildIfExists(target, "HazeLayer");
        RemoveChildIfExists(target, "GrainLayer");
    }

    private static void RemoveChildIfExists(Transform parent, string childName)
    {
        if (parent == null)
        {
            return;
        }

        Transform child = parent.Find(childName);
        if (child == null)
        {
            return;
        }

        Undo.DestroyObjectImmediate(child.gameObject);
    }

    private static Color MultiplyAlpha(Color color, float multiplier)
    {
        return new Color(color.r, color.g, color.b, color.a * multiplier);
    }

    private static RectTransform CreateTagColumn(Transform parent, string name)
    {
        RectTransform column = CreateUIObject(name, parent);
        LayoutElement layout = column.gameObject.AddComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        layout.flexibleHeight = 0f;

        VerticalLayoutGroup columnLayout = column.gameObject.AddComponent<VerticalLayoutGroup>();
        columnLayout.spacing = 12f;
        columnLayout.childAlignment = TextAnchor.UpperLeft;
        columnLayout.childControlWidth = false;
        columnLayout.childControlHeight = false;
        columnLayout.childForceExpandWidth = false;
        columnLayout.childForceExpandHeight = false;
        return column;
    }

    private static void CreateCardIcon(Transform parent, CardIconType iconType)
    {
        switch (iconType)
        {
            case CardIconType.Photo:
                CreatePhotoIcon(parent);
                break;
            case CardIconType.Story:
                CreateStoryIcon(parent);
                break;
            case CardIconType.Memories:
                CreateMemoriesIcon(parent);
                break;
            case CardIconType.Tags:
                CreateTagsIcon(parent);
                break;
        }
    }

    private static void CreatePhotoIcon(Transform parent)
    {
        RectTransform frame = CreateDecorativePanel(parent, "Frame", new Color(1f, 1f, 1f, 0f), new Color(0.98f, 0.96f, 1f, 0.90f));
        SetRect(frame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 14f));

        RectTransform sun = CreateDecorativePanel(parent, "Sun", new Color(0.98f, 0.96f, 1f, 0.92f), new Color(1f, 1f, 1f, 0f));
        SetRect(sun, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-4f, 2f), new Vector2(3f, 3f));

        RectTransform ridgeA = CreateDecorativePanel(parent, "RidgeA", new Color(0.98f, 0.96f, 1f, 0.92f), new Color(1f, 1f, 1f, 0f));
        SetRect(ridgeA, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-4f, -3f), new Vector2(8f, 2f));
        ridgeA.localRotation = Quaternion.Euler(0f, 0f, 28f);

        RectTransform ridgeB = CreateDecorativePanel(parent, "RidgeB", new Color(0.98f, 0.96f, 1f, 0.92f), new Color(1f, 1f, 1f, 0f));
        SetRect(ridgeB, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, -2f), new Vector2(9f, 2f));
        ridgeB.localRotation = Quaternion.Euler(0f, 0f, -28f);
    }

    private static void CreateStoryIcon(Transform parent)
    {
        RectTransform page = CreateDecorativePanel(parent, "Page", new Color(1f, 1f, 1f, 0f), new Color(0.98f, 0.96f, 1f, 0.90f));
        SetRect(page, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12f, 16f));

        RectTransform lineA = CreateDecorativePanel(parent, "LineA", new Color(0.98f, 0.96f, 1f, 0.90f), new Color(1f, 1f, 1f, 0f));
        SetRect(lineA, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 3f), new Vector2(6f, 1.5f));

        RectTransform lineB = CreateDecorativePanel(parent, "LineB", new Color(0.98f, 0.96f, 1f, 0.90f), new Color(1f, 1f, 1f, 0f));
        SetRect(lineB, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -1f), new Vector2(6f, 1.5f));

        RectTransform lineC = CreateDecorativePanel(parent, "LineC", new Color(0.98f, 0.96f, 1f, 0.90f), new Color(1f, 1f, 1f, 0f));
        SetRect(lineC, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-1f, -5f), new Vector2(4f, 1.5f));
    }

    private static void CreateMemoriesIcon(Transform parent)
    {
        float[] heights = { 5f, 9f, 13f, 8f };
        float x = -6f;
        for (int i = 0; i < heights.Length; i++)
        {
            RectTransform bar = CreateDecorativePanel(parent, $"Bar_{i + 1:00}", new Color(0.98f, 0.96f, 1f, 0.90f), new Color(1f, 1f, 1f, 0f));
            SetRect(bar, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(x + (i * 4f), -5f), new Vector2(2f, heights[i]));
        }
    }

    private static void CreateTagsIcon(Transform parent)
    {
        RectTransform diamond = CreateDecorativePanel(parent, "Diamond", new Color(0.98f, 0.96f, 1f, 0.90f), new Color(1f, 1f, 1f, 0f));
        SetRect(diamond, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
        diamond.localRotation = Quaternion.Euler(0f, 0f, 45f);
    }

    private static RectTransform CreateDecorativePanel(Transform parent, string name, Color fillColor, Color borderColor)
    {
        RectTransform rect = CreateUIObject(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        ApplyImageSprite(image);
        image.color = fillColor;
        image.raycastTarget = false;
        SetIgnoreLayout(rect, true);

        if (borderColor.a > 0.001f)
        {
            Image border = CreateLayerImage(rect, "Border", borderColor, false);
            StretchToParent((RectTransform)border.transform, 0f, 0f, 0f, 0f);
        }

        return rect;
    }

    private static void CreatePhotoViewport(Transform parent, string viewportName, string imageName, float inset)
    {
        RectTransform viewport = CreateUIObject(viewportName, parent);
        StretchToParent(viewport, inset, inset, inset, inset);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform imageRect = CreateUIObject(imageName, viewport);
        imageRect.anchorMin = new Vector2(0.5f, 0.5f);
        imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.anchoredPosition = Vector2.zero;
        imageRect.sizeDelta = new Vector2(120f, 120f);

        Image image = imageRect.gameObject.AddComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = false;
        image.enabled = false;

        AspectRatioFitter fitter = imageRect.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = 1f;
    }

    private static Image CreateLayerImage(Transform parent, string name, Color color, bool raycastTarget)
    {
        RectTransform rect = CreateUIObject(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        ApplyImageSprite(image);
        image.color = color;
        image.raycastTarget = raycastTarget;
        SetIgnoreLayout(rect, true);
        return image;
    }

    private static Image CreateFlatImage(Transform parent, string name, Color color, bool raycastTarget)
    {
        RectTransform rect = CreateUIObject(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = roundedSprite;
        image.type = Image.Type.Simple;
        image.color = color;
        image.raycastTarget = raycastTarget;
        SetIgnoreLayout(rect, true);
        return image;
    }

    private static RawImage CreateTextureLayer(Transform parent, string name, Texture texture, Color color, bool raycastTarget)
    {
        RectTransform rect = CreateUIObject(name, parent);
        RawImage image = rect.gameObject.AddComponent<RawImage>();
        image.texture = texture;
        image.color = color;
        image.raycastTarget = raycastTarget;
        SetIgnoreLayout(rect, true);
        return image;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        string text,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment,
        FontStyles fontStyle,
        TMP_FontAsset fontAsset,
        float characterSpacing,
        float lineSpacing,
        bool wordWrap)
    {
        RectTransform rect = CreateUIObject(name, parent);
        TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.fontStyle = fontStyle;
        tmp.characterSpacing = characterSpacing;
        tmp.lineSpacing = lineSpacing;
        tmp.enableWordWrapping = wordWrap;
        tmp.overflowMode = wordWrap ? TextOverflowModes.Overflow : TextOverflowModes.Truncate;
        tmp.richText = true;
        tmp.raycastTarget = false;
        Shadow textShadow = rect.gameObject.AddComponent<Shadow>();
        textShadow.effectColor = new Color(0.02f, 0.02f, 0.05f, 0.42f);
        textShadow.effectDistance = new Vector2(1.4f, -1.4f);
        textShadow.useGraphicAlpha = true;

        if (fontAsset != null)
        {
            tmp.font = fontAsset;
        }

        return tmp;
    }

    private static RectTransform CreateUIObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject.GetComponent<RectTransform>();
    }

    private static void SetIgnoreLayout(RectTransform rect, bool ignoreLayout)
    {
        if (rect == null)
        {
            return;
        }

        LayoutElement layout = rect.GetComponent<LayoutElement>();
        if (layout == null)
        {
            layout = rect.gameObject.AddComponent<LayoutElement>();
        }

        layout.ignoreLayout = ignoreLayout;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void StretchToParent(RectTransform rect, float left, float right, float top, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static GlassStyle CreateGlassStyle(
        Color fillColor,
        Color borderColor,
        Color topHighlightColor,
        Color bottomShadeColor,
        Color hazeColor,
        Color grainColor,
        Color shadowColor,
        Vector2 shadowDistance)
    {
        return new GlassStyle
        {
            fillColor = fillColor,
            borderColor = borderColor,
            topHighlightColor = topHighlightColor,
            bottomShadeColor = bottomShadeColor,
            hazeColor = hazeColor,
            grainColor = grainColor,
            shadowColor = shadowColor,
            shadowDistance = shadowDistance
        };
    }

    private static TMP_FontAsset ResolveDefaultFontAsset()
    {
        if (TMP_Settings.defaultFontAsset != null)
        {
            return TMP_Settings.defaultFontAsset;
        }

        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
    }

    private static void EnsureGeneratedSpriteAssets()
    {
        if (!AssetDatabase.IsValidFolder(GeneratedUiSpriteFolder))
        {
            EnsureFolderHierarchy(GeneratedUiSpriteFolder);
        }

        EnsureSpriteAsset(
            RoundedSpriteAssetPath,
            CreateRoundedRectTexture(128, 34f, 2f),
            new Vector4(36f, 36f, 36f, 36f));

        EnsureSpriteAsset(
            CircleSpriteAssetPath,
            CreateCircleTexture(64, 1.5f),
            Vector4.zero);
    }

    private static void EnsureFolderHierarchy(string assetFolderPath)
    {
        string[] parts = assetFolderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static void EnsureSpriteAsset(string assetPath, Texture2D texture, Vector4 border)
    {
        bool needsWrite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath) == null;
        if (needsWrite)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string filePath = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? projectRoot);
            File.WriteAllBytes(filePath, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        UnityEngine.Object.DestroyImmediate(texture);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = border;
            importer.SaveAndReimport();
        }
    }

    private static Texture2D CreateRoundedRectTexture(int size, float radius, float feather)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        float half = (size - 1) * 0.5f;
        float inner = half - radius;
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = Mathf.Abs(x - half);
                float py = Mathf.Abs(y - half);
                float dx = Mathf.Max(px - inner, 0f);
                float dy = Mathf.Max(py - inner, 0f);
                float distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                float alpha = 1f - Mathf.Clamp01((distance - radius + feather) / feather);
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f);
                pixels[(y * size) + x] = new Color32(255, 255, 255, a);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static Texture2D CreateCircleTexture(int size, float feather)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        float half = (size - 1) * 0.5f;
        float radius = half - 1f;
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - half;
                float dy = y - half;
                float distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                float alpha = 1f - Mathf.Clamp01((distance - radius + feather) / feather);
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f);
                pixels[(y * size) + x] = new Color32(255, 255, 255, a);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static Sprite ResolveRoundedSprite()
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpriteAssetPath);
        if (sprite != null)
        {
            return sprite;
        }

        sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        if (sprite != null)
        {
            return sprite;
        }

        sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        if (sprite != null)
        {
            return sprite;
        }

        Debug.LogWarning("[MemoryUIHiFiBuilder] Could not resolve a built-in rounded UI sprite. Falling back to plain Image components.");
        return null;
    }

    private static Sprite ResolveCircleSprite()
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CircleSpriteAssetPath);
        if (sprite != null)
        {
            return sprite;
        }

        sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        if (sprite != null)
        {
            return sprite;
        }

        return roundedSprite;
    }

    private static void ApplyImageSprite(Image image)
    {
        if (image == null)
        {
            return;
        }

        if (roundedSprite != null)
        {
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            return;
        }

        image.type = Image.Type.Simple;
    }

    private static void ApplyCircleSprite(Image image)
    {
        if (image == null)
        {
            return;
        }

        if (circleSprite != null)
        {
            image.sprite = circleSprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            return;
        }

        image.type = Image.Type.Simple;
    }

    private static void AddBestRaycaster(GameObject canvasObject)
    {
        Type trackedRaycasterType = Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
        if (trackedRaycasterType != null)
        {
            canvasObject.AddComponent(trackedRaycasterType);
            return;
        }

        canvasObject.AddComponent<GraphicRaycaster>();
    }

    private static GameObject FindSceneObject(string name)
    {
        Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name == name)
            {
                return transforms[i].gameObject;
            }
        }

        return null;
    }

    private static void CopyLayer(GameObject target, GameObject reference)
    {
        if (target == null || reference == null)
        {
            return;
        }

        target.layer = reference.layer;
    }
}

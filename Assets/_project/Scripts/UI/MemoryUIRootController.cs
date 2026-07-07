using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class MemoryUIRootController : MonoBehaviour
{
    [SerializeField] private Canvas canvasWorldSpace;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private StoryBoardModuleView storyBoardModule;
    [SerializeField] private InfoGridModuleView infoGridModule;
    [SerializeField] private TimelineModuleView timelineModule;
    [SerializeField] private MemoryUIBillboard billboard;
    [SerializeField] private MemoryModeUIFollower uiFollower;
    [SerializeField] private float fadeDuration = 0.35f;

    private static readonly TagPillView.TagPillData[] DemoTags =
    {
        new TagPillView.TagPillData("Childhood", new Color(0.84f, 0.69f, 1f, 1f)),
        new TagPillView.TagPillData("Gift", new Color(1f, 0.79f, 0.64f, 1f)),
        new TagPillView.TagPillData("Comfort", new Color(1f, 0.77f, 0.85f, 1f)),
        new TagPillView.TagPillData("Home", new Color(0.96f, 0.91f, 1f, 1f)),
        new TagPillView.TagPillData("Memory", new Color(0.85f, 0.69f, 1f, 1f))
    };

    private Coroutine fadeCoroutine;

    private static readonly TagPillView.TagPillData[] DemoStoryTags =
    {
        new TagPillView.TagPillData("Childhood", new Color(0.84f, 0.69f, 1f, 1f)),
        new TagPillView.TagPillData("Gift from Mom", new Color(1f, 0.79f, 0.64f, 1f))
    };

    private static readonly TimelineNodeView.TimelineEntryData[] DemoTimeline =
    {
        new TimelineNodeView.TimelineEntryData("2010", "Birthday Gift"),
        new TimelineNodeView.TimelineEntryData("2012", "First Sleepover"),
        new TimelineNodeView.TimelineEntryData("2015", "Elementary School"),
        new TimelineNodeView.TimelineEntryData("2018", "Moving Day"),
        new TimelineNodeView.TimelineEntryData("2021", "The Last Photo")
    };

    private void Reset()
    {
        AutoAssignReferences();
    }

    private void Awake()
    {
        AutoAssignReferences();
        EnsureReadableTextTreatment();
        EnsureCanvasGroup();
        ApplyHiddenStateImmediate();
        gameObject.SetActive(false);
    }

    public void Show()
    {
        AutoAssignReferences();
        EnsureCanvasGroup();

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        gameObject.SetActive(true);

        if (uiFollower != null)
        {
            if (billboard != null)
            {
                billboard.enabled = false;
            }

            uiFollower.SnapToView();
            uiFollower.BeginFollow();
        }
        else if (billboard != null)
        {
            billboard.enabled = true;
            billboard.SnapInFrontOfCamera();
        }

        fadeCoroutine = StartCoroutine(FadeCanvasGroup(canvasGroup.alpha, 1f, deactivateOnComplete: false));
    }

    public void Hide()
    {
        AutoAssignReferences();
        EnsureCanvasGroup();

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        if (uiFollower != null)
        {
            uiFollower.EndFollow();
        }

        if (!gameObject.activeSelf)
        {
            ApplyHiddenStateImmediate();
            return;
        }

        fadeCoroutine = StartCoroutine(FadeCanvasGroup(canvasGroup.alpha, 0f, deactivateOnComplete: true));
    }

    public void SetStaticDemoContent()
    {
        AutoAssignReferences();

        storyBoardModule?.SetContent(
            "TEDDY BEAR",
            string.Empty,
            "STORY BOARD",
            "This teddy bear was a birthday gift from Mom when I was 6. It has been with me through every move and every milestone.",
            DemoStoryTags);

        infoGridModule?.SetContent(
            string.Empty,
            new InfoCardView.CardContent
            {
                header = "PHOTO",
                badge = "12",
                title = string.Empty,
                body = string.Empty,
                photos = null,
                accentColor = new Color(0.85f, 0.70f, 1f, 0.24f)
            },
            new InfoCardView.CardContent
            {
                header = "STORY",
                badge = "5",
                title = string.Empty,
                body = "A short written memory connected to this object.",
                accentColor = new Color(1f, 0.80f, 0.65f, 0.22f)
            },
            new InfoCardView.CardContent
            {
                header = "SOUNDS",
                badge = "8",
                title = string.Empty,
                body = string.Empty,
                photos = null,
                accentColor = new Color(0.88f, 0.75f, 1f, 0.18f)
            },
            new InfoCardView.CardContent
            {
                header = "TAGS",
                badge = "6",
                title = string.Empty,
                body = string.Empty,
                photos = null,
                tags = DemoTags,
                accentColor = new Color(0.97f, 0.94f, 1f, 0.12f)
            });

        timelineModule?.SetContent(
            string.Empty,
            "TIME LINE",
            DemoTimeline);
    }

    public void Bind(MemoryItemData memoryItemData)
    {
        if (memoryItemData == null)
        {
            Debug.LogWarning("[MemoryUIRootController] Bind received null data. Falling back to static demo content.", this);
            SetStaticDemoContent();
            return;
        }

        string story = !string.IsNullOrWhiteSpace(memoryItemData.StoryText)
            ? memoryItemData.StoryText
            : memoryItemData.ShortDescription;
        string itemName = string.IsNullOrWhiteSpace(memoryItemData.ItemName)
            ? "MEMORY ITEM"
            : memoryItemData.ItemName;
        int photoCount = memoryItemData.Photos != null ? memoryItemData.Photos.Length : 0;
        int timelineCount = memoryItemData.TimelineEvents != null ? memoryItemData.TimelineEvents.Length : 0;

        TagPillView.TagPillData primaryTag = new TagPillView.TagPillData(
            string.IsNullOrWhiteSpace(memoryItemData.EmotionType) ? "Memory" : memoryItemData.EmotionType,
            new Color(0.84f, 0.69f, 1f, 1f));

        storyBoardModule?.SetContent(
            itemName.ToUpperInvariant(),
            string.Empty,
            "STORY BOARD",
            story,
            new[] { primaryTag });

        timelineModule?.SetContent(
            string.Empty,
            "Timeline",
            ConvertTimeline(memoryItemData));

        infoGridModule?.SetContent(
            string.Empty,
            new InfoCardView.CardContent
            {
                header = "PHOTO",
                badge = photoCount.ToString("00"),
                title = string.Empty,
                body = string.Empty,
                photos = memoryItemData.Photos,
                accentColor = new Color(0.85f, 0.70f, 1f, 0.24f)
            },
            new InfoCardView.CardContent
            {
                header = "STORY",
                badge = "01",
                title = string.Empty,
                body = story,
                accentColor = new Color(1f, 0.80f, 0.65f, 0.22f)
            },
            new InfoCardView.CardContent
            {
                header = "SOUNDS",
                badge = timelineCount.ToString("00"),
                title = string.Empty,
                body = string.Empty,
                accentColor = new Color(0.88f, 0.75f, 1f, 0.18f)
            },
            new InfoCardView.CardContent
            {
                header = "TAGS",
                badge = "01",
                title = string.Empty,
                body = string.Empty,
                tags = new[] { primaryTag },
                accentColor = new Color(0.97f, 0.94f, 1f, 0.12f)
            });
    }

    private TimelineNodeView.TimelineEntryData[] ConvertTimeline(MemoryItemData memoryItemData)
    {
        if (memoryItemData.TimelineEvents == null || memoryItemData.TimelineEvents.Length == 0)
        {
            return DemoTimeline;
        }

        TimelineNodeView.TimelineEntryData[] entries = new TimelineNodeView.TimelineEntryData[memoryItemData.TimelineEvents.Length];
        for (int i = 0; i < memoryItemData.TimelineEvents.Length; i++)
        {
            MemoryItemData.TimelineEvent evt = memoryItemData.TimelineEvents[i];
            string year = string.IsNullOrWhiteSpace(evt.dateLabel) ? "----" : evt.dateLabel;
            string label = string.IsNullOrWhiteSpace(evt.title) ? "Untitled Memory" : evt.title;
            entries[i] = new TimelineNodeView.TimelineEntryData(year, label);
        }

        return entries;
    }

    private System.Collections.IEnumerator FadeCanvasGroup(float from, float to, bool deactivateOnComplete)
    {
        float duration = Mathf.Max(0.01f, fadeDuration);
        float elapsed = 0f;

        SetCanvasState(false);
        canvasGroup.alpha = from;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        canvasGroup.alpha = to;
        bool isVisible = to > 0.99f;
        SetCanvasState(isVisible);

        if (!isVisible && billboard != null && uiFollower != null)
        {
            billboard.enabled = true;
        }

        if (deactivateOnComplete)
        {
            gameObject.SetActive(false);
        }

        fadeCoroutine = null;
    }

    private void EnsureCanvasGroup()
    {
        if (canvasGroup != null)
        {
            return;
        }

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void ApplyHiddenStateImmediate()
    {
        EnsureCanvasGroup();
        canvasGroup.alpha = 0f;
        SetCanvasState(false);
    }

    private void SetCanvasState(bool visible)
    {
        EnsureCanvasGroup();
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private void AutoAssignReferences()
    {
        if (canvasWorldSpace == null)
        {
            canvasWorldSpace = GetComponentInChildren<Canvas>(true);
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (storyBoardModule == null)
        {
            storyBoardModule = GetComponentInChildren<StoryBoardModuleView>(true);
        }

        if (infoGridModule == null)
        {
            infoGridModule = GetComponentInChildren<InfoGridModuleView>(true);
        }

        if (timelineModule == null)
        {
            timelineModule = GetComponentInChildren<TimelineModuleView>(true);
        }

        if (billboard == null)
        {
            billboard = GetComponent<MemoryUIBillboard>();
        }

        if (uiFollower == null)
        {
            uiFollower = GetComponent<MemoryModeUIFollower>();
        }
    }

    private void EnsureReadableTextTreatment()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
            {
                continue;
            }

            Shadow shadow = text.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = text.gameObject.AddComponent<Shadow>();
            }

            shadow.effectColor = new Color(0.02f, 0.02f, 0.05f, 0.42f);
            shadow.effectDistance = new Vector2(1.4f, -1.4f);
            shadow.useGraphicAlpha = true;
        }
    }
}

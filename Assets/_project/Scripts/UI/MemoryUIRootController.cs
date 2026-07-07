using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MemoryUIRootController : MonoBehaviour
{
    [SerializeField] private Canvas canvasWorldSpace;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private StoryBoardModuleView storyBoardModule;
    [SerializeField] private InfoGridModuleView infoGridModule;
    [SerializeField] private TimelineModuleView timelineModule;
    [SerializeField] private MemoryUIBillboard billboard;

    private static readonly TagPillView.TagPillData[] DemoTags =
    {
        new TagPillView.TagPillData("Childhood", new Color(0.84f, 0.69f, 1f, 1f)),
        new TagPillView.TagPillData("Gift", new Color(1f, 0.79f, 0.64f, 1f)),
        new TagPillView.TagPillData("Comfort", new Color(1f, 0.77f, 0.85f, 1f)),
        new TagPillView.TagPillData("Home", new Color(0.96f, 0.91f, 1f, 1f)),
        new TagPillView.TagPillData("Memory", new Color(0.85f, 0.69f, 1f, 1f))
    };

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
        SetCanvasState(true);
    }

    public void Show()
    {
        SetCanvasState(true);
    }

    public void Hide()
    {
        SetCanvasState(false);
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
                accentColor = new Color(0.88f, 0.75f, 1f, 0.18f)
            },
            new InfoCardView.CardContent
            {
                header = "TAGS",
                badge = "6",
                title = string.Empty,
                body = string.Empty,
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

        TagPillView.TagPillData primaryTag = new TagPillView.TagPillData(
            string.IsNullOrWhiteSpace(memoryItemData.EmotionType) ? "Memory" : memoryItemData.EmotionType,
            new Color(0.84f, 0.69f, 1f, 1f));

        storyBoardModule?.SetContent(
            string.IsNullOrWhiteSpace(memoryItemData.ItemName) ? "MEMORY ITEM" : memoryItemData.ItemName.ToUpperInvariant(),
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
                badge = memoryItemData.Photos != null ? memoryItemData.Photos.Length.ToString("00") : "00",
                title = "Photo Stack",
                body = "Photo cards are placeholder visuals in this first static Unity pass.",
                accentColor = new Color(0.85f, 0.70f, 1f, 0.24f)
            },
            new InfoCardView.CardContent
            {
                header = "STORY",
                badge = "01",
                title = string.IsNullOrWhiteSpace(memoryItemData.ItemName) ? "Memory Story" : memoryItemData.ItemName,
                body = story,
                accentColor = new Color(1f, 0.80f, 0.65f, 0.22f)
            },
            new InfoCardView.CardContent
            {
                header = "SOUNDS",
                badge = memoryItemData.TimelineEvents != null ? memoryItemData.TimelineEvents.Length.ToString("00") : "00",
                title = "Memory Fragments",
                body = $"Emotion: {memoryItemData.EmotionType}",
                accentColor = new Color(0.88f, 0.75f, 1f, 0.18f)
            },
            new InfoCardView.CardContent
            {
                header = "TAGS",
                badge = "01",
                title = "Emotional Marker",
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

    private void SetCanvasState(bool visible)
    {
        if (canvasGroup == null)
        {
            AutoAssignReferences();
        }

        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = visible ? 1f : 0f;
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
    }
}

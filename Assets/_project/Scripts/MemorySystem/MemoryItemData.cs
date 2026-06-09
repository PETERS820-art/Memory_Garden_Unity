using UnityEngine;

[CreateAssetMenu(fileName = "NewMemoryItemData", menuName = "Memory Garden/Memory Item Data")]
public class MemoryItemData : ScriptableObject
{
    [System.Serializable]
    public class TimelineEvent
    {
        public string title;
        public string dateLabel;
        [TextArea(2, 5)]
        public string description;
        public Sprite image;
    }

    [Header("Identity")]
    [SerializeField] private string itemId;
    [SerializeField] private string itemName;
    [SerializeField, TextArea(2, 5)] private string shortDescription;
    [SerializeField] private string emotionType;

    [Header("Memory Content")]
    [SerializeField, TextArea(3, 10)] private string storyText;
    [SerializeField] private Sprite[] photos;
    [SerializeField] private TimelineEvent[] timelineEvents;

    public string ItemId => itemId;
    public string ItemName => itemName;
    public string ShortDescription => shortDescription;
    public string EmotionType => emotionType;
    public string StoryText => storyText;
    public Sprite[] Photos => photos;
    public TimelineEvent[] TimelineEvents => timelineEvents;
}

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class InfoCardView : MonoBehaviour
{
    [System.Serializable]
    public struct CardContent
    {
        public string header;
        public string badge;
        public string title;
        public string body;
        public TagPillView.TagPillData[] tags;
        public Color accentColor;
    }

    [SerializeField] private TMP_Text headerText;
    [SerializeField] private TMP_Text badgeText;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Image accentPanel;
    [SerializeField] private RectTransform tagContainer;
    [SerializeField] private RectTransform tagColumnA;
    [SerializeField] private RectTransform tagColumnB;
    [SerializeField] private TagPillView tagTemplate;

    private readonly List<TagPillView> spawnedTags = new List<TagPillView>();

    private void Reset()
    {
        AutoAssignReferences();
    }

    public void SetContent(CardContent content)
    {
        AutoAssignReferences();

        if (headerText != null)
        {
            headerText.text = content.header;
            headerText.gameObject.SetActive(!string.IsNullOrWhiteSpace(content.header));
        }

        if (badgeText != null)
        {
            badgeText.text = content.badge;
            badgeText.gameObject.SetActive(!string.IsNullOrWhiteSpace(content.badge));
        }

        if (titleText != null)
        {
            titleText.text = content.title;
            titleText.gameObject.SetActive(!string.IsNullOrWhiteSpace(content.title));
        }

        if (bodyText != null)
        {
            bodyText.text = content.body;
            bodyText.gameObject.SetActive(!string.IsNullOrWhiteSpace(content.body));
        }

        if (accentPanel != null)
        {
            accentPanel.color = content.accentColor;
        }

        ApplyTags(content.tags);
    }

    private void ApplyTags(TagPillView.TagPillData[] tags)
    {
        if (tagContainer == null || tagTemplate == null)
        {
            return;
        }

        int requiredCount = tags != null ? tags.Length : 0;
        int extraNeeded = Mathf.Max(0, requiredCount - 1);

        while (spawnedTags.Count < extraNeeded)
        {
            TagPillView clone = Instantiate(tagTemplate, tagContainer);
            clone.name = $"TagPill_{spawnedTags.Count + 2:00}";
            spawnedTags.Add(clone);
        }

        if (requiredCount > 0)
        {
            tagTemplate.gameObject.SetActive(true);
            MoveTagToExpectedParent(tagTemplate.transform as RectTransform, 0, requiredCount);
            tagTemplate.SetContent(tags[0]);
        }
        else
        {
            tagTemplate.gameObject.SetActive(false);
        }

        for (int i = 0; i < spawnedTags.Count; i++)
        {
            bool shouldShow = i < extraNeeded;
            spawnedTags[i].gameObject.SetActive(shouldShow);
            if (shouldShow)
            {
                MoveTagToExpectedParent(spawnedTags[i].transform as RectTransform, i + 1, requiredCount);
                spawnedTags[i].SetContent(tags[i + 1]);
            }
        }

        tagContainer.gameObject.SetActive(requiredCount > 0);
    }

    private void MoveTagToExpectedParent(RectTransform tagRect, int index, int totalCount)
    {
        if (tagRect == null || tagColumnA == null || tagColumnB == null)
        {
            return;
        }

        int leftColumnCount = Mathf.CeilToInt(totalCount * 0.5f);
        RectTransform expectedParent = index < leftColumnCount ? tagColumnA : tagColumnB;
        if (expectedParent != null && tagRect.parent != expectedParent)
        {
            tagRect.SetParent(expectedParent, false);
        }
    }

    private void AutoAssignReferences()
    {
        if (headerText == null)
        {
            headerText = MemoryUIHierarchyUtility.FindComponentInDeepChild<TMP_Text>(transform, "HeaderText");
        }

        if (badgeText == null)
        {
            badgeText = MemoryUIHierarchyUtility.FindComponentInDeepChild<TMP_Text>(transform, "BadgeText");
        }

        if (titleText == null)
        {
            titleText = MemoryUIHierarchyUtility.FindComponentInDeepChild<TMP_Text>(transform, "TitleText");
        }

        if (bodyText == null)
        {
            bodyText = MemoryUIHierarchyUtility.FindComponentInDeepChild<TMP_Text>(transform, "BodyText");
        }

        if (accentPanel == null)
        {
            accentPanel = MemoryUIHierarchyUtility.FindComponentInDeepChild<Image>(transform, "AccentPanel");
        }

        if (tagContainer == null)
        {
            Transform child = MemoryUIHierarchyUtility.FindDeepChild(transform, "TagContainer");
            if (child != null)
            {
                tagContainer = child as RectTransform;
            }
        }

        if (tagColumnA == null)
        {
            Transform child = MemoryUIHierarchyUtility.FindDeepChild(transform, "TagColumnA");
            if (child != null)
            {
                tagColumnA = child as RectTransform;
            }
        }

        if (tagColumnB == null)
        {
            Transform child = MemoryUIHierarchyUtility.FindDeepChild(transform, "TagColumnB");
            if (child != null)
            {
                tagColumnB = child as RectTransform;
            }
        }

        if (tagTemplate == null)
        {
            tagTemplate = GetComponentInChildren<TagPillView>(true);
        }
    }
}

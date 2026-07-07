using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class StoryBoardModuleView : MonoBehaviour
{
    [SerializeField] private int maxVisibleTags = 2;
    [SerializeField] private TMP_Text objectNameText;
    [SerializeField] private TMP_Text moduleLabelText;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private RectTransform tagPillGroup;
    [SerializeField] private TagPillView tagTemplate;

    private readonly List<TagPillView> spawnedTags = new List<TagPillView>();

    private void Reset()
    {
        AutoAssignReferences();
    }

    public void SetContent(
        string objectName,
        string moduleLabel,
        string title,
        string body,
        TagPillView.TagPillData[] tags)
    {
        AutoAssignReferences();

        if (objectNameText != null)
        {
            objectNameText.text = objectName;
        }

        if (moduleLabelText != null)
        {
            moduleLabelText.text = moduleLabel;
            moduleLabelText.gameObject.SetActive(!string.IsNullOrWhiteSpace(moduleLabel));
        }

        if (titleText != null)
        {
            titleText.text = title;
        }

        if (bodyText != null)
        {
            bodyText.text = body;
        }

        ApplyTags(tags);
    }

    private void ApplyTags(TagPillView.TagPillData[] tags)
    {
        if (tagPillGroup == null || tagTemplate == null)
        {
            return;
        }

        int requiredCount = tags != null ? Mathf.Min(tags.Length, maxVisibleTags) : 0;
        int extraNeeded = Mathf.Max(0, requiredCount - 1);

        while (spawnedTags.Count < extraNeeded)
        {
            TagPillView clone = Instantiate(tagTemplate, tagPillGroup);
            clone.name = $"TagPill_{spawnedTags.Count + 2:00}";
            spawnedTags.Add(clone);
        }

        if (requiredCount > 0)
        {
            tagTemplate.gameObject.SetActive(true);
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
                spawnedTags[i].SetContent(tags[i + 1]);
            }
        }

        tagPillGroup.gameObject.SetActive(requiredCount > 0);
    }

    private void AutoAssignReferences()
    {
        if (objectNameText == null)
        {
            objectNameText = MemoryUIHierarchyUtility.FindComponentInDeepChild<TMP_Text>(transform, "ObjectNameText");
        }

        if (moduleLabelText == null)
        {
            moduleLabelText = MemoryUIHierarchyUtility.FindComponentInDeepChild<TMP_Text>(transform, "ModuleLabelText");
        }

        if (titleText == null)
        {
            titleText = MemoryUIHierarchyUtility.FindComponentInDeepChild<TMP_Text>(transform, "TitleText");
        }

        if (bodyText == null)
        {
            bodyText = MemoryUIHierarchyUtility.FindComponentInDeepChild<TMP_Text>(transform, "BodyText");
        }

        if (tagPillGroup == null)
        {
            Transform child = MemoryUIHierarchyUtility.FindDeepChild(transform, "TagPillGroup");
            if (child != null)
            {
                tagPillGroup = child as RectTransform;
            }
        }

        if (tagTemplate == null && tagPillGroup != null && tagPillGroup.childCount > 0)
        {
            tagTemplate = tagPillGroup.GetChild(0).GetComponent<TagPillView>();
        }
    }
}

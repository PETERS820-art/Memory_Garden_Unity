using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TimelineModuleView : MonoBehaviour
{
    [SerializeField] private TMP_Text introTextBlock;
    [SerializeField] private TMP_Text timelineTitle;
    [SerializeField] private RectTransform timelineNodeGroup;
    [SerializeField] private TimelineNodeView nodeTemplate;
    [SerializeField] private Button leftArrowButton;
    [SerializeField] private Button rightArrowButton;

    private readonly List<TimelineNodeView> spawnedNodes = new List<TimelineNodeView>();

    private void Reset()
    {
        AutoAssignReferences();
    }

    private void Awake()
    {
        AutoAssignReferences();
        ConfigureButtons();
    }

    public void SetContent(
        string intro,
        string title,
        TimelineNodeView.TimelineEntryData[] entries)
    {
        AutoAssignReferences();

        if (introTextBlock != null)
        {
            introTextBlock.text = intro;
            introTextBlock.gameObject.SetActive(!string.IsNullOrWhiteSpace(intro));
        }

        if (timelineTitle != null)
        {
            timelineTitle.text = title;
        }

        ApplyTimeline(entries);
    }

    private void ApplyTimeline(TimelineNodeView.TimelineEntryData[] entries)
    {
        if (timelineNodeGroup == null || nodeTemplate == null)
        {
            return;
        }

        int requiredCount = entries != null ? entries.Length : 0;
        int extraNeeded = Mathf.Max(0, requiredCount - 1);

        while (spawnedNodes.Count < extraNeeded)
        {
            TimelineNodeView clone = Instantiate(nodeTemplate, timelineNodeGroup);
            clone.name = $"TimelineNode_{spawnedNodes.Count + 2:00}";
            spawnedNodes.Add(clone);
        }

        if (requiredCount > 0)
        {
            nodeTemplate.gameObject.SetActive(true);
            nodeTemplate.SetContent(entries[0], true);
        }
        else
        {
            nodeTemplate.gameObject.SetActive(false);
        }

        for (int i = 0; i < spawnedNodes.Count; i++)
        {
            bool shouldShow = i < extraNeeded;
            spawnedNodes[i].gameObject.SetActive(shouldShow);
            if (shouldShow)
            {
                spawnedNodes[i].SetContent(entries[i + 1], false);
            }
        }
    }

    private void ConfigureButtons()
    {
        if (leftArrowButton != null)
        {
            leftArrowButton.onClick.RemoveAllListeners();
            leftArrowButton.onClick.AddListener(() => Debug.Log("[TimelineModuleView] Timeline paging is not implemented yet.", this));
        }

        if (rightArrowButton != null)
        {
            rightArrowButton.onClick.RemoveAllListeners();
            rightArrowButton.onClick.AddListener(() => Debug.Log("[TimelineModuleView] Timeline paging is not implemented yet.", this));
        }
    }

    private void AutoAssignReferences()
    {
        if (introTextBlock == null)
        {
            introTextBlock = MemoryUIHierarchyUtility.FindComponentInDeepChild<TMP_Text>(transform, "IntroTextBlock");
        }

        if (timelineTitle == null)
        {
            timelineTitle = MemoryUIHierarchyUtility.FindComponentInDeepChild<TMP_Text>(transform, "TimelineTitle");
        }

        if (timelineNodeGroup == null)
        {
            Transform child = MemoryUIHierarchyUtility.FindDeepChild(transform, "TimelineNodeGroup");
            if (child != null)
            {
                timelineNodeGroup = child as RectTransform;
            }
        }

        if (nodeTemplate == null && timelineNodeGroup != null && timelineNodeGroup.childCount > 0)
        {
            nodeTemplate = timelineNodeGroup.GetChild(0).GetComponent<TimelineNodeView>();
        }

        if (leftArrowButton == null)
        {
            leftArrowButton = MemoryUIHierarchyUtility.FindComponentInDeepChild<Button>(transform, "LeftArrowButton");
        }

        if (rightArrowButton == null)
        {
            rightArrowButton = MemoryUIHierarchyUtility.FindComponentInDeepChild<Button>(transform, "RightArrowButton");
        }
    }
}

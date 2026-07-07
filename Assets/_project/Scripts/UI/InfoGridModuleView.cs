using UnityEngine;

[DisallowMultipleComponent]
public class InfoGridModuleView : MonoBehaviour
{
    [SerializeField] private TMPro.TMP_Text introTextBlock;
    [SerializeField] private InfoCardView photoCard;
    [SerializeField] private InfoCardView storyCard;
    [SerializeField] private InfoCardView memoriesCard;
    [SerializeField] private InfoCardView tagsCard;

    private void Reset()
    {
        AutoAssignReferences();
    }

    public void SetContent(
        string intro,
        InfoCardView.CardContent photo,
        InfoCardView.CardContent story,
        InfoCardView.CardContent memories,
        InfoCardView.CardContent tags)
    {
        AutoAssignReferences();

        if (introTextBlock != null)
        {
            introTextBlock.text = intro;
            introTextBlock.gameObject.SetActive(!string.IsNullOrWhiteSpace(intro));
        }

        photoCard?.SetContent(photo);
        storyCard?.SetContent(story);
        memoriesCard?.SetContent(memories);
        tagsCard?.SetContent(tags);
    }

    private void AutoAssignReferences()
    {
        if (introTextBlock == null)
        {
            introTextBlock = MemoryUIHierarchyUtility.FindComponentInDeepChild<TMPro.TMP_Text>(transform, "IntroTextBlock");
        }

        if (photoCard == null)
        {
            photoCard = MemoryUIHierarchyUtility.FindComponentInDeepChild<InfoCardView>(transform, "PhotoCard");
        }

        if (storyCard == null)
        {
            storyCard = MemoryUIHierarchyUtility.FindComponentInDeepChild<InfoCardView>(transform, "StoryCard");
        }

        if (memoriesCard == null)
        {
            memoriesCard = MemoryUIHierarchyUtility.FindComponentInDeepChild<InfoCardView>(transform, "MemoriesCard");
        }

        if (tagsCard == null)
        {
            tagsCard = MemoryUIHierarchyUtility.FindComponentInDeepChild<InfoCardView>(transform, "TagsCard");
        }
    }
}

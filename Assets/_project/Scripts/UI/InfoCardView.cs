using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class InfoCardView : MonoBehaviour
{
    private static readonly Color[] PhotoLayerTint =
    {
        new Color(1f, 1f, 1f, 0.96f),
        new Color(0.88f, 0.86f, 0.90f, 0.72f),
        new Color(0.78f, 0.77f, 0.82f, 0.46f)
    };

    [System.Serializable]
    public struct CardContent
    {
        public string header;
        public string badge;
        public string title;
        public string body;
        public Sprite[] photos;
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
    [SerializeField] private Image[] photoStackImages;
    [SerializeField] private RectTransform[] photoStackFrames;

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

        ApplyPhotos(content.photos);
        ApplyTags(content.tags);
    }

    private void ApplyPhotos(Sprite[] photos)
    {
        EnsurePhotoStackReferences();

        if (photoStackImages == null || photoStackImages.Length == 0)
        {
            return;
        }

        int photoCount = photos != null ? photos.Length : 0;

        for (int i = 0; i < photoStackImages.Length; i++)
        {
            Image image = photoStackImages[i];
            if (image == null)
            {
                continue;
            }

            bool hasPhoto = i < photoCount && photos[i] != null;
            image.sprite = hasPhoto ? photos[i] : null;
            image.enabled = hasPhoto;

            AspectRatioFitter fitter = image.GetComponent<AspectRatioFitter>();
            if (fitter != null && hasPhoto)
            {
                Rect spriteRect = photos[i].rect;
                float aspect = spriteRect.height > 0.001f
                    ? spriteRect.width / spriteRect.height
                    : 1f;
                fitter.aspectRatio = Mathf.Max(0.01f, aspect);
            }

            image.color = ResolvePhotoLayerTint(i, hasPhoto);

            if (image.transform.parent != null)
            {
                image.transform.parent.gameObject.SetActive(hasPhoto);
            }
        }
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

        EnsurePhotoStackReferences();
    }

    private void EnsurePhotoStackReferences()
    {
        if (photoStackFrames == null || photoStackFrames.Length != 3)
        {
            photoStackFrames = new RectTransform[3];
        }

        photoStackFrames[0] = photoStackFrames[0] != null
            ? photoStackFrames[0]
            : ResolvePhotoFrame("AccentPanel");
        photoStackFrames[1] = photoStackFrames[1] != null
            ? photoStackFrames[1]
            : ResolvePhotoFrame("PhotoMid");
        photoStackFrames[2] = photoStackFrames[2] != null
            ? photoStackFrames[2]
            : ResolvePhotoFrame("PhotoBack");

        NormalizePhotoFrameLayout();

        if (photoStackImages == null || photoStackImages.Length != 3)
        {
            photoStackImages = new Image[3];
        }

        for (int i = 0; i < photoStackFrames.Length; i++)
        {
            RectTransform frame = photoStackFrames[i];
            if (frame == null)
            {
                continue;
            }

            photoStackImages[i] = EnsurePhotoViewport(frame, $"PhotoViewport_{i + 1:00}", $"PhotoImage_{i + 1:00}");
        }
    }

    private RectTransform ResolvePhotoFrame(string childName)
    {
        Transform child = MemoryUIHierarchyUtility.FindDeepChild(transform, childName);
        return child as RectTransform;
    }

    private void NormalizePhotoFrameLayout()
    {
        if (photoStackFrames == null || photoStackFrames.Length < 3)
        {
            return;
        }

        RectTransform front = photoStackFrames[0];
        RectTransform mid = photoStackFrames[1];
        RectTransform back = photoStackFrames[2];

        if (front == null || mid == null || back == null)
        {
            return;
        }

        ApplyPhotoFrameLayout(back, new Vector2(48f, -92f), new Vector2(186f, 186f), -5f);
        ApplyPhotoFrameLayout(mid, new Vector2(84f, -108f), new Vector2(186f, 186f), 2f);
        ApplyPhotoFrameLayout(front, new Vector2(110f, -120f), new Vector2(196f, 196f), 1.5f);
    }

    private static void ApplyPhotoFrameLayout(RectTransform rect, Vector2 anchoredPosition, Vector2 sizeDelta, float zRotation)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.localRotation = Quaternion.Euler(0f, 0f, zRotation);
    }

    private static Image EnsurePhotoViewport(RectTransform frame, string viewportName, string imageName)
    {
        if (frame == null)
        {
            return null;
        }

        RectTransform viewport = MemoryUIHierarchyUtility.FindDeepChild(frame, viewportName) as RectTransform;
        if (viewport == null)
        {
            GameObject viewportObject = new GameObject(viewportName, typeof(RectTransform), typeof(RectMask2D));
            viewportObject.transform.SetParent(frame, false);
            viewport = viewportObject.GetComponent<RectTransform>();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.offsetMin = new Vector2(6f, 6f);
            viewport.offsetMax = new Vector2(-6f, -6f);
        }

        Transform imageTransform = viewport.Find(imageName);
        Image image = imageTransform != null ? imageTransform.GetComponent<Image>() : null;
        if (image == null)
        {
            GameObject imageObject = new GameObject(imageName, typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            imageObject.transform.SetParent(viewport, false);
            image = imageObject.GetComponent<Image>();
            RectTransform imageRect = image.rectTransform;
            imageRect.anchorMin = new Vector2(0.5f, 0.5f);
            imageRect.anchorMax = new Vector2(0.5f, 0.5f);
            imageRect.pivot = new Vector2(0.5f, 0.5f);
            imageRect.sizeDelta = viewport.rect.size;
            imageRect.anchoredPosition = Vector2.zero;

            AspectRatioFitter fitter = imageObject.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 1f;
        }

        image.color = Color.white;
        image.raycastTarget = false;
        image.preserveAspect = false;
        image.enabled = false;

        return image;
    }

    private static Color ResolvePhotoLayerTint(int layerIndex, bool hasPhoto)
    {
        if (!hasPhoto)
        {
            return new Color(1f, 1f, 1f, 0f);
        }

        if (layerIndex < 0 || layerIndex >= PhotoLayerTint.Length)
        {
            return Color.white;
        }

        return PhotoLayerTint[layerIndex];
    }
}

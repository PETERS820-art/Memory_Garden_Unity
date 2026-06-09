using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MemoryModeUIController : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text storyText;
    [SerializeField] private TMP_Text emotionTagText;
    [SerializeField] private Image[] photoSlots;
    [SerializeField] private GameObject timelineRoot;
    [SerializeField] private float fadeDuration = 0.5f;

    private Coroutine fadeCoroutine;

    private void Reset()
    {
        AutoAssignReferences(logWarnings: false);
    }

    private void Awake()
    {
        AutoAssignReferences(logWarnings: true);
        EnsureCanvasGroup();
        ApplyHiddenStateImmediate();
        gameObject.SetActive(false);
    }

    public void Show(MemoryItemData data)
    {
        if (data == null)
        {
            Debug.LogWarning("[MemoryModeUIController] Show called with null MemoryItemData.", this);
        }

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        EnsureCanvasGroup();
        gameObject.SetActive(true);
        PopulateUI(data);
        fadeCoroutine = StartCoroutine(FadeCanvasGroup(canvasGroup.alpha, 1f, deactivateOnComplete: false));
    }

    public void Hide()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        EnsureCanvasGroup();

        if (!gameObject.activeSelf)
        {
            ApplyHiddenStateImmediate();
            return;
        }

        fadeCoroutine = StartCoroutine(FadeCanvasGroup(canvasGroup.alpha, 0f, deactivateOnComplete: true));
    }

    private void PopulateUI(MemoryItemData data)
    {
        string itemName = data != null && !string.IsNullOrWhiteSpace(data.ItemName) ? data.ItemName : string.Empty;
        string story = string.Empty;
        string emotion = data != null && !string.IsNullOrWhiteSpace(data.EmotionType) ? data.EmotionType : string.Empty;

        if (data != null)
        {
            story = !string.IsNullOrWhiteSpace(data.StoryText) ? data.StoryText : data.ShortDescription;
        }

        if (titleText != null)
        {
            titleText.text = itemName;
        }
        else
        {
            Debug.LogWarning("[MemoryModeUIController] TitleText reference is missing.", this);
        }

        if (storyText != null)
        {
            storyText.text = story;
        }
        else
        {
            Debug.LogWarning("[MemoryModeUIController] StoryText reference is missing.", this);
        }

        if (emotionTagText != null)
        {
            emotionTagText.text = emotion;
        }
        else
        {
            Debug.LogWarning("[MemoryModeUIController] EmotionTagText reference is missing.", this);
        }

        PopulatePhotos(data);

        if (timelineRoot == null)
        {
            Debug.LogWarning("[MemoryModeUIController] TimelineRoot reference is missing.", this);
        }
        else
        {
            timelineRoot.SetActive(true);
        }
    }

    private void PopulatePhotos(MemoryItemData data)
    {
        if (photoSlots == null || photoSlots.Length == 0)
        {
            Debug.LogWarning("[MemoryModeUIController] No photo slots are assigned.", this);
            return;
        }

        var photos = data != null ? data.Photos : null;

        for (int i = 0; i < photoSlots.Length; i++)
        {
            Image slot = photoSlots[i];
            if (slot == null)
            {
                continue;
            }

            bool hasPhoto = photos != null && i < photos.Length && photos[i] != null;
            slot.sprite = hasPhoto ? photos[i] : null;
            slot.enabled = hasPhoto;
            slot.gameObject.SetActive(hasPhoto);
        }
    }

    private IEnumerator FadeCanvasGroup(float from, float to, bool deactivateOnComplete)
    {
        float duration = Mathf.Max(0.01f, fadeDuration);
        float elapsed = 0f;

        SetCanvasInteraction(false);
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
        SetCanvasInteraction(isVisible);

        if (deactivateOnComplete)
        {
            gameObject.SetActive(false);
        }

        fadeCoroutine = null;
    }

    private void AutoAssignReferences(bool logWarnings)
    {
        EnsureCanvasGroup();

        titleText = titleText != null ? titleText : FindText("TitleText");
        storyText = storyText != null ? storyText : FindText("StoryText");
        emotionTagText = emotionTagText != null ? emotionTagText : FindText("EmotionTagText", "FocusTagText");
        timelineRoot = timelineRoot != null ? timelineRoot : FindChildGameObject("Timeline_Static", "TimelineRoot", "Timeline");

        if (photoSlots == null || photoSlots.Length == 0)
        {
            var discoveredSlots = new List<Image>();
            TryAddPhotoSlot(discoveredSlots, "PhotoSlot_01");
            TryAddPhotoSlot(discoveredSlots, "PhotoSlot_02");
            TryAddPhotoSlot(discoveredSlots, "PhotoSlot_03");
            TryAddPhotoSlot(discoveredSlots, "PhotoSlot_04");
            photoSlots = discoveredSlots.ToArray();
        }

        if (logWarnings)
        {
            WarnIfMissing(titleText, "TitleText");
            WarnIfMissing(storyText, "StoryText");
            WarnIfMissing(emotionTagText, "EmotionTagText / FocusTagText");
        }

        if (logWarnings && timelineRoot == null)
        {
            Debug.LogWarning("[MemoryModeUIController] Could not auto-find Timeline_Static or TimelineRoot.", this);
        }

        if (logWarnings && (photoSlots == null || photoSlots.Length == 0))
        {
            Debug.LogWarning("[MemoryModeUIController] Could not auto-find any PhotoSlot_* images.", this);
        }
    }

    private void EnsureCanvasGroup()
    {
        if (canvasGroup != null)
        {
            return;
        }

        canvasGroup = GetComponentInChildren<CanvasGroup>(true);
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void ApplyHiddenStateImmediate()
    {
        EnsureCanvasGroup();
        canvasGroup.alpha = 0f;
        SetCanvasInteraction(false);
    }

    private void SetCanvasInteraction(bool enabled)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.interactable = enabled;
        canvasGroup.blocksRaycasts = enabled;
    }

    private TMP_Text FindText(params string[] candidateNames)
    {
        foreach (string candidateName in candidateNames)
        {
            GameObject foundObject = FindChildGameObject(candidateName);
            if (foundObject == null)
            {
                continue;
            }

            TMP_Text text = foundObject.GetComponent<TMP_Text>();
            if (text != null)
            {
                return text;
            }
        }

        return null;
    }

    private void TryAddPhotoSlot(List<Image> discoveredSlots, string objectName)
    {
        GameObject foundObject = FindChildGameObject(objectName);
        if (foundObject == null)
        {
            return;
        }

        Image image = foundObject.GetComponent<Image>();
        if (image != null)
        {
            discoveredSlots.Add(image);
            return;
        }

        Debug.LogWarning($"[MemoryModeUIController] Found {objectName} but it has no Image component.", this);
    }

    private GameObject FindChildGameObject(params string[] candidateNames)
    {
        foreach (string candidateName in candidateNames)
        {
            Transform foundTransform = FindDeepChild(transform, candidateName);
            if (foundTransform != null)
            {
                return foundTransform.gameObject;
            }
        }

        return null;
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform nestedResult = FindDeepChild(child, childName);
            if (nestedResult != null)
            {
                return nestedResult;
            }
        }

        return null;
    }

    private void WarnIfMissing(Object reference, string label)
    {
        if (reference == null)
        {
            Debug.LogWarning($"[MemoryModeUIController] Could not auto-find {label}.", this);
        }
    }
}

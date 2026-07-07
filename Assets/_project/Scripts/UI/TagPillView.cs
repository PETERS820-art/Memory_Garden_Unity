using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TagPillView : MonoBehaviour
{
    [System.Serializable]
    public struct TagPillData
    {
        public string text;
        public Color dotColor;

        public TagPillData(string text, Color dotColor)
        {
            this.text = text;
            this.dotColor = dotColor;
        }
    }

    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image dotImage;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private LayoutElement layoutElement;
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private Color backgroundColor = new Color(1f, 1f, 1f, 0.08f);
    [SerializeField] private Color textColor = new Color(0.98f, 0.97f, 1f, 1f);

    private void Reset()
    {
        AutoAssignReferences();
    }

    public void SetContent(TagPillData data)
    {
        AutoAssignReferences();

        if (backgroundImage != null)
        {
            backgroundImage.color = backgroundColor;
        }

        if (dotImage != null)
        {
            dotImage.color = data.dotColor;
        }

        if (labelText != null)
        {
            labelText.color = textColor;
            labelText.text = data.text;
        }

        UpdatePreferredSize();
    }

    private void AutoAssignReferences()
    {
        if (rectTransform == null)
        {
            rectTransform = transform as RectTransform;
        }

        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
            if (backgroundImage == null)
            {
                backgroundImage = MemoryUIHierarchyUtility.FindComponentInDeepChild<Image>(transform, "BaseFill");
            }
        }

        if (dotImage == null)
        {
            Transform dot = MemoryUIHierarchyUtility.FindDeepChild(transform, "Dot");
            if (dot != null)
            {
                dotImage = dot.GetComponent<Image>();
            }
        }

        if (labelText == null)
        {
            labelText = GetComponentInChildren<TMP_Text>(true);
        }

        if (layoutElement == null)
        {
            layoutElement = GetComponent<LayoutElement>();
        }
    }

    private void UpdatePreferredSize()
    {
        if (rectTransform == null || labelText == null)
        {
            return;
        }

        HorizontalLayoutGroup layoutGroup = backgroundImage != null
            ? backgroundImage.GetComponent<HorizontalLayoutGroup>()
            : null;

        float spacing = layoutGroup != null ? layoutGroup.spacing : 10f;
        RectOffset padding = layoutGroup != null ? layoutGroup.padding : new RectOffset(16, 16, 12, 12);

        float dotSize = 0f;
        if (dotImage != null)
        {
            LayoutElement dotLayout = dotImage.GetComponent<LayoutElement>();
            if (dotLayout != null && dotLayout.preferredWidth > 0f)
            {
                dotSize = dotLayout.preferredWidth;
            }
            else
            {
                RectTransform dotRect = dotImage.transform as RectTransform;
                dotSize = dotRect != null ? dotRect.rect.width : 6f;
            }
        }

        Vector2 preferred = labelText.GetPreferredValues(labelText.text, 1000f, 64f);
        float width = padding.left + dotSize + spacing + preferred.x + padding.right;
        float height = padding.top + Mathf.Max(dotSize, preferred.y) + padding.bottom;

        if (layoutElement != null)
        {
            layoutElement.preferredWidth = width;
            layoutElement.preferredHeight = height;
            layoutElement.minWidth = width;
            layoutElement.minHeight = height;
            layoutElement.flexibleWidth = 0f;
            layoutElement.flexibleHeight = 0f;
        }

        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }
}

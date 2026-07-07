using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TimelineNodeView : MonoBehaviour
{
    [System.Serializable]
    public struct TimelineEntryData
    {
        public string year;
        public string label;

        public TimelineEntryData(string year, string label)
        {
            this.year = year;
            this.label = label;
        }
    }

    [SerializeField] private Image dotImage;
    [SerializeField] private TMP_Text yearText;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private Color activeDotColor = new Color(0.84f, 0.69f, 1f, 1f);
    [SerializeField] private Color inactiveDotColor = new Color(0.95f, 0.91f, 1f, 0.72f);

    private void Reset()
    {
        AutoAssignReferences();
    }

    public void SetContent(TimelineEntryData data, bool isActive)
    {
        AutoAssignReferences();

        if (yearText != null)
        {
            yearText.text = data.year;
        }

        if (labelText != null)
        {
            labelText.text = data.label;
        }

        if (dotImage != null)
        {
            dotImage.color = isActive ? activeDotColor : inactiveDotColor;
        }
    }

    private void AutoAssignReferences()
    {
        if (dotImage == null)
        {
            Transform dot = MemoryUIHierarchyUtility.FindDeepChild(transform, "Dot");
            if (dot != null)
            {
                dotImage = dot.GetComponent<Image>();
            }
        }

        if (yearText == null)
        {
            Transform year = MemoryUIHierarchyUtility.FindDeepChild(transform, "YearText");
            if (year != null)
            {
                yearText = year.GetComponent<TMP_Text>();
            }
        }

        if (labelText == null)
        {
            Transform label = MemoryUIHierarchyUtility.FindDeepChild(transform, "LabelText");
            if (label != null)
            {
                labelText = label.GetComponent<TMP_Text>();
            }
        }
    }
}

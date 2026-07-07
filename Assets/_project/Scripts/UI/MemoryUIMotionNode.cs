using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MemoryUIMotionNode : MonoBehaviour
{
    [SerializeField] private RectTransform motionRoot;
    [SerializeField] private CanvasGroup motionCanvasGroup;
    [SerializeField] private Vector2 defaultAnchoredPosition;
    [SerializeField] private Vector3 defaultLocalScale = Vector3.one;
    [SerializeField] private Vector3 defaultLocalEulerAngles;

    public RectTransform MotionRoot => motionRoot;
    public CanvasGroup MotionCanvasGroup => motionCanvasGroup;

    private void Reset()
    {
        AutoAssignReferences();
        CacheDefaults();
    }

    private void Awake()
    {
        AutoAssignReferences();
        CacheDefaults();
    }

    public void Configure(RectTransform targetRoot, CanvasGroup targetCanvasGroup)
    {
        motionRoot = targetRoot;
        motionCanvasGroup = targetCanvasGroup;
        CacheDefaults();
    }

    public void ResetMotionState()
    {
        AutoAssignReferences();
        if (motionRoot == null)
        {
            return;
        }

        motionRoot.anchoredPosition = defaultAnchoredPosition;
        motionRoot.localScale = defaultLocalScale;
        motionRoot.localEulerAngles = defaultLocalEulerAngles;

        if (motionCanvasGroup != null)
        {
            motionCanvasGroup.alpha = 1f;
            motionCanvasGroup.interactable = true;
            motionCanvasGroup.blocksRaycasts = true;
        }
    }

    private void CacheDefaults()
    {
        if (motionRoot == null)
        {
            return;
        }

        defaultAnchoredPosition = motionRoot.anchoredPosition;
        defaultLocalScale = motionRoot.localScale;
        defaultLocalEulerAngles = motionRoot.localEulerAngles;
    }

    private void AutoAssignReferences()
    {
        if (motionRoot == null)
        {
            Transform child = transform.Find("MotionRoot");
            if (child != null)
            {
                motionRoot = child as RectTransform;
            }
        }

        if (motionCanvasGroup == null)
        {
            if (motionRoot != null)
            {
                motionCanvasGroup = motionRoot.GetComponent<CanvasGroup>();
            }

            if (motionCanvasGroup == null)
            {
                motionCanvasGroup = GetComponentInChildren<CanvasGroup>(true);
            }
        }
    }
}

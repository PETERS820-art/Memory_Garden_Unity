using UnityEngine;

public class MemoryModeUIFollower : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float followDistance = 1.8f;
    [SerializeField] private float verticalOffset = -0.05f;
    [SerializeField] private float lateralOffset;
    [SerializeField] private float positionSmoothTime = 0.2f;
    [SerializeField] private float rotationSmoothSpeed = 8f;
    [SerializeField] private float recenterAngleThreshold = 20f;
    [SerializeField] private float recenterDistanceThreshold = 0.35f;
    [SerializeField] private bool keepUpright = true;
    [SerializeField] private bool followOnlyWhenVisible = true;

    private bool isFollowing;
    private bool hasAnchor;
    private Vector3 currentVelocity;
    private Vector3 anchorPosition;

    private void Awake()
    {
        EnsureTargetCamera();
    }

    private void OnEnable()
    {
        EnsureTargetCamera();
    }

    private void LateUpdate()
    {
        if (!isFollowing)
        {
            return;
        }

        Camera cameraToUse = EnsureTargetCamera();
        if (cameraToUse == null)
        {
            return;
        }

        Vector3 desiredAnchor = CalculateAnchorPosition(cameraToUse);
        if (!hasAnchor)
        {
            anchorPosition = desiredAnchor;
            hasAnchor = true;
        }
        else if (ShouldRecenter(cameraToUse, desiredAnchor))
        {
            anchorPosition = desiredAnchor;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            anchorPosition,
            ref currentVelocity,
            Mathf.Max(0.01f, positionSmoothTime));

        Quaternion targetRotation = CalculateFacingRotation(cameraToUse, transform.position);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            1f - Mathf.Exp(-Mathf.Max(0.01f, rotationSmoothSpeed) * Time.deltaTime));
    }

    public void SnapToView()
    {
        Camera cameraToUse = EnsureTargetCamera();
        if (cameraToUse == null)
        {
            Debug.LogWarning("[MemoryModeUIFollower] Could not find a target camera.", this);
            return;
        }

        anchorPosition = CalculateAnchorPosition(cameraToUse);
        hasAnchor = true;
        currentVelocity = Vector3.zero;
        transform.position = anchorPosition;
        transform.rotation = CalculateFacingRotation(cameraToUse, transform.position);
    }

    public void BeginFollow()
    {
        EnsureTargetCamera();
        currentVelocity = Vector3.zero;
        isFollowing = true;
    }

    public void EndFollow()
    {
        isFollowing = false;
        currentVelocity = Vector3.zero;
    }

    private Camera EnsureTargetCamera()
    {
        if (targetCamera != null)
        {
            return targetCamera;
        }

        targetCamera = Camera.main;
        if (targetCamera != null)
        {
            return targetCamera;
        }

        Camera[] allCameras = Camera.allCameras;
        for (int i = 0; i < allCameras.Length; i++)
        {
            Camera candidate = allCameras[i];
            if (candidate != null && candidate.enabled)
            {
                targetCamera = candidate;
                return targetCamera;
            }
        }

        return null;
    }

    private Vector3 CalculateAnchorPosition(Camera cameraToUse)
    {
        Vector3 horizontalForward = Vector3.ProjectOnPlane(cameraToUse.transform.forward, Vector3.up);
        if (horizontalForward.sqrMagnitude <= 0.0001f)
        {
            horizontalForward = Vector3.ProjectOnPlane(cameraToUse.transform.up, Vector3.up);
        }

        if (horizontalForward.sqrMagnitude <= 0.0001f)
        {
            horizontalForward = cameraToUse.transform.forward;
        }

        horizontalForward.Normalize();

        Vector3 right = cameraToUse.transform.right;
        right.y = 0f;
        if (right.sqrMagnitude > 0.0001f)
        {
            right.Normalize();
        }
        else
        {
            right = Vector3.right;
        }

        return cameraToUse.transform.position +
            (horizontalForward * followDistance) +
            (Vector3.up * verticalOffset) +
            (right * lateralOffset);
    }

    private bool ShouldRecenter(Camera cameraToUse, Vector3 desiredAnchor)
    {
        if (!hasAnchor)
        {
            return true;
        }

        Vector3 toCurrentAnchor = Vector3.ProjectOnPlane(anchorPosition - cameraToUse.transform.position, Vector3.up);
        Vector3 toDesiredAnchor = Vector3.ProjectOnPlane(desiredAnchor - cameraToUse.transform.position, Vector3.up);

        float angleDelta = 0f;
        if (toCurrentAnchor.sqrMagnitude > 0.0001f && toDesiredAnchor.sqrMagnitude > 0.0001f)
        {
            angleDelta = Vector3.Angle(toCurrentAnchor.normalized, toDesiredAnchor.normalized);
        }

        float distanceDelta = Vector3.Distance(anchorPosition, desiredAnchor);
        bool panelNotVisible = followOnlyWhenVisible && !IsPanelVisible(cameraToUse);

        return panelNotVisible ||
            angleDelta >= recenterAngleThreshold ||
            distanceDelta >= recenterDistanceThreshold;
    }

    private bool IsPanelVisible(Camera cameraToUse)
    {
        Vector3 viewportPoint = cameraToUse.WorldToViewportPoint(transform.position);
        if (viewportPoint.z <= 0f)
        {
            return false;
        }

        const float margin = 0.05f;
        return viewportPoint.x >= margin &&
            viewportPoint.x <= 1f - margin &&
            viewportPoint.y >= margin &&
            viewportPoint.y <= 1f - margin;
    }

    private Quaternion CalculateFacingRotation(Camera cameraToUse, Vector3 panelPosition)
    {
        Vector3 lookDirection = panelPosition - cameraToUse.transform.position;
        if (keepUpright)
        {
            lookDirection = Vector3.ProjectOnPlane(lookDirection, Vector3.up);
        }

        if (lookDirection.sqrMagnitude <= 0.0001f)
        {
            lookDirection = keepUpright ? Vector3.forward : cameraToUse.transform.forward;
        }

        return Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
    }
}

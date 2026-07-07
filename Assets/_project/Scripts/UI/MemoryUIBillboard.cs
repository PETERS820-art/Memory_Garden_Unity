using UnityEngine;

[DisallowMultipleComponent]
public class MemoryUIBillboard : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool enableBillboard = true;
    [SerializeField] private bool followCameraYawOnly = true;
    [SerializeField] private float smoothSpeed = 8f;
    [SerializeField] private float distanceFromCamera = 1.8f;

    private void LateUpdate()
    {
        if (!enableBillboard)
        {
            return;
        }

        Camera cameraToUse = ResolveCamera();
        if (cameraToUse == null)
        {
            return;
        }

        Vector3 lookDirection = transform.position - cameraToUse.transform.position;
        if (followCameraYawOnly)
        {
            lookDirection = Vector3.ProjectOnPlane(lookDirection, Vector3.up);
        }

        if (lookDirection.sqrMagnitude <= 0.0001f)
        {
            lookDirection = followCameraYawOnly
                ? Vector3.ProjectOnPlane(cameraToUse.transform.forward, Vector3.up)
                : cameraToUse.transform.forward;
        }

        if (lookDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        float lerpFactor = 1f - Mathf.Exp(-Mathf.Max(0.01f, smoothSpeed) * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, lerpFactor);
    }

    public void SnapInFrontOfCamera()
    {
        Camera cameraToUse = ResolveCamera();
        if (cameraToUse == null)
        {
            return;
        }

        Vector3 forward = Vector3.ProjectOnPlane(cameraToUse.transform.forward, Vector3.up);
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = cameraToUse.transform.forward;
        }

        forward.Normalize();
        transform.position = cameraToUse.transform.position + (forward * distanceFromCamera);
    }

    private Camera ResolveCamera()
    {
        if (targetCamera != null)
        {
            return targetCamera;
        }

        targetCamera = Camera.main;
        return targetCamera;
    }
}

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

namespace MemoryGarden.Interaction
{
    [DisallowMultipleComponent]
    public sealed class LeftStickTeleportController : MonoBehaviour
    {
        [Header("References")]
        public Transform rayOrigin;
        public InputActionReference leftThumbstickAction;
        public TeleportationProvider teleportationProvider;
        public LayerMask teleportSurfaceMask = ~0;
        public GameObject teleportReticle;

        [Header("Aiming")]
        [Min(0f)] public float activationThreshold = 0.2f;
        [Min(0.1f)] public float maxDistance = 10f;
        public bool requireValidSurface = true;
        public bool showDebugRay;
        public bool useFloorSegmentMarker = true;

        [Header("Diagnostics")]
        public bool logDebug = true;
        [Min(0.1f)] public float debugLogInterval = 0.5f;

        bool m_IsAiming;
        bool m_HasValidTarget;
        Vector3 m_TargetPosition;
        Quaternion m_TargetRotation;
        float m_NextDebugLogTime;
        Collider m_LastHitCollider;
        bool m_LastDidHit;
        bool m_LastHitValid;

        InputAction ThumbstickAction => leftThumbstickAction != null ? leftThumbstickAction.action : null;

        void Awake()
        {
            if (rayOrigin == null)
                rayOrigin = transform;
            if (teleportReticle != null)
                teleportReticle.SetActive(false);

            Log($"Awake | rayOrigin={(rayOrigin != null ? rayOrigin.name : "NULL")} | actionRef={(leftThumbstickAction != null ? leftThumbstickAction.name : "NULL")} | provider={(teleportationProvider != null ? teleportationProvider.name : "NULL")} | reticle={(teleportReticle != null ? teleportReticle.name : "NULL")}");
        }

        void OnEnable()
        {
            ThumbstickAction?.Enable();
            var action = ThumbstickAction;
            Log($"OnEnable | action={(action != null ? action.actionMap.name + "/" + action.name : "NULL")} | enabled={(action != null && action.enabled)} | controls={(action != null ? action.controls.Count : 0)}");
        }

        void OnDisable()
        {
            HideReticle();
            m_IsAiming = false;
            m_HasValidTarget = false;
        }

        void Update()
        {
            var action = ThumbstickAction;
            if (action == null || rayOrigin == null)
            {
                if (Time.unscaledTime >= m_NextDebugLogTime)
                {
                    Log($"Update blocked | action={(action != null ? "OK" : "NULL")} | rayOrigin={(rayOrigin != null ? "OK" : "NULL")}");
                    m_NextDebugLogTime = Time.unscaledTime + debugLogInterval;
                }
                return;

            }

            var stick = action.ReadValue<Vector2>();
            var magnitude = stick.magnitude;
            var stickActive = magnitude >= activationThreshold;
            if (stickActive)
            {
                if (!m_IsAiming)
                    Log($"Stick activated | value={stick} | magnitude={magnitude:F3} | threshold={activationThreshold:F3} | control={(action.activeControl != null ? action.activeControl.path : "none")}");
                else if (Time.unscaledTime >= m_NextDebugLogTime)
                {
                    Log($"Stick held | value={stick} | magnitude={magnitude:F3}");
                    m_NextDebugLogTime = Time.unscaledTime + debugLogInterval;
                }
                m_IsAiming = true;
                UpdateAim();
                return;
            }

            if (!m_IsAiming)
                return;

            if (m_HasValidTarget)
            {
                Log($"Stick released | valid target={m_TargetPosition} | queueing teleport");
                QueueTeleport();
            }
            else
            {
                Log($"Stick released | no valid target | no teleport");
            }

            m_IsAiming = false;
            m_HasValidTarget = false;
            HideReticle();
        }

        void UpdateAim()
        {
            var ray = new Ray(rayOrigin.position, rayOrigin.forward);
            var didHit = Physics.Raycast(ray, out var hit, maxDistance, teleportSurfaceMask, QueryTriggerInteraction.Ignore);
            var marker = didHit ? hit.collider.GetComponentInParent<TeleportSurfaceMarker>() : null;
            var hierarchyFloor = didHit ? FindRecognizedFloorSegment(hit.collider.transform) : null;
            var recognizedFloor = marker != null || hierarchyFloor != null;
            var valid = didHit && (!requireValidSurface || !useFloorSegmentMarker || recognizedFloor);

            var hitSummary = didHit
                ? $"Ray hit | collider={GetPath(hit.collider.transform)} | point={hit.point} | distance={hit.distance:F2} | marker={(marker != null ? GetPath(marker.transform) : "NONE")} | hierarchyFloor={(hierarchyFloor != null ? GetPath(hierarchyFloor) : "NONE")} | valid={valid}"
                : $"Ray miss | maxDistance={maxDistance:F2} | mask={teleportSurfaceMask.value}";
            if (didHit != m_LastDidHit || hit.collider != m_LastHitCollider || valid != m_LastHitValid || Time.unscaledTime >= m_NextDebugLogTime)
            {
                Log(hitSummary);
                m_LastDidHit = didHit;
                m_LastHitCollider = didHit ? hit.collider : null;
                m_LastHitValid = valid;
                m_NextDebugLogTime = Time.unscaledTime + debugLogInterval;
            }

            m_HasValidTarget = valid;
            if (valid)
            {
                m_TargetPosition = hit.point;
                m_TargetRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                if (teleportReticle != null)
                {
                    teleportReticle.transform.SetPositionAndRotation(hit.point + hit.normal * 0.01f, m_TargetRotation);
                    teleportReticle.SetActive(true);
                }
            }
            else
            {
                HideReticle();
            }

            if (showDebugRay)
                Debug.DrawRay(ray.origin, ray.direction * (didHit ? hit.distance : maxDistance), valid ? Color.cyan : Color.red);
        }

        void QueueTeleport()
        {
            if (teleportationProvider == null)
            {
                Log("Teleport blocked | TeleportationProvider is NULL");
                return;

            }

            var request = new TeleportRequest
            {
                destinationPosition = m_TargetPosition,
                destinationRotation = m_TargetRotation,
                matchOrientation = MatchOrientation.None,
                requestTime = Time.time
            };
            var queued = teleportationProvider.QueueTeleportRequest(request);
            Log($"Teleport request | queued={queued} | destination={m_TargetPosition} | providerEnabled={teleportationProvider.enabled}");
        }


        void Log(string message)
        {
            if (logDebug)
                Debug.Log($"[LeftStickTeleport] {message}", this);
        }

        static string GetPath(Transform value)
        {
            if (value == null)
                return "NULL";
            var path = value.name;
            while (value.parent != null)
            {
                value = value.parent;
                path = value.name + "/" + path;
            }
            return path;
        }

        static Transform FindRecognizedFloorSegment(Transform hitTransform)
        {
            Transform floorSegment = null;
            var current = hitTransform;
            while (current != null)
            {
                if (current.name.StartsWith("PF_SM_floor_", System.StringComparison.OrdinalIgnoreCase))
                    floorSegment = current;

                if (current.name.Equals("FloorSegments", System.StringComparison.OrdinalIgnoreCase))
                    return floorSegment;

                current = current.parent;
            }

            return null;
        }

        void HideReticle()
        {
            if (teleportReticle != null && teleportReticle.activeSelf)
                teleportReticle.SetActive(false);
        }
    }
}

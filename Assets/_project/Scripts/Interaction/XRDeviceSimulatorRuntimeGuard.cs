using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-31000)]
public sealed class XRDeviceSimulatorRuntimeGuard : MonoBehaviour
{
#if !UNITY_EDITOR
    private void Awake()
    {
        DisableSimulatorInPlayer();
    }

    private void OnEnable()
    {
        DisableSimulatorInPlayer();
    }

    private void DisableSimulatorInPlayer()
    {
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }
#endif
}

using UnityEngine;

[DisallowMultipleComponent]
public class MemoryDisplayLight : MonoBehaviour
{
    [SerializeField] private string lightId;
    [SerializeField] private FurnitureLightRole lightRole = FurnitureLightRole.Decorative;
    [SerializeField] private Light targetLight;
    [SerializeField] private bool allowRuntimeAdjustment = true;

    public string LightId => string.IsNullOrWhiteSpace(lightId) ? gameObject.name : lightId;
    public FurnitureLightRole LightRole => lightRole;
    public Light TargetLight => targetLight;
    public bool AllowRuntimeAdjustment => allowRuntimeAdjustment;

    public void ApplyAuthoringData(string id, FurnitureLightRole role, Light lightComponent, bool allowAdjustment)
    {
        lightId = string.IsNullOrWhiteSpace(id) ? gameObject.name : id;
        lightRole = role;
        targetLight = lightComponent;
        allowRuntimeAdjustment = allowAdjustment;
    }

    public void AutoAssignLight()
    {
        if (targetLight != null)
        {
            return;
        }

        targetLight = GetComponent<Light>();
        if (targetLight == null)
        {
            targetLight = GetComponentInChildren<Light>(true);
        }
    }

    private void Reset()
    {
        if (string.IsNullOrWhiteSpace(lightId))
        {
            lightId = gameObject.name;
        }

        AutoAssignLight();
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(lightId))
        {
            lightId = gameObject.name;
        }

        AutoAssignLight();
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using InputTrackedPoseDriver = UnityEngine.InputSystem.XR.TrackedPoseDriver;

public sealed class XRRuntimePoseDiagnostics : MonoBehaviour
{
    [SerializeField] private bool logContinuously;
    [SerializeField] private int continuousLogIntervalFrames = 60;

    private readonly List<XRInputSubsystem> _inputSubsystems = new List<XRInputSubsystem>();
    private readonly List<UnityEngine.XR.InputDevice> _headMountedDevices = new List<UnityEngine.XR.InputDevice>();

    private XROrigin _xrOrigin;
    private Camera _targetCamera;
    private InputTrackedPoseDriver _trackedPoseDriver;
    private Coroutine _startupProbeRoutine;

    private void Awake()
    {
        RefreshReferences();
    }

    private void OnEnable()
    {
        InputSystem.onDeviceChange += OnInputDeviceChange;
        _startupProbeRoutine = StartCoroutine(LogStartupSnapshots());
    }

    private void OnDisable()
    {
        InputSystem.onDeviceChange -= OnInputDeviceChange;

        if (_startupProbeRoutine != null)
        {
            StopCoroutine(_startupProbeRoutine);
            _startupProbeRoutine = null;
        }
    }

    private void LateUpdate()
    {
        if (!logContinuously || continuousLogIntervalFrames <= 0)
        {
            return;
        }

        if (Time.frameCount % continuousLogIntervalFrames == 0)
        {
            LogSnapshot("continuous");
        }
    }

    private IEnumerator LogStartupSnapshots()
    {
        LogSnapshot("enable");

        yield return null;
        LogSnapshot("frame+1");

        yield return null;
        LogSnapshot("frame+2");

        for (int i = 0; i < 3; i++)
        {
            yield return null;
        }
        LogSnapshot("frame+5");

        for (int i = 0; i < 10; i++)
        {
            yield return null;
        }
        LogSnapshot("frame+15");

        for (int i = 0; i < 15; i++)
        {
            yield return null;
        }
        LogSnapshot("frame+30");
    }

    private void OnInputDeviceChange(UnityEngine.InputSystem.InputDevice device, InputDeviceChange change)
    {
        if (device is TrackedDevice || device.layout.Contains("XR"))
        {
            Debug.Log($"[XRRuntimePoseDiagnostics] InputSystem device change: {change} -> {DescribeInputSystemDevice(device)}", this);
            LogSnapshot($"device-change:{change}");
        }
    }

    private void RefreshReferences()
    {
        if (_xrOrigin == null)
        {
            _xrOrigin = GetComponent<XROrigin>();
        }

        if (_targetCamera == null)
        {
            _targetCamera = _xrOrigin != null ? _xrOrigin.Camera : GetComponentInChildren<Camera>(true);
        }

        if (_trackedPoseDriver == null && _targetCamera != null)
        {
            _trackedPoseDriver = _targetCamera.GetComponent<InputTrackedPoseDriver>();
        }
    }

    private void LogSnapshot(string label)
    {
        RefreshReferences();

        var builder = new StringBuilder(2048);
        builder.AppendLine($"[XRRuntimePoseDiagnostics] snapshot={label} frame={Time.frameCount}");
        AppendManagerState(builder);
        AppendSubsystemState(builder);
        AppendLegacyHmdState(builder);
        AppendInputSystemHmdState(builder);
        AppendTrackedPoseDriverState(builder);
        AppendCameraState(builder);
        Debug.Log(builder.ToString(), this);
    }

    private void AppendManagerState(StringBuilder builder)
    {
        XRManagerSettings manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        string loaderName = manager != null && manager.activeLoader != null ? manager.activeLoader.GetType().FullName : "<null>";
        bool initComplete = manager != null && manager.isInitializationComplete;
        builder.AppendLine($"manager.loader={loaderName}");
        builder.AppendLine($"manager.initComplete={initComplete}");
    }

    private void AppendSubsystemState(StringBuilder builder)
    {
        _inputSubsystems.Clear();
        SubsystemManager.GetInstances(_inputSubsystems);

        builder.AppendLine($"xrInputSubsystem.count={_inputSubsystems.Count}");
        for (int i = 0; i < _inputSubsystems.Count; i++)
        {
            XRInputSubsystem subsystem = _inputSubsystems[i];
            if (subsystem == null)
            {
                builder.AppendLine($"xrInputSubsystem[{i}]=<null>");
                continue;
            }

            builder.AppendLine(
                $"xrInputSubsystem[{i}].running={subsystem.running} trackingOrigin={subsystem.GetTrackingOriginMode()}");
        }
    }

    private void AppendLegacyHmdState(StringBuilder builder)
    {
        _headMountedDevices.Clear();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, _headMountedDevices);

        builder.AppendLine($"legacyXR.hmdCount={_headMountedDevices.Count}");
        for (int i = 0; i < _headMountedDevices.Count; i++)
        {
            UnityEngine.XR.InputDevice device = _headMountedDevices[i];
            bool hasCenterPos = device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.centerEyePosition, out Vector3 centerPos);
            bool hasCenterRot = device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.centerEyeRotation, out Quaternion centerRot);
            bool hasDevicePos = device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 devicePos);
            bool hasDeviceRot = device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out Quaternion deviceRot);

            builder.AppendLine(
                $"legacyXR.hmd[{i}] name={device.name} valid={device.isValid} chars={device.characteristics}");
            builder.AppendLine(
                $"legacyXR.hmd[{i}] centerEyePosition={FormatValue(hasCenterPos, centerPos)} centerEyeRotation={FormatValue(hasCenterRot, centerRot.eulerAngles)}");
            builder.AppendLine(
                $"legacyXR.hmd[{i}] devicePosition={FormatValue(hasDevicePos, devicePos)} deviceRotation={FormatValue(hasDeviceRot, deviceRot.eulerAngles)}");
        }
    }

    private void AppendInputSystemHmdState(StringBuilder builder)
    {
        XRHMD[] hmds = InputSystem.devices.OfType<XRHMD>().ToArray();
        builder.AppendLine($"inputSystem.xrhmdCount={hmds.Length}");
        for (int i = 0; i < hmds.Length; i++)
        {
            XRHMD hmd = hmds[i];
            builder.AppendLine($"inputSystem.xrhmd[{i}] {DescribeInputSystemDevice(hmd)}");
            builder.AppendLine(
                $"inputSystem.xrhmd[{i}] centerEyePosition={hmd.centerEyePosition?.ReadValue()} centerEyeRotation={hmd.centerEyeRotation?.ReadValue().eulerAngles}");
            builder.AppendLine(
                $"inputSystem.xrhmd[{i}] devicePosition={hmd.devicePosition?.ReadValue()} deviceRotation={hmd.deviceRotation?.ReadValue().eulerAngles}");
        }
    }

    private void AppendTrackedPoseDriverState(StringBuilder builder)
    {
        if (_trackedPoseDriver == null)
        {
            builder.AppendLine("tpd=<missing>");
            return;
        }

        builder.AppendLine($"tpd.updateType={_trackedPoseDriver.updateType} trackingType={_trackedPoseDriver.trackingType} ignoreTrackingState={_trackedPoseDriver.ignoreTrackingState}");
        AppendActionState(builder, "tpd.position", _trackedPoseDriver.positionInput.action, TryReadVector3);
        AppendActionState(builder, "tpd.rotation", _trackedPoseDriver.rotationInput.action, TryReadQuaternionEuler);
        AppendActionState(builder, "tpd.trackingState", _trackedPoseDriver.trackingStateInput.action, TryReadInt);
    }

    private void AppendCameraState(StringBuilder builder)
    {
        if (_xrOrigin != null)
        {
            builder.AppendLine($"xrOrigin.requestedMode={_xrOrigin.RequestedTrackingOriginMode} currentMode={_xrOrigin.CurrentTrackingOriginMode}");
            if (_xrOrigin.CameraFloorOffsetObject != null)
            {
                builder.AppendLine($"xrOrigin.cameraOffsetLocalPos={_xrOrigin.CameraFloorOffsetObject.transform.localPosition}");
            }
        }

        if (_targetCamera == null)
        {
            builder.AppendLine("camera=<missing>");
            return;
        }

        Transform cameraTransform = _targetCamera.transform;
        builder.AppendLine($"camera.localPosition={cameraTransform.localPosition} localEuler={cameraTransform.localRotation.eulerAngles}");
        builder.AppendLine($"camera.worldPosition={cameraTransform.position} worldEuler={cameraTransform.rotation.eulerAngles}");
    }

    private void AppendActionState(
        StringBuilder builder,
        string label,
        InputAction action,
        System.Func<InputAction, string> valueReader)
    {
        if (action == null)
        {
            builder.AppendLine($"{label}.action=<null>");
            return;
        }

        builder.AppendLine($"{label}.enabled={action.enabled} phase={action.phase} controls={action.controls.Count}");
        if (action.controls.Count > 0)
        {
            builder.AppendLine($"{label}.controlList={string.Join(" | ", action.controls.Select(DescribeControl))}");
        }
        builder.AppendLine($"{label}.value={valueReader(action)}");
    }

    private static string DescribeControl(InputControl control)
    {
        if (control == null)
        {
            return "<null>";
        }

        return $"{control.path} device={control.device.layout}/{control.device.displayName}";
    }

    private static string DescribeInputSystemDevice(UnityEngine.InputSystem.InputDevice device)
    {
        if (device == null)
        {
            return "<null>";
        }

        return $"layout={device.layout} displayName={device.displayName} product={device.description.product} manufacturer={device.description.manufacturer} beforeRender={device.updateBeforeRender}";
    }

    private static string TryReadVector3(InputAction action)
    {
        return action.enabled && action.controls.Count > 0 ? action.ReadValue<Vector3>().ToString() : "<unresolved>";
    }

    private static string TryReadQuaternionEuler(InputAction action)
    {
        return action.enabled && action.controls.Count > 0 ? action.ReadValue<Quaternion>().eulerAngles.ToString() : "<unresolved>";
    }

    private static string TryReadInt(InputAction action)
    {
        return action.enabled && action.controls.Count > 0 ? action.ReadValue<int>().ToString() : "<unresolved>";
    }

    private static string FormatValue<T>(bool hasValue, T value)
    {
        return hasValue ? value?.ToString() ?? "<null>" : "<missing>";
    }
}

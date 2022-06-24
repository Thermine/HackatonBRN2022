using System;
using System.Collections.Generic;
using Assets.HurricaneVR.Framework.Shared.Utilities;
using HurricaneVR.Framework.Shared;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;
#if HVR_OCULUS
using HurricaneVR.Framework.Oculus;
#endif

#if HVR_STEAMVR
using HurricaneVR.Framework.SteamVR;
#endif

namespace HurricaneVR.Framework.ControllerInput
{
    [Serializable]
    public class HVRControllerEvent : UnityEvent<HVRController> { }

    public class HVRInputManager : MonoBehaviour
    {
        public UnityEvent HMDFirstActivation = new UnityEvent();
        public UnityEvent HMDActivated = new UnityEvent();
        public UnityEvent HMDDeactivated = new UnityEvent();
        public UnityEvent HMDRecentered = new UnityEvent();
        public UnityEvent UserSensed = new UnityEvent();
        public UnityEvent UserNotSensed = new UnityEvent();

        public HVRControllerEvent LeftControllerConnected = new HVRControllerEvent();
        public HVRControllerEvent RightControllerConnected = new HVRControllerEvent();

        public const string OpenVR = "openvr";
        public const string WindowsMR = "windowsmr";
        public const string Vive = "vive";
        public const string Cosmos = "cosmos";
        public const string Oculus = "oculus";
        public const string Knuckles = "knuckles";
        public const string WMRController = "spatial";
        public const string HTC = "htc";

        public static HVRInputManager Instance { get; private set; }

        [Tooltip("If using OVRInput for Oculus devices without OVRManager in the scene then set this to true.")]
        public bool ForceOVRInputUpdate;



        public HVRController LeftController;
        public HVRController RightController;

        public HVRController LeftXRInputController;
        public HVRController RightXRInputController;

        public HVRController LeftOculusController;
        public HVRController RightOculusController;

        public HVRController LeftSteamController;
        public HVRController RightSteamController;

        public InputSDK OculusInputSDK = InputSDK.XRInput;
        public InputSDK WMRInputSDK = InputSDK.XRInput;
        public InputSDK ViveInputSDK = InputSDK.XRInput;
        public InputSDK KnucklesSDK = InputSDK.SteamVR;
        public InputSDK CosmosSDK = InputSDK.SteamVR;

        public HVRInputSettings WMRInputMap;
        public HVRInputSettings OculusInputMap;
        public HVRInputSettings WMROpenVRInputMap;
        public HVRInputSettings OculusOpenVRInputMap;
        public HVRInputSettings ViveInputMap;
        public HVRInputSettings KnucklesInputMap;
        public HVRInputSettings CosmosInputMap;

        public string HMDManufacturer;
        public string HMDName;

        public string LeftManufacturer;
        public string LeftControllerName;

        public string RightManufacturer;
        public string RightControllerName;

        [Tooltip("WMR device deadzone, if any.")]
        public Vector2 WMRDeadzone = new Vector2(.15f, .15f);
        [Tooltip("Oculus device deadzone, if any.")]
        public Vector2 OculusDeadzone = new Vector2(.15f, .15f);
        [Tooltip("Vive device deadzone, if any.")]
        public Vector2 ViveDeadzone = new Vector2(0f, 0f);
        [Tooltip("Knuckles device deadzone, if any.")]
        public Vector2 KnucklesDeadzone = new Vector2(0f, 0f);

        [Tooltip("Cosmos device deadzone, if any.")]
        public Vector2 CosmosDeadzone = new Vector2(0f, 0f);

        [Tooltip("Master deadzone, useful if you want the user to set.")]
        public Vector2 DeadzoneOverride;

        [Tooltip("Override provider level deadzone.")]
        public bool OverrideDeadzone;

        public List<string> LeftFeatures = new List<string>();
        public List<string> RightFeatures = new List<string>();
        public List<string> HMDFeatures = new List<string>();

        private readonly List<XRDisplaySubsystem> _displaySubsystems = new List<XRDisplaySubsystem>();
        private bool _applicationExiting;
        private InputDevice _hmdDevice;
        public InputDevice HMDDevice
        {
            get
            {
                if (_hmdDevice.isValid)
                    return _hmdDevice;
                _hmdDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                return _hmdDevice;
            }
        }

        public bool PreviousHMDActive { get; private set; }
        public bool PreviousUserPresent { get; private set; }

        public bool HMDActive
        {
            get
            {
                if (HMDDevice.isValid)
                    return true;
                //beware this still returns true if the device is not rendering
                return XRSettings.isDeviceActive;
            }
        }

        public bool UserPresent
        {
            get
            {

                if (!HMDActive)
                    return false;

#if USING_XR_MANAGEMENT
        
                //https://forum.unity.com/threads/commonusages-userpresence-doesnt-report-correctly.818766/
                //this features requires XR Management
                //might be active but not rendering due to headset not on
                if (HMDDevice.TryGetFeatureValue(CommonUsages.userPresence, out var present))
                {
                    return present;
                }

#elif !UNITY_2020_1_OR_NEWER


                //https://stackoverflow.com/questions/51372771/how-to-check-if-a-hmd-in-unity-2018-is-in-use

                //if xr management wasn't detected use the old API for legacy VR
                if (XRDevice.userPresence == UserPresenceState.Present)
                    return true;
#endif

                //todo, test if this is a xrplugin only thing
                //https://docs.unity3d.com/ScriptReference/XR.XRDevice-isPresent.html

                _displaySubsystems.Clear();
                SubsystemManager.GetInstances<XRDisplaySubsystem>(_displaySubsystems);
                foreach (var xrDisplay in _displaySubsystems)
                {
                    if (xrDisplay.running)
                    {
                        return true;
                    }
                }


                return false;
            }
        }

        private InputDevice _leftDevice;
        public InputDevice LeftDevice
        {
            get
            {
                if (_leftDevice.isValid)
                    return _leftDevice;
                _leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
                return _leftDevice;
            }
        }

        private InputDevice _rightDevice;
        public InputDevice RightDevice
        {
            get
            {
                if (_rightDevice.isValid)
                    return _rightDevice;
                _rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                return _rightDevice;
            }
        }


        public XRInputSystem LeftXRInputSystem { get; private set; }
        public XRInputSystem RightXRInputSystem { get; private set; }

        private bool _isHMDFirstActivationReported;


        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(this.gameObject);
            }
            else
            {
                Destroy(this);
                return;
            }

            InputDevices.deviceConfigChanged += OnDeviceConfigChanged;
            InputDevices.deviceConnected += OnDeviceConnected;
            InputDevices.deviceDisconnected += OnDeviceDisconnected;

            UpdateDevices();
        }

        private void OnApplicationQuit()
        {
            _applicationExiting = true;
        }

        private void OnEnable()
        {
            UpdateDevices();
        }

        private void UpdateDevices()
        {
            UpdateDeviceInformation(LeftDevice);
            UpdateLeftController(LeftDevice);
            UpdateDeviceInformation(RightDevice);
            UpdateRightController(RightDevice);

            UpdateDeviceInformation(HMDDevice);
        }

        private void Update()
        {
            //events aren't firing in build for some headsets?
            if (!HMDDevice.isValid)
            {
                UpdateHMD(HMDDevice);
            }

            CheckHMDEvents();
            CheckUserPresentEvents();

            PreviousHMDActive = HMDActive;
            PreviousUserPresent = UserPresent;

#if HVR_OCULUS

            if (OculusInputSDK == InputSDK.Oculus && ForceOVRInputUpdate && (LeftXRInputSystem == XRInputSystem.Oculus || RightXRInputSystem == XRInputSystem.Oculus))
            {
                HVROculusController.UpdateOVRInput();
            }
#endif
        }

        private void CheckHMDEvents()
        {
            if (!PreviousHMDActive && HMDActive)
            {
                if (!_isHMDFirstActivationReported)
                {
                    HMDFirstActivation.Invoke();
                    _isHMDFirstActivationReported = true;
                }

                HMDActivated.Invoke();
            }
            else if (PreviousHMDActive && !HMDActive)
            {
                HMDDeactivated.Invoke();
            }
        }

        private void CheckUserPresentEvents()
        {
            if (!PreviousUserPresent && UserPresent)
            {
                UserSensed.Invoke();
            }
            else if (PreviousUserPresent && !UserPresent)
            {
                UserNotSensed.Invoke();
            }
        }


        private void Start()
        {
            UpdateDevices();
        }


        private void OnDeviceDisconnected(InputDevice device)
        {
            if (_applicationExiting)
                return;
            Debug.Log($"disconnected {device.name},{device.manufacturer}");
            UpdateDeviceInformation(device);
        }

        private void OnDeviceConnected(InputDevice device)
        {
            if (_applicationExiting)
                return;
            Debug.Log($"connected {device.name},{device.manufacturer}");
            //FYI: steamvr causes this to fire even if the controller is off if the controller was previously on.
            UpdateDeviceInformation(device);
        }

        private void OnDeviceConfigChanged(InputDevice device)
        {
            if (_applicationExiting)
                return;
            Debug.Log($"config changed {device.name},{device.manufacturer}");
            UpdateDeviceInformation(device);
        }


        private void UpdateDeviceInformation(InputDevice device)
        {
            if (device.characteristics.HasFlag(InputDeviceCharacteristics.Controller))
            {
                if (device.characteristics.HasFlag(InputDeviceCharacteristics.Left))
                {
                    RightControllerName = device.name;
                    RightManufacturer = device.manufacturer;
                    UpdateLeftController(device);
                }

                if (device.characteristics.HasFlag(InputDeviceCharacteristics.Right))
                {
                    LeftControllerName = device.name;
                    LeftManufacturer = device.manufacturer;
                    UpdateRightController(device);
                }
            }

            if (device.characteristics.HasFlag(InputDeviceCharacteristics.HeadMounted))
            {
                UpdateHMD(device);
            }
        }

        public Vector3 hmdpos;

        private void UpdateHMD(InputDevice device)
        {
            if (device.isValid)
            {
                HMDName = device.name;
                HMDManufacturer = device.manufacturer;

                HMDFeatures.Clear();
                var inputFeatures = new List<UnityEngine.XR.InputFeatureUsage>();
                if (device.TryGetFeatureUsages(inputFeatures))
                {
                    foreach (var feature in inputFeatures)
                    {
                        HMDFeatures.Add($"{feature.name}");
                    }
                }
            }
        }

        private void UpdateRightController(InputDevice device)
        {
            RightXRInputSystem = GetSDK(RightDevice.manufacturer?.ToLower(), RightDevice.name?.ToLower());
            RightController = UpdateController(RightXRInputSystem, device, HVRHandSide.Right);
            if (device.isValid)
                RightControllerConnected.Invoke(RightController);

            RightFeatures.Clear();
            var inputFeatures = new List<UnityEngine.XR.InputFeatureUsage>();
            if (device.TryGetFeatureUsages(inputFeatures))
            {
                foreach (var feature in inputFeatures)
                {
                    RightFeatures.Add($"{feature.name}");
                }
            }
        }

        private void UpdateLeftController(InputDevice device)
        {
            LeftXRInputSystem = GetSDK(LeftDevice.manufacturer?.ToLower(), LeftDevice.name?.ToLower());
            LeftController = UpdateController(LeftXRInputSystem, device, HVRHandSide.Left);
            if (device.isValid)
                LeftControllerConnected.Invoke(LeftController);

            LeftFeatures.Clear();

            var inputFeatures = new List<UnityEngine.XR.InputFeatureUsage>();
            if (device.TryGetFeatureUsages(inputFeatures))
            {
                foreach (var feature in inputFeatures)
                {
                    LeftFeatures.Add($"{feature.name}");
                }
            }
        }

        private HVRController UpdateController(XRInputSystem sdk, InputDevice device, HVRHandSide side)
        {
            InputSDK inputSdk;
            HVRInputSettings inputMap = null;
            var deadZone = Vector2.zero;
            HVRControllerType controllerType;
            switch (sdk)
            {
                case XRInputSystem.WMROpenVR:
                    inputSdk = WMRInputSDK;
                    inputMap = WMROpenVRInputMap;
                    deadZone = WMRDeadzone;
                    controllerType = HVRControllerType.WMR;
                    break;
                case XRInputSystem.WMR:
                    inputSdk = WMRInputSDK;
                    inputMap = WMRInputMap;
                    deadZone = WMRDeadzone;
                    controllerType = HVRControllerType.WMR;
                    break;
                case XRInputSystem.OculusOpenVR:
                    inputSdk = OculusInputSDK;
                    inputMap = OculusOpenVRInputMap;
                    deadZone = OculusDeadzone;
                    controllerType = HVRControllerType.Oculus;
                    break;
                case XRInputSystem.Oculus:
                    inputSdk = OculusInputSDK;
                    inputMap = OculusInputMap;
                    deadZone = OculusDeadzone;
                    controllerType = HVRControllerType.Oculus;
                    break;
                case XRInputSystem.Vive:
                    inputSdk = ViveInputSDK;
                    deadZone = ViveDeadzone;
                    inputMap = ViveInputMap;
                    controllerType = HVRControllerType.Vive;
                    break;
                case XRInputSystem.Knuckles:
                    inputSdk = KnucklesSDK;
                    deadZone = KnucklesDeadzone;
                    inputMap = KnucklesInputMap;
                    controllerType = HVRControllerType.Knuckles;
                    break;
                case XRInputSystem.Cosmos:
                    inputSdk = CosmosSDK;
                    inputMap = CosmosInputMap;
                    deadZone = CosmosDeadzone;
                    controllerType = HVRControllerType.Cosmos;
                    break;
                default:
                    inputSdk = InputSDK.XRInput;
                    inputMap = OculusInputMap;
                    deadZone = OculusDeadzone;
                    controllerType = HVRControllerType.Oculus;
                    break;
            }

            HVRController controller = null;

            if (side == HVRHandSide.Left)
            {
                if (LeftOculusController) LeftOculusController.enabled = false;
                if (LeftSteamController) LeftSteamController.enabled = false;
                if (LeftXRInputController) LeftXRInputController.enabled = false;
            }
            else
            {
                if (RightOculusController) RightOculusController.enabled = false;
                if (RightSteamController) RightSteamController.enabled = false;
                if (RightXRInputController) RightXRInputController.enabled = false;
            }

            switch (inputSdk)
            {
                case InputSDK.XRInput:

                    controller = side == HVRHandSide.Left ? LeftXRInputController : RightXRInputController;

                    HVRXRInputController xrController = controller as HVRXRInputController;

                    if (!controller)
                    {
                        xrController = gameObject.AddComponent<HVRXRInputController>();

                        controller = xrController;

                        if (side == HVRHandSide.Left)
                        {
                            LeftXRInputController = controller;
                        }
                        else
                        {
                            RightXRInputController = controller;
                        }
                    }

                    break;
                case InputSDK.Oculus:

#if HVR_OCULUS
                    controller = side == HVRHandSide.Left ? LeftOculusController : RightOculusController;

                    if (!controller)
                    {
                        var oculusController = gameObject.AddComponent<HVROculusController>();
                        controller = oculusController;

                        if (side == HVRHandSide.Left)
                        {
                            LeftOculusController = controller;
                        }
                        else
                        {
                            RightOculusController = controller;
                        }
                    }
#endif

                    break;
                case InputSDK.SteamVR:

#if HVR_STEAMVR
                    controller = side == HVRHandSide.Left ? LeftSteamController : RightSteamController;
                    if (!controller)
                    {
                        var steamController = gameObject.AddComponent<HVRSteamVRController>();
                        controller = steamController;
                        if (side == HVRHandSide.Left)
                            LeftSteamController = steamController;
                        else
                            RightSteamController = steamController;
                    }
#endif
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (OverrideDeadzone)
                deadZone = DeadzoneOverride;

            if (controller != null)
            {
                controller.XRNode = side == HVRHandSide.Left ? XRNode.LeftHand : XRNode.RightHand;
                controller.ThumbstickDeadZone = deadZone;
                controller.Side = side;
                controller.InputMap = inputMap;
                controller.enabled = true;
                controller.ControllerType = controllerType;
            }

            return controller;
        }

        public HVRController GetController(HVRHandSide side)
        {
            return side == HVRHandSide.Left ? LeftController : RightController;
        }

        public InputDevice GetDevice(HVRHandSide side)
        {
            if (side == HVRHandSide.Left) return LeftDevice;
            return RightDevice;
        }

        private static XRInputSystem GetSDK(string manufacturer, string controllerName)
        {
            if (string.IsNullOrWhiteSpace(manufacturer) && string.IsNullOrWhiteSpace(controllerName))
                return XRInputSystem.None;

            if (manufacturer == null)
                manufacturer = "";
            if (controllerName == null)
                controllerName = "";

            manufacturer = manufacturer.ToLower();
            controllerName = controllerName.ToLower();

            if (manufacturer.Contains(Oculus))
            {
                if (controllerName.Contains(OpenVR))
                {
                    return XRInputSystem.OculusOpenVR;
                }
                return XRInputSystem.Oculus;
            }

            //connected OpenVR Controller(vive_cosmos_controller) - Right,HTC - courtesy of AnriCZ
            if (controllerName.Contains(Cosmos))
                return XRInputSystem.Cosmos;

            if (manufacturer.Contains(HTC) || controllerName.Contains(Vive))
                return XRInputSystem.Vive;

            if (manufacturer.Contains(WindowsMR) || controllerName.Contains(WMRController))
            {
                if (controllerName.Contains(OpenVR))
                {
                    return XRInputSystem.WMROpenVR;
                }

                return XRInputSystem.WMR;
            }

            if (controllerName.Contains(Knuckles))
                return XRInputSystem.Knuckles;

            return XRInputSystem.None;
        }

        public void Recenter()
        {

#if USING_XR_MANAGEMENT

            var subsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetInstances<XRInputSubsystem>(subsystems);

            for (int i = 0; i < subsystems.Count; i++)
            {
                var currentInputSubsystem = subsystems[i];
                if (currentInputSubsystem != null)
                {
                    if (!currentInputSubsystem.TryRecenter())
                    {
                        Debug.Log($"Failed to recenter.");
                    }

                }
            }

#elif !UNITY_2020_1_OR_NEWER


            InputTracking.Recenter();

#endif

            this.ExecuteNextUpdate(() => HMDRecentered.Invoke());
        }
    }





}

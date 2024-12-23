﻿using System.Collections.Generic;
using HurricaneVR.Framework.Shared.Utilities;
using UnityEngine;
using UnityEngine.XR;

namespace HurricaneVR.Framework.Shared
{


    public abstract class HVRController : MonoBehaviour
    {
        public HVRHandSide Side { get; set; }

        public HVRButtonState GripButtonState;
        public HVRButtonState TriggerButtonState;
        public HVRButtonState PrimaryButtonState;
        public HVRButtonState SecondaryButtonState;
        public HVRButtonState MenuButtonState;
        public HVRButtonState PrimaryTouchButtonState;
        public HVRButtonState SecondaryTouchButtonState;
        public HVRButtonState JoystickButtonState;
        public HVRButtonState TrackpadButtonState;

        public HVRButtonState JoystickTouchState;
        public HVRButtonState TriggerTouchState;
        public HVRButtonState ThumbTouchState;

        public HVRButtonState TriggerNearTouchState;
        public HVRButtonState ThumbNearTouchState;

        public HVRButtonState TrackPadUp;
        public HVRButtonState TrackPadLeft;
        public HVRButtonState TrackPadRight;
        public HVRButtonState TrackPadDown;

        public Vector2 JoystickAxis;
        public Vector2 TrackpadAxis;

        public bool PrimaryButton;
        public bool SecondaryButton;
        public bool JoystickClicked;
        public bool TrackPadClicked;
        public bool MenuButton;
        public bool PrimaryTouch;
        public bool SecondaryTouch;

        public float Grip;
        public float Trigger;

        public bool ThumbTouch;
        public bool TriggerTouch;

        public bool ThumbNearTouch;
        public bool TriggerNearTouch;

        public bool GripButton;
        public bool TriggerButton;

        public bool JoystickTouch;


        public Vector3 Velocity;
        public Vector3 AngularVelocity;

        public bool IsActive { get; set; }

        public XRNode XRNode;

        private InputDevice _device;
        public InputDevice Device
        {
            get
            {
                if (_device.isValid)
                    return _device;
                _device = InputDevices.GetDeviceAtXRNode(XRNode);
                return _device;
            }
        }

        public Vector2 ThumbstickDeadZone { get; set; }
        public HVRInputSettings InputMap { get; set; }

        private static readonly Dictionary<HVRButtons, HVRButtonState> _leftButtonStates = new Dictionary<HVRButtons, HVRButtonState>();
        private static readonly Dictionary<HVRButtons, HVRButtonState> _rightButtonStates = new Dictionary<HVRButtons, HVRButtonState>();

        public HVRControllerType ControllerType { get; set; }

        public float AngularVelocityMagnitude;
        public float VelocityMagnitude;

        public readonly CircularBuffer<float> RecentVelocities = new CircularBuffer<float>(200);

        private void Start()
        {
            ResetTrackedVelocities();
        }

        private void Update()
        {
            UpdateInput();

            CorrectDeadzone();

            CheckButtonState(HVRButtons.Grip, ref GripButtonState);
            CheckButtonState(HVRButtons.Trigger, ref TriggerButtonState);
            CheckButtonState(HVRButtons.JoystickButton, ref JoystickButtonState);
            CheckButtonState(HVRButtons.TrackPadButton, ref TrackpadButtonState);
            CheckButtonState(HVRButtons.Primary, ref PrimaryButtonState);
            CheckButtonState(HVRButtons.Secondary, ref SecondaryButtonState);
            CheckButtonState(HVRButtons.Menu, ref MenuButtonState);
            CheckButtonState(HVRButtons.PrimaryTouch, ref PrimaryTouchButtonState);
            CheckButtonState(HVRButtons.SecondaryTouch, ref SecondaryTouchButtonState);
            CheckButtonState(HVRButtons.JoystickTouch, ref JoystickTouchState);
            CheckButtonState(HVRButtons.TriggerTouch, ref TriggerTouchState);
            CheckButtonState(HVRButtons.ThumbTouch, ref ThumbTouchState);
            CheckButtonState(HVRButtons.TriggerNearTouch, ref TriggerNearTouchState);
            CheckButtonState(HVRButtons.ThumbNearTouch, ref ThumbNearTouchState);

            CheckButtonState(HVRButtons.TrackPadUp, ref TrackPadUp);
            CheckButtonState(HVRButtons.TrackPadLeft, ref TrackPadLeft);
            CheckButtonState(HVRButtons.TrackPadRight, ref TrackPadRight);
            CheckButtonState(HVRButtons.TrackPadDown, ref TrackPadDown);


            RecentVelocities.Enqueue(Velocity.magnitude);

            Device.TryGetFeatureValue(CommonUsages.deviceVelocity, out Velocity);
            Device.TryGetFeatureValue(CommonUsages.deviceAngularVelocity, out AngularVelocity);

            AngularVelocityMagnitude = AngularVelocity.magnitude;
            VelocityMagnitude = Velocity.magnitude;
        }



        protected void ResetTrackedVelocities()
        {
            for (var i = 0; i < 200; i++)
            {
                RecentVelocities.Enqueue(0f);
            }
        }

        public float GetAverageVelocity(float seconds)
        {
            var frames = seconds / Time.fixedDeltaTime;
            var sum = 0f;
            for (var i = 0; i < frames; i++)
            {
                sum += RecentVelocities[i];
            }

            if (frames == 0f) return 0f;
            return sum / frames;
        }

        private void CorrectDeadzone()
        {
            if (Mathf.Abs(JoystickAxis.x) < ThumbstickDeadZone.x) JoystickAxis.x = 0f;
            if (Mathf.Abs(JoystickAxis.y) < ThumbstickDeadZone.y) JoystickAxis.y = 0f;
        }

        protected abstract void UpdateInput();

        protected abstract void CheckButtonState(HVRButtons button, ref HVRButtonState buttonState);

        protected void SetButtonState(HVRButtons button, ref HVRButtonState buttonState, bool pressed)
        {
            if (pressed)
            {
                if (!buttonState.Active)
                {
                    buttonState.JustActivated = true;
                    buttonState.Active = true;
                }
            }
            else
            {
                if (buttonState.Active)
                {
                    buttonState.Active = false;
                    buttonState.JustDeactivated = true;
                }
            }

            SetButtonState(Side, button, buttonState);
        }

        protected void ResetButton(ref HVRButtonState buttonState)
        {
            buttonState.JustDeactivated = false;
            buttonState.JustActivated = false;
            buttonState.Value = 0f;
        }

        public static void SetButtonState(HVRHandSide side, HVRButtons button, HVRButtonState state)
        {
            var map = side == HVRHandSide.Right ? _rightButtonStates : _leftButtonStates;
            map[button] = state;
        }

        public static HVRButtonState GetButtonState(HVRHandSide side, HVRButtons button)
        {
            var map = side == HVRHandSide.Right ? _rightButtonStates : _leftButtonStates;
            map.TryGetValue(button, out var state);
            return state;
        }

        /// <summary>
        ///   <para>Sends a haptic impulse to the controller.</para>
        /// </summary>
        /// <param name="amplitude">The normalized (0.0 to 1.0) amplitude value of the haptic impulse to play on the device.</param>
        /// <param name="duration">The duration in seconds that the haptic impulse will play. Only supported on Oculus.</param>
        public void Vibrate(float amplitude, float duration = 1f)
        {
            if (Device.isValid && Device.TryGetHapticCapabilities(out var hapticCapabilities) && hapticCapabilities.supportsImpulse)
            {
                amplitude = Mathf.Clamp(amplitude, 0f, 1f);
                Device.SendHapticImpulse(0, amplitude, duration);
            }
        }
    }

    public enum InputSDK
    {
        XRInput,
        Oculus,
        SteamVR
    }

    public enum XRInputSystem
    {
        None,
        WMR,
        Oculus,
        OculusOpenVR,
        Vive,
        WMROpenVR,
        Knuckles,
        Cosmos
    }

    public enum HVRControllerType
    {
        None,
        Oculus,
        WMR,
        Vive,
        Knuckles,
        Cosmos
    }
}
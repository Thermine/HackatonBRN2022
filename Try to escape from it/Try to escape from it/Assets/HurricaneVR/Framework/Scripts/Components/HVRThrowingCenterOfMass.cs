﻿using HurricaneVR.Framework.ControllerInput;
using HurricaneVR.Framework.Shared;
using UnityEngine;

namespace HurricaneVR.Framework.Components
{
    public class HVRThrowingCenterOfMass : MonoBehaviour
    {
        public HVRHandSide HandSide;
        public Transform Oculus;
        public Transform Vive;
        public Transform WMR;
        public Transform Knuckles;
        public Transform Fallback;

        public Transform CenterOfMass;

        private void Start()
        {
            if (HandSide == HVRHandSide.Left)
            {
                if (HVRInputManager.Instance.LeftController)
                    ControllerConnected(HVRInputManager.Instance.LeftController);
                HVRInputManager.Instance.LeftControllerConnected.AddListener(ControllerConnected);
            }
            else
            {
                if (HVRInputManager.Instance.RightController)
                    ControllerConnected(HVRInputManager.Instance.RightController);
                HVRInputManager.Instance.RightControllerConnected.AddListener(ControllerConnected);
            }
        }

        private void ControllerConnected(HVRController controller)
        {
            if (controller.InputMap)
            {
                transform.localEulerAngles = controller.InputMap.ControllerRotationOffset;
                transform.localPosition = controller.InputMap.ControllerPositionOffset;
            }

            switch (controller.ControllerType)
            {
                case HVRControllerType.Oculus:
                    CenterOfMass = Oculus;
                    break;
                case HVRControllerType.WMR:
                    CenterOfMass = WMR;
                    break;
                case HVRControllerType.Vive:
                    CenterOfMass = Vive;
                    break;
                case HVRControllerType.Knuckles:
                    CenterOfMass = Knuckles;
                    break;
                default:
                    CenterOfMass = Fallback;
                    break;
            }

            if (!CenterOfMass)
                CenterOfMass = Fallback;
            if (!CenterOfMass)
                CenterOfMass = transform;
        }
    }
}

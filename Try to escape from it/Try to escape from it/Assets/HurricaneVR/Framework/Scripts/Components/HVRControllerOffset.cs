using HurricaneVR.Framework.ControllerInput;
using HurricaneVR.Framework.Shared;
using UnityEngine;

namespace HurricaneVR.Framework.Components
{
    public class HVRControllerOffset : MonoBehaviour
    {
        public HVRHandSide HandSide;

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
                //Debug.Log($"offsetting based on {controller.InputMap.name},{controller.InputMap.ControllerRotationOffset},{controller.InputMap.ControllerPositionOffset}");
                var angles = transform.localEulerAngles = controller.InputMap.ControllerRotationOffset;
                transform.localPosition = controller.InputMap.ControllerPositionOffset;
                if (HandSide == HVRHandSide.Left)
                {
                    var position = transform.localPosition;
                    position.x *= -1;
                    transform.localPosition = position;

                    angles.y *= -1;
                    angles.z *= -1;

                    transform.localEulerAngles = angles;
                } 
            }
        }

    }
}
